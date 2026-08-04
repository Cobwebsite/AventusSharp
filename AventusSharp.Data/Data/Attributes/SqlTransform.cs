using System;
using System.Data;
using AventusSharp.Data.Storage.Default.TableMember;

namespace AventusSharp.Data.Attributes
{

    public interface ISqlTransform
    {
        public object? ToSql(object? value, TableMemberInfoSql member);
        public object? FromSql(string? value, TableMemberInfoSql member);
        public abstract DbType? GetDbType(TableMemberInfoSql member);
    }
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public abstract class SqlTransform : Attribute, ISqlTransform
    {
        public abstract object? ToSql(object? value, TableMemberInfoSql member);
        public abstract object? FromSql(string? value, TableMemberInfoSql member);
        public abstract DbType? GetDbType(TableMemberInfoSql member);

    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public abstract class SqlTransform<T> : Attribute, ISqlTransform
    {

        public abstract object? ToSql(T value, TableMemberInfoSql member);

        public abstract T FromSql(string? value, TableMemberInfoSql member);
        public abstract DbType? GetDbType(TableMemberInfoSql member);
        object? ISqlTransform.ToSql(object? value, TableMemberInfoSql member)
        {
            if (value is T t)
                return ToSql(t, member);
            return null;
        }

        object? ISqlTransform.FromSql(string? value, TableMemberInfoSql member)
        {
            return FromSql(value, member);
        }
    }
}
