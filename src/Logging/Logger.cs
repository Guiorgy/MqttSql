/*
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
        /// <remarks><seealso cref="Microsoft.Extensions.Logging.LogLevel.None"/></remarks>
        None = 0,

        /// <summary>
        /// Logs that describe an unrecoverable application or system crash, or a catastrophic
        /// failure that requires immediate attention.
        /// </summary>
        /// <remarks><seealso cref="Microsoft.Extensions.Logging.LogLevel.Critical"/></remarks>
        Critical = 1,

        /// <summary>
        /// Logs that highlight when the current flow of execution is stopped due to a failure.
        /// These should indicate a failure in the current activity, not an application-wide
        /// failure.
        /// </summary>
        /// <remarks><seealso cref="Microsoft.Extensions.Logging.LogLevel.Error"/></remarks>
        Error = 1 << 1,

        /// <summary>
        /// Logs that highlight an abnormal or unexpected event in the application flow,
        /// but do not otherwise cause the application execution to stop.
        /// </summary>
        /// <remarks><seealso cref="Microsoft.Extensions.Logging.LogLevel.Warning"/></remarks>
        Warning = 1 << 2,

        /// <summary>
        /// Logs that track the general flow of the application. These logs should have long-term
        /// value.
        /// </summary>
        /// <remarks><seealso cref="Microsoft.Extensions.Logging.LogLevel.Information"/></remarks>
        Information = 1 << 3,

        /// <summary>
        /// Logs that are used for interactive investigation during development. These logs
        /// should primarily contain information useful for debugging and have no long-term
        /// value.
        /// </summary>
        /// <remarks><seealso cref="Microsoft.Extensions.Logging.LogLevel.Debug"/></remarks>
        Debug = 1 << 4,

        /// <summary>
        /// Logs that contain the most detailed messages. These messages may contain sensitive
        /// application data. These messages are disabled by default and should never be
        /// enabled in a production environment.
        /// </summary>
        /// <remarks><seealso cref="Microsoft.Extensions.Logging.LogLevel.Trace"/></remarks>
        Trace = 1 << 5
    }

    /// <summary>
    /// Converts a <see cref="LogLevel"/> flag into a bitmask where every lower bit to the one set in the falg is also set.
    /// </summary>
    /// <param name="logLevel">The <see cref="LogLevel"/> flag to convert to a bitmask.</param>
    /// <returns>The resulting bitmask.</returns>
    [SuppressMessage("Major Code Smell", "S1121:Assignments should not be made from within sub-expressions")]
    private static uint ToBitMask(LogLevel logLevel)
    {
        uint mask = (uint)logLevel;

        uint bit = mask;
        while (bit > 1) mask |= bit >>= 1;

        return mask;
    }

    private readonly string? _logFilePath;
    private readonly bool _logToConsole;
    private readonly byte _logLevelMask;
    private readonly int _flushOnMessageCount;
    private readonly int _logFileMinSize;
    private readonly int _logFileMaxSize;
    private readonly bool _logTimestamp;
    private readonly ConcurrentQueue<string> _logBuffer = new();
    private readonly ConcurrentQueue<List<string>> _failedLogs = new();
    private bool _flushingFailedLogs;
    private readonly object _lock = new();

    private const int _defaultFlushOnMessageCount = 1000;
    private const int _defaultLogFileMinSize = 1_000_000;
    private const int _defaultLogFileMaxSize = 100_000_000;

    public Logger(
        string? logFilePath,
        bool logToConsole,
        LogLevel logLevel,
        int flushOnMessageCount = _defaultFlushOnMessageCount,
        int logFileMinSize = _defaultLogFileMinSize,
        int logFileMaxSize = _defaultLogFileMaxSize,
        bool logTimestamp = true
    ) : this(logFilePath, logToConsole, (byte)ToBitMask(logLevel), flushOnMessageCount, logFileMinSize, logFileMaxSize, logTimestamp) { }

    public Logger(
        string? logFilePath,
        bool logToConsole,
        byte logLevelMask,
        int flushOnMessageCount = _defaultFlushOnMessageCount,
        int logFileMinSize = _defaultLogFileMinSize,
        int logFileMaxSize = _defaultLogFileMaxSize,
        bool logTimestamp = true
    )
    {
        if (logLevelMask >= (byte)LogLevel.Trace << 1) throw new ArgumentOutOfRangeException(nameof(logLevelMask));

        _logFilePath = logFilePath;
        _logToConsole = logToConsole;
        _logLevelMask = logLevelMask;
        _flushOnMessageCount = flushOnMessageCount < 1 ? 1 : flushOnMessageCount;
        _logFileMinSize = Math.Max(_defaultLogFileMinSize, logFileMinSize);
        _logFileMaxSize = Math.Max(logFileMinSize * 2, logFileMaxSize);
        _logTimestamp = logTimestamp;
    }

    public bool EnabledFor(LogLevel logLevel) => ((byte)logLevel & _logLevelMask) != 0;

    private static void PrefixTimestamp(ref string message)
    {
        message = $"[{DateTime.Now.ToIsoString(milliseconds: true)}] {message}";
    }

    private static string PrefixTimestamp(string message)
    {
        return $"[{DateTime.Now.ToIsoString(milliseconds: true)}] {message}";
    }

    /*public void Log(LogLevel logLevel, params string[] messageBits)
    {
        if (!EnabledFor(logLevel) || messageBits.Length == 0) return;

        var message = messageBits.Length == 1 ? messageBits[0] : string.Concat(messageBits);

        if (_logTimestamp) PrefixTimestamp(ref message);

        PrintLog(message);
        QueueLog(message);
    }*/

    public void Log(LogLevel logLevel, params object?[]? messageBits)
    {
        if (!EnabledFor(logLevel) || messageBits == null || messageBits.Length == 0) return;

        string message = string.Concat(messageBits.Select(obj => obj?.ToString()).Where(str => str != null));

        if (message.Length == 0) return;

        if (_logTimestamp) PrefixTimestamp(ref message);

        PrintLog(message);
        QueueLog(message);
    }

    /*public void Log(LogLevel logLevel, Exception exception, params string[] messageBits)
    {
        if (!EnabledFor(logLevel)) return;

        string? message = null;
        if (messageBits.Length != 0) message = messageBits.Length == 1 ? messageBits[0] : string.Concat(messageBits);

        LogException(exception, message);
    }*/

    public void Log(LogLevel logLevel, Exception exception, params object?[]? messageBits)
    {
        if (!EnabledFor(logLevel)) return;

        string? message = null;
        if (messageBits != null && messageBits.Length != 0) message = string.Concat(messageBits.Select(obj => obj?.ToString()).Where(str => str != null));

        LogException(exception, message);
    }

    public void Log(LogLevel logLevel, Exception exception) => Log(logLevel, exception, (object?[]?)null);

    private void LogException(Exception exception, string? message = null, bool skipFlush = false)
    {
        if (message != null)
        {
            if (_logTimestamp) PrefixTimestamp(ref message);

            PrintLog(message);
            QueueLog(message, true);
        }

        message = message == null ? PrefixTimestamp(exception.Message) : exception.Message;
        message = exception.StackTrace == null ? message : message + Environment.NewLine + exception.StackTrace;
        PrintLog(message, ConsoleColor.Red, ConsoleColor.Yellow);
        QueueLog(message, skipFlush);
    }

    private void PrintLog(string message)
    {
        if (_logToConsole) Console.WriteLine(message);
    }

    private void PrintLog(string message, ConsoleColor foregroundColor, ConsoleColor backgroundColor)
    {
        if (_logToConsole)
        {
            Console.ForegroundColor = foregroundColor;
            Console.BackgroundColor = backgroundColor;
            Console.WriteLine(message);
            Console.ResetColor();
        }
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

                    using (BinaryWriter writer = new(file.Open(FileMode.Truncate), Encoding.UTF8))
                    {
                        writer.BaseStream.Position = 0;
                        writer.Write(buffer);
                        writer.Write(Encoding.UTF8.GetBytes(stringBuffer));
                    }
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
            LogException(ex, "Failed to flush logs", true);

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
