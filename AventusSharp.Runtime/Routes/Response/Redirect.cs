using System.Threading.Tasks;
using AventusSharp.Hosting;

namespace AventusSharp.Routes.Response
{
    public class Redirect : IResponse
    {
        private string uri;
        public Redirect(string uri)
        {
            this.uri = uri;
        }
        public Task send(IAventusContext context, IRouter? from = null)
        {
           context.Response.StatusCode = 302;
           context.Response.Headers["Location"] = [uri];
           return Task.CompletedTask;
        }
    }
}
