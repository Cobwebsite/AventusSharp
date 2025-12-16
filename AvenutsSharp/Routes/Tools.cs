using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AventusSharp.Routes
{
    public static class Tools
    {
        public static string GetDefaultMethodUrl(MethodInfo method, DefaultUrlConfig? config)
        {
            string defaultName = method.Name.Split("`")[0].ToLower();
            if (defaultName == "index")
            {
                defaultName = "";
            }
            defaultName = "/" + defaultName;

            if (config != null && config.useNamespace && method.DeclaringType?.FullName != null)
            {
                string path = method.DeclaringType.FullName;
                if (config.namespaceRoot != null)
                    path = path.Replace(config.namespaceRoot, "");

                List<string> splitted = path.Split(".").ToList();

                if (!config.useClassName)
                {
                    splitted.RemoveAt(splitted.Count - 1);
                }

                string final = string.Join("/", splitted);

                defaultName = final + defaultName;
            }

            return defaultName;
        }
    }
}
