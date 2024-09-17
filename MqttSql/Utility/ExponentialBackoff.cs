/*
    This file is part of MqttSql (Copyright © 2024 Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License along with Foobar. If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Threading;
using System.Threading.Tasks;

namespace MqttSql;

public sealed class ExponentialBackoff
{
    private readonly TimeSpan initialDelay;
    private readonly TimeSpan maxDelay;
    private readonly double multiplier;
    private readonly int maxRetries;
    private TimeSpan currentDelay;
    private int retriesRemaining;

    public ExponentialBackoff(TimeSpan initialDelay, TimeSpan maxDelay, double multiplier = 2, int maxRetries = 0)
    {
        this.initialDelay = initialDelay.TotalMilliseconds < 1 ? TimeSpan.FromMilliseconds(1) : initialDelay;
        this.maxDelay = this.initialDelay <= maxDelay ? maxDelay : this.initialDelay;
        this.multiplier = Math.Max(1, multiplier);
        this.maxRetries = Math.Max(0, maxRetries);
        currentDelay = this.initialDelay;
        retriesRemaining = this.maxRetries == 0 ? -1 : this.maxRetries;
    }

    public bool FirstTime => currentDelay == initialDelay;

    public Task Delay(CancellationToken cancellationToken = default)
    {
        if (retriesRemaining == 0) throw new MaxRetriesReachedException();
        else if (retriesRemaining > 0) retriesRemaining--;

        var delayTask = Task.Delay(currentDelay, cancellationToken);

        if (currentDelay != maxDelay)
        {
            currentDelay *= multiplier;
            currentDelay = currentDelay <= maxDelay ? currentDelay : maxDelay;
        }

        return delayTask;
    }

    public void Reset()
    {
        currentDelay = initialDelay;
        retriesRemaining = maxRetries == 0 ? -1 : maxRetries;
    }

    public sealed class MaxRetriesReachedException : Exception
    {
        public MaxRetriesReachedException()
        {
        }

        public MaxRetriesReachedException(string? message) : base(message)
        {
        }

        public MaxRetriesReachedException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
