using AventusSharp.Routes;
using AventusSharp.Tools;
using AventusSharp.Tools.Attributes;
using System.Runtime.CompilerServices;
using System;

namespace AventusSharp.WebSocket
{

    [Export]
    public enum WsErrorCode
    {
        UnknowError,
        CantDefineAssembly,
        ConfigError,
        MultipleMainEndpoint,
        CantGetValueFromBody,
        NoConnection,
        NoEndPoint,
        NoPath,
    }
    public class WsError : GenericError<WsErrorCode>
    {
        public WsError(WsErrorCode code, string message, [CallerFilePath] string callerPath = "", [CallerLineNumber] int callerNo = 0) : base(code, message, callerPath, callerNo)
        {
        }

        public WsError(WsErrorCode code, Exception exception, [CallerFilePath] string callerPath = "", [CallerLineNumber] int callerNo = 0) : base(code, "", callerPath, callerNo)
        {
            Message = exception.Message;
        }
    }
    public class VoidWithWsError : VoidWithError<WsError>
    {

    }
    public class ResultWithWsError<T> : ResultWithError<T, WsError>
    {

    }
}
