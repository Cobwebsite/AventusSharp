using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace AventusSharp.Routes.Attributes
{

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public abstract class Middleware : Attribute
    {
        public Middleware()
        {

        }

        public abstract Task Run(HttpContext context, RouteInfo info, Func<Task> next);
    }
}
