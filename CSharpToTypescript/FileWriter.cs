using MySqlX.XDevAPI.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpToTypescript
{
    public class FileWriter
    {
        protected int indent = 0;
        protected List<string> content = new List<string>();

        public string GetIndentedText(string txt)
        {
            for (int i = 0; i < indent; i++)
            {
                txt = "\t" + txt;
            }
            return txt;
        }
        public void AddTxt(string txt, List<string>? result = null)
        {
            if (result == null) result = content;

            for (int i = 0; i < indent; i++)
            {
                txt = "\t" + txt;
            }
            result.Add(txt);
        }
        public void AddTxt(List<string> txts, List<string>? result = null)
        {
            if (result == null) result = content;

            foreach (var txt in txts)
            {
                AddTxt(txt, result);
            }
        }
        public void AddTxtOpen(string txt, List<string>? result = null)
        {
            if (result == null) result = content;
            AddTxt(txt, result);
            indent++;
        }
        public void AddTxtClose(string txt, List<string>? result = null)
        {
            if (result == null) result = content;
            indent--;
            AddTxt(txt, result);
        }

        public void AddIndent()
        {
            indent++;
        }
        public void RemoveIndent()
        {
            indent--;
        }


        public string GetContent()
        {
            return string.Join("\r\n", content);
        }
    }
}
