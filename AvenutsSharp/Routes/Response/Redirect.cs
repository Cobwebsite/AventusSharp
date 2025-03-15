using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace AventusSharp.Routes.Response
{
    public class Redirect : IResponse
    {
        private string uri;
        public Redirect(string uri)
        {
            this.uri = uri;
        }
        public async Task send(HttpContext context, IRouter? from = null)
        {
           context.Response.Redirect(uri);
        }
    }
}
