﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Immutable;
#if LOGGEREXTENSIONSGENERATORDEBUG
using System.Diagnostics;
#endif
using System.Linq;
using System.Text;
using System.Threading;

namespace SourceGenerators;

using Capture = TypeDeclarationTreeAndAttributeData<(int genericOverrideCount, string[] logLevels)>;
using CaptureOrError = TypeDeclarationTreeAndAttributeDataOrError<(int genericOverrideCount, string[] logLevels)>;

[Generator(LanguageNames.CSharp)]
internal sealed class LoggerExtensionsSourceGenerator : IIncrementalGenerator
{
    private static readonly AttributeSource<LoggerExtensionsSourceGenerator> AttributeSource = new(
        "LoggerExtensionsAttribute",
        validOn: AttributeTargets.Class | AttributeTargets.Interface,
        allowMultiple: false,
        inherited: false,
        attributeSourceCode: """
            public string[] LogLevels { get; init; } = global::System.Array.Empty<string>();
            public int GenericOverrideCount { get; init; } = 0;
            """
    );

    private const string Indentation = "    ";
    private static readonly int AppendLineLength = new StringBuilder().AppendLine().Length;

    /*
     * 0: namespace
     * 1: class/interface modifiers
     * 2: class/interface keyword
     * 3: class/interface name
     * 4: class/interface contents (each line prefixed with a tab)
     */
    private const string ClassFormat = """
        // <auto-generated/>
        #nullable enable

        using System;
        using static {0}.Logger;

        namespace {0};

        {1} {2} {3}
        {{
        {4}
        }}
        """;

    /*
     * 0: tab
     * 1: log level
     */
    private const string ExtensionMethodFromat = """
        {0}// public static void {1}(this Logger logger, params string[] messageBits) => logger.Log(LogLevel.{1}, messageBits);
        {0}public static void {1}(this Logger logger, params object?[]? messageBits) => logger.Log(LogLevel.{1}, messageBits);
        {0}// public static void {1}(this Logger logger, Exception exception, params string[] messageBits) => logger.Log(LogLevel.{1}, exception, messageBits);
        {0}public static void {1}(this Logger logger, Exception exception, params object?[]? messageBits) => logger.Log(LogLevel.{1}, exception, messageBits);
        {0}public static void {1}(this Logger logger, Exception exception) => logger.Log(LogLevel.{1}, exception);
        """;

    /*
     * 0: tab
     * 1: log level
     * 2: generic parameters
     * 3: method parameters
     * 4: comma separated
     */
    private const string GenericExtensionMethodFormat = """
        {0}public static void {1}<{2}>(this Logger logger, {3}) {{
        {0}{0}if (!logger.EnabledFor(LogLevel.{1})) return;
        {0}{0}logger.Log(LogLevel.{1}, new object?[] {{ {4} }});
        {0}}}
        {0}public static void {1}<{2}>(this Logger logger, Exception exception, {3}) {{
        {0}{0}if (!logger.EnabledFor(LogLevel.{1})) return;
        {0}{0}logger.Log(LogLevel.{1}, exception, new object?[] {{ {4} }});
        {0}}}
        """;

    private static void GenerateSource(SourceProductionContext context, ImmutableArray<CaptureOrError?> captures)
    {
#if LOGGEREXTENSIONSGENERATORDEBUG
        if (!Debugger.IsAttached) Debugger.Launch();
#endif

        if (captures.IsDefaultOrEmpty) return;

        foreach (var capture in captures.Distinct().Select(capture => capture!.TypeDeclarationTreeAndAttributeData))
        {
            (var genericOverrideCount, var logLevels) = capture.AttributeData;

            var avgLogLevelLength = logLevels.Average(logLevel => logLevel.Length).Ceiling();
            var methodSources = new StringBuilder((
                ExtensionMethodFromat.Length + (5 * Indentation.Length) + (10 * avgLogLevelLength)
                + ((GenericExtensionMethodFormat.Length + (6 * Indentation.Length) + (3 * avgLogLevelLength) + (("T**, ".Length + "messageBit**, ".Length) * genericOverrideCount)) * genericOverrideCount)
            ) * logLevels.Length);

            foreach (var logLevel in logLevels)
            {
                methodSources.AppendFormat(ExtensionMethodFromat, Indentation, logLevel).AppendLine();
            }

            if (genericOverrideCount != 0)
            {
                var genericParameters = new StringBuilder(
                    (", ".Length * (genericOverrideCount - 1))
                    + ("T**".Length * genericOverrideCount)
                );
                var methodParameters = new StringBuilder(
                    (", ".Length * (genericOverrideCount - 1))
                    + (("T**".Length + " messageBit**".Length) * genericOverrideCount)
                );
                var newArrayValues = new StringBuilder(
                    (", ".Length * (genericOverrideCount - 1))
                    + ("messageBit**".Length * genericOverrideCount)
                );

                for (int i = 0; i < genericOverrideCount; i++)
                {
                    if (i != 0)
                    {
                        genericParameters.Append(", ");
                        methodParameters.Append(", ");
                        newArrayValues.Append(", ");
                    }

                    genericParameters.Append('T').Append(i);
                    methodParameters.Append('T').Append(i).Append(" messageBit").Append(i);
                    newArrayValues.Append("messageBit").Append(i);

                    foreach (var logLevel in logLevels)
                    {
                        methodSources.AppendFormat(GenericExtensionMethodFormat, Indentation, logLevel, genericParameters, methodParameters, newArrayValues).AppendLine();
                    }
                }
            }

            methodSources.Length -= AppendLineLength; // undo last .AppendLine()

            var source = string.Format(ClassFormat, capture.TypeDeclarationTree.Namespace, capture.TypeDeclarationTree.TypeDeclarationAncestry[0].Modifiers, capture.TypeDeclarationTree.TypeDeclarationAncestry[0].Keyword, capture.TypeDeclarationTree.TypeDeclarationAncestry[0].Name, methodSources.ToString());

            context.AddSource($"{capture.TypeDeclarationTree.TypeDeclarationAncestry[0].Name}.generated.cs", source);
        }
    }

    public static bool Predicate(SyntaxNode syntaxNode, CancellationToken _) => syntaxNode is TypeDeclarationSyntax;

    public static CaptureOrError? Transform(GeneratorAttributeSyntaxContext context, CancellationToken _)
    {
        var typeDeclarationSyntax = (TypeDeclarationSyntax)context.TargetNode;

        string @namespace = typeDeclarationSyntax.GetNamespace();
        if (string.IsNullOrEmpty(@namespace)) return new DiagnosticMessage(typeDeclarationSyntax.GetLocation(), "Couldn't get the namespace enclosing the marked class/interface");

        var attributeLocation = typeDeclarationSyntax.GetAttributeSyntax(AttributeSource.AttributeName)!.GetLocation();
        int genericOvverrideCount = 0;
        string[] logLevels = [];

        foreach ((string parameter, TypedConstant valueConstant) in context.GetAttributeData(AttributeSource.AttributeName)!.NamedArguments)
        {
            switch (parameter)
            {
                case "GenericOverrideCount":
                    if (valueConstant.Value is int value) genericOvverrideCount = value;
                    else return new DiagnosticMessage(attributeLocation, $"{parameter} must me an int");
                    break;
                case "LogLevels":
                    if (valueConstant.Values.All(vc => vc.Value is string))
                        logLevels = valueConstant.Values.Select(vc => (string)vc.Value!).ToArray();
                    else
                        return new DiagnosticMessage(attributeLocation, $"{parameter} must be a string array");
                    break;
            }
        }

        return new Capture(new(@namespace, typeDeclarationSyntax), (genericOvverrideCount, logLevels));
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.EmitAttribute(AttributeSource);

        var captures = context.SyntaxProvider
            .ForAttributeWithMetadataName(AttributeSource.AttributeFullName, Predicate, Transform)
            .Where(static capture => capture is not null)
            .WithComparer(CaptureOrError.EqualityComparer);

        var failedCaptures = captures.Where(static capture => capture!.IsError);
        context.RegisterImplementationSourceOutput(failedCaptures, static (context, capture) => capture!.DiagnosticMessage.ReportDiagnostic(context));

        var successfulCaptures = captures.Where(static capture => capture!.IsTypeDeclarationTreeAndAttributeData).Collect();
        context.RegisterSourceOutput(successfulCaptures, static (context, captures) => GenerateSource(context, captures));
    }
}
