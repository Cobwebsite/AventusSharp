using System;

namespace AventusSharp.Routes.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class FrontCache : Attribute
{
    public bool Value { get; private set; }
    public FrontCache()
    {
        Value = true;
    }
    public FrontCache(bool value)
    {
        Value = value;
    }
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class FrontCacheClearable : Attribute
{
    public bool Cache { get; private set; }
    public bool Clear { get; private set; }
    public FrontCacheClearable()
    {
        Cache = true;
        Clear = true;
    }
    public FrontCacheClearable(bool cache, bool clear)
    {
        Cache = cache;
        Clear = clear;
    }
}