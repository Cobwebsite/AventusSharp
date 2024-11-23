using AventusSharp.Tools.Attributes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace AventusSharp.Tools
{
    public interface IGenericError
    {
        void Print();
        Exception GetException();
    }
    [NoExport]
    public class GenericError : IGenericError
    {
        public int Code { get; set; }

        public string Message { get; set; }

        public List<object> Details { get; set; } = new List<object>();

        public string File { get; set; } = "";

        public int Line { get; set; } = -1;

        public GenericError(int code, string message, [CallerFilePath] string callerPath = "", [CallerLineNumber] int callerNo = 0)
        {
            Code = code;
            Message = message;
            File = callerPath;
            Line = callerNo;
        }
        public GenericError(int code, Exception exception, [CallerFilePath] string callerPath = "", [CallerLineNumber] int callerNo = 0) : this(code, "", callerPath, callerNo)
        {
            Message = exception.ToString();
        }

        public virtual string GetMessageException(bool showDetails)
        {
            string exceptionMsg = "Error " + Code + ": " + Message;
            if (Details.Count > 0)
            {
                exceptionMsg += "\n" + string.Join("\n", Details.Select(d => d.ToString()));
            }
            if (!string.IsNullOrEmpty(File) && Line != -1)
            {
                exceptionMsg += "\n at " + File + ":" + Line;
            }
            return exceptionMsg;
        }
        protected virtual string GetMessagePrint()
        {
            string callerPath = File.Split('\\').Last();
            DateTime now = DateTime.Now;
            return "[" + now + "] " + callerPath + ":" + Line + " => " + Message;
        }
        public Exception GetException()
        {
            return GetException(true);
        }
        public Exception GetException(bool showDetails)
        {
            return new AventusException(GetMessageException(showDetails), this);
        }

        public void Print()
        {
            Console.WriteLine(GetMessagePrint());
        }


    }

    [NoExport]
    public abstract class GenericError<T> : GenericError where T : Enum
    {
        public new T Code { get; set; }

        public GenericError(T code, string message, [CallerFilePath] string callerPath = "", [CallerLineNumber] int callerNo = 0) : base((int)Convert.ChangeType(code, typeof(int)), message, callerPath, callerNo)
        {
            this.Code = code;
        }

        public GenericError(T code, Exception exception, [CallerFilePath] string callerPath = "", [CallerLineNumber] int callerNo = 0) : base((int)Convert.ChangeType(code, typeof(int)), exception, callerPath, callerNo)
        {
            Code = code;
        }



    }


    public class AventusException : Exception
    {

        public readonly GenericError Error;

        public AventusException(string message, GenericError error) : base(message)
        {
            Error = error;
        }
    }

}
