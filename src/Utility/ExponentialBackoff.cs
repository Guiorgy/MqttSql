using System;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MqttSql;

public sealed partial class ExponentialBackoff
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
        this.currentDelay = this.initialDelay;
        this.retriesRemaining = this.maxRetries == 0 ? -1 : this.maxRetries;
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

    [Serializable]
    public sealed class MaxRetriesReachedException : Exception
    {
        public MaxRetriesReachedException()
        {
        }

        public MaxRetriesReachedException(string? message) : base(message)
        {
        }

        private MaxRetriesReachedException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public MaxRetriesReachedException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
