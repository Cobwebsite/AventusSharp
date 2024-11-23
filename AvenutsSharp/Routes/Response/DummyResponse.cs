using Microsoft.AspNetCore.Http;
using System.Text;
using System.Threading.Tasks;

namespace AventusSharp.Routes.Response
{
    public class DummyResponse : IResponse
    {
        public async Task send(HttpContext context, IRouter? from = null)
        {
            byte[]? bytes = Encoding.UTF8.GetBytes("Im dummy");
            context.Response.StatusCode = 200;
            await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
        }
    }
}
