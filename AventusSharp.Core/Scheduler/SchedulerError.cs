using System.Runtime.CompilerServices;
using AventusSharp.Tools;
using AventusSharp.Tools.Attributes;

namespace AventusSharp.Scheduler;

/// <summary>
/// Error codes produced by the scheduler infrastructure.
/// </summary>
[Export]
public enum SchedulerErrorCode
{
    AssemblyLoadError,
    SchedulableCreationError,
    ScheduleRegistrationError,
    SchedulableNotRegistered,
    SchedulableAlreadyRunning,
    SchedulableExecutionError,
    UnknownError
}

/// <summary>
/// Error produced by the scheduler infrastructure.
/// </summary>
public class SchedulerError : GenericError<SchedulerErrorCode>
{
    public SchedulerError(
        SchedulerErrorCode code,
        string message,
        [CallerFilePath] string callerPath = "",
        [CallerLineNumber] int callerNo = 0)
        : base(code, message, callerPath, callerNo)
    {
    }

    public SchedulerError(
        SchedulerErrorCode code,
        Exception exception,
        [CallerFilePath] string callerPath = "",
        [CallerLineNumber] int callerNo = 0)
        : base(code, exception, callerPath, callerNo)
    {
    }
}

/// <summary>
/// Scheduler result retaining strongly typed scheduler errors.
/// </summary>
public class VoidWithSchedulerError : VoidWithError<SchedulerError>
{
}

/// <summary>
/// Scheduler result with a value and strongly typed scheduler errors.
/// </summary>
public class ResultWithSchedulerError<T> : ResultWithError<T, SchedulerError>
{
}
