using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace SourceGenerators;

internal sealed class ClassOrInterfaceAttributeSyntaxProvider
{
    private const string attributePostfix = nameof(Attribute);

    private readonly string attributeIdentifierName;
    private readonly string attributeIdentifierNameShort;

    public ClassOrInterfaceAttributeSyntaxProvider(string attributeIdentifierName, string? attributeIdentifierNameShort = null)
    {
        if (attributeIdentifierNameShort != null)
        {
            this.attributeIdentifierName = attributeIdentifierName;
            this.attributeIdentifierNameShort = attributeIdentifierNameShort;
        }
        else
        {
            if (attributeIdentifierName.EndsWith(attributePostfix))
            {
                this.attributeIdentifierName = attributeIdentifierName;
                this.attributeIdentifierNameShort = attributeIdentifierName.Substring(0, attributeIdentifierName.Length - attributePostfix.Length);
            }
            else
            {
                this.attributeIdentifierName = attributeIdentifierName + attributePostfix;
                this.attributeIdentifierNameShort = attributeIdentifierName;
            }
        }
    }

    public bool ThrowException { get; set; } = false;

    public bool Predicate(SyntaxNode syntaxNode, CancellationToken _)
    {
        return
            syntaxNode is AttributeSyntax attributeSyntax
            && attributeSyntax.Name is IdentifierNameSyntax identifierNameSyntax
            && (identifierNameSyntax.Identifier.Text == attributeIdentifierNameShort || identifierNameSyntax.Identifier.Text == attributeIdentifierName);
    }

    public Capture? Transform(GeneratorSyntaxContext context, CancellationToken _)
    {
        var attribute = (AttributeSyntax)context.Node;

        (var @class, var @interface) = attribute.FirstAncestorOrNulls<ClassDeclarationSyntax, InterfaceDeclarationSyntax>();
        if (@class == null && @interface == null)
        {
            if (ThrowException) throw new ExtensionsSourceGeneratorException("Couldn't get the class/interface marked with the attribute");
            else return null;
        }

        (var @namespace, var fileScopedNamespace) = (@class ?? (SyntaxNode)@interface!).FirstAncestorOrNulls<NamespaceDeclarationSyntax, FileScopedNamespaceDeclarationSyntax>();
        if (@namespace == null && fileScopedNamespace == null)
        {
            if (ThrowException) throw new ExtensionsSourceGeneratorException("Couldn't get the namespace enclosing the marked class/interface");
            else return null;
        }

        return new(@namespace, fileScopedNamespace, attribute, @class, @interface);
    }

    public readonly struct Capture : IEquatable<Capture>
    {
        public NamespaceDeclarationSyntax? NamespaceDeclaration { get; }
        public FileScopedNamespaceDeclarationSyntax? FileScopedNamespaceDeclaration { get; }
        public AttributeSyntax AttributeDeclaration { get; }
        public ClassDeclarationSyntax? ClassDeclaration { get; }
        public InterfaceDeclarationSyntax? InterfaceDeclaration { get; }

        public Capture(NamespaceDeclarationSyntax? @namespace, FileScopedNamespaceDeclarationSyntax? fileScopedNamespaceDeclaration, AttributeSyntax attribute, ClassDeclarationSyntax? @class, InterfaceDeclarationSyntax? @interface)
        {
            if (@class == null && @interface == null) throw new ArgumentNullException(nameof(@class), "Either a class or interface must be provided");
            if (@namespace == null && fileScopedNamespaceDeclaration == null) throw new ArgumentNullException(nameof(@class), "Either a namespace or file scoped namespace must be provided");

            NamespaceDeclaration = @namespace;
            FileScopedNamespaceDeclaration = fileScopedNamespaceDeclaration;
            AttributeDeclaration = attribute;
            ClassDeclaration = @class;
            InterfaceDeclaration = @interface;

            Namespace = (@namespace ?? (BaseNamespaceDeclarationSyntax)fileScopedNamespaceDeclaration!).Name.ToString();
            Modifiers = (@class ?? (TypeDeclarationSyntax)@interface!).Modifiers.ToString();
            Keyword = (@class ?? (TypeDeclarationSyntax)@interface!).Keyword.Text;
            Name = (@class ?? (TypeDeclarationSyntax)@interface!).Identifier.Text;
        }

        public string Namespace { get; }
        public string Modifiers { get; }
        public string Keyword { get; }
        public string Name { get; }

        public override bool Equals(object obj)
        {
            return obj is Capture capture && ((IEquatable<Capture>)this).Equals(capture);
        }

        bool IEquatable<Capture>.Equals(Capture other)
        {
            return
                (NamespaceDeclaration == null) == (other.NamespaceDeclaration == null)
                && (ClassDeclaration == null) == (other.ClassDeclaration == null)
                && Namespace.Equals(other.Namespace)
                && Name.Equals(other.Name);
        }

        public override int GetHashCode()
        {
            static int StringHash(string s) => EqualityComparer<string>.Default.GetHashCode(s);

            const int multiplier = -1521134295;
            int hashCode = 1895444307;
            hashCode = (hashCode * multiplier) + StringHash(Namespace);
            hashCode = (hashCode * multiplier) + StringHash(Modifiers);
            hashCode = (hashCode * multiplier) + StringHash(Keyword);
            hashCode = (hashCode * multiplier) + StringHash(Name);
            return hashCode;
        }
    }
}
