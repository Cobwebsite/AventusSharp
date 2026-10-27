using Microsoft.Extensions.DependencyInjection;

namespace AventusSharp;

public class AventusService : IServiceProvider
{
    public static IServiceProvider Provider { get; set; } = new AventusService();

    public object? GetService(Type serviceType)
    {
        return null;
    }
}