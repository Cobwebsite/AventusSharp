using AventusSharp.Data.Attributes;
using System;
using System.Data;
using System.Reflection;

namespace AventusSharp.Data.Storage.Default.TableMember
{
    public class TableMemberInfoSql1NInt : TableMemberInfoSql, ITableMemberInfoSqlLinkSingle
    {
        public TableInfo? TableLinked { get; set; }
        public Type? TableLinkedType { get; protected set; }

        public DbType SqlType { get; protected set; } = DbType.Int32;

        public TableMemberInfoSql1NInt(MemberInfo? memberInfo, TableInfo tableInfo, bool isNullable) : base(memberInfo, tableInfo, isNullable)
        {
        }

        public override VoidWithDataError PrepareForSQL()
        {
            VoidWithDataError result = new VoidWithDataError();
            if (memberInfo == null)
            {
                result.Errors.Add(new DataError(DataErrorCode.MemberNotFound, "Member not found"));
                return result;
            }
            SqlName = memberInfo.Name;
            return result;
        }

        protected override void ParseAttributes()
        {
            IsAutoCreate = false;
            IsAutoDelete = false;
            IsAutoUpdate = false;
            base.ParseAttributes();
        }
        protected override bool ParseAttribute(Attribute attribute)
        {
            if (base.ParseAttribute(attribute))
            {
                return true;
            }

            if (attribute is ForeignKey foreignKeyAttr)
            {
                TableLinkedType = foreignKeyAttr.Type;
                return true;
            }
            return false;
        }

        public override object? GetSqlValue(object obj)
        {
            return GetValue(obj);
        }

        protected override void SetSqlValue(object obj, string? value)
        {
            if (int.TryParse(value, out int nb))
            {
                SetValue(obj, nb);
            }
        }


    }
}
