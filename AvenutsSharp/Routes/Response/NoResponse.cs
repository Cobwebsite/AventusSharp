using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace AventusSharp.Routes.Response
{
    public class NoResponse : IResponse
    {
        public async Task send(HttpContext context, IRouter? from = null)
        {
            
        }
    }
}
