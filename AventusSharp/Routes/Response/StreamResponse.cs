using Microsoft.AspNetCore.Http;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AventusSharp.Hosting;

namespace AventusSharp.Routes.Response
{
    public class StreamResponse : IResponse
    {
        private Stream stream;
        private int code;
        private string contentType;
        public StreamResponse(Stream stream, string contentType = "application/octet-stream", int code = 200)
        {
            this.stream = stream;
            this.code = code;
            this.contentType = contentType;
        }
        public async Task send(IAventusContext context, IRouter? from = null)
        {
            context.Response.ContentType = contentType;
            context.Response.StatusCode = code;
            using (stream)
            {
                await stream.CopyToAsync(context.Response.Body);
            }
        }
    }
}
