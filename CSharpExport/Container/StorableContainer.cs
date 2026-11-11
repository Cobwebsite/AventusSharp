using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpExport.Container;

internal sealed class StorableContainer(INamedTypeSymbol type, ClassDeclarationSyntax declaration,
    CompilationUnitSyntax root, SemanticModel model, INamedTypeSymbol? noExport)
    : BaseClassContainer(type, declaration, root, model, noExport);
