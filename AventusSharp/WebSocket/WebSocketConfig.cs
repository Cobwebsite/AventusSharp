using AventusSharp.Tools;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace AventusSharp.WebSocket
{
    public class WebSocketConfig
    {
        /// <summary>
        /// Define the regex to match the route based on various info
        /// </summary>
        public Func<string, Dictionary<string, WebSocketRouterParameterInfo>, object, bool, string>? transformPattern;
        /// <summary>
        /// Define how to transform result from transformPattern into regex
        /// </summary>
        public Func<string, Regex>? transformPatternIntoRegex;
        /// <summary>
        /// Define how the object must be converted from/to json
        /// </summary>
        public JsonSerializerSettings JSONSettings { get; set; } = new JsonSerializerSettings()
        {
            TypeNameHandling = TypeNameHandling.Auto,
            NullValueHandling = NullValueHandling.Ignore,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            DateFormatString = "yyyy-MM-ddTHH:mm:ss.ffffffZ",
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
    }
}
