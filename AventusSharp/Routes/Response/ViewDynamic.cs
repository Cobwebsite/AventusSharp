using Microsoft.AspNetCore.Http;
using Scriban;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AventusSharp.Hosting;

namespace AventusSharp.Routes.Response
{
    public class ViewDynamic : IResponse
    {
        private static readonly ConcurrentDictionary<string, Template> parsed = new();

        private string viewName;
        private object model;
        public ViewDynamic(string viewName, object model)
        {
            this.viewName = viewName;
            this.model = model;
        }
        public async Task send(IAventusContext context, IRouter? from)
        {
            string directory = RouterMiddleware.config.ViewDir(context, from);
            string path = Path.Combine(directory, viewName);
            if (!path.EndsWith(".sbnhtml"))
            {
                path += ".sbnhtml";
            }
            if (File.Exists(path))
            {
                Template template = parsed.GetOrAdd(
                    Path.GetFullPath(path),
                    static templatePath => Template.Parse(File.ReadAllText(templatePath)));
                string html = template.Render(model);
                byte[] bytes = Encoding.UTF8.GetBytes(html);
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/html";
                context.Response.ContentLength = bytes.Length;
                await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
            }
            else
            {
                byte[] bytes = Encoding.ASCII.GetBytes("View " + path + " not found");
                context.Response.StatusCode = 400;
                context.Response.ContentLength = bytes.Length;
                await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
            }
        }
    }
}
