using AventusSharp.SSE.Event;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json;

namespace CSharpToTypescript.Container;

internal class SSEEventContainer : BaseContainer
{
    private readonly ITypeSymbol? bodyType;
    private readonly INamedTypeSymbol? endpointType;
    private readonly string? topic;

    public static bool Is(INamedTypeSymbol type, string fileName, out BaseContainer? result)
    {
        result = null;
        if (!type.AllInterfaces.Any(value => Tools.IsSameType<ISSEEvent>(value)))
            return false;
        if (type.ContainingAssembly.Name == typeof(ISSEEvent).Assembly.GetName().Name)
            return true;
        if (Tools.ExportToTypesript(type, ProjectManager.Config.exportSseEventByDefault))
            result = new SSEEventContainer(type, fileName);
        return true;
    }

    public SSEEventContainer(INamedTypeSymbol type, string fileName) : base(type)
    {
        for (INamedTypeSymbol? current = type; current != null; current = current.BaseType)
        {
            if (current.OriginalDefinition.ToDisplayString() == "AventusSharp.SSE.Event.SSEEvent<T>")
            {
                bodyType = current.TypeArguments[0];
                break;
            }
        }
        RegisterPayloadTypes(bodyType, fileName, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));

        for (INamedTypeSymbol? current = type; current != null; current = current.BaseType)
        {
            foreach (AttributeData attr in current.GetAttributes())
            {
                if (attr.AttributeClass?.ContainingNamespace.ToDisplayString() != "AventusSharp.SSE.Attributes")
                    continue;
                if (attr.AttributeClass.Name == "EndPoint")
                {
                    if (attr.AttributeClass.IsGenericType)
                        endpointType = attr.AttributeClass.TypeArguments[0] as INamedTypeSymbol;
                    else if (attr.ConstructorArguments.Length > 0)
                        endpointType = attr.ConstructorArguments[0].Value as INamedTypeSymbol;
                    break;
                }
            }
            if (endpointType != null) break;
        }
        topic = FindConstantTopic(type);
    }

    private static void RegisterPayloadTypes(ITypeSymbol? payload, string fileName, HashSet<ITypeSymbol> visited)
    {
        if (payload == null || !visited.Add(payload)) return;
        if (payload is IArrayTypeSymbol array)
        {
            RegisterPayloadTypes(array.ElementType, fileName, visited);
            return;
        }
        if (payload is not INamedTypeSymbol named) return;
        foreach (ITypeSymbol argument in named.TypeArguments)
            RegisterPayloadTypes(argument, fileName, visited);
        if (named.ContainingType == null || !named.Locations.Any(location => location.IsInSource)) return;
        if (FileToWrite.GetContainer(named) == null)
            FileToWrite.AddBaseContainer(new SSEBodyContainer(named), FileToWrite.GetFileName(named) ?? fileName);
        foreach (ISymbol member in named.GetMembers())
        {
            if (member is IPropertySymbol property)
                RegisterPayloadTypes(property.Type, fileName, visited);
            else if (member is IFieldSymbol field)
                RegisterPayloadTypes(field.Type, fileName, visited);
        }
    }

    public override string GetTypeName(ISymbol symbol, int depth = 0, bool genericExtendsConstraint = false)
    {
        string name = base.GetTypeName(symbol, depth, genericExtendsConstraint);
        if (!SymbolEqualityComparer.Default.Equals(symbol, type)
            && symbol is INamedTypeSymbol nested && nested.ContainingType != null
            && nested.ContainingAssembly.Name == ProjectManager.CurrentAssemblyName
            && FileToWrite.GetContainer(nested) is SSEBodyContainer)
            return SSEBodyContainer.PayloadName(nested, name);
        return name;
    }

    private static string? FindConstantTopic(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current != null; current = current.BaseType)
        {
            IMethodSymbol? method = current.GetMembers("GetTopic").OfType<IMethodSymbol>().FirstOrDefault();
            if (method == null) continue;
            foreach (SyntaxReference reference in method.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax() is not MethodDeclarationSyntax declaration) continue;
                ExpressionSyntax? expression = declaration.ExpressionBody?.Expression;
                if (expression == null && declaration.Body?.Statements.Count == 1
                    && declaration.Body.Statements[0] is ReturnStatementSyntax returned)
                    expression = returned.Expression;
                if (expression != null)
                {
                    var constant = ProjectManager.Compilation.GetSemanticModel(expression.SyntaxTree).GetConstantValue(expression);
                    if (constant.HasValue && constant.Value is string value) return value;
                }
            }
            return null;
        }
        return null;
    }

    protected override string WriteAction()
    {
        List<string> result = [];
        if (ProjectManager.Config.useNamespace && Namespace.Length > 0) AddIndent();
        string payload = "any";
        if (bodyType != null)
            payload = GetTypeName(bodyType);
        else if (typeof(SSEEmptyEvent).IsAssignableFrom(realType))
            payload = "void";
        string parent = GetAventusTypeName("AventusSharp.SSE.SSEEvent");
        string endpoint = GetAventusTypeName("AventusSharp.SSE.EndPoint");
        AddTxtOpen(GetAccessibilityExport(type) + (type.IsAbstract ? "abstract " : "")
            + "class " + GetTypeName(type, 0, true) + " extends " + parent + "<" + payload + "> {", result);
        bool dynamicTopic = topic == null && !type.IsAbstract;
        if (dynamicTopic) AddTxt("private getTopic: () => string;", result);
        string topicArgument = dynamicTopic ? "getTopic: () => string, " : "";
        AddTxtOpen("public constructor(" + topicArgument + "endpoint?: " + endpoint + ", getPrefix?: () => string) {", result);
        string selectedEndpoint = "endpoint";
        INamedTypeSymbol? defaultEndpoint = endpointType ?? SSEEndPointContainer.GetDefaultEndpoint();
        if (defaultEndpoint != null) selectedEndpoint += " ?? " + GetTypeName(defaultEndpoint) + ".getInstance()";
        AddTxt("super(" + selectedEndpoint + ", getPrefix);", result);
        if (dynamicTopic) AddTxt("this.getTopic = getTopic;", result);
        // An abstract event cannot safely call a derived path before derived fields initialize.
        if (!type.IsAbstract) AddTxt("this.init();", result);
        AddTxtClose("}", result);
        if (topic != null || dynamicTopic)
        {
            AddTxtOpen("protected override path(): string {", result);
            string topicExpression = dynamicTopic ? "this.getTopic()" : JsonConvert.SerializeObject(topic);
            AddTxt("return this.getPrefix() + " + topicExpression + ";", result);
            AddTxtClose("}", result);
        }
        if (ProjectManager.Config.sseEndpoint.listenOnBoot)
        {
            AddTxtOpen("protected override listenOnBoot(): boolean {", result);
            AddTxt("return true;", result);
            AddTxtClose("}", result);
        }
        AddTxtClose("}", result);
        if (ProjectManager.Config.useNamespace && Namespace.Length > 0) RemoveIndent();
        return string.Join("\n", result);
    }

    protected override string? CustomReplacer(ISymbol? type, string fullname, string? result)
        => applyReplacer(ProjectManager.Config.replacer.sseEvent, fullname, result);
}

internal class SSEBodyContainer : NormalClassContainer
{
    public SSEBodyContainer(INamedTypeSymbol type) : base(type)
    {
        string[] parts = type.ContainingNamespace.ToDisplayString().Split('.');
        Namespace = string.Join(".", parts.Skip(1));
    }

    internal static string PayloadName(INamedTypeSymbol type, string name)
    {
        for (INamedTypeSymbol? parent = type.ContainingType; parent != null; parent = parent.ContainingType)
            name = parent.Name + name;
        return name;
    }

    public override string GetTypeName(ISymbol symbol, int depth = 0, bool genericExtendsConstraint = false)
    {
        string name = base.GetTypeName(symbol, depth, genericExtendsConstraint);
        if (symbol is INamedTypeSymbol nested && nested.ContainingType != null
            && FileToWrite.GetContainer(nested) is SSEBodyContainer)
            return PayloadName(nested, name);
        return name;
    }
}
