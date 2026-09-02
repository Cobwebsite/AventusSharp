using AventusSharp.Tools;

namespace AventusSharp.Scheduler;

/// <summary>
/// Convenient base class for tasks managed by <see cref="SchedulerManager"/>.
/// </summary>
public abstract class Schedulable : ISchedulable
{
    /// <summary>
    /// Executes a registered schedulable immediately.
    /// </summary>
    public static Task<VoidWithError> Exec<T>() where T : ISchedulable =>
        SchedulerManager.Exec<T>();

    /// <inheritdoc />
    public virtual bool TriggerOnStart() => false;

    /// <inheritdoc />
    public Task<VoidWithError> Trigger() => Run();

    /// <summary>
    /// Contains the task work.
    /// </summary>
    protected abstract Task<VoidWithError> Run();

    /// <inheritdoc />
    public abstract void Schedule(Schedule schedule);
}
