using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using AventusSharp.Hosting;

namespace AventusSharp.Routes.Response
{
    public interface IResponse
    {
        public Task send(IAventusContext context, IRouter? from);
    }
}
