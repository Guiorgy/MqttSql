/*
    This file is part of MqttSql (Copyright © 2024 Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License along with MqttSql. If not, see <https://www.gnu.org/licenses/>.
*/

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
    extension(byte @byte)
    {
        /// <summary>
        /// Determines whether the given byte is the first byte of a UTF-8 encoded character.
        /// </summary>
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
        public bool IsFirstByteOfUtf8Character()
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
    }

    extension(Logger logger)
    {
        /// <summary>
        /// Clears buffers for this logger and causes any buffered data to be written to the log file.
        /// </summary>
        /// <exception cref="IOException">Failed to flush logs.</exception>
        public void Flush()
        {
            if (!logger.TryFlush()) throw new IOException("Failed to flush logs");
        }

        /// <summary>
        /// Asynchronously clears buffers for this logger and causes any buffered data to be written to the log file.
        /// </summary>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous flush operation.</returns>
        /// <exception cref="OperationCanceledException">The cancellation token was canceled. This exception is stored into the returned task.</exception>
        public async Task FlushAsync(CancellationToken cancellationToken)
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
        /// <param name="timeout">The timeout after which the Task should be cancelled if it hasn't otherwise completed.</param>
        /// <returns>
        /// A task that represents the asynchronous flush operation. The result of the task is <see langword="true"/>
        /// if logger was successfully flushed, otherwise <see langword="false"/>.
        /// </returns>
        public async Task<bool> FlushAsync(TimeSpan timeout)
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
        /// <param name="timeoutAfterCancellation">
        /// The timeout after cancellation is requested by <paramref name="cancellationToken"/> after which the Task
        /// should be cancelled if it hasn't otherwise completed.
        /// </param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>
        /// A task that represents the asynchronous flush operation. The result of the task is <see langword="true"/>
        /// if logger was successfully flushed, otherwise <see langword="false"/>.
        /// </returns>
        public async Task<bool> FlushAsync(TimeSpan timeoutAfterCancellation, CancellationToken cancellationToken)
        {
#pragma warning disable CA2016 // Forward the 'CancellationToken' parameter to methods (Justification: The overloaded method being forwarded to does not accept cancellation tokens)
            if (cancellationToken == CancellationToken.None) return await FlushAsync(logger, timeoutAfterCancellation);
#pragma warning restore CA2016 // Forward the 'CancellationToken' parameter to methods

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
                _ = registration.Unregister();
            }
        }
    }
}
