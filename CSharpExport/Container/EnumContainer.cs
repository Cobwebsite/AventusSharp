using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpExport.Container;

internal sealed class EnumContainer(INamedTypeSymbol type, EnumDeclarationSyntax declaration,
    CompilationUnitSyntax root, SemanticModel model, INamedTypeSymbol? noExport)
    : BaseContainer(type, declaration, root, model, noExport)
{
    public override string Usings => string.Join("\n", Root.Usings
        .Where(u => u.Name?.ToString().StartsWith("AventusSharp", StringComparison.Ordinal) != true)
        .Select(u => u.ToFullString().Trim()));

    public override string Write()
    {
        if (Type.ContainingType != null)
            throw new NotSupportedException($"Enum imbriquée non prise en charge : {Type}");
        var syntax = declaration.WithAttributeLists(PublicAttributes(Type, declaration.AttributeLists))
            .WithMembers(SyntaxFactory.SeparatedList(declaration.Members.Where(member =>
                Model.GetDeclaredSymbol(member) is not IFieldSymbol field || !HasAttribute(field, NoExport))));
        AddTxt("namespace " + Namespace);
        AddTxtOpen("{");
        foreach (var line in syntax.NormalizeWhitespace(indentation: "\t").ToFullString().Split('\n'))
            AddTxt(line.TrimEnd('\r'));
        AddTxtClose("}");
        return fileWriter.GetContent();
    }
}
