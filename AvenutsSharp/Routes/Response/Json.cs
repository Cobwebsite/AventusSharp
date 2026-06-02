using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading.Tasks;

namespace AventusSharp.Routes.Response
{
    public class Json : IResponse
    {
        private string txt;
        private int code;

        public Json(object? o, JsonConverter converter, int code = 200) : this(JsonConvert.SerializeObject(o, converter), code)
        {
        }
        public Json(object? o, JsonSerializerSettings converter, int code = 200) : this(JsonConvert.SerializeObject(o, converter), code)
        {
        }

        public Json(object? o, int code = 200) : this(o, RouterMiddleware.config.JSONSettings, code)
        {
        }

        public Json(string json, int code = 200)
        {
            txt = json;
            this.code = code;
        }

        public async Task send(HttpContext context, IRouter? from = null)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(txt);
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = code;
            await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
        }
    }
}
