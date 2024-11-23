using System.Reflection;

namespace AventusSharp.Routes
{
    public static class Tools
    {
        public static string GetDefaultMethodUrl(MethodInfo method)
        {
            string defaultName = method.Name.Split("`")[0].ToLower();
            if (defaultName == "index")
            {
                defaultName = "";
            }
            defaultName = "/" + defaultName;
            return defaultName;
        }
    }
}
