using AventusSharp.Data;
using HttpMultipartParser;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AventusSharp.Routes.Form
{
    public static class Tools
    {
        public static object? DefaultValue(Type type)
        {
            object? value = null;
            if (type.IsValueType && Nullable.GetUnderlyingType(type) == null)
            {
                value = Activator.CreateInstance(type);
            }
            return value;
        }

    }
}
