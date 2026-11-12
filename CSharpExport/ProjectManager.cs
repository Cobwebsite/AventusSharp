using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using CSharpExport.Container;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

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
        var router = compilation.GetTypeByMetadataName("AventusSharp.Routes.IRouter");
        List<CompiledHttpRoute>? compiledRoutes = null;
        var files = new FileToWrite(config);
        foreach (var document in project.Documents)
        {
            if (await document.GetSyntaxRootAsync() is not CompilationUnitSyntax root) continue;
            var model = compilation.GetSemanticModel(root.SyntaxTree);
            foreach (var declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol) continue;
                if (HasAttribute(symbol, noExport)) continue;
                if (Implements(symbol, router) &&
                    (config.ExportHttpRouteByDefault || HasAttribute(symbol, export)))
                {
                    compiledRoutes ??= await LoadCompiledRoutes(config);
                    files.RegisterType(new HttpRouterContainer(symbol, declaration, root, model, noExport,
                        config.HttpRouter, compiledRoutes.Where(r => r.ClassName == symbol.ToDisplayString()).ToList()));
                }
                else if (Implements(symbol, storable))
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

    private static async Task<List<CompiledHttpRoute>> LoadCompiledRoutes(ProjectConfig config)
    {
        using var process = Process.Start(new ProcessStartInfo("dotnet")
        {
            ArgumentList = { "run", "--project", config.CsProj, "--", "--export-info" },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        }) ?? throw new InvalidOperationException("Impossible de lancer le projet source.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
            throw new InvalidOperationException("Export des routes compilées impossible : " + await error);
        var text = await output;
        var match = Regex.Match(text, @"--- Routes HTTP ---\s*(.*?)\s*-------------------", RegexOptions.Singleline);
        if (!match.Success)
            throw new InvalidOperationException("Le projet source doit appeler UseAventusExport() apres l enregistrement des routes HTTP.");
        return JsonSerializer.Deserialize<List<CompiledHttpRoute>>(match.Groups[1].Value,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
    }
}
