using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AventusSharp.Data.Attributes;

namespace AventusSharp.Data.Migrations;

public class MigrationMember : MemberInfo
{
    private Type _parent;
    public override Type? DeclaringType => _parent;

    public override MemberTypes MemberType => MemberTypes.Property;

    private string _name;
    public override string Name => _name;

    public override Type? ReflectedType => _parent;

    protected List<Attribute> attributes;

    public Type PropertyType { get; private set; }

    public override object[] GetCustomAttributes(bool inherit)
    {
        return attributes.ToArray();
    }

    public override object[] GetCustomAttributes(Type attributeType, bool inherit)
    {
        return attributes.Where(p => p.GetType().IsAssignableTo(attributeType)).ToArray();
    }

    public override bool IsDefined(Type attributeType, bool inherit)
    {
        return attributes.Any(p => p.GetType().IsAssignableTo(attributeType));
    }

    public MigrationMember(IMigrationProperty property)
    {
        _name = property.Name;
        _parent = property.Parent;
        attributes = new List<Attribute>();
        PropertyType = property.Type;

        if (property.Options.AutoIncrement)
        {
            attributes.Add(new AutoIncrement());
        }
        if (property.Options.Unique)
        {
            attributes.Add(new Unique());
        }
        if (property.Options.Primary)
        {
            attributes.Add(new Primary());
        }
        if (property.Options.Nullable)
        {
            attributes.Add(new Attributes.Nullable());
        }
        if (property.Options.Index)
        {
            attributes.Add(new Attributes.Index());
        }
        if (property.Options.Default != null)
        {
            attributes.Add(new Default(property.Options.Default));
        }
        if (property.Options.Size != null)
        {
            attributes.Add(property.Options.Size);
        }

        if (property is IMigrationPropertyRef propertyRef)
        {
            if (propertyRef.Options.DeleteKind == DeleteKind.DeleteOnCascade)
            {
                attributes.Add(new DeleteOnCascade());
            }
            else if (propertyRef.Options.DeleteKind == DeleteKind.DeleteSetNull)
            {
                attributes.Add(new DeleteSetNull());
            }
        }
    }

    public MigrationMember(string name, Type parent, Type type)
    {
        _name = name;
        _parent = parent;
        attributes = new List<Attribute>();
        PropertyType = type;
    }
}

public class MigrationMemberId : MigrationMember
{

    public MigrationMemberId(Type parent, string name = "Id", Type? type = null) : base(name, parent, type ?? typeof(int))
    {
        attributes.Add(new Primary());
    }
}