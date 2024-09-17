// Based on the work by andrewlock licensed under the MIT license: https://github.com/andrewlock/NetEscapades.EnumGenerators/blob/446ad826d99e1d4d7acbf8414c6c19e706b14ece/tests/NetEscapades.EnumGenerators.Tests/TestHelpers.cs
// All the modifications to this file are also licensed by Guiorgy under the MIT license.

/*
The MIT License (MIT)

Copyright (c) 2019 andrewlock
              2024 Guiorgy

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using IncrementalGeneratorRunSteps = System.Collections.Immutable.ImmutableArray<Microsoft.CodeAnalysis.IncrementalGeneratorRunStep>;

namespace Tests;

internal static class GeneratorTestHelpers
{
    public static (ImmutableArray<Diagnostic> Diagnostics, string Output) GetGeneratedOutput<TGenerator, TTrackingNames>(params string[] source) where TGenerator : IIncrementalGenerator, new()
    {
        var (diagnostics, trees) = GetGeneratedTrees<TGenerator, TTrackingNames>(source);
        return (diagnostics, trees.LastOrDefault(string.Empty));
    }

    public static (ImmutableArray<Diagnostic> Diagnostics, string[] Output) GetGeneratedTrees<TGenerator, TTrackingNames>(params string[] sources) where TGenerator : IIncrementalGenerator, new()
    {
        // get all the const string fields
        var trackingNames =
            typeof(TTrackingNames)
                .GetFields()
                .Where(fi => fi.IsLiteral && !fi.IsInitOnly && fi.FieldType == typeof(string))
                .Select(x => (string?)x.GetRawConstantValue()!)
                .Where(x => !string.IsNullOrEmpty(x))
                .ToArray();

        return GetGeneratedTrees<TGenerator>(sources, trackingNames);
    }

    public static (ImmutableArray<Diagnostic> Diagnostics, string[] Output) GetGeneratedTrees<T>(string[] sources, params string[] stages) where T : IIncrementalGenerator, new()
    {
        var syntaxTrees = sources.Select(static x => CSharpSyntaxTree.ParseText(x));

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
            .Concat([
                MetadataReference.CreateFromFile(typeof(T).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.ComponentModel.DataAnnotations.DisplayAttribute).Assembly.Location),
            ]);

        var compilation = CSharpCompilation.Create(
            "generator",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        GeneratorDriverRunResult runResult = RunGeneratorAndAssertOutput<T>(compilation, stages);

        return (runResult.Diagnostics, runResult.GeneratedTrees.Select(x => x.ToString()).ToArray());
    }

    private static GeneratorDriverRunResult RunGeneratorAndAssertOutput<T>(CSharpCompilation compilation, string[] trackingNames, bool assertOutput = true) where T : IIncrementalGenerator, new()
    {
        ISourceGenerator generator = new T().AsSourceGenerator();

        var options = new GeneratorDriverOptions(
            disabledOutputs: IncrementalGeneratorOutputKind.None,
            trackIncrementalGeneratorSteps: true
        );

        GeneratorDriver driver = CSharpGeneratorDriver.Create([generator], driverOptions: options);

        var clone = compilation.Clone();
        // Run twice, once with a clone of the compilation
        // Note that we store the returned drive value, as it contains cached previous outputs
        driver = driver.RunGenerators(compilation);
        GeneratorDriverRunResult runResult = driver.GetRunResult();

        if (assertOutput)
        {
            // Run with a clone of the compilation
            GeneratorDriverRunResult runResult2 = driver.RunGenerators(clone).GetRunResult();

            AssertRunsEqual(runResult, runResult2, trackingNames);

            // verify the second run only generated cached source outputs
            Assert.IsTrue(
                runResult2.Results[0]
                    .TrackedOutputSteps
                    .SelectMany(x => x.Value) // step executions
                    .SelectMany(x => x.Outputs) // execution results
                    .All(x => x.Reason == IncrementalStepRunReason.Cached)
            );
        }

        return runResult;
    }

    private static void AssertRunsEqual(GeneratorDriverRunResult runResult1, GeneratorDriverRunResult runResult2, string[] trackingNames)
    {
        // We're given all the tracking names, but not all the stages have necessarily executed so filter
        Dictionary<string, IncrementalGeneratorRunSteps> trackedSteps1 = GetTrackedSteps(runResult1, trackingNames);
        Dictionary<string, IncrementalGeneratorRunSteps> trackedSteps2 = GetTrackedSteps(runResult2, trackingNames);

        // These should be the same
        Assert.IsTrue(trackedSteps1.Count != 0);
        Assert.AreEqual(trackedSteps1.Count, trackedSteps2.Count);
        Assert.IsTrue(trackedSteps2.Keys.All(trackedSteps1.ContainsKey));

        foreach (var trackedStep in trackedSteps1)
        {
            var trackingName = trackedStep.Key;
            var runSteps1 = trackedStep.Value;
            var runSteps2 = trackedSteps2[trackingName];
            AssertEqual(runSteps1, runSteps2, trackingName);
        }

        static Dictionary<string, IncrementalGeneratorRunSteps> GetTrackedSteps(GeneratorDriverRunResult runResult, string[] trackingNames) =>
            runResult.Results[0]
                .TrackedSteps
                .Where(step => trackingNames.Contains(step.Key))
                .ToDictionary(x => x.Key, x => x.Value);
    }

    private static void AssertEqual(IncrementalGeneratorRunSteps runSteps1, IncrementalGeneratorRunSteps runSteps2, string stepName)
    {
        Assert.AreEqual(runSteps1.Length, runSteps2.Length);

        for (var i = 0; i < runSteps1.Length; i++)
        {
            var runStep1 = runSteps1[i];
            var runStep2 = runSteps2[i];

            // The outputs should be equal between different runs
            IEnumerable<object> outputs1 = runStep1.Outputs.Select(x => x.Value);
            IEnumerable<object> outputs2 = runStep2.Outputs.Select(x => x.Value);

            Assert.IsTrue(outputs1.SequenceEqual(outputs2), $"because {stepName} should produce cacheable outputs");

            // Therefore, on the second run the results should always be cached or unchanged!
            // - Unchanged is when the _input_ has changed, but the output hasn't
            // - Cached is when the the input has not changed, so the cached output is used
            Assert.IsTrue(
                runStep2.Outputs.All(x => x.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged),
                $"{stepName} expected to have reason {IncrementalStepRunReason.Cached} or {IncrementalStepRunReason.Unchanged}"
            );

            // Make sure we're not using anything we shouldn't
            AssertObjectGraph(runStep1, stepName);
            AssertObjectGraph(runStep2, stepName);
        }

        static void AssertObjectGraph(IncrementalGeneratorRunStep runStep, string stepName)
        {
            var because = $"{stepName} shouldn't contain banned symbols";
            var visited = new HashSet<object>();

            foreach (var (obj, _) in runStep.Outputs)
            {
                Visit(obj);
            }

            void Visit(object? node)
            {
                if (node is null || !visited.Add(node))
                {
                    return;
                }

                Assert.IsNotInstanceOfType<Compilation>(node, because);
                Assert.IsNotInstanceOfType<ISymbol>(node, because);
                Assert.IsNotInstanceOfType<SyntaxNode>(node, because);

                Type type = node.GetType();
                if (type.IsPrimitive || type.IsEnum || type == typeof(string))
                {
                    return;
                }

                if (node is IEnumerable collection and not string)
                {
                    foreach (object element in collection)
                    {
                        Visit(element);
                    }

                    return;
                }

                foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    object? fieldValue = field.GetValue(node);
                    Visit(fieldValue);
                }
            }
        }
    }
}
