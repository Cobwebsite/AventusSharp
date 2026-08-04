namespace AventusSharp.Hosting;

/// <summary>
/// Executes an Aventus request independently from its host.
/// </summary>
public interface IAventusRequestDispatcher
{
    Task DispatchAsync(IAventusContext context);
}
