using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading.Tasks;
using AventusSharp.Hosting;
using AventusSharp.Tools;
using System.Net;

namespace AventusSharp.Routes.Response
{
    public class Json : IResponse
    {
        private string txt;
        private int code;

        public Json(object? o, JsonConverter converter, int code = 200) : this(JsonConvert.SerializeObject(o, converter), code)
        {
            Parse(o);
        }
        public Json(object? o, JsonSerializerSettings converter, int code = 200) : this(JsonConvert.SerializeObject(o, converter), code)
        {
            Parse(o);
        }

        public Json(object? o, int code = 200) : this(o, RouterMiddleware.config.JSONSettings, code)
        {
        }

        public Json(string json, int code = 200)
        {
            txt = json;
            this.code = code;
        }

        public async Task send(IAventusContext context, IRouter? from = null)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(txt);
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = code;
            await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
        }

        private void Parse(object? o)
        {
            if (!RouterMiddleware.config.MapErrorCodeToHttpStatusCode) return;
            
            if (o is IWithError w && code == 200)
            {
                if (!w.Success && w.Errors.Count > 0)
                {
                    if (Enum.IsDefined((HttpStatusCode)w.Errors[0].Code))
                    {
                        code = w.Errors[0].Code;
                    }
                }
            }
        }
    }
}
