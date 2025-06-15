using AventusSharp.Routes.Response;
using AventusSharp.Tools;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        /// <summary>
        /// Define how the object must be converted from/to json
        /// </summary>
        public JsonSerializerOptions JSONSettings { get; set; } = new JsonSerializerOptions()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new AventusJsonConverter(), new CustomDateTimeConverter() }
        };

        /// <summary>
        /// Set to true to list all route on startup
        /// </summary>
        public bool PrintRoute { get; set; } = false;
        /// <summary>
        /// Set to true to print route triggered
        /// </summary>
        public bool PrintTrigger { get; set; } = false;
        /// <summary>
        /// Set to true to print all requests 
        /// </summary>
        public bool PrintRequest { get; set; } = false;


        public RouterConfig()
        {
            ViewDir = (HttpContext context, IRouter? from) =>
            {
                return Path.Combine(Environment.CurrentDirectory, "Views");
            };
        }
    }
}
