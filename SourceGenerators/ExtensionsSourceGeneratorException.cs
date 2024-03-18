using System;
#if !NET8_0_OR_GREATER
using System.Runtime.Serialization;
#endif

namespace LoggerExtensionsGenerator;

#if !NET8_0_OR_GREATER
[Serializable]
#endif
public sealed class ExtensionsSourceGeneratorException : Exception
{
    public ExtensionsSourceGeneratorException() : base()
    {
    }

    public ExtensionsSourceGeneratorException(string message) : base(message)
    {
    }

    public ExtensionsSourceGeneratorException(string message, Exception innerException) : base(message, innerException)
    {
    }

#if !NET8_0_OR_GREATER
    private ExtensionsSourceGeneratorException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
#endif
}
