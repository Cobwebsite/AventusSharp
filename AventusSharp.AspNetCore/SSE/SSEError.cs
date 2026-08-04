using AventusSharp.Routes;
using AventusSharp.Tools;
using AventusSharp.Tools.Attributes;
using System.Runtime.CompilerServices;
using System;

namespace AventusSharp.SSE
{

    [Export]
    public enum SSEErrorCode
    {
        UnknownError,
        CantDefineAssembly,
        ConfigError,
        MultipleMainEndpoint,
        CantGetValueFromBody,
        NoEndPoint
    }
    public class SSEError : GenericError<SSEErrorCode>
    {
        public SSEError(SSEErrorCode code, string message, [CallerFilePath] string callerPath = "", [CallerLineNumber] int callerNo = 0) : base(code, message, callerPath, callerNo)
        {
        }

        public SSEError(SSEErrorCode code, Exception exception, [CallerFilePath] string callerPath = "", [CallerLineNumber] int callerNo = 0) : base(code, "", callerPath, callerNo)
        {
            Message = exception.Message;
        }
    }
    public class VoidWithSSEError : VoidWithError<SSEError>
    {

    }
    public class ResultWithSSEError<T> : ResultWithError<T, SSEError>
    {

    }
}
