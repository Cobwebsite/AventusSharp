using Microsoft.AspNetCore.Http;
using Scriban;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace AventusSharp.Routes.Response
{
    public class View : IResponse
    {
        private string viewName;
        public View(string viewName)
        {
            this.viewName = viewName;
        }
        public async Task send(HttpContext context, IRouter? from = null)
        {
            string directory = RouterMiddleware.config.ViewDir(context, from);
            string path = Path.Combine(directory, viewName);
            if (!path.EndsWith(".html"))
            {
                path += ".html";
            }
            if (File.Exists(path))
            {
                byte[] bytes = File.ReadAllBytes(path) ?? Array.Empty<byte>();
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/html";
                context.Response.Headers.Append("content-length", bytes.Length + "");
                await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
            }
            else
            {
                byte[] bytes = Encoding.ASCII.GetBytes("View " + path + " not found");
                context.Response.StatusCode = 400;
                context.Response.Headers.Append("content-length", bytes.Length + "");
                await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
            }
        }
    }
}
