using AventusSharp.Hosting;
using AventusSharp.Maui;

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
