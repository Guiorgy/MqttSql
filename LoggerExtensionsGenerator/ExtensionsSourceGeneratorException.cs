using System;
using System.Runtime.Serialization;

namespace LoggerExtensionsGenerator;

[Serializable]
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

    private ExtensionsSourceGeneratorException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}
