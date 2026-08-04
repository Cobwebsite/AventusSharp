using System;
using System.Reflection;
using AventusSharp.Tools.Attributes;
using AventusSharp.Tools;
using Binder = AventusSharp.Tools.Binder;

namespace AventusSharp.Routes.Request;

[Export]
public abstract class Request
{
    public void AutoBind(object source)
    {
        Binder.AutoBind(this, source);
    }
}

