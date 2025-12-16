using AventusSharp.Routes.Response;
using AventusSharp.Tools;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace AventusSharp.Routes
{
    public class RouterConfig
    {
        /// <summary>
        /// Define where to look for the view based on the current context and the router
        /// </summary>
        public Func<HttpContext, IRouter?, string> ViewDir { get; set; }

        /// <summary>
        /// Define where to save temp file for upload
        /// </summary>
        public string FileUploadTempDir { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
        /// <summary>
        /// Define the regex to match the route based on various info
        /// </summary>
        public Func<string, Dictionary<string, RouterParameterInfo>, Type, MethodInfo, Regex>? transformPattern;

        public DefaultUrlConfig defaultUrl = new();
        /// <summary>
        /// Define how the object must be converted from/to json
        /// </summary>
        public JsonSerializerSettings JSONSettings { get; set; } = new JsonSerializerSettings()
        {
            TypeNameHandling = TypeNameHandling.Auto,
            NullValueHandling = NullValueHandling.Ignore,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            DateFormatString = "yyyy-MM-ddTHH:mm:ss.fffZ",
            Converters = new List<JsonConverter>() { new AventusJsonConverter() }
        };

        /// <summary>
        /// Set to true to list all route on startup
        /// </summary>
        public bool PrintRoute { get; set; } = false;
        /// <summary>
        /// Set to true to print route triggered
        /// </summary>
        public bool PrintTrigger { get; set; } = false;


        public RouterConfig()
        {
            ViewDir = (HttpContext context, IRouter? from) =>
            {
                return Path.Combine(Environment.CurrentDirectory, "Views");
            };
        }
    }

    public class DefaultUrlConfig
    {
        public bool useNamespace = false;
        public bool useClassName = true;
        public string? namespaceRoot;
    }
}
