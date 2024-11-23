using System;
using System.Collections.Generic;
using System.Data;

namespace AventusSharp.Data.Storage.Default.TableMember
{
    internal class CustomInternalTableMemberInfoSql : TableMemberInfoSql, ITableMemberInfoSqlWritable
    {
        public DbType SqlType { get; protected set; } = DbType.String;

        private Func<object, object?>? fctGetValue;
        private Func<object, object?>? fctGetSQLValue;
        private Action<object, object?>? fctSetValue;
        private Action<object, object?>? fctSetSQLValue;
        public CustomInternalTableMemberInfoSql(string name, Type type, TableInfo tableInfo) : base(tableInfo)
        {
            _Name = name;
            _Type = type;
        }

        public void DefineGetValue(Func<object, object?> fctGetValue)
        {
            this.fctGetValue = fctGetValue;
        }
        public void DefineGetSQLValue(Func<object, object> fctGetSQLValue)
        {
            this.fctGetSQLValue = fctGetSQLValue;
        }
        /// <summary>
        /// Param 1 => the object
        /// Param 2 => the value
        /// </summary>
        /// <param name="fctSetValue"></param>
        public void DefineSetValue(Action<object, object?> fctSetValue)
        {
            this.fctSetValue = fctSetValue;
        }
        /// <summary>
        /// Param 1 => the object
        /// Param 2 => the value
        /// </summary>
        /// <param name="fctSetSQLValue"></param>
        public void DefineSetSqlValue(Action<object, object?> fctSetSQLValue)
        {
            this.fctSetSQLValue = fctSetSQLValue;
        }

        public void DefineReflectedType(Type refelctedType)
        {
            _ReflectedType = refelctedType;
        }


        public void DefineType(Type type)
        {
            _Type = type;
        }

        public void DefineSQLInformation(SQLInformation information)
        {
            IsAutoIncrement = information.IsAutoIncrement;
            IsNullable = information.IsNullable;
            IsPrimary = information.IsPrimary;
            IsUnique = information.IsUnique;
            SqlName = information.SqlName;
            SqlType = information.SqlType;
        }

        public override object? GetSqlValue(object obj)
        {
            if (fctGetSQLValue != null)
            {
                return fctGetSQLValue(obj);
            }
            return GetValue(obj);
        }
        protected override void SetSqlValue(object obj, string? value)
        {
            if (fctSetSQLValue != null)
            {
                fctSetSQLValue(obj, value);
                return;
            }
            SetValue(obj, value);
        }

        public override T GetCustomAttribute<T>()
        {
#pragma warning disable CS8603 // Existence possible d'un retour de référence null.
            return null;
#pragma warning restore CS8603 // Existence possible d'un retour de référence null.
        }
        public override List<object> GetCustomAttributes(bool inherit)
        {
            return new List<object>();
        }
        public override object? GetValue(object obj)
        {
            if (fctGetValue != null)
            {
                return fctGetValue(obj);
            }
            return null;
        }
        public override void SetValue(object obj, object? value)
        {
            fctSetValue?.Invoke(obj, value);
        }

        public override VoidWithDataError PrepareForSQL()
        {
            return new VoidWithDataError();
        }

        private readonly string _Name;
        public override string Name => _Name;

        private Type? _ReflectedType;
        public override Type? ReflectedType => _ReflectedType;

        private Type _Type;
        public override Type MemberType => _Type;
    }

    public class SQLInformation
    {
        public bool IsPrimary { get; set; }
        public bool IsAutoIncrement { get; set; }
        public bool IsNullable { get; set; }
        public bool IsParentLink { get; set; }
        public bool IsUnique { get; set; }
        public string SqlTypeTxt { get; set; } = "";
        public DbType SqlType { get; set; }
        public string SqlName { get; set; } = "";
    }
}
