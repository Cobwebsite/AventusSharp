using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpExport.Container;

internal sealed class HttpRouterContainer(INamedTypeSymbol type, ClassDeclarationSyntax declaration,
    CompilationUnitSyntax root, SemanticModel model, INamedTypeSymbol? noExport, HttpRouterConfig config,
    IReadOnlyList<CompiledHttpRoute> compiledRoutes)
    : BaseContainer(type, declaration, root, model, noExport)
{
    private static readonly string Attributes = "AventusSharp.Routes.Attributes.";
    public override string Usings => "";

    public override string Write()
    {
        if (Type.ContainingType != null) throw new NotSupportedException($"Routeur imbriqué non pris en charge : {Type}");
        var prefix = TextAttribute(Type, Attributes + "Prefix") ?? "";
        if (prefix.Length > 0 && !prefix.StartsWith('/')) prefix = "/" + prefix;
        if (prefix.Length > 1) prefix = prefix.TrimEnd('/');
        var baseUrl = ((config.Host ?? "") + config.Uri).TrimEnd('/');
        AddTxt("namespace " + Namespace);
        AddTxtOpen("{");
        var parent = config.CreateRouter
            ? "global::" + config.Namespace + "." + config.RouterName
            : config.Parent == "HttpRouteClient" ? "global::CSharpExport.Http.HttpRouteClient" : config.Parent;
        var generic = Type.TypeParameters.Length == 0 ? "" :
            "<" + string.Join(", ", Type.TypeParameters.Select(p => p.Name)) + ">";
        AddTxt("public " + (Type.IsAbstract ? "abstract " : "") + "class " + Type.Name + generic + " : " + parent);
        AddTxtOpen("{");
        AddTxt("public " + Type.Name + "(global::System.Net.Http.HttpClient? client = null, string? baseUrl = null)");
        AddTxt("    : base(client, baseUrl ?? " + Literal(baseUrl) + ") { }");
        var used = new HashSet<string>();
        for (INamedTypeSymbol? owner = Type; owner != null && owner.SpecialType != SpecialType.System_Object;
             owner = owner.BaseType)
        {
            foreach (var method in owner.GetMembers().OfType<IMethodSymbol>())
            {
                if (method.MethodKind != MethodKind.Ordinary || method.IsStatic ||
                    method.DeclaredAccessibility != Accessibility.Public ||
                    HasAttribute(method, NoExport) || HasNamedAttribute(method, "AventusSharp.Tools.Attributes.NoRoute")) continue;
                var key = method.Name + "(" + string.Join(",", method.Parameters.Select(p => p.Type.ToDisplayString())) + ")";
                if (!used.Add(key)) continue;
                WriteMethod(method, owner, prefix);
            }
        }
        AddTxtClose("}");
        AddTxtClose("}");
        return fileWriter.GetContent();
    }

    private void WriteMethod(IMethodSymbol method, INamedTypeSymbol owner, string prefix)
    {
        var matchedRoutes = compiledRoutes.Where(r => r.MethodName == method.Name &&
            r.Params.Count == method.Parameters.Length &&
            r.Params.Zip(method.Parameters).All(pair => MatchesParameterType(pair.First, pair.Second.Type, owner))).ToList();
        if (matchedRoutes.Count == 0) return;
        var visible = method.Parameters.Where(p => !HasAttribute(p, NoExport) &&
            !p.Type.ContainingNamespace.ToDisplayString().StartsWith("Microsoft", StringComparison.Ordinal) &&
            p.Type.ToDisplayString() != "AventusSharp.Hosting.IAventusContext").ToList();
        var name = TextAttribute(method, "AventusSharp.Tools.Attributes.FctName") ?? method.Name;
        var response = ReturnKind(method, owner);
        if (response == null) return;
        var overload = 0;
        var routeEntries = matchedRoutes.Select(r => (Path: CompiledPath(r),
            Verb: new[] { "GET", "POST", "PUT", "DELETE", "OPTIONS" }[r.Method]));
        foreach (var (path, verb) in routeEntries)
        {
            var routeParams = Regex.Matches(path, @"\{([^{}]+)\}")
                .Select(match => match.Groups[1].Value).ToHashSet();
            var body = visible.Where(parameter => !routeParams.Contains(parameter.Name)).ToList();
            var form = (BoolAttribute(method, Attributes + "AddFormData", 0) ?? false) ||
                body.Any(parameter => ContainsHttpFile(parameter.Type));
            var regexRoute = method.GetAttributes().Any(a => Is(a, Attributes + "PathRegex"));
            var dynamicArgs = new List<string>();
            var route = regexRoute ? "" : ResolveRoute(path, owner, dynamicArgs);
            var methodName = name + (overload++ == 0 ? "" : "_" + verb + overload);
            var methodGeneric = method.TypeParameters.Length == 0 ? "" :
                "<" + string.Join(", ", method.TypeParameters.Select(p => p.Name)) + ">";
            var arguments = new List<string>();
            if (regexRoute) arguments.Add("string routePath");
            arguments.AddRange(dynamicArgs.Select(arg => "string " + arg));
            arguments.AddRange(visible.Select(p => RenderType(p.Type, owner) + " " + p.Name +
                (p.IsOptional ? " = " + DefaultValue(p, owner) : "")));
            arguments.Add("global::System.Threading.CancellationToken cancellationToken = default");
            var returnType = response.Value.Kind switch
            {
                "Void" => "object",
                "Text" => "string",
                "Bytes" => "byte[]",
                _ => response.Value.Type
            };
            AddTxt("");
            AddTxt("public async global::System.Threading.Tasks.Task<global::CSharpExport.Http.HttpResult<" + returnType + ">> " +
                methodName + methodGeneric + "(" + string.Join(", ", arguments) + ")");
            AddTxtOpen("{");
            var url = Regex.Replace(route, @"\{([^{}]+)\}", match =>
            {
                var parameter = visible.FirstOrDefault(p => p.Name == match.Groups[1].Value)?.Name;
                if (parameter == null && dynamicArgs.Contains(match.Groups[1].Value))
                    parameter = match.Groups[1].Value;
                if (parameter == null)
                    throw new InvalidOperationException($"Paramètre d'URL {match.Value} absent sur {Type}.{method.Name}");
                return "{(global::System.Uri.EscapeDataString(global::System.Convert.ToString(" + parameter +
                    ", global::System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty))}";
            });
            AddTxt(regexRoute ? "var path = routePath;" : "var path = $" + Literal(url) + ";");
            var bodyValue = body.Count == 0 ? "null" : "body";
            if (body.Count > 0)
            {
                AddTxt("var body = new global::System.Collections.Generic.Dictionary<string, object?>");
                AddTxtOpen("{");
                foreach (var parameter in body)
                    AddTxt("[" + Literal(parameter.Name) + "] = " + parameter.Name + ",");
                AddTxtClose("};");
            }
            var call = response.Value.Kind switch
            {
                "Void" => "SendVoidAsync",
                "Text" => "SendTextAsync",
                "Bytes" => "SendBytesAsync",
                _ => "SendJsonAsync<" + returnType + ">"
            };
            AddTxt("return await " + call + "(new global::System.Net.Http.HttpMethod(" + Literal(verb) +
                "), path, " + bodyValue + ", " + form.ToString().ToLowerInvariant() +
                (response.Value.Kind is "Json" or "WrappedJson"
                    ? ", " + (response.Value.Kind == "WrappedJson" ? "true" : "false") : "") +
                ", cancellationToken);");
            AddTxtClose("}");
        }
    }

    private static string CompiledPath(CompiledHttpRoute route)
    {
        var placeholders = Regex.Matches(route.BaseUrl, @"\{[^{}]+\}").Select(m => m.Value).ToList();
        var pattern = route.Pattern.TrimStart('^').TrimEnd('$').Replace("\\/", "/");
        var index = 0;
        var candidate = Regex.Replace(pattern, @"\([^()]*\)", _ =>
            index < placeholders.Count ? placeholders[index++] : "");
        if (index == placeholders.Count && candidate.EndsWith(route.BaseUrl, StringComparison.Ordinal) &&
            !Regex.IsMatch(candidate, @"[\[\]\^$*+?]") )
            return candidate;
        return route.BaseUrl;
    }

    private static bool MatchesParameterType(string declared, ITypeSymbol type, INamedTypeSymbol owner)
    {
        declared = declared.Trim();
        if (type is ITypeParameterSymbol parameter)
        {
            for (var i = 0; i < owner.TypeParameters.Length; i++)
                if (SymbolEqualityComparer.Default.Equals(parameter, owner.TypeParameters[i]) &&
                    !SymbolEqualityComparer.Default.Equals(parameter, owner.TypeArguments[i]))
                    return MatchesParameterType(declared, owner.TypeArguments[i], owner);
            return declared == parameter.Name;
        }
        if (type is IArrayTypeSymbol array)
        {
            var suffix = "[" + new string(',', array.Rank - 1) + "]";
            return declared.EndsWith(suffix, StringComparison.Ordinal) &&
                MatchesParameterType(declared[..^suffix.Length], array.ElementType, owner);
        }
        if (type is not INamedTypeSymbol named) return declared == type.ToDisplayString();
        var metadataName = named.ContainingType == null
            ? named.ContainingNamespace.ToDisplayString() + "." + named.MetadataName
            : ClrNestedName(named.ContainingType) + "+" + named.MetadataName;
        if (!named.IsGenericType) return declared == metadataName;
        if (!declared.StartsWith(metadataName + "[", StringComparison.Ordinal) || !declared.EndsWith(']'))
            return false;
        var arguments = SplitClrGenericArguments(declared[(metadataName.Length + 1)..^1]);
        return arguments.Count == named.TypeArguments.Length &&
            arguments.Zip(named.TypeArguments).All(pair => MatchesParameterType(pair.First, pair.Second, owner));
    }

    private static string ClrNestedName(INamedTypeSymbol type) => type.ContainingType == null
        ? type.ContainingNamespace.ToDisplayString() + "." + type.MetadataName
        : ClrNestedName(type.ContainingType) + "+" + type.MetadataName;

    private static List<string> SplitClrGenericArguments(string inner)
    {
        var result = new List<string>();
        for (var i = 0; i < inner.Length;)
        {
            while (i < inner.Length && (inner[i] == ',' || char.IsWhiteSpace(inner[i]))) i++;
            if (i == inner.Length) break;
            var bracketed = inner[i] == '[';
            if (bracketed) i++;
            var start = i;
            var depth = 0;
            while (i < inner.Length)
            {
                if (inner[i] == '[') depth++;
                else if (inner[i] == ']')
                {
                    if (depth == 0 && bracketed) break;
                    depth--;
                }
                else if (inner[i] == ',' && depth == 0 && !bracketed) break;
                i++;
            }
            var argument = inner[start..i];
            if (bracketed && i < inner.Length) i++;
            depth = 0;
            for (var j = 0; j < argument.Length; j++)
            {
                if (argument[j] == '[') depth++;
                else if (argument[j] == ']') depth--;
                else if (argument[j] == ',' && depth == 0)
                {
                    argument = argument[..j];
                    break;
                }
            }
            result.Add(argument.Trim());
        }
        return result;
    }

    private string ResolveRoute(string route, INamedTypeSymbol owner, List<string> dynamicArgs)
    {
        route = route.StartsWith('/') ? route : "/" + route;
        if (route.Length > 1) route = route.TrimEnd('/');
        return Regex.Replace(route, @"\[([^\]]+)\]", match =>
        {
            if (match.Groups[1].Value == "StorableName" && owner.TypeArguments.Length > 0)
            {
                var storableName = owner.TypeArguments[0].Name;
                return owner.TypeArguments[0].TypeKind == TypeKind.Interface && storableName.StartsWith('I')
                    ? storableName[1..] : storableName;
            }
            var method = owner.GetMembers(match.Groups[1].Value).OfType<IMethodSymbol>().FirstOrDefault();
            var syntax = method?.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as MethodDeclarationSyntax;
            var expression = syntax?.ExpressionBody?.Expression ??
                syntax?.Body?.Statements.OfType<ReturnStatementSyntax>().FirstOrDefault()?.Expression;
            var literal = expression as LiteralExpressionSyntax;
            if (literal != null) return literal.Token.ValueText;
            var name = "dynamic" + match.Groups[1].Value;
            if (!dynamicArgs.Contains(name)) dynamicArgs.Add(name);
            return "{" + name + "}";
        });
    }

    private static string RenderType(ITypeSymbol type, INamedTypeSymbol? owner = null)
    {
        if (type.ToDisplayString() == "AventusSharp.Routes.Request.HttpFile")
            return "global::CSharpExport.Http.HttpUpload";
        if (type is ITypeParameterSymbol parameter && owner != null)
        {
            for (var i = 0; i < owner.TypeParameters.Length; i++)
                if (SymbolEqualityComparer.Default.Equals(parameter, owner.TypeParameters[i]))
                    return SymbolEqualityComparer.Default.Equals(owner.TypeArguments[i], parameter)
                        ? parameter.Name : RenderType(owner.TypeArguments[i]);
        }
        if (type is IArrayTypeSymbol array)
            return RenderType(array.ElementType, owner) + "[" + new string(',', array.Rank - 1) + "]";
        if (type is INamedTypeSymbol generic && generic.IsGenericType)
            return generic.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Split('<')[0] +
                "<" + string.Join(", ", generic.TypeArguments.Select(arg => RenderType(arg, owner))) + ">";
        if (type.ToDisplayString().Contains("AventusSharp", StringComparison.Ordinal))
            throw new NotSupportedException($"Type HTTP {type} dépend d'AventusSharp.");
        return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    private static string DefaultValue(IParameterSymbol parameter, INamedTypeSymbol owner)
    {
        var value = parameter.ExplicitDefaultValue;
        if (value == null) return "null";
        if (parameter.Type.TypeKind == TypeKind.Enum)
            return "(" + RenderType(parameter.Type, owner) + ")" + Convert.ToString(value,
                System.Globalization.CultureInfo.InvariantCulture);
        if (value is string text) return Literal(text);
        if (value is char character) return "'" + character.ToString().Replace("'", "\\'") + "'";
        if (value is bool boolean) return boolean ? "true" : "false";
        return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "default";
    }

    private (string Kind, string Type)? ReturnKind(IMethodSymbol method, INamedTypeSymbol owner)
    {
        ITypeSymbol type = method.ReturnType;
        if (type is INamedTypeSymbol named && named.Name == "Task" &&
            named.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks")
            type = named.TypeArguments.Length == 1 ? named.TypeArguments[0] : named;
        if (type.SpecialType == SpecialType.System_Void || type.Name == "Task") return ("Void", "object");
        if (type is INamedTypeSymbol wrapper && wrapper.ContainingNamespace.ToDisplayString() == "AventusSharp.Tools")
        {
            if (wrapper.Name == "VoidWithError") return ("Void", "object");
            if (wrapper.Name == "ResultWithError" && wrapper.TypeArguments.Length > 0)
                return IsAventusType(wrapper.TypeArguments[0])
                    ? ("Json", "global::System.Text.Json.JsonElement")
                    : ("WrappedJson", RenderType(wrapper.TypeArguments[0], owner));
        }
        var full = type.ToDisplayString();
        if (full == "AventusSharp.Routes.Response.View" || full == "AventusSharp.Routes.Response.ViewDynamic") return null;
        if (full == "AventusSharp.Routes.Response.TextResponse") return ("Text", "string");
        if (full == "AventusSharp.Routes.Response.ByteResponse" || type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte })
            return ("Bytes", "byte[]");
        if (full == "AventusSharp.Routes.Response.Json" || full == "AventusSharp.Routes.Response.IResponse")
        {
            var syntax = method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as MethodDeclarationSyntax;
            var model = syntax == null ? null : Model.Compilation.GetSemanticModel(syntax.SyntaxTree);
            var returns = syntax?.DescendantNodes().OfType<ReturnStatementSyntax>()
                .Select(r => r.Expression).Where(e => e != null).Cast<ExpressionSyntax>().ToList() ?? new();
            if (syntax?.ExpressionBody != null) returns.Add(syntax.ExpressionBody.Expression);
            var kinds = new List<(string Kind, string Type)>();
            foreach (var expression in returns)
            {
                var arguments = expression switch
                {
                    ObjectCreationExpressionSyntax creation => creation.ArgumentList?.Arguments,
                    ImplicitObjectCreationExpressionSyntax creation => creation.ArgumentList.Arguments,
                    _ => null
                };
                if (arguments == null) continue;
                var created = model?.GetTypeInfo(expression).Type?.ToDisplayString();
                if (created == "AventusSharp.Routes.Response.TextResponse") kinds.Add(("Text", "string"));
                else if (created == "AventusSharp.Routes.Response.ByteResponse") kinds.Add(("Bytes", "byte[]"));
                else if (created == "AventusSharp.Routes.Response.View" || created == "AventusSharp.Routes.Response.ViewDynamic")
                    return null;
                else if (created == "AventusSharp.Routes.Response.Json" && arguments.Value.Count > 0)
                {
                    var valueType = model?.GetTypeInfo(arguments.Value[0].Expression).Type;
                    if (valueType is INamedTypeSymbol wrapped && wrapped.Name == "ResultWithError" &&
                        wrapped.ContainingNamespace.ToDisplayString() == "AventusSharp.Tools" &&
                        wrapped.TypeArguments.Length > 0 && !IsAventusType(wrapped.TypeArguments[0]))
                        kinds.Add(("WrappedJson", RenderType(wrapped.TypeArguments[0], owner)));
                    else if (valueType is INamedTypeSymbol empty && empty.Name == "VoidWithError" &&
                        empty.ContainingNamespace.ToDisplayString() == "AventusSharp.Tools")
                        kinds.Add(("Void", "object"));
                    else if (valueType != null && !valueType.IsAnonymousType && !IsAventusType(valueType))
                        kinds.Add(("Json", RenderType(valueType, owner)));
                }
            }
            return kinds.Count > 0 && kinds.Distinct().Count() == 1
                ? kinds[0] : ("Json", "global::System.Text.Json.JsonElement");
        }
        return ("Json", RenderType(type, owner));
    }

    private static bool HasNamedAttribute(ISymbol symbol, string name) =>
        symbol.GetAttributes().Any(a => Is(a, name));
    private static bool IsAventusType(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol array => IsAventusType(array.ElementType),
        INamedTypeSymbol named => named.ContainingNamespace.ToDisplayString().StartsWith("AventusSharp", StringComparison.Ordinal) ||
            named.TypeArguments.Any(IsAventusType),
        _ => false
    };
    private static bool ContainsHttpFile(ITypeSymbol type) => ContainsHttpFile(type,
        new HashSet<ISymbol>(SymbolEqualityComparer.Default));

    private static bool ContainsHttpFile(ITypeSymbol type, HashSet<ISymbol> seen)
    {
        if (type.ToDisplayString() == "AventusSharp.Routes.Request.HttpFile") return true;
        if (type is IArrayTypeSymbol array) return ContainsHttpFile(array.ElementType, seen);
        if (type is not INamedTypeSymbol named || !seen.Add(named)) return false;
        if (named.TypeArguments.Any(arg => ContainsHttpFile(arg, seen))) return true;
        if (named.ContainingNamespace.ToDisplayString().StartsWith("System", StringComparison.Ordinal)) return false;
        return named.GetMembers().OfType<IPropertySymbol>().Any(property =>
            property.DeclaredAccessibility == Accessibility.Public && ContainsHttpFile(property.Type, seen));
    }
    private static bool Is(AttributeData a, string name) => a.AttributeClass?.ToDisplayString() == name;
    private static string? TextAttribute(ISymbol symbol, string name) =>
        symbol.GetAttributes().FirstOrDefault(a => Is(a, name))?.ConstructorArguments.FirstOrDefault().Value?.ToString();
    private static bool? BoolAttribute(ISymbol symbol, string name, int index)
    {
        var attribute = symbol.GetAttributes().FirstOrDefault(a => Is(a, name));
        if (attribute == null) return null;
        if (index >= attribute.ConstructorArguments.Length) return true;
        return attribute.ConstructorArguments[index].Value is bool value ? value : true;
    }
    private static string Literal(string value) => System.Text.Json.JsonSerializer.Serialize(value);
}
