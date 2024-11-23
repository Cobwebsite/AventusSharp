using AventusSharp.Tools;
using System;
using System.Data;
using System.Reflection;

namespace AventusSharp.Data.Storage.Default.TableMember
{
    public class TableMemberInfoSql1N : TableMemberInfoSql, ITableMemberInfoSqlLinkSingle
    {
        public TableInfo? TableLinked { get; set; }
        public Type? TableLinkedType { get; protected set; }
        public DbType SqlType { get; protected set; } = DbType.Int32;


        public TableMemberInfoSql1N(MemberInfo? memberInfo, TableInfo tableInfo, bool isNullable) : base(memberInfo, tableInfo, isNullable)
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
            TableLinkedType = MemberType;
            SqlName = memberInfo.Name;
            return result;
        }

        public override void SetValue(object obj, object? value)
        {
            if (value is int)
            {
                SetIntValueToStorable(obj, value);
                return;
            }
            base.SetValue(obj, value);
        }

        private void SetIntValueToStorable(object obj, object? value)
        {
            object? temp = GetValue(obj);
            if (temp == null)
            {
                temp = Activator.CreateInstance(MemberType);
                if (temp == null)
                {
                    throw new Exception("Can't create the type " + MemberType.Name);
                }
                SetValue(obj, temp);
            }
            TableLinked?.Primary?.SetValue(temp, value);
        }

        public override object? GetSqlValue(object obj)
        {
            object? elementRef = GetValue(obj);
            if (elementRef is IStorable storableLink)
            {
                return storableLink.Id;
            }
            return null;
        }

        protected override void SetSqlValue(object obj, string? value)
        {
            // it's link
            if (string.IsNullOrEmpty(value))
            {
                SetValue(obj, null);
            }
        }

        protected override void ParseAttributes()
        {
            IsAutoCreate = false;
            IsAutoDelete = false;
            IsAutoUpdate = false;
            base.ParseAttributes();
        }

    }

    public class TableMemberInfoSqlParent : TableMemberInfoSql1N
    {
        public TableMemberInfoSqlParent(MemberInfo? memberInfo, TableInfo tableInfo, bool isNullable) : base(memberInfo, tableInfo, isNullable)
        {
            IsAutoIncrement = false;
        }

        protected override void ParseAttributes()
        {
            base.ParseAttributes();
            IsUpdatable = false;
        }
    }
}
