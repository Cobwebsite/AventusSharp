
using System.Collections.Generic;
using AventusSharp.Routes.Request;

namespace AventusSharp.WebSocket;
public class WsExpose
{
    public required string BaseUrl { get; set; }
    public required string Pattern { get; set; }
    public required string MethodName { get; set; }
    public required string ClassName { get; set; }
    public required List<string> Params { get; set; }
    public string? EndPointClass { get; set; }
    public string? EndPointPath { get; set; }
    public int ResponseType { get; set; }
}
