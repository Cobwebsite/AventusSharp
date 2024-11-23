using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Storage.Default.TableMember;
using AventusSharp.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;

namespace AventusSharp.Data.Manager.DB
{
    public enum WhereGroupFctEnum
    {
        None,
        Not,
        And,
        Or,
        Equal,
        NotEqual,
        LessThan,
        GreaterThan,
        LessThanOrEqual,
        GreaterThanOrEqual,
        Add,
        Subtract,
        Multiply,
        Divide,
        StartsWith,
        EndsWith,
        ContainsStr,
        ListContains,
        Link,
    }

    public enum WhereGroupFctSqlEnum
    {
        Date,
        Time,
        ToLower,
        ToUpper,
        Year,
        Month,
        Day,
        Hour,
        Minute,
        Second,
    }

    public interface IWhereGroup { }
    public interface IWhereRootGroup
    {
        public bool negate { get; set; }
    }
    public class WhereGroup : IWhereGroup, IWhereRootGroup
    {
        public List<IWhereGroup> Groups { get; set; } = new List<IWhereGroup>();
        public bool negate { get; set; } = false;

    }
    public class WhereGroupFct : IWhereGroup
    {
        public WhereGroupFctEnum Fct { get; set; }
        public WhereGroupFct(WhereGroupFctEnum fct)
        {
            Fct = fct;
        }
    }

    public class WhereGroupFctSql : IWhereGroup
    {
        public WhereGroupFctSqlEnum Fct { get; set; }
        public WhereGroupFctSql(WhereGroupFctSqlEnum fct)
        {
            Fct = fct;
        }
    }

    public class WhereGroupSingleBool : IWhereGroup, IWhereRootGroup
    {
        public string Alias { get; set; }
        public TableMemberInfoSql TableMemberInfo { get; set; }
        public bool negate { get; set; } = false;

        public WhereGroupSingleBool(string alias, TableMemberInfoSql tableMemberInfo)
        {
            Alias = alias;
            TableMemberInfo = tableMemberInfo;
        }
    }
    public class WhereGroupConstantNull : IWhereGroup
    {
    }
    public class WhereGroupConstantString : IWhereGroup
    {
        public string Value { get; set; } = "";
        public WhereGroupConstantString(string value)
        {
            Value = value;
        }
    }
    public class WhereGroupConstantOther : IWhereGroup
    {
        public string Value { get; set; } = "";
        public WhereGroupConstantOther(string value)
        {
            Value = value;
        }
    }
    public class WhereGroupConstantBool : IWhereGroup
    {
        public bool Value { get; set; }
        public WhereGroupConstantBool(bool value)
        {
            Value = value;
        }
    }
    public class WhereGroupConstantDateTime : IWhereGroup
    {
        public DateTime Value { get; set; }
        public WhereGroupConstantDateTime(DateTime value)
        {
            Value = value;
        }
    }
    public class WhereGroupConstantParameter : IWhereGroup
    {
        public string Value { get; set; }
        public WhereGroupConstantParameter(string value)
        {
            Value = value;
        }
    }
    public class WhereGroupField : IWhereGroup
    {
        public string Alias { get; set; }
        // private TableMemberInfoSql TableMemberInfo { get; set; }

        public string SqlName { get; set; }

        public WhereGroupField(string alias, TableMemberInfoSql tableMemberInfo)
        {
            Alias = alias;
            // TableMemberInfo = tableMemberInfo;
            SqlName = tableMemberInfo.SqlName;

            if (tableMemberInfo is ITableMemberInfoSqlLinkMultiple multiple && multiple.TableIntermediateKey2 != null)
            {
                SqlName = multiple.TableIntermediateKey2;
            }
        }
    }

    public class ParamsInfo
    {

        public ParamsInfo()
        {

        }
        public string Name { get; set; } = "";
        public Type TypeLvl0 { get; set; } = typeof(object);
        public DbType DbType { get; set; }
        public List<TableMemberInfoSql> MembersList { get; set; } = new List<TableMemberInfoSql>();

        public object? RootValue { get; set; }
        public object? Value { get; set; }

        public WhereGroupFctEnum FctMethodCall { get; set; }

        public bool IsNameSimilar(string name)
        {
            return Regex.IsMatch(name, @"^" + this.Name + @"(\.|$)");
        }
        public void SetValue(object value)
        {
            RootValue = value;
            if (value.GetType() == TypeLvl0)
            {
                object? valueToSet = value;
                for (int i = 0; i < MembersList.Count - 1; i++)
                {
                    TableMemberInfo member = MembersList[i];
                    valueToSet = member.GetValue(valueToSet);
                    if (valueToSet == null)
                    {
                        Value = null;
                        return;
                    }
                }
                if (MembersList.Count > 0)
                {
                    valueToSet = MembersList[MembersList.Count - 1].GetValueToSave(valueToSet);
                }
                if (valueToSet is IList listToSet)
                {
                    List<int> ids = new List<int>();
                    foreach (object valueUnique in listToSet)
                    {
                        if (valueUnique is IStorable storable)
                        {
                            ids.Add(storable.Id);
                        }
                        else if (valueUnique is int storableId)
                        {
                            ids.Add(storableId);
                        }
                    }
                    valueToSet = ids;
                }
                else if (valueToSet is IStorable storable)
                {
                    valueToSet = storable.Id;
                }

                Value = valueToSet;
            }
        }

        public void SetCurrentValueOnObject(object baseObj)
        {
            RootValue = baseObj;
            Type toCheck = TypeLvl0;
            if (toCheck.IsGenericType)
            {
                try
                {
                    toCheck = toCheck.MakeGenericType(baseObj.GetType());
                }
                catch { }
            }
            if (toCheck.IsInstanceOfType(baseObj))
            {
                object? whereToSet = baseObj;
                for (int i = 0; i < MembersList.Count - 1; i++)
                {
                    whereToSet = MembersList[i].GetValue(whereToSet);
                    if (whereToSet == null)
                    {
                        break;
                    }
                }
                if (whereToSet != null)
                {
                    if (Value is string stringVal)
                    {
                        MembersList[^1].ApplySqlValue(whereToSet, stringVal);
                    }
                    else
                    {
                        MembersList[^1].SetValue(whereToSet, Value);
                    }
                }
            }
        }

        public List<GenericError> IsValueValid()
        {
            TableMemberInfo memberInfo = MembersList[^1];
            return memberInfo.IsValid(Value, RootValue);
        }

    }

    public class SortInfo
    {
        public TableMemberInfoSql TableMember { get; set; }
        public string Alias { get; set; }

        public Sort Sort { get; set; }

        public SortInfo(TableMemberInfoSql tableMember, string alias, Sort sort)
        {
            TableMember = tableMember;
            Alias = alias;
            Sort = sort;
        }
    }
    public class DatabaseBuilderInfoChild
    {
        public string Alias { get; set; }
        public TableInfo TableInfo { get; set; }

        public List<DatabaseBuilderInfoChild> Children { get; set; } = new List<DatabaseBuilderInfoChild>();

        public DatabaseBuilderInfoChild(string alias, TableInfo tableInfo)
        {
            this.Alias = alias;
            this.TableInfo = tableInfo;
        }
    }
    public class DatabaseBuilderInfoMember
    {
        public TableMemberInfoSql MemberInfo { get; set; }
        public string Alias { get; set; }
        public bool UseDM { get; set; }

        public Type? Type { get; }

        public TableMemberInfo? TypeMemberInfo { get; }

        public DatabaseBuilderInfoMember(TableMemberInfoSql memberInfo, string alias, IDBStorage from)
        {
            this.MemberInfo = memberInfo;
            this.Alias = alias;
            Type = memberInfo.MemberType;
            if (memberInfo is TableMemberInfoSql1N)
            {
                if (memberInfo.DM is IDatabaseDM databaseDM && databaseDM.NeedLocalCache && databaseDM.Storage == from)
                {
                    UseDM = true;
                }
                else if (memberInfo.TableInfo.IsAbstract)
                {
                    TypeMemberInfo = memberInfo.TableInfo.Members.Find(m => m.SqlName == TableInfo.TypeIdentifierName);
                }
            }

        }

        public bool IsGeneric()
        {
            if (Type != null)
            {
                if (Type.IsGenericType || Type.IsInterface)
                {
                    return true;
                }
            }
            return false;
        }

    }
    public class DatabaseBuilderInfo
    {
        public TableInfo TableInfo { get; set; }
        public string Alias { get; set; }
        public Dictionary<TableMemberInfoSql, DatabaseBuilderInfoMember> Members { get; set; } = new Dictionary<TableMemberInfoSql, DatabaseBuilderInfoMember>();

        public Dictionary<TableInfo, string> Parents { get; set; } = new Dictionary<TableInfo, string>(); // string is the alias
        public List<DatabaseBuilderInfoChild> Children { get; set; } = new List<DatabaseBuilderInfoChild>();

        public Dictionary<TableMemberInfoSql, DatabaseBuilderInfo> joins = new();
        public Dictionary<ITableMemberInfoSqlLinkMultiple, string> joinsNM = new();
        public List<TableReverseMemberInfo> ReverseLinks { get; set; } = new List<TableReverseMemberInfo>();

        public DatabaseBuilderInfo(string alias, TableInfo tableInfo)
        {
            this.Alias = alias;
            this.TableInfo = tableInfo;
        }
        public KeyValuePair<TableMemberInfoSql?, string> GetTableMemberInfoAndAlias(string field)
        {
            string aliasTemp = Alias;
            KeyValuePair<TableMemberInfoSql?, string> result = new();
            TableMemberInfoSql? memberInfo = null;
            memberInfo = TableInfo.Members.Find(m => m.Name == field);
            if (memberInfo == null)
            {
                foreach (KeyValuePair<TableInfo, string> parent in Parents)
                {
                    memberInfo = parent.Key.Members.Find(m => m.Name == field);
                    if (memberInfo != null)
                    {
                        aliasTemp = parent.Value;
                        break;
                    }
                }
            }
            // result = new KeyValuePair<TableMemberInfoSql?, string>(memberInfo, aliasTemp);

            if (memberInfo is ITableMemberInfoSqlLinkMultiple linkMultiple)
            {
                if (joinsNM.ContainsKey(linkMultiple))
                {
                    result = new KeyValuePair<TableMemberInfoSql?, string>(memberInfo, joinsNM[linkMultiple]);
                }
            }
            else
            {
                result = new KeyValuePair<TableMemberInfoSql?, string>(memberInfo, aliasTemp);
            }
            return result;
        }

        public TableMemberInfoSql? GetTableMemberInfo(string field)
        {
            TableMemberInfoSql? memberInfo = null;
            memberInfo = TableInfo.Members.Find(m => m.Name == field);
            if (memberInfo == null)
            {
                foreach (KeyValuePair<TableInfo, string> parent in Parents)
                {
                    memberInfo = parent.Key.Members.Find(m => m.Name == field);
                    if (memberInfo != null)
                    {
                        return memberInfo;
                    }
                }
            }
            return memberInfo;
        }

        public TableReverseMemberInfo? GetReverseTableMemberInfo(string field)
        {
            TableReverseMemberInfo? memberInfo = null;
            memberInfo = TableInfo.ReverseMembers.Find(m => m.Name == field);
            if (memberInfo == null)
            {
                foreach (KeyValuePair<TableInfo, string> parent in Parents)
                {
                    memberInfo = parent.Key.ReverseMembers.Find(m => m.Name == field);
                    if (memberInfo != null)
                    {
                        break;
                    }
                }
            }
            return memberInfo;
        }
    }

}
