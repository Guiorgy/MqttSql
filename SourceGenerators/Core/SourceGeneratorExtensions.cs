/*
    This file is part of MqttSql (Copyright © 2024  Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
    You should have received a copy of the GNU General Public License along with Foobar. If not, see <https://www.gnu.org/licenses/>.
*/

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SourceGenerators;

internal static class SourceGeneratorExtensions
{
    /// <summary>
    /// Returns the first ancestor node of type <see cref="{TAncestor}"/>, or a default value if such ancestor can't be found.
    /// </summary>
    /// <typeparam name="TAncestor">The type of the ancestor.</typeparam>
    /// <param name="node">The node whose parents to search.</param>
    /// <param name="includeSelf">Whether to include self in the search.</param>
    /// <param name="default">The default value to return if no ancestor of type <see cref="{TAncestor}"/> is found.</param>
    /// <returns><paramref name="default"/> if <paramref name="node"/> is <see langword="null"/> or if no ancestor of type <see cref="{TAncestor}"/>
    /// is found; otherwise, the first ancestor of <paramref name="node"/> that is of type <see cref="{TAncestor}"/>.</returns>
    public static TAncestor? FirstAncestorOrDefault<TAncestor>(this SyntaxNode? node, bool includeSelf = false, TAncestor? @default = default) where TAncestor : SyntaxNode
    {
        var parent = includeSelf ? node : node?.Parent;

        while (parent != null)
        {
            if (parent is TAncestor Parent) return Parent;

            parent = parent.Parent;
        }

        return @default;
    }

    /// <summary>
    /// Returns the first ancestor node of type <see cref="{TAncestor1}"/> or <see cref="{TAncestor2}"/>, or <see langword="null"/>s if such ancestor can't be found.
    /// </summary>
    /// <typeparam name="TAncestor1">The first type of the ancestor.</typeparam>
    /// <typeparam name="TAncestor2">The second type of the ancestor.</typeparam>
    /// <param name="node">The node whose parents to search.</param>
    /// <param name="includeSelf">Whether to include self in the search.</param>
    /// <returns>A tuple of <see langword="null"/>s if <paramref name="node"/> is <see langword="null"/> or if no ancestor of type <see cref="{TAncestor1}"/>
    /// or <see cref="{TAncestor2}"/> is found; otherwise, the first ancestor of <paramref name="node"/> that is of type <see cref="{TAncestor1}"/> or <see cref="{TAncestor2}"/>.</returns>
    public static (TAncestor1?, TAncestor2?) FirstAncestorOrNulls<TAncestor1, TAncestor2>(this SyntaxNode? node, bool includeSelf = false) where TAncestor1 : SyntaxNode where TAncestor2 : SyntaxNode
    {
        var parent = includeSelf ? node : node?.Parent;

        while (parent != null)
        {
            if (parent is TAncestor1 Parent1) return (Parent1, null);
            if (parent is TAncestor2 Parent2) return (null, Parent2);

            parent = parent.Parent;
        }

        return (null, null);
    }

    /// <summary>
    /// Returns the first ancestor node of type <see cref="{TAncestor1}"/> or <see cref="{TAncestor2}"/> or <see cref="{TAncestor3}"/>, or <see langword="null"/>s if such ancestor can't be found.
    /// </summary>
    /// <typeparam name="TAncestor1">The first type of the ancestor.</typeparam>
    /// <typeparam name="TAncestor2">The second type of the ancestor.</typeparam>
    /// <typeparam name="TAncestor3">The third type of the ancestor.</typeparam>
    /// <param name="node">The node whose parents to search.</param>
    /// <param name="includeSelf">Whether to include self in the search.</param>
    /// <returns>A tuple of <see langword="null"/>s if <paramref name="node"/> is <see langword="null"/> or if no ancestor of type <see cref="{TAncestor1}"/>
    /// or <see cref="{TAncestor2}"/> or <see cref="{TAncestor3}"/> is found; otherwise, the first ancestor of <paramref name="node"/> that is of type
    /// <see cref="{TAncestor1}"/> or <see cref="{TAncestor2}"/> or <see cref="{TAncestor3}"/>.</returns>
    public static (TAncestor1?, TAncestor2?, TAncestor3?) FirstAncestorOrNulls<TAncestor1, TAncestor2, TAncestor3>(this SyntaxNode? node, bool includeSelf = false) where TAncestor1 : SyntaxNode where TAncestor2 : SyntaxNode where TAncestor3 : SyntaxNode
    {
        var parent = includeSelf ? node : node?.Parent;

        while (parent != null)
        {
            if (parent is TAncestor1 Parent1) return (Parent1, null, null);
            if (parent is TAncestor2 Parent2) return (null, Parent2, null);
            if (parent is TAncestor3 Parent3) return (null, null, Parent3);

            parent = parent.Parent;
        }

        return (null, null, null);
    }

    /// <summary>
    /// Returns the first ancestor node of type <see cref="{TAncestor1}"/> or <see cref="{TAncestor2}"/> or <see cref="{TAncestor3}"/> or <see cref="{TAncestor4}"/>,
    /// or <see langword="null"/>s if such ancestor can't be found.
    /// </summary>
    /// <typeparam name="TAncestor1">The first type of the ancestor.</typeparam>
    /// <typeparam name="TAncestor2">The second type of the ancestor.</typeparam>
    /// <typeparam name="TAncestor3">The third type of the ancestor.</typeparam>
    /// <typeparam name="TAncestor4">The fourth type of the ancestor.</typeparam>
    /// <param name="node">The node whose parents to search.</param>
    /// <param name="includeSelf">Whether to include self in the search.</param>
    /// <returns>A tuple of <see langword="null"/>s if <paramref name="node"/> is <see langword="null"/> or if no ancestor of type <see cref="{TAncestor1}"/>
    /// or <see cref="{TAncestor2}"/> or <see cref="{TAncestor3}"/> or <see cref="{TAncestor4}"/> is found; otherwise, the first ancestor of <paramref name="node"/>
    /// that is of type <see cref="{TAncestor1}"/> or <see cref="{TAncestor2}"/> or <see cref="{TAncestor3}"/> or <see cref="{TAncestor4}"/>.</returns>
    public static (TAncestor1?, TAncestor2?, TAncestor3?, TAncestor4?) FirstAncestorOrNulls<TAncestor1, TAncestor2, TAncestor3, TAncestor4>(this SyntaxNode? node, bool includeSelf = false) where TAncestor1 : SyntaxNode where TAncestor2 : SyntaxNode where TAncestor3 : SyntaxNode where TAncestor4 : SyntaxNode
    {
        var parent = includeSelf ? node : node?.Parent;

        while (parent != null)
        {
            if (parent is TAncestor1 Parent1) return (Parent1, null, null, null);
            if (parent is TAncestor2 Parent2) return (null, Parent2, null, null);
            if (parent is TAncestor3 Parent3) return (null, null, Parent3, null);
            if (parent is TAncestor4 Parent4) return (null, null, null, Parent4);

            parent = parent.Parent;
        }

        return (null, null, null, null);
    }

    /// <summary>
    /// Returns the first ancestor node of type <see cref="{TAncestor1}"/> or <see cref="{TAncestor2}"/> or <see cref="{TAncestor3}"/> or <see cref="{TAncestor4}"/> or <see cref="{TAncestor5}"/>,
    /// or <see langword="null"/>s if such ancestor can't be found.
    /// </summary>
    /// <typeparam name="TAncestor1">The first type of the ancestor.</typeparam>
    /// <typeparam name="TAncestor2">The second type of the ancestor.</typeparam>
    /// <typeparam name="TAncestor3">The third type of the ancestor.</typeparam>
    /// <typeparam name="TAncestor4">The fourth type of the ancestor.</typeparam>
    /// <typeparam name="TAncestor5">The fifth type of the ancestor.</typeparam>
    /// <param name="node">The node whose parents to search.</param>
    /// <param name="includeSelf">Whether to include self in the search.</param>
    /// <returns>A tuple of <see langword="null"/>s if <paramref name="node"/> is <see langword="null"/> or if no ancestor of type <see cref="{TAncestor1}"/>
    /// or <see cref="{TAncestor2}"/> or <see cref="{TAncestor3}"/> or <see cref="{TAncestor4}"/> or <see cref="{TAncestor5}"/> is found; otherwise, the first ancestor of <paramref name="node"/>
    /// that is of type <see cref="{TAncestor1}"/> or <see cref="{TAncestor2}"/> or <see cref="{TAncestor3}"/> or <see cref="{TAncestor4}"/> or <see cref="{TAncestor5}"/>.</returns>
    public static (TAncestor1?, TAncestor2?, TAncestor3?, TAncestor4?, TAncestor5?) FirstAncestorOrNulls<TAncestor1, TAncestor2, TAncestor3, TAncestor4, TAncestor5>(this SyntaxNode? node, bool includeSelf = false) where TAncestor1 : SyntaxNode where TAncestor2 : SyntaxNode where TAncestor3 : SyntaxNode where TAncestor4 : SyntaxNode where TAncestor5 : SyntaxNode
    {
        var parent = includeSelf ? node : node?.Parent;

        while (parent != null)
        {
            if (parent is TAncestor1 Parent1) return (Parent1, null, null, null, null);
            if (parent is TAncestor2 Parent2) return (null, Parent2, null, null, null);
            if (parent is TAncestor3 Parent3) return (null, null, Parent3, null, null);
            if (parent is TAncestor4 Parent4) return (null, null, null, Parent4, null);
            if (parent is TAncestor5 Parent5) return (null, null, null, null, Parent5);

            parent = parent.Parent;
        }

        return (null, null, null, null, null);
    }

    /// <summary>
    /// Get a list of using statements in the of the specified <see cref="SyntaxNode"/>.
    /// </summary>
    /// <param name="syntaxNode">The node whose parents to search.</param>
    /// <returns>The list of the found using statements.</returns>
    public static IEnumerable<string> GetUsings(this SyntaxNode? syntaxNode)
    {
        CompilationUnitSyntax? compilationSyntax = syntaxNode.FirstAncestorOrDefault<CompilationUnitSyntax>(true);

        return compilationSyntax != null
            ? compilationSyntax.Usings.Select(@using => @using.ToString())
            : [];
    }

    /// <summary>
    /// Get the enclosing namespace of the specified <see cref="SyntaxNode"/>.
    /// </summary>
    /// <param name="syntaxNode">The node to search for the namespace of.</param>
    /// <returns>The found namespace.</returns>
    public static string GetNamespace(this SyntaxNode? syntaxNode)
    {
        var (@namespace, fileScopedNamespace) = syntaxNode.FirstAncestorOrNulls<NamespaceDeclarationSyntax, FileScopedNamespaceDeclarationSyntax>();

        var baseNamespace = (BaseNamespaceDeclarationSyntax?)@namespace ?? fileScopedNamespace;

        return baseNamespace?.Name.ToString() ?? "";
    }

    /// <summary>
    /// Get the enclosing namespace of the specified <see cref="INamedTypeSymbol"/>.
    /// </summary>
    /// <param name="namedTypeSymbol">The symbol to search for the namespace of.</param>
    /// <returns>The found namespace.</returns>
    public static string GetNamespace(this INamedTypeSymbol namedTypeSymbol)
    {
        INamespaceSymbol? currentNameSpace = namedTypeSymbol.ContainingNamespace;

        if (currentNameSpace?.IsGlobalNamespace != false) return "";

        List<string> namespaceParts = [];
        do
        {
            if (!currentNameSpace.IsGlobalNamespace)
            {
                namespaceParts.Add(currentNameSpace.Name);
            }
        }
        while ((currentNameSpace = currentNameSpace!.ContainingNamespace) is not null);

        namespaceParts.Reverse();
        return string.Join(".", namespaceParts);
    }

    /// <summary>
    /// Get <see cref="AttributeData"/> for the attribute with the specified name.
    /// </summary>
    /// <param name="context">The context to search in.</param>
    /// <param name="attributeName">The attribute name to search for.</param>
    /// <returns>The <see cref="AttributeData"/> for the found attribute if found, or <see cref="null"/> if not.</returns>
    public static AttributeData? GetAttributeData(this GeneratorAttributeSyntaxContext context, string attributeName)
    {
        return context.Attributes.FirstOrDefault(attribute => attribute.AttributeClass?.Name == attributeName);
    }

    /// <summary>
    /// Gets the <see cref="AttributeSyntax"/> with the specified name on the specified <see cref="MemberDeclarationSyntax"/>.
    /// </summary>
    /// <param name="syntaxNode">The node to search for the attribute for.</param>
    /// <param name="attributeName">The name of the attribute to get.</param>
    /// <returns>The <see cref="AttributeSyntax"/> if found, or <see langword="null"/> if not.</returns>
    public static AttributeSyntax? GetAttributeSyntax(this MemberDeclarationSyntax syntaxNode, string attributeName)
    {
        string[] attributeNames = attributeName.EndsWith("Attribute")
            ? [attributeName.Substring(0, attributeName.Length - "Attribute".Length), attributeName]
            : [attributeName, attributeName + "Attribute"];

        return syntaxNode.AttributeLists
            .Select(attributeList => attributeList.Attributes.FirstOrDefault(attribute => attributeNames.Contains(attribute.Name.ToString())))
            .FirstOrDefault(attribute => attribute != null);
    }

    /// <summary>
    /// Returns a <see cref="AttributeListSyntax"/> that doesn't include the specified <see cref="AttributeSyntax"/>.
    /// </summary>
    /// <param name="list">The <see cref="AttributeListSyntax"/> to search in.</param>
    /// <param name="attribute">The <see cref="AttributeSyntax"/> to search for.</param>
    /// <returns>The initial <see cref="AttributeListSyntax"/> unchanged if <paramref name="attribute"/> wasn't found, or a new node without <paramref name="attribute"/> if found.</returns>
    public static AttributeListSyntax RemoveIfContains(this AttributeListSyntax list, AttributeSyntax attribute)
    {
        if (!list.Contains(attribute)) return list;

        return list.RemoveNode(attribute, SyntaxRemoveOptions.KeepNoTrivia)!;
    }

    /// <summary>
    /// Get the full name of the <see cref="TypedConstant"/> type.
    /// </summary>
    /// <param name="typedConstant">The target <see cref="TypedConstant"/>.</param>
    /// <returns>The full name of the underlying type.</returns>
    public static string GetTypeFullName(this TypedConstant typedConstant)
    {
        static string GetTypeFullName(ITypeSymbol? typeSymbol)
        {
            if (typeSymbol == null) return "";

            return typeSymbol.SpecialType == SpecialType.None
                ? typeSymbol.ToDisplayString()
                : typeSymbol.SpecialType.ToString().Replace("_", ".");
        }

        return GetTypeFullName(typedConstant.Type);
    }

    /// <summary>
    /// Deconstruct a <see cref="KeyValuePair{TKey, TValue}"/> into <see cref="(TKey, TValue)"/>, a key-value tuple.
    /// </summary>
    public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> source, out TKey Key, out TValue Value)
    {
        Key = source.Key;
        Value = source.Value;
    }

    /// <summary>
    /// Returns the smallest integral value greater than or equal to the specified number.
    /// </summary>
    /// <param name="decimalNumber">A double-precision floating-point number.</param>
    /// <returns>The smallest integral value that is greater than or equal to <paramref name="decimalNumber"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="decimalNumber"/> is equal to NaN, NegativeInfinity, or PositiveInfinity.</exception>
    public static int Ceiling(this double decimalNumber)
    {
        if (double.IsNaN(decimalNumber) || double.IsInfinity(decimalNumber)) throw new ArgumentException(nameof(decimalNumber));

        return (int)Math.Ceiling(decimalNumber);
    }

    /// <summary>
    /// Add a specified element to the end of an enumerable sequence.
    /// </summary>
    /// <typeparam name="T">The type of the source sequence.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="after">The element to append to the end of the sequence.</param>
    /// <returns>A new enumerable sequence that enumerates over <paramref name="source"/> and then also returns <paramref name="after"/>.</returns>
    public static IEnumerable<T> And<T>(this IEnumerable<T> source, T after)
    {
        foreach (T element in source)
            yield return element;

        yield return after;
    }

    /// <summary>
    /// Bypass elements that equal to the specified value.
    /// </summary>
    /// <typeparam name="T">The type of the source sequence.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="skip">The element to skip.</param>
    /// <returns>A new enumerable sequence with no occurrence of <paramref name="skip"/>.</returns>
    public static IEnumerable<T> Skip<T>(this IEnumerable<T> source, T skip) where T : IEquatable<T>
    {
        foreach (T element in source)
        {
            if (!element.Equals(skip))
                yield return element;
        }
    }
}
