using AventusSharp.Attributes.Data;
using AventusSharp.Data.Attributes;
using System.Collections.Generic;
using System.Data;
using System;
using System.Reflection;
using System.Linq;
using System.Collections;
using AventusSharp.Data.Manager;

namespace AventusSharp.Data.Storage.Default.TableMember
{
    public interface ITableMemberInfoSqlLink
    {
        public TableInfo? TableLinked { get; set; }
        public Type? TableLinkedType { get; }
    }
    public interface ITableMemberInfoSqlWritable
    {
        public DbType SqlType { get; }
    }
    public interface ITableMemberInfoSizable {
        public Size? SizeAttr { get; }
    }
    public interface ITableMemberInfoSqlLinkSingle : ITableMemberInfoSqlLink, ITableMemberInfoSqlWritable
    {

    }
    public interface ITableMemberInfoSqlLinkMultiple : ITableMemberInfoSqlLink
    {
        public string? TableIntermediateName { get; }
        public string? TableIntermediateKey1 { get; }
        public string? TableIntermediateKey2 { get; }
        public DbType LinkFieldType { get; }
        public string LinkTableName { get; }

        public string LinkPrimaryName { get; }

    }
    public abstract class TableMemberInfoSql : TableMemberInfo
    {
        public static DbType? GetDbType(Type? type)
        {
            if (type == null)
                return null;
            if (type == typeof(int))
                return DbType.Int32;
            if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
                return DbType.Double;
            if (type == typeof(string))
                return DbType.String;
            if (type == typeof(char))
                return DbType.String;
            if (type == typeof(bool))
                return DbType.Boolean;
            if (type == typeof(DateTime))
                return DbType.DateTime;
            if (type.IsEnum)
                return DbType.String;
            if (IsTypeUsable(type))
                return DbType.Int32;
            return null;
        }

        public static bool IsTypeUsable(Type type)
        {
            if (type == null)
            {
                return false;
            }
            return type.GetInterfaces().Contains(typeof(IStorable));
        }
        public static Type? IsListTypeUsable(Type type)
        {
            if (type.IsGenericType && type.GetInterfaces().Contains(typeof(IList)))
            {
                Type typeInList = type.GetGenericArguments()[0];
                if (IsTypeUsable(typeInList))
                {
                    return typeInList;
                }
            }
            return null;
        }
        public static Type? IsDictionaryTypeUsable(Type type)
        {
            if (type.IsGenericType && type.GetInterfaces().Contains(typeof(IDictionary)))
            {
                Type typeIndex = type.GetGenericArguments()[0];
                if (typeIndex == typeof(int))
                {
                    Type typeValue = type.GetGenericArguments()[1];
                    if (IsTypeUsable(typeValue))
                    {
                        return typeValue;
                    }
                }
            }
            return null;
        }

        public static CustomTableMember? GetCustomType(MemberInfo memberInfo, TableInfo tableInfo, bool isNullable)
        {
            DataMemberInfo dataMemberInfo = new DataMemberInfo(memberInfo);
            CustomTableMemberType? attr = dataMemberInfo.Type?.GetCustomAttribute<CustomTableMemberType>(true);
            if (attr != null)
            {
                try
                {
                    object? o = Activator.CreateInstance(attr.Type, memberInfo, tableInfo, isNullable);
                    if (o is CustomTableMember customTableMember)
                    {
                        return customTableMember;
                    }
                }
                catch (Exception e)
                {
                    new DataError(DataErrorCode.UnknowError, e).Print();
                }
            }
            return null;
        }

        public static TableMemberInfoSql? CreateSql(MemberInfo memberInfo, TableInfo tableInfo)
        {
            TableMemberInfoSql? result = null;
            Type type = GetMemberInfoType(memberInfo);
            bool isNullable = false;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                isNullable = true;
                type = type.GetGenericArguments()[0];
            }
            else
            {
                if (memberInfo is PropertyInfo propertyInfo)
                {
                    NullabilityInfo info =  new NullabilityInfoContext().Create(propertyInfo);
                    if (info.WriteState == NullabilityState.Nullable || info.ReadState == NullabilityState.Nullable)
                    {
                        isNullable = true;
                    }
                }
                else if (memberInfo is FieldInfo fieldInfo)
                {
                    NullabilityInfo info =  new NullabilityInfoContext().Create(fieldInfo);
                    if (info.WriteState == NullabilityState.Nullable || info.ReadState == NullabilityState.Nullable)
                    {
                        isNullable = true;
                    }
                }
            }
            bool isList = type.GetInterfaces().Contains(typeof(IList));
            bool isDico = type.GetInterfaces().Contains(typeof(IDictionary));
            if (memberInfo.GetCustomAttribute<ForeignKey>() != null)
            {
                if (isList && type.GetGenericArguments()[0] == typeof(int))
                {
                    result = new TableMemberInfoSqlNMInt(memberInfo, tableInfo, isNullable);
                    return result;
                }
                if (type == typeof(int))
                {
                    return new TableMemberInfoSql1NInt(memberInfo, tableInfo, isNullable);
                }
            }

            // TODO manage List<int,... and Enum>
            if (isList && IsListTypeUsable(type) != null)
            {
                return new TableMemberInfoSqlNM(memberInfo, tableInfo, isNullable);
            }
            else if (isDico && IsDictionaryTypeUsable(type) != null)
            {
                return new TableMemberInfoSqlNM(memberInfo, tableInfo, isNullable);
            }

            else if (IsTypeUsable(type))
            {
                return new TableMemberInfoSql1N(memberInfo, tableInfo, isNullable);
            }

            else if (GetDbType(type) != null)
            {
                return new TableMemberInfoSqlBasic(memberInfo, tableInfo, isNullable);
            }

            return GetCustomType(memberInfo, tableInfo, isNullable);
        }
        private static Type GetMemberInfoType(MemberInfo memberInfo)
        {
            if (memberInfo is FieldInfo fieldInfo)
            {
                return fieldInfo.FieldType;
            }
            else if (memberInfo is PropertyInfo propertyInfo)
            {
                return propertyInfo.PropertyType;
            }
            throw new Exception("No type for field??");
        }


        public TableMemberInfoSql(TableInfo tableInfo) : base(tableInfo)
        {
        }
        public TableMemberInfoSql(MemberInfo? memberInfo, TableInfo tableInfo, bool isNullable) : base(memberInfo, tableInfo)
        {
            // prevent ? overriding attribute
            if (!IsNullByAttribute)
            {
                IsNullable = isNullable;
            }
        }

        #region SQL

        public override object? GetValueToSave(object? obj)
        {
            if (obj == null)
            {
                return null;
            }
            return GetSqlValue(obj);
        }
        public abstract object? GetSqlValue(object obj);

        protected abstract void SetSqlValue(object obj, string? value);
        public virtual void ApplySqlValue(object? obj, string? value)
        {
            if (obj == null)
            {
                return;
            }
            SetSqlValue(obj, value);
        }


        #region link
        //public TableMemberInfoLink Link { get; protected set; } = TableMemberInfoLink.None;
        //public TableInfo? TableLinked { get; set; }
        //public Type? TableLinkedType { get; protected set; }

        //public string? TableIntermediateName
        //{
        //    get
        //    {
        //        if (TableLinked == null)
        //        {
        //            return null;
        //        }
        //        return TableInfo.SqlTableName + "_" + TableLinked.SqlTableName;
        //    }
        //}
        //public string? TableIntermediateKey1
        //{
        //    get
        //    {
        //        return TableInfo.Primary == null ? null : TableInfo.SqlTableName + "_" + TableInfo.Primary.SqlName;
        //    }
        //}

        //public string? TableIntermediateKey2
        //{
        //    get
        //    {
        //        return TableLinked?.Primary == null ? null : TableLinked.SqlTableName + "_" + TableLinked.Primary.SqlName;
        //    }
        //}


        #endregion

        public abstract VoidWithDataError PrepareForSQL();


        #endregion

        #region attributes
        public bool IsPrimary { get; protected set; }
        public bool IsAutoIncrement { get; protected set; }
        public bool IsNullable { get; protected set; }
        public bool IsDeleteOnCascade { get; protected set; }
        public bool IsDeleteSetNull { get; protected set; }
        public bool IsUpdatable { get; internal set; } = true;
        public bool IsUnique { get; internal set; }
        public string SqlName { get; protected set; } = "";

        protected bool IsNullByAttribute { get; set; } = false;


        protected override void ParseAttributes()
        {
            if (!IsNullable)
            {
                IsNullable = DataMainManager.Config?.nullByDefault ?? false;
            }
            base.ParseAttributes();
        }

        protected override bool ParseAttribute(Attribute attribute)
        {

            bool result = base.ParseAttribute(attribute);
            if (result)
            {
                return true;
            }
            if (attribute is Primary)
            {
                IsPrimary = true;
                IsUpdatable = false;
                return true;
            }
            if (attribute is AutoIncrement)
            {
                IsAutoIncrement = true;
                return true;
            }
            // if (attribute.GetType().FullName == "System.Runtime.CompilerServices.NullableAttribute")
            // {
            //     // use it to handle null on string
            //     // by default a string can be null so the compiler won't change the type but add an attribute
            //     IsNullable = true;
            //     IsNullByAttribute = true;
            //     return true;
            // }
            if (attribute is Attributes.Nullable)
            {
                IsNullable = true;
                IsNullByAttribute = true;
                return true;
            }
            if (attribute is NotNullable notNullable)
            {
                IsNullable = false;
                IsNullByAttribute = true;
                return true;
            }
            if (attribute is DeleteOnCascade)
            {
                IsDeleteOnCascade = true;
                return true;
            }
            if (attribute is DeleteSetNull)
            {
                IsDeleteSetNull = true;
                return true;
            }
            if (attribute is Unique)
            {
                IsUnique = true;
                return true;
            }
            if (attribute is SqlName attrSqlName)
            {
                SqlName = attrSqlName.Name;
                return true;
            }
            return false;
        }

        #endregion

    }
}
