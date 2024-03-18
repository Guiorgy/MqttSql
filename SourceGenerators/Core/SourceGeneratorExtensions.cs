using Microsoft.CodeAnalysis;
using System;

namespace SourceGenerators;

public static class SourceGeneratorExtensions
{
    /// <summary>
    /// Returns the first ancestor node of type <see cref="{TAncestor}"/>, or a default value if such ancestor can't be found.
    /// </summary>
    /// <typeparam name="TAncestor">The type of the ancestor.</typeparam>
    /// <param name="node">The node.</param>
    /// <param name="default">The default value to return if no ancestor of type <see cref="{TAncestor}"/> is found.</param>
    /// <returns><paramref name="default"/> if <paramref name="node"/> is <see langword="null"/> or if no ancestor of type <see cref="{TAncestor}"/>
    /// is found; otherwise, the first ancestor of <paramref name="node"/> that is of type <see cref="{TAncestor}"/>.</returns>
    public static TAncestor? FirstAncestorOrDefault<TAncestor>(this SyntaxNode? node, TAncestor? @default = default) where TAncestor : SyntaxNode
    {
        if (node == null) return @default;

        var parent = node.Parent;

        while (parent != null)
        {
            if (parent is TAncestor Parent) return Parent;

            parent = parent.Parent;
        }

        return @default;
    }

    /// <summary>
    /// Returns the first ancestor node of type <see cref="{TAncestor1}"/> or <see cref="{TAncestor2}"/>, or nulls if such ancestor can't be found.
    /// </summary>
    /// <typeparam name="TAncestor1">The first type of the ancestor.</typeparam>
    /// <typeparam name="TAncestor2">The second type of the ancestor.</typeparam>
    /// <param name="node">The node.</param>
    /// <returns>A tuple of <see langword="null"/>s if <paramref name="node"/> is <see langword="null"/> or if no ancestor of type <see cref="{TAncestor1}"/>
    /// or <see cref="{TAncestor2}"/> is found; otherwise, the first ancestor of <paramref name="node"/> that is of type <see cref="{TAncestor1}"/> or <see cref="{TAncestor2}"/>.</returns>
    public static (TAncestor1?, TAncestor2?) FirstAncestorOrNulls<TAncestor1, TAncestor2>(this SyntaxNode? node) where TAncestor1 : SyntaxNode where TAncestor2 : SyntaxNode
    {
        if (node == null) return (null, null);

        var parent = node.Parent;

        while (parent != null)
        {
            if (parent is TAncestor1 Parent1) return (Parent1, null);
            if (parent is TAncestor2 Parent2) return (null, Parent2);

            parent = parent.Parent;
        }

        return (null, null);
    }

    /// <summary>
    /// Returns the first ancestor node of type <see cref="{TAncestor1}"/> or <see cref="{TAncestor2}"/> or <see cref="{TAncestor3}"/>, or nulls if such ancestor can't be found.
    /// </summary>
    /// <typeparam name="TAncestor1">The first type of the ancestor.</typeparam>
    /// <typeparam name="TAncestor2">The second type of the ancestor.</typeparam>
    /// <typeparam name="TAncestor3">The third type of the ancestor.</typeparam>
    /// <param name="node">The node.</param>
    /// <returns>A tuple of <see langword="null"/>s if <paramref name="node"/> is <see langword="null"/> or if no ancestor of type <see cref="{TAncestor1}"/>
    /// or <see cref="{TAncestor2}"/> or <see cref="{TAncestor3}"/> is found; otherwise, the first ancestor of <paramref name="node"/> that is of type
    /// <see cref="{TAncestor1}"/> or <see cref="{TAncestor2}"/> or <see cref="{TAncestor3}"/>.</returns>
    public static (TAncestor1?, TAncestor2?, TAncestor3?) FirstAncestorOrNulls<TAncestor1, TAncestor2, TAncestor3>(this SyntaxNode? node) where TAncestor1 : SyntaxNode where TAncestor2 : SyntaxNode where TAncestor3 : SyntaxNode
    {
        if (node == null) return (null, null, null);

        var parent = node.Parent;

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
    /// or nulls if such ancestor can't be found.
    /// </summary>
    /// <typeparam name="TAncestor1">The first type of the ancestor.</typeparam>
    /// <typeparam name="TAncestor2">The second type of the ancestor.</typeparam>
    /// <typeparam name="TAncestor3">The third type of the ancestor.</typeparam>
    /// <param name="node">The node.</param>
    /// <returns>A tuple of <see langword="null"/>s if <paramref name="node"/> is <see langword="null"/> or if no ancestor of type <see cref="{TAncestor1}"/>
    /// or <see cref="{TAncestor2}"/> or <see cref="{TAncestor3}"/> or <see cref="{TAncestor4}"/> is found; otherwise, the first ancestor of <paramref name="node"/>
    /// that is of type <see cref="{TAncestor1}"/> or <see cref="{TAncestor2}"/> or <see cref="{TAncestor3}"/> or <see cref="{TAncestor4}"/>.</returns>
    public static (TAncestor1?, TAncestor2?, TAncestor3?, TAncestor4?) FirstAncestorOrNulls<TAncestor1, TAncestor2, TAncestor3, TAncestor4>(this SyntaxNode? node) where TAncestor1 : SyntaxNode where TAncestor2 : SyntaxNode where TAncestor3 : SyntaxNode where TAncestor4 : SyntaxNode
    {
        if (node == null) return (null, null, null, null);

        var parent = node.Parent;

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
}
