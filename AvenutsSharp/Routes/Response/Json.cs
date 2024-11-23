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
        public Json(object? o)
        {
            txt = JsonConvert.SerializeObject(o, RouterMiddleware.config.JSONSettings);
        }

        public Json(object? o, JsonConverter converter)
        {
            txt = JsonConvert.SerializeObject(o, converter);
        }

        public Json(string json)
        {
            txt = json;
        }

        public async Task send(HttpContext context, IRouter? from = null)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(txt);
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = 200;
            await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
        }
    }
}
