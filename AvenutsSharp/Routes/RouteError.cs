using AventusSharp.Tools;
using AventusSharp.Tools.Attributes;
using System;
using System.Runtime.CompilerServices;

namespace AventusSharp.Routes
{
    [Export]
    public enum RouteErrorCode
    {
        UnknowError,
        FormContentTypeUnknown,
        CantGetValueFromBody,
        CantMoveFile,
        CantCreateFolders,
        RouteAlreadyExist,
    }
    public class RouteError : GenericError<RouteErrorCode>
    {
        public RouteError(RouteErrorCode code, string message, [CallerFilePath] string callerPath = "", [CallerLineNumber] int callerNo = 0) : base(code, message, callerPath, callerNo)
        {
        }

        public RouteError(RouteErrorCode code, Exception exception, [CallerFilePath] string callerPath = "", [CallerLineNumber] int callerNo = 0) : base(code, "", callerPath, callerNo)
        {
            Message = exception.Message;
        }
    }

    public class VoidWithRouteError : VoidWithError<RouteError>
    {

    }
    public class ResultWithRouteError<T> : ResultWithError<T, RouteError>
    {

    }
}
