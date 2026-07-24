using Microsoft.AspNetCore.Http;
using System.Text;
using System.Threading.Tasks;

namespace AventusSharp.Routes.Response
{
    public class ByteResponse : IResponse
    {
        private byte[] bytes;
        private int code;
        private string contentType;
        public ByteResponse(byte[] bytes, string contentType = "application/octet-stream", int code = 200)
        {
            this.bytes = bytes;
            this.code = code;
            this.contentType = contentType;
        }
        public async Task send(HttpContext context, IRouter? from = null)
        {
            context.Response.ContentType = contentType;
            context.Response.StatusCode = code;
            await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
        }
    }
}
