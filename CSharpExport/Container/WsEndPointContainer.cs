using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpExport.Container;

internal sealed class WsEndPointContainer(INamedTypeSymbol type, ClassDeclarationSyntax declaration,
    CompilationUnitSyntax root, SemanticModel model, INamedTypeSymbol? noExport, WsConfig config,
    Compilation compilation)
    : BaseContainer(type, declaration, root, model, noExport)
{
    public override string Usings => "";

    public override string Write()
    {
        if (Type.ContainingType != null || Type.IsGenericType)
            throw new NotSupportedException($"Endpoint WS imbriqué ou générique non pris en charge : {Type}");
        var path = SseMetadata.ConstantReturn(Type, "DefinePath", compilation) as string;
        var baseUrl = ((config.Host ?? "") + (config.Uri ?? "")).TrimEnd('/');
        AddTxt("namespace " + Namespace);
        AddTxtOpen("{");
        AddTxt("public " + (Type.IsAbstract ? "abstract " : "") + "partial class " + Type.Name +
            " : global::" + RuntimeNamespace + ".Ws.WsEndPointClient");
        AddTxtOpen("{");
        AddTxt("public " + Type.Name + "(string? baseUrl = null, string? path = null,");
        AddTxt("    bool? autoReconnect = null, global::System.TimeSpan? reconnectDelay = null,");
        AddTxt("    global::System.TimeSpan? requestTimeout = null,");
        AddTxt("    global::System.Action<global::System.Net.WebSockets.ClientWebSocketOptions>? configureWebSocket = null)");
        AddTxt("    : base(path ?? " + (path == null
            ? "throw new global::System.ArgumentNullException(nameof(path))"
            : SseMetadata.Literal(path)) + ", baseUrl ?? " + SseMetadata.Literal(baseUrl) + ")");
        AddTxt("    { AutoReconnect = autoReconnect ?? " + (config.AutoReconnect ? "true" : "false") + ";");
        AddTxt("      ReconnectDelay = reconnectDelay ?? global::System.TimeSpan.FromMilliseconds(" + config.ReconnectDelayMs + ");");
        AddTxt("      RequestTimeout = requestTimeout ?? global::System.TimeSpan.FromMilliseconds(" + config.RequestTimeoutMs + ");");
        AddTxt("      ConfigureWebSocket = configureWebSocket; }");
        AddTxtClose("}");
        AddTxtClose("}");
        return fileWriter.GetContent();
    }
}
