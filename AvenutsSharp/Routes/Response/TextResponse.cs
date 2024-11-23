using Microsoft.AspNetCore.Http;
using System.Text;
using System.Threading.Tasks;

namespace AventusSharp.Routes.Response
{
    public class TextResponse : IResponse
    {
        private string text;
        private int code;
        public TextResponse(string text, int code = 200)
        {
            this.text = text;
            this.code = code;
        }
        public async Task send(HttpContext context, IRouter? from = null)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            context.Response.StatusCode = code;
            await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
        }
    }
}
