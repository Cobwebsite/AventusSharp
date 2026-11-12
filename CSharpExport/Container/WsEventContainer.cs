using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace CSharpExport.Container;

internal sealed class WsEventContainer(INamedTypeSymbol type, ClassDeclarationSyntax declaration,
    CompilationUnitSyntax root, SemanticModel model, INamedTypeSymbol? noExport,
    IReadOnlyList<INamedTypeSymbol> endPoints, Compilation compilation)
    : BaseContainer(type, declaration, root, model, noExport)
{
    public override string Usings => "";

    public override string Write()
    {
        if (Type.ContainingType != null || Type.IsGenericType)
            throw new NotSupportedException($"Événement WS imbriqué ou générique non pris en charge : {Type}");
        var payload = FindPayloadType(Type);
        var payloadName = payload?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "object";
        var topic = Type.GetAttributes().FirstOrDefault(a =>
            a.AttributeClass?.ToDisplayString() == "AventusSharp.WebSocket.Attributes.Path")?
            .ConstructorArguments.FirstOrDefault().Value as string ?? Type.ToDisplayString();
        var dynamicParts = Regex.Matches(topic, @"\[([A-Za-z0-9_]+)\]")
            .Select(match => match.Groups[1].Value).Distinct().ToList();
        foreach (var part in dynamicParts)
        {
            var constant = SseMetadata.ConstantReturn(Type, part, compilation);
            if (constant != null)
                topic = topic.Replace("[" + part + "]", Convert.ToString(constant,
                    System.Globalization.CultureInfo.InvariantCulture));
        }
        dynamicParts = Regex.Matches(topic, @"\[([A-Za-z0-9_]+)\]")
            .Select(match => match.Groups[1].Value).Distinct().ToList();
        var endpoint = SelectedEndPoint();
        AddTxt("namespace " + Namespace);
        AddTxtOpen("{");
        AddTxt("public " + (Type.IsAbstract ? "abstract " : "") + "partial class " + Type.Name +
            " : global::" + RuntimeNamespace + ".Ws.WsEventClient<" + payloadName + ">");
        AddTxtOpen("{");
        AddTxt("public " + Type.Name + "(global::" + RuntimeNamespace + ".Ws.WsEndPointClient? endpoint = null, string? topic = null" +
            string.Concat(dynamicParts.Select(part => ", global::System.Func<string>? " + part + " = null")) + ")");
        AddTxt("    : base(" + (endpoint == null
            ? "endpoint ?? throw new global::System.ArgumentNullException(nameof(endpoint))"
            : "endpoint ?? new " + endpoint + "()") + ", " + (dynamicParts.Count == 0
                ? "topic ?? " + SseMetadata.Literal(topic)
                : "() => topic ?? " + BuildDynamicTopic(topic, dynamicParts)) + ") { }");
        AddTxtClose("}");
        AddTxtClose("}");
        return fileWriter.GetContent();
    }

    private static string BuildDynamicTopic(string topic, IReadOnlyList<string> parts)
    {
        var expression = SseMetadata.Literal(topic);
        foreach (var part in parts)
            expression += ".Replace(" + SseMetadata.Literal("[" + part + "]") +
                ", (" + part + " ?? throw new global::System.ArgumentNullException(nameof(" + part + ")))())";
        return expression;
    }

    internal static ITypeSymbol? FindPayloadType(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current != null; current = current.BaseType)
            if (current.OriginalDefinition.ToDisplayString() == "AventusSharp.WebSocket.Event.WsEvent<T>")
                return current.TypeArguments[0];
        return null;
    }

    private string? SelectedEndPoint()
    {
        foreach (var attribute in Type.GetAttributes())
        {
            if (attribute.AttributeClass?.ContainingNamespace.ToDisplayString() != "AventusSharp.WebSocket.Attributes" ||
                !attribute.AttributeClass.Name.StartsWith("EndPoint", StringComparison.Ordinal)) continue;
            var selected = attribute.AttributeClass.TypeArguments.FirstOrDefault() as INamedTypeSymbol ??
                attribute.ConstructorArguments.FirstOrDefault().Value as INamedTypeSymbol;
            if (selected != null)
            {
                if (!endPoints.Any(e => SymbolEqualityComparer.Default.Equals(e, selected)))
                    throw new InvalidOperationException($"L'endpoint WS {selected} de {Type} n'est pas exporté.");
                return selected.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }
        }
        var main = endPoints.Where(e => SseMetadata.ConstantReturn(e, "Main", compilation) is true).ToList();
        if (main.Count > 1) throw new InvalidOperationException("Plusieurs endpoints WS principaux sont exportés.");
        var selectedDefault = main.Count == 1 ? main[0] : endPoints.Count == 1 ? endPoints[0] : null;
        return selectedDefault?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }
}
