using AventusSharp.Tools;
using AventusSharp.Tools.Attributes;
using System;
using System.Runtime.CompilerServices;

namespace AventusSharp.Mcp;

[Export]
public enum McpErrorCode
{
    UnknowError,
}
public class McpError : GenericError<McpErrorCode>
{
    public McpError(McpErrorCode code, string message, [CallerFilePath] string callerPath = "", [CallerLineNumber] int callerNo = 0) : base(code, message, callerPath, callerNo)
    {
    }

    public McpError(McpErrorCode code, Exception exception, [CallerFilePath] string callerPath = "", [CallerLineNumber] int callerNo = 0) : base(code, "", callerPath, callerNo)
    {
        Message = exception.Message;
    }
}

public class VoidWithMcpError : VoidWithError<McpError>
{

}
public class ResultWithMcpError<T> : ResultWithError<T, McpError>
{

}
