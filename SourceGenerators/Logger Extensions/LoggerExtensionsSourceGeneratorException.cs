using System;
#if !NET8_0_OR_GREATER
using System.Runtime.Serialization;
#endif

namespace SourceGenerators;

#if !NET8_0_OR_GREATER
[Serializable]
#endif
public sealed class LoggerExtensionsSourceGeneratorException : Exception
{
    public LoggerExtensionsSourceGeneratorException() : base()
    {
    }

    public LoggerExtensionsSourceGeneratorException(string message) : base(message)
    {
    }

    public LoggerExtensionsSourceGeneratorException(string message, Exception innerException) : base(message, innerException)
    {
    }

#if !NET8_0_OR_GREATER
    private LoggerExtensionsSourceGeneratorException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
#endif
}
