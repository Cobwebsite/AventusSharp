using Microsoft.AspNetCore.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AventusSharp.Routes.Response
{
    public class Json : IResponse
    {
        public string txt;
        public Json(object? o)
        {
            txt = JsonSerializer.Serialize(o, RouterMiddleware.config.JSONSettings);
        }

        public Json(object? o, JsonConverter converter)
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                Converters = { converter }
            };
            txt = JsonSerializer.Serialize(o, options);
        }

        public Json(object? o, JsonSerializerOptions options)
        {
            txt = JsonSerializer.Serialize(o, options);
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
