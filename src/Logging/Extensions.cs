using SourceGenerators;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static MqttSql.Logging.Logger;

namespace MqttSql.Logging;

[LoggerExtensions(
    GenericOverrideCount = 12,
    LogLevels = [
        nameof(LogLevel.Trace),
        nameof(LogLevel.Debug),
        nameof(LogLevel.Information),
        nameof(LogLevel.Warning),
        nameof(LogLevel.Error),
        nameof(LogLevel.Critical)
    ]
)]
public static partial class Extensions
{
    /// <summary>
    /// Determines whether the given byte is the first byte of a UTF-8 encoded character.
    /// </summary>
    /// <param name="byte">The byte to test.</param>
    /// <returns><see langword="true"/> if <paramref name="byte"/> is the first byte of a UTF-8 encoded character.</returns>
    /// <remarks>
    /// <list type="table">
    /// <item>
    /// <term>1 byte</term>
    /// <description>0xxxxxxx</description>
    /// </item>
    /// <item>
    /// <term>2 bytes</term>
    /// <description>110xxxxx 10xxxxxx</description>
    /// </item>
    /// <item>
    /// <term>3 bytes</term>
    /// <description>1110xxxx 10xxxxxx 10xxxxxx</description>
    /// </item>
    /// <item>
    /// <term>4 bytes</term>
    /// <description>11110xxx 10xxxxxx 10xxxxxx 10xxxxxx</description>
    /// </item>
    /// </list>
    /// </remarks>
    public static bool IsFirstByteOfUtf8Character(this byte @byte)
    {
        const byte oneByteBitmask = 0b1_0000000;
        const byte oneByteResult = 0b0_0000000;

        const byte twoByteBitmask = 0b111_00000;
        const byte twoByteResult = 0b110_00000;

        const byte threeByteBitmask = 0b1111_0000;
        const byte threeByteResult = 0b1110_0000;

        const byte fourByteBitmask = 0b11111_000;
        const byte fourByteResult = 0b11110_000;

        return (@byte & oneByteBitmask) == oneByteResult
            || (@byte & twoByteBitmask) == twoByteResult
            || (@byte & threeByteBitmask) == threeByteResult
            || (@byte & fourByteBitmask) == fourByteResult;
    }

    /// <summary>
    /// Clears buffers for this logger and causes any buffered data to be written to the log file.
    /// </summary>
    /// <param name="logger">The logger to be flushed.</param>
    /// <exception cref="IOException">Failed to flush logs.</exception>
    public static void Flush(this Logger logger)
    {
        if (!logger.TryFlush()) throw new IOException("Failed to flush logs");
    }

    /// <summary>
    /// Asynchronously clears buffers for this logger and causes any buffered data to be written to the log file.
    /// </summary>
    /// <param name="logger">The logger to be flushed.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous flush operation.</returns>
    /// <exception cref="OperationCanceledException">The cancellation token was canceled. This exception is stored into the returned task.</exception>
    public static async Task FlushAsync(this Logger logger, CancellationToken cancellationToken)
    {
        var exponentialBackoff = new ExponentialBackoff(
            initialDelay: TimeSpan.FromMilliseconds(1),
            maxDelay: TimeSpan.FromSeconds(10),
            multiplier: 1.7,
            maxRetries: 0
        );

        while (!cancellationToken.IsCancellationRequested && !logger.TryFlush())
            await exponentialBackoff.Delay(cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// Asynchronously clears buffers for this logger and causes any buffered data to be written to the log file.
    /// </summary>
    /// <param name="logger">The logger to be flushed.</param>
    /// <param name="timeout">The timeout after which the Task should be cancelled if it hasn't otherwise completed.</param>
    /// <returns>
    /// A task that represents the asynchronous flush operation. The result of the task is <see langword="true"/>
    /// if logger was successfully flushed, otherwise <see langword="false"/>.
    /// </returns>
    public static async Task<bool> FlushAsync(this Logger logger, TimeSpan timeout)
    {
        using CancellationTokenSource flushCancellationTokenSource = new(timeout);

        try
        {
            await FlushAsync(logger, flushCancellationTokenSource.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// Asynchronously clears buffers for this logger and causes any buffered data to be written to the log file.
    /// </summary>
    /// <param name="logger">The logger to be flushed.</param>
    /// <param name="timeoutAfterCancellation">
    /// The timeout after cancellation is requested by <paramref name="cancellationToken"/> after which the Task
    /// should be cancelled if it hasn't otherwise completed.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous flush operation. The result of the task is <see langword="true"/>
    /// if logger was successfully flushed, otherwise <see langword="false"/>.
    /// </returns>
    public static async Task<bool> FlushAsync(this Logger logger, TimeSpan timeoutAfterCancellation, CancellationToken cancellationToken)
    {
        using CancellationTokenSource flushCancellationTokenSource = new();
        var registration = cancellationToken.Register(() => flushCancellationTokenSource.CancelAfter(timeoutAfterCancellation));

        try
        {
            await FlushAsync(logger, flushCancellationTokenSource.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            registration.Unregister();
        }
    }
}
