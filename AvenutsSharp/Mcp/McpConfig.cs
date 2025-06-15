namespace AventusSharp.Mcp;


public class McpConfig
{
    public string route = "/mcp";
}

/*
using Microsoft.AspNetCore.Mvc;
using ModelContextProtocol;
using ModelContextProtocol.Messages;
using System.Text.Json;

namespace YourNamespace.Controllers
{
    [ApiController]
    [Route("mcp")]
    public class McpController : ControllerBase
    {
        private readonly McpServer _mcpServer;
        private readonly ILogger<McpController> _logger;

        public McpController(McpServer mcpServer, ILogger<McpController> logger)
        {
            _mcpServer = mcpServer;
            _logger = logger;
        }

        [HttpPost("rpc")]
        public async Task<IActionResult> HandleRpcRequest([FromBody] JsonElement request)
        {
            try
            {
                _logger.LogInformation("Received MCP RPC request: {Request}", request.ToString());

                // Désérialiser la requête JSON-RPC
                var jsonRpcRequest = JsonSerializer.Deserialize<JsonRpcRequest>(request.GetRawText());
                
                if (jsonRpcRequest == null)
                {
                    return BadRequest(CreateErrorResponse(null, -32700, "Parse error"));
                }

                // Traiter la requête selon la méthode
                var response = await ProcessRequest(jsonRpcRequest);
                
                return Ok(response);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON parsing error");
                return BadRequest(CreateErrorResponse(null, -32700, "Parse error"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing MCP request");
                return StatusCode(500, CreateErrorResponse(null, -32603, "Internal error"));
            }
        }

        private async Task<object> ProcessRequest(JsonRpcRequest request)
        {
            try
            {
                switch (request.Method)
                {
                    case "initialize":
                        return await HandleInitialize(request);
                    
                    case "tools/list":
                        return await HandleToolsList(request);
                    
                    case "tools/call":
                        return await HandleToolsCall(request);
                    
                    case "resources/list":
                        return await HandleResourcesList(request);
                    
                    case "resources/read":
                        return await HandleResourcesRead(request);
                    
                    case "prompts/list":
                        return await HandlePromptsList(request);
                    
                    case "prompts/get":
                        return await HandlePromptsGet(request);
                    
                    default:
                        return CreateErrorResponse(request.Id, -32601, $"Method not found: {request.Method}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing method {Method}", request.Method);
                return CreateErrorResponse(request.Id, -32603, "Internal error");
            }
        }

        private async Task<object> HandleInitialize(JsonRpcRequest request)
        {
            var initParams = JsonSerializer.Deserialize<InitializeParams>(
                request.Params?.GetRawText() ?? "{}");

            var result = new InitializeResult
            {
                ProtocolVersion = "2024-11-05",
                Capabilities = new ServerCapabilities
                {
                    Tools = new ToolsCapability(),
                    Resources = new ResourcesCapability(),
                    Prompts = new PromptsCapability()
                },
                ServerInfo = new ServerInfo
                {
                    Name = "Your MCP Server",
                    Version = "1.0.0"
                }
            };

            return CreateSuccessResponse(request.Id, result);
        }

        private async Task<object> HandleToolsList(JsonRpcRequest request)
        {
            // Récupérer la liste des outils depuis votre serveur MCP
            var tools = await _mcpServer.GetToolsAsync();
            
            var result = new ToolsListResult
            {
                Tools = tools.Select(tool => new Tool
                {
                    Name = tool.Name,
                    Description = tool.Description,
                    InputSchema = tool.InputSchema
                }).ToArray()
            };

            return CreateSuccessResponse(request.Id, result);
        }

        private async Task<object> HandleToolsCall(JsonRpcRequest request)
        {
            var callParams = JsonSerializer.Deserialize<ToolCallParams>(
                request.Params?.GetRawText() ?? "{}");

            if (callParams?.Name == null)
            {
                return CreateErrorResponse(request.Id, -32602, "Invalid params: missing tool name");
            }

            var result = await _mcpServer.CallToolAsync(callParams.Name, callParams.Arguments);
            
            return CreateSuccessResponse(request.Id, new ToolCallResult
            {
                Content = result.Content,
                IsError = result.IsError
            });
        }

        private async Task<object> HandleResourcesList(JsonRpcRequest request)
        {
            var resources = await _mcpServer.GetResourcesAsync();
            
            var result = new ResourcesListResult
            {
                Resources = resources.Select(resource => new Resource
                {
                    Uri = resource.Uri,
                    Name = resource.Name,
                    Description = resource.Description,
                    MimeType = resource.MimeType
                }).ToArray()
            };

            return CreateSuccessResponse(request.Id, result);
        }

        private async Task<object> HandleResourcesRead(JsonRpcRequest request)
        {
            var readParams = JsonSerializer.Deserialize<ResourceReadParams>(
                request.Params?.GetRawText() ?? "{}");

            if (readParams?.Uri == null)
            {
                return CreateErrorResponse(request.Id, -32602, "Invalid params: missing resource URI");
            }

            var result = await _mcpServer.ReadResourceAsync(readParams.Uri);
            
            return CreateSuccessResponse(request.Id, new ResourceReadResult
            {
                Contents = result.Contents
            });
        }

        private async Task<object> HandlePromptsList(JsonRpcRequest request)
        {
            var prompts = await _mcpServer.GetPromptsAsync();
            
            var result = new PromptsListResult
            {
                Prompts = prompts.Select(prompt => new Prompt
                {
                    Name = prompt.Name,
                    Description = prompt.Description,
                    Arguments = prompt.Arguments
                }).ToArray()
            };

            return CreateSuccessResponse(request.Id, result);
        }

        private async Task<object> HandlePromptsGet(JsonRpcRequest request)
        {
            var getParams = JsonSerializer.Deserialize<PromptGetParams>(
                request.Params?.GetRawText() ?? "{}");

            if (getParams?.Name == null)
            {
                return CreateErrorResponse(request.Id, -32602, "Invalid params: missing prompt name");
            }

            var result = await _mcpServer.GetPromptAsync(getParams.Name, getParams.Arguments);
            
            return CreateSuccessResponse(request.Id, new PromptGetResult
            {
                Description = result.Description,
                Messages = result.Messages
            });
        }

        private static object CreateSuccessResponse(object? id, object result)
        {
            return new
            {
                jsonrpc = "2.0",
                id = id,
                result = result
            };
        }

        private static object CreateErrorResponse(object? id, int code, string message)
        {
            return new
            {
                jsonrpc = "2.0",
                id = id,
                error = new
                {
                    code = code,
                    message = message
                }
            };
        }
    }

    // Classes pour la désérialisation JSON-RPC
    public class JsonRpcRequest
    {
        public string JsonRpc { get; set; } = "2.0";
        public object? Id { get; set; }
        public string Method { get; set; } = string.Empty;
        public JsonElement? Params { get; set; }
    }

    public class InitializeParams
    {
        public string ProtocolVersion { get; set; } = string.Empty;
        public ClientCapabilities? Capabilities { get; set; }
        public ClientInfo? ClientInfo { get; set; }
    }

    public class ClientCapabilities
    {
        public object? Experimental { get; set; }
        public SamplingCapability? Sampling { get; set; }
    }

    public class SamplingCapability
    {
        // Propriétés spécifiques au sampling si nécessaire
    }

    public class ClientInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
    }

    public class ToolCallParams
    {
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, object>? Arguments { get; set; }
    }

    public class ResourceReadParams
    {
        public string Uri { get; set; } = string.Empty;
    }

    public class PromptGetParams
    {
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, object>? Arguments { get; set; }
    }
}

// Configuration dans Program.cs ou Startup.cs
public static class McpServerConfiguration
{
    public static void AddMcpServer(this IServiceCollection services)
    {
        // Enregistrer votre implémentation de McpServer
        services.AddSingleton<McpServer>();
        
        // Configurer JSON options pour MCP
        services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.PropertyNameCaseInsensitive = true;
        });
    }
}

// Interface pour votre serveur MCP personnalisé
public interface IMcpServer
{
    Task<IEnumerable<ToolInfo>> GetToolsAsync();
    Task<ToolResult> CallToolAsync(string toolName, Dictionary<string, object>? arguments);
    Task<IEnumerable<ResourceInfo>> GetResourcesAsync();
    Task<ResourceContent> ReadResourceAsync(string uri);
    Task<IEnumerable<PromptInfo>> GetPromptsAsync();
    Task<PromptContent> GetPromptAsync(string name, Dictionary<string, object>? arguments);
}

// Implémentation de base
public class McpServer : IMcpServer
{
    private readonly ILogger<McpServer> _logger;

    public McpServer(ILogger<McpServer> logger)
    {
        _logger = logger;
    }

    public virtual async Task<IEnumerable<ToolInfo>> GetToolsAsync()
    {
        // Implémentez votre logique pour retourner les outils disponibles
        return new[]
        {
            new ToolInfo
            {
                Name = "example_tool",
                Description = "An example tool",
                InputSchema = new { type = "object", properties = new { } }
            }
        };
    }

    public virtual async Task<ToolResult> CallToolAsync(string toolName, Dictionary<string, object>? arguments)
    {
        // Implémentez votre logique d'exécution d'outils
        return new ToolResult
        {
            Content = new[] { new { type = "text", text = $"Tool {toolName} executed successfully" } },
            IsError = false
        };
    }

    public virtual async Task<IEnumerable<ResourceInfo>> GetResourcesAsync()
    {
        // Implémentez votre logique pour retourner les ressources disponibles
        return Array.Empty<ResourceInfo>();
    }

    public virtual async Task<ResourceContent> ReadResourceAsync(string uri)
    {
        // Implémentez votre logique de lecture de ressources
        return new ResourceContent
        {
            Contents = Array.Empty<object>()
        };
    }

    public virtual async Task<IEnumerable<PromptInfo>> GetPromptsAsync()
    {
        // Implémentez votre logique pour retourner les prompts disponibles
        return Array.Empty<PromptInfo>();
    }

    public virtual async Task<PromptContent> GetPromptAsync(string name, Dictionary<string, object>? arguments)
    {
        // Implémentez votre logique de récupération de prompts
        return new PromptContent
        {
            Description = $"Prompt {name}",
            Messages = Array.Empty<object>()
        };
    }
}

// Classes de données pour les résultats
public class ToolInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public object InputSchema { get; set; } = new();
}

public class ToolResult
{
    public object[] Content { get; set; } = Array.Empty<object>();
    public bool IsError { get; set; }
}

public class ResourceInfo
{
    public string Uri { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
}

public class ResourceContent
{
    public object[] Contents { get; set; } = Array.Empty<object>();
}

public class PromptInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public object[] Arguments { get; set; } = Array.Empty<object>();
}

public class PromptContent
{
    public string Description { get; set; } = string.Empty;
    public object[] Messages { get; set; } = Array.Empty<object>();
}
*/
