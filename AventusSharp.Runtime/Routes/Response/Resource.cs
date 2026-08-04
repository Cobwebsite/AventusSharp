using AventusSharp.Tools;
using AventusSharp.Tools.Attributes;

namespace AventusSharp.Routes.Response;

public abstract class Resource
{
    public void AutoBind(object source)
    {
        Binder.AutoBind(source, this);
    }
}