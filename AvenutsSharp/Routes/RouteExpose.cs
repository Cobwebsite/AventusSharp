
using System.Collections.Generic;
using AventusSharp.Routes.Request;

namespace AventusSharp.Routes;
public class RouteExposeHttp
{
    public MethodType Method { get; set; }
    public required string BaseUrl { get; set; }
    public required string Pattern { get; set; }
    public required string MethodName { get; set; }
    public required string ClassName { get; set; }
    public required List<string> Params { get; set; }
}