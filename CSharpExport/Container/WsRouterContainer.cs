using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpExport.Container;

internal sealed class WsRouterContainer(INamedTypeSymbol type, ClassDeclarationSyntax declaration,
    CompilationUnitSyntax root, SemanticModel model, INamedTypeSymbol? noExport,
    WsConfig config, IReadOnlyList<CompiledWsRoute> compiledRoutes)
    : BaseContainer(type, declaration, root, model, noExport)
{
    public override string Usings => "";

    public override string Write()
    {
        if (Type.ContainingType != null)
            throw new NotSupportedException($"Routeur WS imbriqué non pris en charge : {Type}");
        var baseUrl = ((config.Host ?? "") + (config.Uri ?? "")).TrimEnd('/');
        AddTxt("namespace " + Namespace);
        AddTxtOpen("{");
        var generic = Type.TypeParameters.Length == 0 ? "" :
            "<" + string.Join(", ", Type.TypeParameters.Select(p => p.Name)) + ">";
        AddTxt("public " + (Type.IsAbstract ? "abstract " : "") + "partial class " + Type.Name + generic +
            " : global::" + RuntimeNamespace + ".Ws.WsRouterClient");
        AddTxtOpen("{");
        AddTxt("public " + Type.Name + "(string? baseUrl = null, bool? autoReconnect = null,");
        AddTxt("    global::System.TimeSpan? reconnectDelay = null, global::System.TimeSpan? requestTimeout = null,");
        AddTxt("    global::System.Action<global::System.Net.WebSockets.ClientWebSocketOptions>? configureWebSocket = null)");
        AddTxt("    : base(baseUrl ?? " + SseMetadata.Literal(baseUrl) + ", autoReconnect ?? " +
            (config.AutoReconnect ? "true" : "false") + ", reconnectDelay ?? global::System.TimeSpan.FromMilliseconds(" +
            config.ReconnectDelayMs + "), requestTimeout ?? global::System.TimeSpan.FromMilliseconds(" +
            config.RequestTimeoutMs + "), configureWebSocket) { }");
        var visited = new HashSet<string>();
        for (INamedTypeSymbol? owner = Type; owner != null && owner.SpecialType != SpecialType.System_Object;
             owner = owner.BaseType)
        foreach (var method in owner.GetMembers().OfType<IMethodSymbol>())
        {
            if (method.MethodKind != MethodKind.Ordinary || method.IsStatic ||
                method.DeclaredAccessibility != Accessibility.Public || HasAttribute(method, NoExport) ||
                method.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() ==
                    "AventusSharp.Tools.Attributes.NoRoute")) continue;
            var key = method.Name + "(" + string.Join(",", method.Parameters.Select(p => p.Type.ToDisplayString())) + ")";
            if (!visited.Add(key)) continue;
            var routes = compiledRoutes.Where(route => route.MethodName == method.Name &&
                route.Params.Count == method.Parameters.Length &&
                route.Params.Zip(method.Parameters).All(pair =>
                    HttpRouterContainer.MatchesParameterType(pair.First, pair.Second.Type, owner))).ToList();
            for (var i = 0; i < routes.Count; i++) WriteMethod(method, routes[i], i, routes.Count);
        }
        AddTxtClose("}");
        AddTxtClose("}");
        return fileWriter.GetContent();
    }

    private void WriteMethod(IMethodSymbol method, CompiledWsRoute compiled, int index, int routeCount)
    {
        var path = CompiledPath(compiled);
        var routeParameters = Regex.Matches(path, @"\{([^{}]+)\}")
            .Select(match => match.Groups[1].Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dynamicParts = Regex.Matches(path, @"\[([A-Za-z0-9_]+)\]")
            .Select(match => match.Groups[1].Value).Distinct().ToList();
        var visible = method.Parameters.Where(parameter => !IsInjected(parameter.Type) &&
            !HasAttribute(parameter, NoExport)).ToList();
        var name = method.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() ==
            "AventusSharp.Routes.Attributes.FctName")?.ConstructorArguments.FirstOrDefault().Value as string
            ?? method.Name;
        if (routeCount > 1) name += "_" + (index + 1);
        var returnType = ReturnType(method.ReturnType);
        var expectsReply = compiled.ResponseType is not (1 or 3);
        var arguments = visible.Select(parameter =>
            TypeName(parameter.Type) + " " + parameter.Name +
            (parameter.HasExplicitDefaultValue ? " = " + DefaultValue(parameter) : "")).ToList();
        arguments.AddRange(dynamicParts.Select(part => "string dynamic" + part));
        arguments.Add("global::" + RuntimeNamespace + ".Ws.WsEndPointClient? endpoint = null");
        arguments.Add("global::System.Threading.CancellationToken cancellationToken = default");
        var taskType = expectsReply
            ? "global::System.Threading.Tasks.Task<global::" + RuntimeNamespace + ".Ws.WsResult<" + returnType + ">>"
            : "global::System.Threading.Tasks.Task";
        AddTxt("public async " + taskType + " " + name + "(" + string.Join(", ", arguments) + ")");
        AddTxtOpen("{");
        AddTxt("var channel = " + SseMetadata.Literal(path) + ";");
        foreach (var part in routeParameters)
        {
            var parameter = visible.FirstOrDefault(p => p.Name.Equals(part, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Paramètre WS {{{part}}} absent sur {Type}.{method.Name}");
            AddTxt("channel = channel.Replace(" + SseMetadata.Literal("{" + part + "}") + ", " +
                "global::System.Uri.EscapeDataString(global::System.Convert.ToString(" + parameter.Name +
                ", global::System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty));");
        }
        foreach (var part in dynamicParts)
            AddTxt("channel = channel.Replace(" + SseMetadata.Literal("[" + part + "]") +
                ", global::System.Uri.EscapeDataString(dynamic" + part + "));");
        var body = visible.Where(parameter => !routeParameters.Contains(parameter.Name)).ToList();
        if (body.Count > 0)
        {
            AddTxt("var body = new global::System.Collections.Generic.Dictionary<string, object?>");
            AddTxtOpen("{");
            foreach (var parameter in body)
                AddTxt("[" + SseMetadata.Literal(parameter.Name) + "] = " + parameter.Name + ",");
            AddTxtClose("};");
        }
        var target = "(endpoint ?? GetEndPoint(" + SseMetadata.Literal(compiled.EndPointPath ?? "/ws") + "))";
        if (expectsReply)
            AddTxt("return await " + target + ".RequestAsync<" + returnType + ">(channel, " +
                (body.Count > 0 ? "body" : "null") + ", cancellationToken);");
        else
            AddTxt("await " + target + ".SendAsync(channel, " + (body.Count > 0 ? "body" : "null") +
                ", cancellationToken: cancellationToken);");
        AddTxtClose("}");
    }

    private static string CompiledPath(CompiledWsRoute route)
    {
        var placeholders = Regex.Matches(route.BaseUrl, @"\{[^{}]+\}").Select(match => match.Value).ToList();
        var pattern = route.Pattern.TrimStart('^').TrimEnd('$').Replace("\\/", "/");
        var index = 0;
        var candidate = Regex.Replace(pattern, @"\([^()]*\)", _ =>
            index < placeholders.Count ? placeholders[index++] : "");
        return index == placeholders.Count && !Regex.IsMatch(candidate, @"[\[\]\^$*+?]")
            ? candidate : route.BaseUrl;
    }

    private static bool IsInjected(ITypeSymbol type) => type.ToDisplayString() is
        "AventusSharp.WebSocket.WebSocketConnection" or "AventusSharp.WebSocket.WsEndPoint" or
        "Microsoft.AspNetCore.Http.HttpContext" or "AventusSharp.Hosting.IAventusContext" or
        "System.Net.WebSockets.WebSocket";

    private static string ReturnType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol task && task.Name == "Task" &&
            task.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks")
            type = task.TypeArguments.Length == 1 ? task.TypeArguments[0] : task;
        if (type.SpecialType == SpecialType.System_Void || type.Name == "Task") return "object";
        for (var current = type as INamedTypeSymbol; current != null; current = current.BaseType)
        {
            if (current.OriginalDefinition.ToDisplayString() == "AventusSharp.WebSocket.Event.WsEvent<T>")
                return TypeName(current.TypeArguments[0]);
        }
        if (type is INamedTypeSymbol named && named.ContainingNamespace.ToDisplayString() == "AventusSharp.Tools")
        {
            if (named.Name.StartsWith("VoidWithError", StringComparison.Ordinal)) return "object";
            if (named.Name == "ResultWithError" && named.TypeArguments.Length > 0)
                return TypeName(named.TypeArguments[0]);
        }
        return TypeName(type);
    }

    private static string TypeName(ITypeSymbol type)
    {
        var name = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (name.StartsWith("global::AventusSharp", StringComparison.Ordinal))
            throw new NotSupportedException($"Type WS {type} dépend d'AventusSharp.");
        return name;
    }

    private static string DefaultValue(IParameterSymbol parameter) => parameter.ExplicitDefaultValue switch
    {
        null => "null",
        string text => SseMetadata.Literal(text),
        bool value => value ? "true" : "false",
        _ => System.Convert.ToString(parameter.ExplicitDefaultValue,
            System.Globalization.CultureInfo.InvariantCulture) ?? "default"
    };
}
