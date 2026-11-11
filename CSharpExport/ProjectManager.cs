using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using CSharpExport.Container;

namespace CSharpExport;

internal sealed class ProjectManager
{
    public async Task<int> Init(ProjectConfig config)
    {
        MSBuildLocator.RegisterDefaults();
        using var workspace = MSBuildWorkspace.Create();
        var project = await workspace.OpenProjectAsync(config.CsProj);
        var compilation = await project.GetCompilationAsync()
            ?? throw new InvalidOperationException("Compilation Roslyn indisponible.");
        var storable = compilation.GetTypeByMetadataName("AventusSharp.Data.IStorable")
            ?? throw new InvalidOperationException("IStorable est introuvable dans le projet source.");
        var noExport = compilation.GetTypeByMetadataName("AventusSharp.Tools.Attributes.NoExport");
        var export = compilation.GetTypeByMetadataName("AventusSharp.Tools.Attributes.Export");
        var genericError = compilation.GetTypeByMetadataName("AventusSharp.Tools.IGenericError");
        var withError = compilation.GetTypeByMetadataName("AventusSharp.Tools.IWithError");
        var files = new FileToWrite(config);
        foreach (var document in project.Documents)
        {
            if (await document.GetSyntaxRootAsync() is not CompilationUnitSyntax root) continue;
            var model = compilation.GetSemanticModel(root.SyntaxTree);
            foreach (var declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol) continue;
                if (HasAttribute(symbol, noExport)) continue;
                if (Implements(symbol, storable))
                    files.RegisterType(new StorableContainer(symbol, declaration, root, model, noExport));
                else if (Implements(symbol, genericError) &&
                    (config.ExportErrorsByDefault || HasAttribute(symbol, export)))
                    files.RegisterType(new GenericErrorContainer(symbol, declaration, root, model, noExport));
                else if (Implements(symbol, withError) &&
                    (config.ExportErrorsByDefault || HasAttribute(symbol, export)))
                    files.RegisterType(new WithErrorContainer(symbol, declaration, root, model, noExport));
                else if (HasAttribute(symbol, export))
                    files.RegisterType(new NormalClassContainer(symbol, declaration, root, model, noExport));
            }
            foreach (var declaration in root.DescendantNodes().OfType<EnumDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol) continue;
                if (!HasAttribute(symbol, noExport) && (config.ExportEnumByDefault || HasAttribute(symbol, export)))
                    files.RegisterType(new EnumContainer(symbol, declaration, root, model, noExport));
            }
        }
        return await files.WriteAll();
    }

    private static bool HasAttribute(ISymbol symbol, INamedTypeSymbol? attribute) => attribute != null &&
        symbol.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attribute));

    private static bool Implements(INamedTypeSymbol type, INamedTypeSymbol? contract) => contract != null &&
        type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, contract));
}
