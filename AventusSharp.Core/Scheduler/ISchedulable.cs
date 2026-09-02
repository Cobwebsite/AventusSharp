using AventusSharp.Tools;

namespace AventusSharp.Scheduler;

/// <summary>
/// A task discovered and registered by <see cref="SchedulerManager"/>.
/// </summary>
public interface ISchedulable
{
    /// <summary>
    /// Indicates whether the task must also run during scheduler initialization.
    /// </summary>
    bool TriggerOnStart();

    /// <summary>
    /// Executes the task.
    /// </summary>
    Task<VoidWithError> Trigger();

    /// <summary>
    /// Configures when the task runs.
    /// </summary>
    void Schedule(Schedule schedule);
}
