namespace AventusSharp.Tools;

public static class InterfaceDumperExtension
{
    public static Type[] GetCurrentInterfaces(this Type type)
    {
        HashSet<Type> allInterfaces = new(type.GetInterfaces());
        Type? baseType = type.BaseType;
        if (baseType != null)
        {
            allInterfaces.ExceptWith(baseType.GetInterfaces());
        }

        HashSet<Type> toRemove = new();
        foreach (Type currentInterface in allInterfaces)
        {
            foreach (Type inheritedInterface in currentInterface.GetInterfaces())
            {
                toRemove.Add(inheritedInterface);
            }
        }

        allInterfaces.ExceptWith(toRemove);
        return allInterfaces.ToArray();
    }
}
