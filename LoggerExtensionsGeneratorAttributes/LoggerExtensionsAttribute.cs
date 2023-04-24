namespace LoggerExtensionsGenerator;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class LoggerExtensionsAttribute : Attribute
{
    public string[] LogLevels { get; }
    public int GenericOverrideCount { get; }

    public LoggerExtensionsAttribute(int genericOverrideCount, params string[] logLevels)
    {
        if ((logLevels ?? throw  new ArgumentNullException(nameof(logLevels))).Length == 0) throw new ArgumentException("Must not be empty", nameof(logLevels));

        LogLevels = logLevels;
        GenericOverrideCount = Math.Max(0, genericOverrideCount);
    }
}
