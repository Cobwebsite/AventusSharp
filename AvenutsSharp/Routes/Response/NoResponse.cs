using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace AventusSharp.Routes.Response
{
    public class NoResponse : IResponse
    {
        public Task send(HttpContext context, IRouter? from = null)
        {
           return Task.CompletedTask;
        }
    }
}
