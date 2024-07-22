using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.CodeAnalysis;
using SourceGenerators;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Tests;

[TestClass]
public sealed class TestLoggerExtensionsSourceGenerator : VerifyBaseWithDefaultSettings
{
    private static (ImmutableArray<Diagnostic> Diagnostics, string Output) GetGeneratedOutput(params string[] source)
        => GeneratorTestHelpers.GetGeneratedOutput<LoggerExtensionsSourceGenerator, LoggerExtensionsSourceGenerator.TrackingNames>(source);

    [TestMethod]
    public Task TestGenerationInGlobalFileScopedNamespace()
    {
        const string input = """
            using SourceGenerators;
            using static MqttSql.Logging.Logger;

            namespace MqttSql.Test.Logging;

            [LoggerExtensions(
                GenericOverrideCount = 3,
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
            }
            """;

        var (diagnostics, output) = GetGeneratedOutput(input);

        Assert.IsTrue(diagnostics.IsEmpty);
        return Verify(output);
    }

    [TestMethod]
    public Task TestGenerationInGlobalNamespaceBlock()
    {
        const string input = """
            using SourceGenerators;
            using static MqttSql.Logging.Logger;

            namespace MqttSql.Test.Logging;
            {
                [LoggerExtensions(
                    GenericOverrideCount = 3,
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
                }
            }
            """;

        var (diagnostics, output) = GetGeneratedOutput(input);

        Assert.IsTrue(diagnostics.IsEmpty);
        return Verify(output);
    }

    // TODO: Support nested namespaces
    //[TestMethod]
    //public Task TestGenerationInNestedNamespace()
    //{
    //    const string input = """
    //        using SourceGenerators;
    //        using static MqttSql.Logging.Logger;

    //        namespace MqttSql.Test.Logging;

    //        namespace NestedNamespaceTest
    //        {
    //            [LoggerExtensions(
    //                GenericOverrideCount = 3,
    //                LogLevels = [
    //                    nameof(LogLevel.Trace),
    //                    nameof(LogLevel.Debug),
    //                    nameof(LogLevel.Information),
    //                    nameof(LogLevel.Warning),
    //                    nameof(LogLevel.Error),
    //                    nameof(LogLevel.Critical)
    //                ]
    //            )]
    //            public static partial class Extensions
    //            {
    //            }
    //        }
    //        """;

    //    var (diagnostics, output) = GetGeneratedOutput(input);

    //    Assert.IsTrue(diagnostics.IsEmpty);
    //    return Verify(output);
    //}

    // TODO: Support nested type declarations
    //[TestMethod]
    //public Task TestGenerationInNestedTypeDeclaration()
    //{
    //    const string input = """
    //        using SourceGenerators;
    //        using static MqttSql.Logging.Logger;

    //        namespace MqttSql.Test.Logging;

    //        namespace NestedNamespaceTest
    //        {
    //            public static class ParentClassTest
    //            {
    //                [LoggerExtensions(
    //                    GenericOverrideCount = 3,
    //                    LogLevels = [
    //                        nameof(LogLevel.Trace),
    //                        nameof(LogLevel.Debug),
    //                        nameof(LogLevel.Information),
    //                        nameof(LogLevel.Warning),
    //                        nameof(LogLevel.Error),
    //                        nameof(LogLevel.Critical)
    //                    ]
    //                )]
    //                public static partial class Extensions
    //                {
    //                }
    //            }
    //        }
    //        """;

    //    var (diagnostics, output) = GetGeneratedOutput(input);

    //    Assert.IsTrue(diagnostics.IsEmpty);
    //    return Verify(output);
    //}
}
