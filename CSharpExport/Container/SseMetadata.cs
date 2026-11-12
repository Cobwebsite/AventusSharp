using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpExport.Container;

internal static class SseMetadata
{
    public static object? ConstantReturn(INamedTypeSymbol type, string methodName, Compilation compilation)
    {
        for (INamedTypeSymbol? current = type; current != null; current = current.BaseType)
        {
            foreach (var method in current.GetMembers(methodName).OfType<IMethodSymbol>())
            foreach (var reference in method.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax() is not MethodDeclarationSyntax declaration) continue;
                var expression = declaration.ExpressionBody?.Expression;
                if (expression == null && declaration.Body?.Statements.Count == 1 &&
                    declaration.Body.Statements[0] is ReturnStatementSyntax returned)
                    expression = returned.Expression;
                if (expression == null) continue;
                if (!compilation.SyntaxTrees.Contains(expression.SyntaxTree)) continue;
                var constant = compilation.GetSemanticModel(expression.SyntaxTree).GetConstantValue(expression);
                if (constant.HasValue) return constant.Value;
            }
        }
        return null;
    }

    public static string Literal(string value) => System.Text.Json.JsonSerializer.Serialize(value);
}
