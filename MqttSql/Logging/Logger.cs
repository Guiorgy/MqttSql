﻿/*
    This file is part of MqttSql (Copyright © 2024  Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
    You should have received a copy of the GNU General Public License along with Foobar. If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;

using LogLevelFlag = MqttSql.Logging.Logger.LogLevel;
using LogLevelEnum = MqttSql.Logging.Logger.LogLevel;

using MicrosoftLogLevel = Microsoft.Extensions.Logging.LogLevel;
using MicrosoftLogger = Microsoft.Extensions.Logging.ILogger;
using Microsoft.Extensions.Logging;

namespace MqttSql.Logging;

public sealed class Logger
{
    /// <summary>
    /// Defines logging severity levels.
    /// </summary>
    [Flags]
    public enum LogLevel : byte
    {
        /// <summary>
        /// Not used for writing log messages. Specifies that a logging category should not
        /// write any messages.
        /// </summary>
        /// <remarks><seealso cref="MicrosoftLogLevel.None"/></remarks>
        None = 0,

        /// <summary>
        /// Logs that describe an unrecoverable application or system crash, or a catastrophic
        /// failure that requires immediate attention.
        /// </summary>
        /// <remarks><seealso cref="MicrosoftLogLevel.Critical"/></remarks>
        Critical = 1,

        /// <summary>
        /// Logs that highlight when the current flow of execution is stopped due to a failure.
        /// These should indicate a failure in the current activity, not an application-wide
        /// failure.
        /// </summary>
        /// <remarks><seealso cref="MicrosoftLogLevel.Error"/></remarks>
        Error = 1 << 1,

        /// <summary>
        /// Logs that highlight an abnormal or unexpected event in the application flow,
        /// but do not otherwise cause the application execution to stop.
        /// </summary>
        /// <remarks><seealso cref="MicrosoftLogLevel.Warning"/></remarks>
        Warning = 1 << 2,

        /// <summary>
        /// Logs that track the general flow of the application. These logs should have long-term
        /// value.
        /// </summary>
        /// <remarks><seealso cref="MicrosoftLogLevel.Information"/></remarks>
        Information = 1 << 3,

        /// <summary>
        /// Logs that are used for interactive investigation during development. These logs
        /// should primarily contain information useful for debugging and have no long-term
        /// value.
        /// </summary>
        /// <remarks><seealso cref="MicrosoftLogLevel.Debug"/></remarks>
        Debug = 1 << 4,

        /// <summary>
        /// Logs that contain the most detailed messages. These messages may contain sensitive
        /// application data. These messages are disabled by default and should never be
        /// enabled in a production environment.
        /// </summary>
        /// <remarks><seealso cref="MicrosoftLogLevel.Trace"/></remarks>
        Trace = 1 << 5
    }

    public const LogLevelFlag AllLogLevels = LogLevelEnum.Critical | LogLevelEnum.Error | LogLevelFlag.Warning | LogLevelEnum.Information | LogLevelEnum.Debug | LogLevelEnum.Trace;
    public const byte AllLogLevelsMask = (byte)AllLogLevels;

    private static string ToDisplayString(LogLevelEnum logLevel) => logLevel switch
    {
        LogLevelEnum.None => nameof(LogLevelEnum.None),
        LogLevelEnum.Critical => nameof(LogLevelEnum.Critical),
        LogLevelEnum.Error => nameof(LogLevelEnum.Error),
        LogLevelEnum.Warning => nameof(LogLevelEnum.Warning),
        LogLevelEnum.Information => nameof(LogLevelEnum.Information),
        LogLevelEnum.Debug => nameof(LogLevelEnum.Debug),
        LogLevelEnum.Trace => nameof(LogLevelEnum.Trace),
        _ => throw new UnreachableException($"Unhandled LogLevel {logLevel}")
    };

    /// <summary>
    /// Maps a <see cref="LogLevelEnum"/> onto the built-in <see cref="MicrosoftLogLevel"/>.
    /// </summary>
    /// <param name="logLevel">The enum to map.</param>
    /// <returns>The mapped built-in enum.</returns>
    private static MicrosoftLogLevel ToMicrosoftLogLevel(LogLevelEnum logLevel) => logLevel switch
    {
        LogLevelEnum.None => MicrosoftLogLevel.None,
        LogLevelEnum.Critical => MicrosoftLogLevel.Critical,
        LogLevelEnum.Error => MicrosoftLogLevel.Error,
        LogLevelEnum.Warning => MicrosoftLogLevel.Warning,
        LogLevelEnum.Information => MicrosoftLogLevel.Information,
        LogLevelEnum.Debug => MicrosoftLogLevel.Debug,
        LogLevelEnum.Trace => MicrosoftLogLevel.Trace,
        _ => throw new UnreachableException($"Unhandled LogLevel {logLevel}")
    };

    /// <summary>
    /// Converts a <see cref="LogLevelEnum"/> into a bitmask where every lower bit to the one set in the falg is also set.
    /// </summary>
    /// <param name="logLevel">The <see cref="LogLevelEnum"/> to convert to a bitmask.</param>
    /// <returns>The resulting bitmask.</returns>
    [SuppressMessage("Major Code Smell", "S1121:Assignments should not be made from within sub-expressions")]
    private static uint ToBitMask(LogLevelEnum logLevel)
    {
        uint mask = (uint)logLevel;

        uint bit = mask;
        while (bit > 1) mask |= bit >>= 1;

        return mask;
    }

    private readonly MicrosoftLogger? _linkedLogger;

    private readonly string? _logFilePath;
    private readonly bool _logToConsole;
    private readonly byte _logLevelMask;
    private readonly byte _includeLogLevelMask;
    private readonly bool _includeTimestamp;
    private readonly int _flushOnMessageCount;
    private readonly int _logFileMinSize;
    private readonly int _logFileMaxSize;
    private readonly ConcurrentQueue<string> _logBuffer = new();
    private readonly ConcurrentQueue<List<string>> _failedLogs = new();
    private bool _flushingFailedLogs;
    private readonly object _lock = new();

    private const int _defaultFlushOnMessageCount = 1000;
    private const int _defaultLogFileMinSize = 1_000_000;
    private const int _defaultLogFileMaxSize = 100_000_000;

    public bool LogToLinkedLoggerEnabled => _linkedLogger != null;
    public bool LogToFileEnabled => _logFilePath != null;
    public bool LogToConsoleEnabled => _logToConsole;

    public Logger(LogLevelEnum logLevel, MicrosoftLogger linkedLogger, LogLevelFlag includeLogLevels = LogLevelEnum.None, bool includeTimestamp = true)
        : this(null, false, logLevel, includeLogLevels, includeTimestamp, 0, 0, 0, linkedLogger) { }

    public Logger(
        string? logFilePath,
        bool logToConsole,
        LogLevelEnum logLevel,
        LogLevelFlag includeLogLevels = AllLogLevels,
        bool includeTimestamp = true,
        int flushOnMessageCount = _defaultFlushOnMessageCount,
        int logFileMinSize = _defaultLogFileMinSize,
        int logFileMaxSize = _defaultLogFileMaxSize,
        MicrosoftLogger? linkedLogger = null
    ) : this(logFilePath, logToConsole, (byte)ToBitMask(logLevel), (byte)ToBitMask(includeLogLevels), includeTimestamp, flushOnMessageCount, logFileMinSize, logFileMaxSize, linkedLogger) { }

    public Logger(
        string? logFilePath,
        bool logToConsole,
        byte logLevelMask,
        byte includeLogLevelMask = AllLogLevelsMask,
        bool includeTimestamp = true,
        int flushOnMessageCount = _defaultFlushOnMessageCount,
        int logFileMinSize = _defaultLogFileMinSize,
        int logFileMaxSize = _defaultLogFileMaxSize,
        MicrosoftLogger? linkedLogger = null
    )
    {
        if (logLevelMask >= (byte)LogLevelEnum.Trace << 1) throw new ArgumentOutOfRangeException(nameof(logLevelMask));

        _logFilePath = logFilePath;
        _logToConsole = logToConsole;
        _logLevelMask = logLevelMask;
        _includeLogLevelMask = includeLogLevelMask;
        _includeTimestamp = includeTimestamp;
        _flushOnMessageCount = flushOnMessageCount < 1 ? 1 : flushOnMessageCount;
        _logFileMinSize = Math.Max(_defaultLogFileMinSize, logFileMinSize);
        _logFileMaxSize = Math.Max(logFileMinSize * 2, logFileMaxSize);

        _linkedLogger = linkedLogger;
    }

    public bool EnabledFor(LogLevelEnum logLevel) => ((byte)logLevel & _logLevelMask) != 0;

    private const LogLevelFlag _printToStdErrLogLevels = LogLevelEnum.Critical | LogLevelEnum.Error | LogLevelEnum.Warning;
    private static bool ShouldPrintToStdErr(LogLevelEnum logLevel) => (logLevel & _printToStdErrLogLevels) != 0;

    private static void PrefixTimestamp(ref string message) => message = $"[{DateTime.Now.ToIsoString(milliseconds: true)}] {message}";

    private bool PrefixLogLevelFor(LogLevelEnum logLevel) => ((byte)logLevel & _includeLogLevelMask) != 0;
    private static void PrefixLogLevel(ref string message, LogLevelEnum logLevel) => message = $"[{ToDisplayString(logLevel)}] {message}";

    public void Log(LogLevelEnum logLevel, params object?[]? messageBits)
    {
        if (!EnabledFor(logLevel) || messageBits == null || messageBits.Length == 0) return;

        string message = string.Concat(messageBits.Select(obj => obj?.ToString()).Where(str => str != null));

        if (message.Length == 0) return;

        if (_includeTimestamp) PrefixTimestamp(ref message);
        if (PrefixLogLevelFor(logLevel)) PrefixLogLevel(ref message, logLevel);

        PrintLog(logLevel, message);
        QueueLog(message);
        LogToLinkedLogger(logLevel, message);
    }

    public void Log(LogLevelEnum logLevel, Exception exception, params object?[]? messageBits)
    {
        if (!EnabledFor(logLevel)) return;

        string? message = null;
        if (messageBits != null && messageBits.Length != 0) message = string.Concat(messageBits.Select(obj => obj?.ToString()).Where(str => str != null));

        LogException(logLevel, exception, message);
    }

    public void Log(LogLevelEnum logLevel, Exception exception) => Log(logLevel, exception, (object?[]?)null);

    private void LogException(LogLevelEnum logLevel, Exception exception, string? message = null, bool skipFlush = false)
    {
        if (message != null)
        {
            if (_includeTimestamp) PrefixTimestamp(ref message);
            if (PrefixLogLevelFor(logLevel)) PrefixLogLevel(ref message, logLevel);

            PrintLog(logLevel, message);
            QueueLog(message, true);
            LogToLinkedLogger(logLevel, message);

            message = exception.Message;
        }
        else
        {
            message = exception.Message;

            if (_includeTimestamp) PrefixTimestamp(ref message);
            if (PrefixLogLevelFor(logLevel)) PrefixLogLevel(ref message, logLevel);
        }

        message = exception.StackTrace == null ? message : message + Environment.NewLine + exception.StackTrace;

        PrintLog(logLevel, message, ConsoleColor.Red, ConsoleColor.Yellow);
        QueueLog(message, skipFlush);
        LogToLinkedLogger(logLevel, message);
    }

    private void PrintLog(LogLevelEnum logLevel, string message)
    {
        if (!_logToConsole) return;

        if (ShouldPrintToStdErr(logLevel))
            Console.Error.WriteLine(message);
        else
            Console.WriteLine(message);
    }

    private void PrintLog(LogLevelEnum logLevel, string message, ConsoleColor foregroundColor, ConsoleColor backgroundColor)
    {
        if (!_logToConsole) return;

        Console.ForegroundColor = foregroundColor;
        Console.BackgroundColor = backgroundColor;
        if (ShouldPrintToStdErr(logLevel))
            Console.Error.WriteLine(message);
        else
            Console.WriteLine(message);
        Console.ResetColor();
    }

    private void QueueLog(string message, bool skipFlush = false)
    {
        if (_logFilePath == null) return;

        _logBuffer.Enqueue(message);

        if (!skipFlush)
        {
            if (!_failedLogs.IsEmpty) FlushFailedLogs();
            else if (!_flushingFailedLogs && _logBuffer.Count >= _flushOnMessageCount) FlushLogs();
        }
    }

    [SuppressMessage("Usage", "CA2254:Template should be a static expression", Justification = "Structured logging not supported")]
    private void LogToLinkedLogger(LogLevelEnum logLevel, string message)
    {
        if (_linkedLogger == null) return;

        _linkedLogger.Log(ToMicrosoftLogLevel(logLevel), message);
    }

    private void FlushFailedLogs()
    {
        if (_flushingFailedLogs) return;
        lock (_lock)
        {
            if (_flushingFailedLogs) return;
            _flushingFailedLogs = true;
        }

        List<string> logBuffer = _failedLogs.ToArray().Flatten().ToList();

        if (TryFlushLogs(logBuffer))
        {
            _failedLogs.Clear();
        }

        _flushingFailedLogs = false;
    }

    private void FlushLogs()
    {
        List<string> logBuffer = new(_logBuffer.Count + 10);

        lock (_lock)
        {
            while (_logBuffer.TryDequeue(out var message)) logBuffer.Add(message);
        }

        if (!TryFlushLogs(logBuffer))
        {
            _failedLogs.Enqueue(logBuffer);
        }
    }

    private bool TryFlushLogs(List<string> messages)
    {
        try
        {
            if (messages.Count != 0)
            {
                string stringBuffer = string.Join(Environment.NewLine, messages) + Environment.NewLine;

                var file = new FileInfo(_logFilePath!);

                if (!file.Exists)
                {
                    using StreamWriter writer = new(file.Open(FileMode.CreateNew), Encoding.UTF8);
                    writer.Write(stringBuffer);
                }
                else if (file.Length > _logFileMaxSize)
                {
                    ReadOnlySpan<byte> buffer;

                    using (BinaryReader reader = new(file.Open(FileMode.Open), Encoding.UTF8))
                    {
                        reader.BaseStream.Position = file.Length - _logFileMinSize;
                        byte[] _buffer = reader.ReadBytes(_logFileMinSize);

                        int offset = 0;
                        while (offset <= _buffer.Length && !_buffer[offset].IsFirstByteOfUtf8Character()) offset++;

                        buffer = _buffer.AsSpan()[offset..];
                    }

                    using BinaryWriter writer = new(file.Open(FileMode.Truncate), Encoding.UTF8);
                    writer.BaseStream.Position = 0;
                    writer.Write(buffer);
                    writer.Write(Encoding.UTF8.GetBytes(stringBuffer));
                }
                else
                {
                    File.AppendAllText(_logFilePath!, stringBuffer, Encoding.UTF8);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            LogException(LogLevel.Critical, ex, "Failed to flush logs", true);

            return false;
        }
    }

    public bool TryFlush()
    {
        if (_logBuffer.IsEmpty && _failedLogs.IsEmpty) return true;

        if (_flushingFailedLogs) return false;
        if (!_failedLogs.IsEmpty) FlushFailedLogs();
        if (!_failedLogs.IsEmpty) return false;

        if (!_logBuffer.IsEmpty) FlushLogs();

        return _failedLogs.IsEmpty;
    }
}