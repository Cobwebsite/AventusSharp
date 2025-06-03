using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using AventusSharp.Mcp.Attributes;
using AventusSharp.Routes;
using AventusSharp.Tools;
using Microsoft.AspNetCore.Http.Connections;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AventusSharp.Mcp;


public static class McpMiddleware
{
    private static Action<McpConfig> configAction = (config) => { };
    internal static McpConfig config = new McpConfig();

    public static void Configure(Action<McpConfig> configAction)
    {
        McpMiddleware.configAction = configAction;
    }

    // public static VoidWithError Register()
    // {
    //     Assembly? entry = Assembly.GetEntryAssembly();
    //     if (entry != null)
    //     {
    //         return Register(entry);
    //     }
    //     return new VoidWithError();
    // }

    // public static VoidWithError Register(Assembly assembly)
    // {
    //     List<Type> types = assembly.GetTypes().Where(p => p.GetInterfaces().Contains(typeof(IRouter))).ToList();
    //     return Register(types);
    // }

    public static VoidWithError Register()
    {
        VoidWithMcpError result = new VoidWithMcpError();
        Dictionary<string, AventusMcpTool> tools = new Dictionary<string, AventusMcpTool>();

        foreach (McpHttpMethod methodInfo in httpMethods)
        {
            AventusMcpTool? mcpTool = PrepareMethodToTool(methodInfo);
            if (mcpTool != null)
            {
                tools.Add(mcpTool.Tool.Name, mcpTool);
            }
        }
        McpServerOptions options = new()
        {
            ServerInfo = new Implementation() { Name = "MyServer", Version = "1.0.0" },
            Capabilities = new ServerCapabilities()
            {
                Tools = new ToolsCapability()
                {
                    ListToolsHandler = (request, cancellationToken) => ValueTask.FromResult(new ListToolsResult() { Tools = tools.Values.Select(p => p.Tool).ToList() }),
                    CallToolHandler = (request, cancellationToken) =>
                    {
                        string? name = request.Params?.Name;
                        if (name != null && tools.ContainsKey(name))
                        {
                            return tools[name].Run(request);
                        }
                        throw new McpException($"Unknown tool: '{request.Params?.Name}'");

                        // if (request.Params?.Name == "echo")
                        // {
                        //     if (request.Params.Arguments?.TryGetValue("message", out var message) is not true)
                        //     {
                        //         throw new McpException("Missing required argument 'message'");
                        //     }

                        //     return ValueTask.FromResult(new CallToolResponse()
                        //     {
                        //         Content = [new Content() { Text = $"Echo: {message}", Type = "text" }]
                        //     });
                        // }

                    },
                }
            },
        };


        // IMcpServer server = McpServerFactory.Create(new StdioServerTransport("MyServer"), options);
        IMcpServer server = McpServerFactory.Create(new StreamableHttpServerTransport(), options);
        // IMcpServer server = McpServerFactory.Create(new StreamableHttpPostTransport(), options);
        server.RunAsync();
        return result.ToGeneric();
    }

    private static AventusMcpTool? PrepareMethodToTool(McpHttpMethod mcpInfo)
    {
        McpTool? attr = mcpInfo.method.GetCustomAttribute<McpTool>();
        if (attr == null) return null;

        Tool tool = new Tool();
        tool.Name = attr.Name ?? mcpInfo.method.Name;
        tool.Description = attr.Description;
        tool.Annotations = new()
        {
            DestructiveHint = attr.Destructive,
            IdempotentHint = attr.Idempotent,
            OpenWorldHint = attr.OpenWorld,
            ReadOnlyHint = attr.ReadOnly,
            Title = attr.Title
        };

        if (mcpInfo._paramsFilter.Count > 0)
        {
            var obj = new JsonObject();
            obj.Add("type", "object");
            var properties = new JsonObject();
            var required = new JsonArray();

            foreach (RouterParameterInfo info in mcpInfo._paramsFilter)
            {
                var property = new JsonObject();
                var type = info.type;
                property.Add("type", "string"); // string || number || object || array || boolean || null
                if (info.description != null)
                {
                    properties.Add("description", info.description);
                }

                properties.Add(info.name, property);

                if (info.mandatory)
                {
                    required.Add(info.name);
                }
            }

            obj.Add("properties", properties);
            obj.Add("required", required);

            tool.InputSchema = JsonSerializer.Deserialize<JsonElement>(obj);

        }

        AventusMcpTool mcpTool = new AventusMcpTool()
        {
            Tool = tool,
            Run = (request) =>
            {
                // var server = new StreamableHttpServerTransport();
                // server.
                
                return ValueTask.FromResult(new CallToolResponse()
                {
                    Content = [new Content() { Text = $"Echo: ", Type = "text" }]
                });
            }
        };


        return mcpTool;
    }

    private static List<McpHttpMethod> httpMethods = new();
    internal static void AddHttpMethod(McpHttpMethod method)
    {
        httpMethods.Add(method);
    }
    internal static void ClearHttpMethods()
    {
        httpMethods.Clear();
    }
}

internal class AventusMcpTool
{
    public required Tool Tool { get; set; }
    public required Func<RequestContext<CallToolRequestParams>, ValueTask<CallToolResponse>> Run { get; set; }
}
internal class McpHttpMethod
{
    public required MethodInfo method { get; set; }
    public required IRouter router { get; set; }
    public required List<RouterParameterInfo> _params { get; set; }
    public required List<RouterParameterInfo> _paramsFilter { get; set; }
}