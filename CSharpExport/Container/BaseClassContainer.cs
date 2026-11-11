using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpExport.Container;

internal abstract class BaseClassContainer(
    INamedTypeSymbol type, ClassDeclarationSyntax declaration,
    CompilationUnitSyntax root, SemanticModel model, INamedTypeSymbol? noExport)
    : BaseContainer(type, declaration, root, model, noExport)
{
    public override string Usings => string.Join("\n", Root.Usings
        .Where(u => u.Name?.ToString().StartsWith("AventusSharp", StringComparison.Ordinal) != true)
        .Select(u => u.ToFullString().Trim())
        .Concat(PropertySymbols().SelectMany(x => x.Property.GetAttributes())
            .Where(a => a.AttributeClass != null &&
                !a.AttributeClass.ContainingNamespace.ToDisplayString().StartsWith("AventusSharp", StringComparison.Ordinal))
            .Select(a => "using " + a.AttributeClass!.ContainingNamespace.ToDisplayString() + ";"))
        .Distinct());

    private static bool IsAventusType(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol array => IsAventusType(array.ElementType),
        INamedTypeSymbol named => named.ContainingNamespace.ToDisplayString().StartsWith("AventusSharp", StringComparison.Ordinal) ||
            named.TypeArguments.Any(IsAventusType),
        _ => false
    };

    private IEnumerable<(IPropertySymbol Property, INamedTypeSymbol Owner)> PropertySymbols()
    {
        var seen = new HashSet<string>();
        for (INamedTypeSymbol? current = Type; current != null; current = current.BaseType)
        {
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (property.IsStatic || property.IsIndexer || property.DeclaredAccessibility != Accessibility.Public ||
                    HasAttribute(property, NoExport) || !seen.Add(property.Name)) continue;
                yield return (property, current);
            }
        }
    }

    public override string Write()
    {
        if (Type.ContainingType != null)
            throw new NotSupportedException($"Storable imbriqué non pris en charge : {Type}");
        var properties = PropertySymbols().Select(entry =>
        {
            var (property, owner) = entry;
            if (IsAventusType(property.Type))
                throw new NotSupportedException($"{Type}.{property.Name} utilise le type AventusSharp {property.Type}. Un contrat indépendant est nécessaire.");
            var p = property.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as PropertyDeclarationSyntax;
            if (p?.AccessorList == null) return null;
            return p.WithInitializer(null).WithExpressionBody(null).WithSemicolonToken(default)
                .WithType(UsesOwnerTypeParameter(p.Type, owner)
                    ? SyntaxFactory.ParseTypeName(RenderType(property.Type, owner)) : p.Type)
                .WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.List(
                    (p.AccessorList?.Accessors ?? default).Select(a => a.WithBody(null).WithExpressionBody(null)
                        .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))))))
                .WithAttributeLists(PublicAttributes(property, p.AttributeLists))
                .WithModifiers(SyntaxFactory.TokenList(p.Modifiers.Where(m =>
                    !m.IsKind(SyntaxKind.OverrideKeyword) && !m.IsKind(SyntaxKind.AbstractKeyword))));
        }).Where(p => p != null).Select(p => p!)
            .ToArray();
        var exportedClass = declaration.WithBaseList(null).WithConstraintClauses(default)
            .WithAttributeLists(PublicAttributes(Type, Declaration.AttributeLists))
            .WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>(properties))
            .WithModifiers(SyntaxFactory.TokenList(Declaration.Modifiers.Where(m => !m.IsKind(SyntaxKind.PartialKeyword))));
        AddTxt("namespace " + Namespace);
        AddTxtOpen("{");
        foreach (var line in exportedClass.NormalizeWhitespace(indentation: "\t").ToFullString().Split('\n'))
            AddTxt(line.TrimEnd('\r'));
        AddTxtClose("}");
        return fileWriter.GetContent();
    }

    private static string RenderType(ITypeSymbol type, INamedTypeSymbol owner)
    {
        if (type is ITypeParameterSymbol parameter)
        {
            for (var i = 0; i < owner.TypeParameters.Length; i++)
                if (SymbolEqualityComparer.Default.Equals(owner.TypeParameters[i], parameter))
                    return RenderType(owner.TypeArguments[i], owner);
        }
        if (type is IArrayTypeSymbol array)
            return RenderType(array.ElementType, owner) + "[" + new string(',', array.Rank - 1) + "]";
        if (type is INamedTypeSymbol named && named.IsGenericType)
            return named.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                .Split('<')[0] + "<" +
                string.Join(", ", named.TypeArguments.Select(arg => RenderType(arg, owner))) + ">";
        return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    private static bool UsesOwnerTypeParameter(TypeSyntax type, INamedTypeSymbol owner) =>
        type.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
            .Any(name => owner.TypeParameters.Any(parameter => parameter.Name == name.Identifier.ValueText));
}
