using System.Collections.Concurrent;
using System.Reflection;
using AventusSharp.Tools;
using Microsoft.Extensions.Logging;

namespace AventusSharp.Scheduler;

/// <summary>
/// Discovers, registers and executes <see cref="ISchedulable"/> tasks.
/// </summary>
public static class SchedulerManager
{
    private static readonly object Sync = new();
    private static readonly Dictionary<Type, ISchedulable> Schedulables = [];
    private static readonly HashSet<string> ScheduleNames = [];
    private static readonly ConcurrentDictionary<Type, byte> Running = new();
    private static Action<SchedulerManagerConfig> configureAction = _ => { };
    private static SchedulerManagerConfig config = new();
    private static bool configLoaded;

    /// <summary>
    /// Currently registered task instances.
    /// </summary>
    public static IReadOnlyCollection<ISchedulable> All
    {
        get
        {
            lock (Sync)
            {
                return Schedulables.Values.ToArray();
            }
        }
    }

    public static void Configure(Action<SchedulerManagerConfig> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        lock (Sync)
        {
            configureAction = configure;
            configLoaded = false;
        }
    }

    public static Task<VoidWithError> Init() => Init([Assembly.GetEntryAssembly()]);

    public static Task<VoidWithError> Init(Assembly? assembly) => Init([assembly]);

    public static async Task<VoidWithError> Init(IEnumerable<Assembly?> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        var result = new VoidWithError();

        lock (Sync)
        {
            if (!configLoaded)
            {
                config = new SchedulerManagerConfig();
                configureAction(config);
                configLoaded = true;
            }
        }

        if (config.UseUtcTime)
        {
            JobManager.UseUtcTime();
        }

        foreach (Assembly assembly in assemblies.OfType<Assembly>().Distinct())
        {
            IEnumerable<Type> types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = exception.Types.OfType<Type>();
                foreach (Exception loaderException in exception.LoaderExceptions.OfType<Exception>())
                {
                    result.Errors.Add(new SchedulerError(
                        SchedulerErrorCode.AssemblyLoadError,
                        loaderException));
                }
            }

            foreach (Type type in types.Where(IsSchedulableType))
            {
                ISchedulable? schedulable;
                lock (Sync)
                {
                    if (Schedulables.ContainsKey(type))
                    {
                        continue;
                    }
                }

                try
                {
                    schedulable = config.CreateSchedulable?.Invoke(type)
                        ?? Activator.CreateInstance(type) as ISchedulable;
                    if (schedulable is null)
                    {
                        result.Errors.Add(new SchedulerError(
                            SchedulerErrorCode.SchedulableCreationError,
                            $"Unable to create schedulable '{type.FullName}'."));
                        continue;
                    }
                }
                catch (Exception exception)
                {
                    result.Errors.Add(new SchedulerError(
                        SchedulerErrorCode.SchedulableCreationError,
                        exception));
                    continue;
                }

                string name = type.FullName ?? type.Name;
                try
                {
                    JobManager.RemoveJob(name);
                    JobManager.AddJob(
                        () => Execute(schedulable).GetAwaiter().GetResult(),
                        schedulable.Schedule,
                        name);
                    lock (Sync)
                    {
                        Schedulables[type] = schedulable;
                        ScheduleNames.Add(name);
                    }

                    if (schedulable.TriggerOnStart())
                    {
                        await Execute(schedulable).ConfigureAwait(false);
                    }
                }
                catch (Exception exception)
                {
                    JobManager.RemoveJob(name);
                    result.Errors.Add(new SchedulerError(
                        SchedulerErrorCode.ScheduleRegistrationError,
                        exception));
                }
            }
        }

        JobManager.Start();
        return result;
    }

    /// <summary>
    /// Executes a registered task immediately.
    /// </summary>
    public static Task<VoidWithError> Exec<T>() where T : ISchedulable
    {
        ISchedulable? schedulable;
        lock (Sync)
        {
            Schedulables.TryGetValue(typeof(T), out schedulable);
        }

        if (schedulable is null)
        {
            return Task.FromResult(new VoidWithError
            {
                Errors = [new SchedulerError(
                    SchedulerErrorCode.SchedulableNotRegistered,
                    $"Schedulable '{typeof(T).FullName}' is not registered.")]
            });
        }

        return Execute(schedulable);
    }

    /// <summary>
    /// Stops scheduling and unregisters only schedules owned by this manager.
    /// </summary>
    public static void Stop()
    {
        lock (Sync)
        {
            foreach (string name in ScheduleNames)
            {
                JobManager.RemoveJob(name);
            }
            ScheduleNames.Clear();
            Schedulables.Clear();
        }
    }

    private static bool IsSchedulableType(Type type) =>
        typeof(ISchedulable).IsAssignableFrom(type) &&
        type.IsClass &&
        !type.IsAbstract &&
        !type.IsGenericTypeDefinition;

    private static async Task<VoidWithError> Execute(ISchedulable schedulable)
    {
        Type type = schedulable.GetType();
        string name = type.FullName ?? type.Name;
        if (!Running.TryAdd(type, 0))
        {
            return new VoidWithError
            {
                Errors = [new SchedulerError(
                    SchedulerErrorCode.SchedulableAlreadyRunning,
                    $"Schedulable '{name}' is already running.")]
            };
        }

        try
        {
            VoidWithError result = await schedulable.Trigger().ConfigureAwait(false);
            if (!result.Success)
            {
                NotifyError(new SchedulerTaskErrorInfo
                {
                    SchedulableType = type,
                    Name = name,
                    Errors = result.Errors
                });
            }
            return result;
        }
        catch (Exception exception)
        {
            NotifyError(new SchedulerTaskErrorInfo
            {
                SchedulableType = type,
                Name = name,
                Exception = exception
            });
            return new VoidWithError
            {
                Errors = [new SchedulerError(
                    SchedulerErrorCode.SchedulableExecutionError,
                    exception)]
            };
        }
        finally
        {
            Running.TryRemove(type, out _);
        }
    }

    private static void NotifyError(SchedulerTaskErrorInfo info)
    {
        try
        {
            config.OnError(info);
        }
        catch (Exception exception)
        {
            AventusLogger.Instance.LogError(
                exception,
                "The scheduler error handler failed for {TaskName}",
                info.Name);
        }
    }
}
