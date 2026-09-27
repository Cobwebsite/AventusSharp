using AventusSharp.Tools;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace AventusSharp.SSE
{
    public class SSEConfig
    {

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

    }
}
