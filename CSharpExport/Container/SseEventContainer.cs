using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpExport.Container;

internal sealed class SseEventContainer(INamedTypeSymbol type, ClassDeclarationSyntax declaration,
    CompilationUnitSyntax root, SemanticModel model, INamedTypeSymbol? noExport,
    Compilation compilation, IReadOnlyList<INamedTypeSymbol> endPoints)
    : BaseContainer(type, declaration, root, model, noExport)
{
    public override string Usings => "";

    public override string Write()
    {
        if (Type.ContainingType != null || Type.IsGenericType)
            throw new NotSupportedException($"Événement SSE imbriqué ou générique non pris en charge : {Type}");
        var payload = FindPayloadType(Type);
        var payloadName = payload?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "object";
        if (payloadName.StartsWith("global::AventusSharp", StringComparison.Ordinal))
            throw new NotSupportedException($"Le corps de {Type} dépend d'AventusSharp : {payloadName}");
        var topic = SseMetadata.ConstantReturn(Type, "GetTopic", compilation) as string;
        var endpoint = SelectedEndPoint();
        AddTxt("namespace " + Namespace);
        AddTxtOpen("{");
        AddTxt("public " + (Type.IsAbstract ? "abstract " : "") + "partial class " + Type.Name +
            " : global::" + RuntimeNamespace + ".Sse.SseEventClient<" + payloadName + ">");
        AddTxtOpen("{");
        AddTxt("public " + Type.Name + "(global::" + RuntimeNamespace + ".Sse.SseEndPointClient? endpoint = null, string? topic = null)");
        AddTxt("    : base(" + (endpoint == null
            ? "endpoint ?? throw new global::System.ArgumentNullException(nameof(endpoint))"
            : "endpoint ?? new " + endpoint + "()") + ", topic ?? " +
            (topic == null ? "throw new global::System.ArgumentNullException(nameof(topic))" : SseMetadata.Literal(topic)) + ") { }");
        AddTxtClose("}");
        AddTxtClose("}");
        return fileWriter.GetContent();
    }

    internal static ITypeSymbol? FindPayloadType(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current != null; current = current.BaseType)
            if (current.OriginalDefinition.ToDisplayString() == "AventusSharp.SSE.Event.SSEEvent<T>")
                return current.TypeArguments[0];
        return null;
    }

    private string? SelectedEndPoint()
    {
        for (INamedTypeSymbol? current = Type; current != null; current = current.BaseType)
        foreach (var attribute in current.GetAttributes())
        {
            if (attribute.AttributeClass?.ContainingNamespace.ToDisplayString() != "AventusSharp.SSE.Attributes" ||
                !attribute.AttributeClass.Name.StartsWith("EndPoint", StringComparison.Ordinal)) continue;
            var selected = attribute.AttributeClass.TypeArguments.FirstOrDefault() as INamedTypeSymbol ??
                attribute.ConstructorArguments.FirstOrDefault().Value as INamedTypeSymbol;
            if (selected != null)
            {
                if (!endPoints.Any(e => SymbolEqualityComparer.Default.Equals(e, selected)))
                    throw new InvalidOperationException($"L'endpoint SSE {selected} de {Type} n'est pas exporté.");
                return selected.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }
        }
        var mains = endPoints.Where(e => SseMetadata.ConstantReturn(e, "Main", compilation) is true).ToList();
        if (mains.Count > 1) throw new InvalidOperationException("Plusieurs endpoints SSE principaux sont exportés.");
        var chosen = mains.Count == 1 ? mains[0] : endPoints.Count == 1 ? endPoints[0] : null;
        return chosen?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }
}
