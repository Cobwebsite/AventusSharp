using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpExport.Container;

internal abstract class BaseContainer(
    INamedTypeSymbol type, BaseTypeDeclarationSyntax declaration,
    CompilationUnitSyntax root, SemanticModel model, INamedTypeSymbol? noExport)
{
    protected INamedTypeSymbol Type { get; } = type;
    protected BaseTypeDeclarationSyntax Declaration { get; } = declaration;
    protected CompilationUnitSyntax Root { get; } = root;
    protected SemanticModel Model { get; } = model;
    protected INamedTypeSymbol? NoExport { get; } = noExport;
    public string Namespace => Type.ContainingNamespace.ToDisplayString();
    public string RuntimeNamespace { get; set; } = "CSharpExport.Generated";
    public string SourceFile => Type.Locations.First().SourceTree?.FilePath
        ?? throw new InvalidOperationException($"Fichier source introuvable pour {Type}");
    public virtual string Usings => Root.Usings.ToFullString();
    public abstract string Write();
    protected FileWriter fileWriter = new();
    protected string GetIndentedText(string text) => fileWriter.GetIndentedText(text);
    protected void AddTxt(string text, List<string>? result = null) => fileWriter.AddTxt(text, result);
    protected void AddTxt(IEnumerable<string> texts, List<string>? result = null) => fileWriter.AddTxt(texts, result);
    protected void AddTxtOpen(string text, List<string>? result = null) => fileWriter.AddTxtOpen(text, result);
    protected void AddTxtClose(string text, List<string>? result = null) => fileWriter.AddTxtClose(text, result);
    protected void AddIndent() => fileWriter.AddIndent();
    protected void RemoveIndent() => fileWriter.RemoveIndent();
    protected static bool HasAttribute(ISymbol symbol, INamedTypeSymbol? attribute) => attribute != null &&
        symbol.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attribute));
    protected static SyntaxList<AttributeListSyntax> PublicAttributes(ISymbol symbol, SyntaxList<AttributeListSyntax> lists)
    {
        var retained = new List<AttributeListSyntax>();
        foreach (var list in lists)
        {
            var attributes = list.Attributes.Where(syntax => !symbol.GetAttributes().Any(attribute =>
                attribute.ApplicationSyntaxReference?.SyntaxTree == syntax.SyntaxTree &&
                attribute.ApplicationSyntaxReference.Span == syntax.Span &&
                attribute.AttributeClass?.ContainingNamespace.ToDisplayString().StartsWith("AventusSharp", StringComparison.Ordinal) == true));
            var filtered = list.WithAttributes(SyntaxFactory.SeparatedList(attributes));
            if (filtered.Attributes.Count > 0) retained.Add(filtered);
        }
        return SyntaxFactory.List(retained);
    }
}
