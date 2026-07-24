using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace AventusSharp.Routes.Response
{
    public interface IResponse
    {
        public Task send(HttpContext context, IRouter? from);
    }
}
