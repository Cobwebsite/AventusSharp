using System.IO;

namespace AventusSharp.Routes.Form
{
    public interface IBodyElement
    {
        string name { get; set; }
        string value { get; set; }
    }
    public class BodyElement : IBodyElement
    {
        public string name { get; set; }
        public string value { get; set; }

        public BodyElement(string name, string value)
        {
            this.name = name;
            this.value = value;
        }

        public override string ToString()
        {
            return "\"" + name + "\":\"" + value + "\"";
        }
    }

    public class FileBodyElement : IBodyElement
    {
        public string name { get; set; }
        public string value { get; set; } = "";
        public string filename { get; set; } = "";
        public string type { get; set; }

        internal FileStream stream;
        internal string pathTemp;

        public FileBodyElement(string name, string type, FileStream stream, string pathTemp)
        {
            this.name = name;
            this.type = type;
            this.stream = stream;
            this.pathTemp = pathTemp;
        }

        public override string ToString()
        {
            return "file " + name + " (" + type + ")\r\n" + pathTemp;
        }
    }

}
