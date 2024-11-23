using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using AventusSharp.Data.Attributes;
using AventusSharp.Tools;

namespace AventusSharp.Data.Storage.Default.TableMember
{
    public class CustomTableMemberType : Attribute
    {
        public Type Type { get; set; }

        public CustomTableMemberType(Type type)
        {
            Type = type;
        }
    }
    public class CustomTableMemberType<T> : CustomTableMemberType where T : CustomTableMember
    {
        public CustomTableMemberType() : base(typeof(T))
        {

        }
    }


    public abstract class CustomTableMember : TableMemberInfoSql, ITableMemberInfoSqlWritable, ITableMemberInfoSizable
    {
        public DbType SqlType { get; protected set; } = DbType.String;

        public Size? SizeAttr { get; protected set; }
        public CustomTableMember(MemberInfo? memberInfo, TableInfo tableInfo, bool isNullable) : base(memberInfo, tableInfo, isNullable)
        {
        }

        public abstract DbType? GetDbType();

        public override VoidWithDataError PrepareForSQL()
        {
            VoidWithDataError result = new VoidWithDataError();
            if (memberInfo == null)
            {
                result.Errors.Add(new DataError(DataErrorCode.MemberNotFound, "Member not found"));
                return result;
            }

            SqlName = memberInfo.Name;
            DbType? dbType = GetDbType();
            if (dbType == null)
            {
                result.Errors.Add(new DataError(DataErrorCode.TypeNotFound, "Type " + TypeTools.GetReadableName(MemberType) + " can't be parsed into Database type"));
                return result;
            }
            SqlType = (DbType)dbType;
            return result;
        }

        protected override bool ParseAttribute(Attribute attribute)
        {
            if (base.ParseAttribute(attribute))
            {
                return true;
            }

            if (attribute is Size sizeAttr)
            {
                SizeAttr = sizeAttr;
                return true;
            }
            return false;
        }
    }

}
