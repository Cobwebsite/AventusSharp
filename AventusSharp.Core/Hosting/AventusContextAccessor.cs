namespace AventusSharp.Hosting;

/// <summary>
/// Provides the Aventus context associated with the current asynchronous flow.
/// </summary>
public static class AventusContextAccessor
{
    private static readonly AsyncLocal<IAventusContext?> CurrentScope = new();

    public static IAventusContext? Current
    {
        get => CurrentScope.Value;
        set => CurrentScope.Value = value;
    }
}
