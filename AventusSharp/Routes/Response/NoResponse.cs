using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using AventusSharp.Hosting;

namespace AventusSharp.Routes.Response
{
    public class NoResponse : IResponse
    {
        public Task send(IAventusContext context, IRouter? from = null)
        {
           return Task.CompletedTask;
        }
    }
}
