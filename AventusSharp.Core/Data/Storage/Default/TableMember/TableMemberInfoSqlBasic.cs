using AventusSharp.Data.Attributes;
using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Migrations;
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
        public TableMemberInfoSqlBasic(IMigrationProperty property, TableInfo tableInfo) : base(property, tableInfo) { }

        public override VoidWithDataError PrepareForSQL()
        {
            VoidWithDataError result = new VoidWithDataError();
            if (memberInfo == null)
            {
                result.Errors.Add(new DataError(DataErrorCode.MemberNotFound, "Member not found"));
                return result;
            }

            SqlName = memberInfo.Name;
            DbType? dbType = GetDbType(MemberType, this);
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
            var result = GetValue(obj);
            if (SqlTransform != null)
            {
                result = SqlTransform.ToSql(result, this);
            }
            else if (result is DateTime dt && DM is IDatabaseDM database && database.Storage.DateTimeFormat != null)
            {
                return dt.ToString(database.Storage.DateTimeFormat);
            }
            else if (result?.GetType().IsEnum == true)
            {
                return result.ToString();
            }
            return result;
        }

        protected override void SetSqlValue(object obj, string? value)
        {
            Type effectiveMemberType = System.Nullable.GetUnderlyingType(MemberType) ?? MemberType;
            if (SqlTransform != null)
            {
                SetValue(obj, SqlTransform.FromSql(value, this));
            }
            else if (value == null && IsNullable)
            {
                SetValue(obj, value);
            }
            else if (effectiveMemberType == typeof(int))
            {
                if (int.TryParse(value, out int nb))
                {
                    SetValue(obj, nb);
                }
            }
            else if (effectiveMemberType == typeof(short))
            {
                if (short.TryParse(value, out short nb))
                {
                    SetValue(obj, nb);
                }
            }
            else if (effectiveMemberType == typeof(long))
            {
                if (long.TryParse(value, out long nb))
                {
                    SetValue(obj, nb);
                }
            }
            else if (effectiveMemberType == typeof(double))
            {
                if (double.TryParse(value, out double nb))
                {
                    SetValue(obj, nb);
                }
            }
            else if (effectiveMemberType == typeof(float))
            {
                if (float.TryParse(value, out float nb))
                {
                    SetValue(obj, nb);
                }
            }
            else if (effectiveMemberType == typeof(decimal))
            {
                if (decimal.TryParse(value, out decimal nb))
                {
                    SetValue(obj, nb);
                }
            }
            else if (effectiveMemberType == typeof(string))
            {
                SetValue(obj, value);
            }
            else if (effectiveMemberType == typeof(char))
            {
                if (!string.IsNullOrEmpty(value))
                {
                    SetValue(obj, value[0]);
                }
            }
            else if (effectiveMemberType == typeof(bool))
            {
                if (value == "1" || value?.ToLower() == "true")
                {
                    SetValue(obj, true);
                }
                else
                {
                    SetValue(obj, false);
                }
            }
            else if (effectiveMemberType == typeof(DateTime))
            {
                if (value == null)
                {
                    SetValue(obj, null);
                }
                else if (DateTime.TryParse(value, out DateTime dateTime))
                {
                    SetValue(obj, dateTime);
                }
            }
            else if (effectiveMemberType == typeof(TimeSpan))
            {
                if (value == null)
                {
                    SetValue(obj, null);
                }
                else if (TimeSpan.TryParse(value, out TimeSpan dateTime))
                {
                    SetValue(obj, dateTime);
                }
            }
            else if (effectiveMemberType == typeof(TimeOnly))
            {
                if (value == null)
                {
                    SetValue(obj, null);
                }
                else if (TimeOnly.TryParse(value, out TimeOnly dateTime))
                {
                    SetValue(obj, dateTime);
                }
            }
            else if (effectiveMemberType.IsEnum)
            {
                if (value == null)
                {
                    SetValue(obj, null);
                }
                else if (Enum.TryParse(effectiveMemberType, value, out object? val))
                {
                    SetValue(obj, val);
                }
            }
        }
    }
}
