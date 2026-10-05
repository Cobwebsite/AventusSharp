using System.Reflection;
using System.Runtime.Loader;
using AventusSharp.Localization;

namespace CSharpToTypescript;

internal static class MauiExport
{
    public static int Run(string assemblyPath)
    {
        try
        {
            string path = Path.GetFullPath(assemblyPath);
            var context = new MauiAssemblyLoadContext(path);
            Assembly assembly = context.LoadFromAssemblyPath(path);
            Assembly core = context.LoadFromAssemblyName(new AssemblyName("AventusSharp.Core"));
            Type routerConfig = core.GetType("AventusSharp.Routes.RouterConfig", throwOnError: true)!;
            Type middleware = core.GetType("AventusSharp.Routes.RouterMiddleware", throwOnError: true)!;
            Type? program = assembly.GetTypes().FirstOrDefault(type => type.Name == "MauiProgram");
            if (program is null) throw new InvalidOperationException(AventusTranslations.Get(AventusMessageKeys.Converter.MauiProgramMissing));

            MethodInfo[] callbacks = program.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public)
                .SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                .Where(method => method.Name.Contains("<CreateMauiApp>") &&
                    method.GetParameters() is { Length: 1 } parameters &&
                    parameters[0].ParameterType == routerConfig)
                .ToArray();
            if (callbacks.Length != 1)
            {
                throw new InvalidOperationException(AventusTranslations.Get(AventusMessageKeys.Converter.MauiConfigurationCallbackCount, callbacks.Length));
            }

            MethodInfo callback = callbacks[0];
            object? target = callback.IsStatic ? null : Activator.CreateInstance(callback.DeclaringType!, nonPublic: true);
            Type actionType = typeof(Action<>).MakeGenericType(routerConfig);
            Delegate configure = Delegate.CreateDelegate(actionType, target, callback);
            middleware.GetMethod("Configure", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [configure]);
            middleware.GetMethod("Register", [typeof(Assembly)])!.Invoke(null, [assembly]);
            middleware
                .GetMethod("PrintForExport", BindingFlags.Public | BindingFlags.Static)!
                .Invoke(null, null);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine((exception as TargetInvocationException)?.InnerException ?? exception);
            return 1;
        }
    }

    private sealed class MauiAssemblyLoadContext(string assemblyPath) : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver resolver = new(assemblyPath);
        private readonly string directory = Path.GetDirectoryName(assemblyPath)!;

        protected override Assembly? Load(AssemblyName name)
        {
            string? path = resolver.ResolveAssemblyToPath(name);
            path ??= File.Exists(Path.Combine(directory, name.Name + ".dll"))
                ? Path.Combine(directory, name.Name + ".dll")
                : null;
            return path is null ? null : LoadFromAssemblyPath(path);
        }

        protected override IntPtr LoadUnmanagedDll(string name)
        {
            string? path = resolver.ResolveUnmanagedDllToPath(name);
            return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
        }
    }
}
