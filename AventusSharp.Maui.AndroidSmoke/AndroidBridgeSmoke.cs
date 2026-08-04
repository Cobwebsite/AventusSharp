using AventusSharp.Hosting;
using AventusSharp.Maui;
using AventusSharp.Data;

namespace AventusSharp.Maui.AndroidSmoke;

public static class AndroidBridgeSmoke
{
    public static AventusMauiBridge Create(
        IAventusRequestDispatcher dispatcher,
        IServiceProvider services)
    {
        return new AventusMauiBridge(dispatcher, () => services);
    }
}

public sealed class AndroidSmokeRecord : Storable<AndroidSmokeRecord>
{
    public string Name { get; set; } = string.Empty;
}
