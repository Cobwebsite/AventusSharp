using AventusSharp.Data.Attributes;
using AventusSharp.Tools;
using System;
using System.Data;
using System.Reflection;

namespace AventusSharp.Data.Storage.Default.TableMember
{
    public class TableMemberInfoSqlBasic : TableMemberInfoSql, ITableMemberInfoSqlWritable, ITableMemberInfoSizable
    {
        public DbType SqlType { get; protected set; } = DbType.String;

        public Size? SizeAttr { get; protected set; }

        public TableMemberInfoSqlBasic(MemberInfo? memberInfo, TableInfo tableInfo, bool isNullable) : base(memberInfo, tableInfo, isNullable)
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
            DbType? dbType = GetDbType(MemberType);
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

        public override object? GetSqlValue(object obj)
        {
            return GetValue(obj);
        }

        protected override void SetSqlValue(object obj, string? value)
        {
            if (MemberType == typeof(int))
            {
                if (int.TryParse(value, out int nb))
                {
                    SetValue(obj, nb);
                }
            }
            else if (MemberType == typeof(double))
            {
                if (double.TryParse(value, out double nb))
                {
                    SetValue(obj, nb);
                }
            }
            else if (MemberType == typeof(float))
            {
                if (float.TryParse(value, out float nb))
                {
                    SetValue(obj, nb);
                }
            }
            else if (MemberType == typeof(decimal))
            {
                if (decimal.TryParse(value, out decimal nb))
                {
                    SetValue(obj, nb);
                }
            }
            else if (MemberType == typeof(string))
            {
                SetValue(obj, value);
            }
            else if (MemberType == typeof(bool))
            {
                if (value == "1")
                {
                    SetValue(obj, true);
                }
                else
                {
                    SetValue(obj, false);
                }
            }
            else if (MemberType == typeof(DateTime))
            {
                if(value == null)
                {
                    SetValue(obj, null);
                }
                else if(DateTime.TryParse(value, out DateTime dateTime))
                {
                    SetValue(obj, dateTime);
                }
            }
            else if (MemberType.IsEnum)
            {
                if (value == null)
                {
                    SetValue(obj, null);
                }
                else if (Enum.TryParse(MemberType, value, out object? val))
                {
                    SetValue(obj, val);
                }
            }
        }
    }
}
