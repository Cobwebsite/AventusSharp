using AventusSharp.Data.Manager;
using AventusSharp.Data.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AventusSharp.Tools;
using AventusSharp.Data.Storage.Default.TableMember;

namespace AventusSharp.Data.Storage.Default
{
    public class TableInfo
    {
        public readonly static string TypeIdentifierName = "__type";
        public static string GetSQLTableName(Type type)
        {
            return DataMainManager.Config.GetSQLTableName(type);
        }

        /// <summary>
        /// The sql name of the class
        /// </summary>
        public string SqlTableName { get; private set; }

        /// <summary>
        /// The name of the class
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// The class parent, for heritance
        /// </summary>
        public TableInfo? Parent { get; internal set; }

        /// <summary>
        /// The class children, for heritance
        /// </summary>
        public List<TableInfo> Children { get; private set; } = new List<TableInfo>();

        /// <summary>
        /// Is the class abstract ?
        /// </summary>
        public bool IsAbstract { get; private set; }

        /// <summary>
        /// Has the class an infinite loop
        /// </summary>
        /// <remarks>True if the class itself or a field contains a reference on itself</remarks>
        public bool IsInfinite { get; private set; }


        private Dictionary<string, TableMemberInfoSql> _Members = new();
        /// <summary>
        /// List of members for this class only
        /// </summary>
        /// <remarks></remarks>
        public List<TableMemberInfoSql> Members
        {
            get
            {
                List<TableMemberInfoSql> list = _Members.Values.ToList();
                if (TypeMember != null)
                {
                    list.Insert(0, TypeMember);
                }
                return list;
            }
        }



        internal CustomInternalTableMemberInfoSql? TypeMember { get; private set; } = null;

        /// <summary>
        /// List of primaries members for this class only
        /// </summary>
        /// <remarks></remarks>
        public TableMemberInfoSql? Primary { get; set; }
        /// <summary>
        /// List of reverse members for this class only
        /// </summary>
        /// <remarks></remarks>
        public List<TableReverseMemberInfo> ReverseMembers { get; private set; } = new List<TableReverseMemberInfo>();

        public Type Type { get; private set; }

        public IGenericDM? DM { get; private set; }

        public TableInfo(Type type)
        {
            SqlTableName = GetSQLTableName(type);
            Type = type;
            if (type.IsGenericType)
            {
                IsAbstract = true;
            }
            Name = TypeTools.GetReadableName(Type);
        }
        public TableInfo(PyramidInfo pyramid) : this(pyramid.type)
        {

        }



        public VoidWithDataError Init()
        {
            return LoadMembers(Type);
        }

        public VoidWithError LoadDM()
        {
            VoidWithError result = new VoidWithError();
            ResultWithError<IGenericDM> resultTemp = GenericDM.GetWithError(Type);
            if (resultTemp.Success && resultTemp.Result != null)
            {
                DM = resultTemp.Result;
            }
            else
            {
                result.Errors.AddRange(resultTemp.Errors);
            }
            return result;
        }

        public void AddTypeMember()
        {
            CustomInternalTableMemberInfoSql typeMember = new(TypeIdentifierName, typeof(string), this);
            typeMember.DefineSQLInformation(new SQLInformation()
            {
                SqlName = TypeIdentifierName,
                SqlType = System.Data.DbType.String,
                SqlTypeTxt = "varchar(255)",
            });
            typeMember.DefineGetValue(delegate (object obj)
            {
                return obj.GetType().AssemblyQualifiedName;
            });
            typeMember.IsUpdatable = false;
            TypeMember = typeMember;
        }

        private VoidWithDataError LoadMembers(Type type)
        {
            foreach (FieldInfo field in type.GetFields())
            {
                if (field.DeclaringType == type)
                {
                    TableMemberInfo? temp = TableMemberInfo.Create(field, this);
                    VoidWithDataError result = PrepareMembers(temp);
                    if (!result.Success)
                    {
                        return result;
                    }
                }
            }
            foreach (PropertyInfo property in type.GetProperties())
            {
                if (property.DeclaringType == type)
                {
                    TableMemberInfo? temp = TableMemberInfo.Create(property, this);
                    VoidWithDataError result = PrepareMembers(temp);
                    if (!result.Success)
                    {
                        return result;
                    }
                }
            }
            return new VoidWithDataError();
        }
        private VoidWithDataError PrepareMembers(TableMemberInfo? temp)
        {

            if (temp is TableMemberInfoSql sqlMember)
            {
                VoidWithDataError result = sqlMember.PrepareForSQL();
                if (!result.Success)
                {
                    return result;
                }
                _Members[sqlMember.SqlName] = sqlMember;
                if (sqlMember.IsPrimary)
                {
                    Primary = sqlMember;
                }
            }
            else if (temp is TableReverseMemberInfo reverseMember)
            {

                VoidWithDataError result = reverseMember.Prepare();
                if (!result.Success)
                {
                    return result;
                }
                ReverseMembers.Add(reverseMember);
            }
            return new VoidWithDataError();
        }

        public bool IsChildOf(object o)
        {
            Type O = o.GetType();
            if (Type.IsGenericType)
            {
                try
                {
                    Type T = Type.MakeGenericType(O);
                    return O.IsSubclassOf(T);
                }
                catch
                {
                    return false;
                }
            }
            return O == Type;
        }
    }
}
