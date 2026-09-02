using AventusSharp.Tools;
using Microsoft.Extensions.Logging;

namespace AventusSharp.Scheduler;

/// <summary>
/// Configures schedulable discovery, creation and error handling.
/// </summary>
public sealed class SchedulerManagerConfig
{
    /// <summary>
    /// Uses UTC rather than local time to calculate scheduler occurrences.
    /// This setting is applied before any schedulable is registered.
    /// </summary>
    public bool UseUtcTime { get; set; }

    /// <summary>
    /// Creates a schedulable instance. When left null, the current host adapter
    /// chooses its default creation strategy. Core usage falls back to a
    /// parameterless constructor.
    /// </summary>
    public Func<Type, ISchedulable?>? CreateSchedulable { get; set; }

    /// <summary>
    /// Called when a task returns errors or throws. Handler failures are logged
    /// and do not stop the scheduler.
    /// </summary>
    public Action<SchedulerTaskErrorInfo> OnError { get; set; } = info =>
    {
        if (info.Exception is not null)
        {
            AventusLogger.Instance.LogError(
                info.Exception,
                "The scheduled task {TaskName} failed",
                info.Name);
            return;
        }

        foreach (GenericError error in info.Errors)
        {
            AventusLogger.Instance.LogError(
                error.GetException(),
                "The scheduled task {TaskName} returned an error",
                info.Name);
        }
    };
}

/// <summary>
/// Describes an execution failure passed to the configured error handler.
/// </summary>
public sealed class SchedulerTaskErrorInfo
{
    public required Type SchedulableType { get; init; }
    public required string Name { get; init; }
    public IReadOnlyList<GenericError> Errors { get; init; } = [];
    public Exception? Exception { get; init; }
}
