using Microsoft.AspNetCore.Http;
using Scriban;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace AventusSharp.Routes.Response
{
    public class ViewDynamic : IResponse
    {
        private static Dictionary<string, Template> parsed = new Dictionary<string, Template>();

        private string viewName;
        private object model;
        public ViewDynamic(string viewName, object model)
        {
            this.viewName = viewName;
            this.model = model;
        }
        public async Task send(HttpContext context, IRouter? from)
        {
            string directory = RouterMiddleware.config.ViewDir(context, from);
            string path = Path.Combine(directory, viewName);
            if (!path.EndsWith(".sbnhtml"))
            {
                path += ".sbnhtml";
            }
            if (File.Exists(path))
            {
                if (!parsed.ContainsKey(viewName))
                {
                    parsed[viewName] = Template.Parse(File.ReadAllText(path));
                }

                string html = parsed[viewName].Render(model);
                byte[] bytes = Encoding.UTF8.GetBytes(html);
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
