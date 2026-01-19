using AventusSharp.Data.Attributes;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Storage.Default.TableMember;
using AventusSharp.Tools;
using Microsoft.AspNetCore.Http.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace AventusSharp.Data.Manager.DB
{
    public enum Sort
    {
        ASC,
        DESC
    }
    public class DatabaseGenericBuilder<T> : ILambdaTranslatable where T : IStorable
    {
        public Dictionary<string, bool> AllMembersByPath = new Dictionary<string, bool>() { { "", true } };
        public IDBStorage Storage { get; private set; }

        public IGenericDM DM { get; private set; }

        public Dictionary<string, DatabaseBuilderInfo> InfoByPath { get; set; } = new Dictionary<string, DatabaseBuilderInfo>();

        public List<string> Aliases { get; set; } = new();
        public Dictionary<Type, TableInfo> LoadedTableInfo { get; set; } = new Dictionary<Type, TableInfo>();
        public List<IWhereRootGroup>? Wheres { get; set; } = null;
        public bool ReplaceWhereByParameters { get; set; } = false;

        public Dictionary<string, ParamsInfo> WhereParamsInfo { get; set; } = new Dictionary<string, ParamsInfo>(); // type is the type of the variable to use

        public int? LimitSize { get; private set; } = null;
        public int? OffsetSize { get; private set; } = null;
        public List<SortInfo>? Sorting { get; private set; } = null;
        public List<GroupInfo>? Groups { get; private set; } = null;

        public WhereGroup? Scopes { get; set; }

        public DatabaseGenericBuilder(IDBStorage storage, IGenericDM DM, Type? baseType = null) : base()
        {
            Storage = storage;
            this.DM = DM;
            // load basic info for the main class
            if (baseType == null)
            {
                baseType = typeof(T);
            }
            TableInfo tableInfo = GetTableInfo(baseType);
            LoadTable(tableInfo, "");
        }

        protected TableInfo GetTableInfo(Type u)
        {
            if (LoadedTableInfo.ContainsKey(u))
            {
                return LoadedTableInfo[u];
            }

            TableInfo? tableInfo = Storage.GetTableInfo(u);
            if (tableInfo != null)
            {
                LoadedTableInfo.Add(u, tableInfo);
                return tableInfo;
            }
            throw new Exception();
        }
        public string CreateAlias(TableInfo tableInfo)
        {
            return CreateAlias(tableInfo.Type);
        }
        public string CreateAlias(Type type)
        {
            string alias = string.Concat(type.Name.Where(c => char.IsUpper(c)));
            if (alias.Length == 0)
            {
                alias = type.Name[..2];
            }
            int i = 1;
            string baseAlias = alias;
            while (Aliases.Contains(alias))
            {
                alias = baseAlias + i;
                i++;
            }
            Aliases.Add(alias);
            return alias;
        }
        public string CreateAlias(TableInfo tableInfo1, TableInfo tableInfo2)
        {
            return CreateAlias(tableInfo1.Type, tableInfo2.Type);
        }
        public string CreateAlias(Type type1, Type type2)
        {
            string alias1 = string.Concat(type1.Name.Where(c => char.IsUpper(c)));
            if (alias1.Length == 0)
            {
                alias1 = type1.Name[..2];
            }
            string alias2 = string.Concat(type2.Name.Where(c => char.IsUpper(c)));
            if (alias2.Length == 0)
            {
                alias2 = type2.Name[..2];
            }
            int i = 1;
            string baseAlias = alias1 + alias2;
            string alias = alias1 + alias2;
            while (Aliases.Contains(alias))
            {
                alias = baseAlias + i;
                i++;
            }
            Aliases.Add(alias);
            return alias;
        }

        protected DatabaseBuilderInfo LoadTable(TableInfo table, string path)
        {
            if (InfoByPath.ContainsKey(path))
            {
                return InfoByPath[path];
            }
            string alias = CreateAlias(table);

            DatabaseBuilderInfo info = new(alias, table);
            InfoByPath[path] = info;

            if (table.Scopes.Count > 0)
            {
                if (Scopes == null) Scopes = new();

                LambdaTranslator<T> translator = new(this);
                foreach (var scope in table.Scopes)
                {
                    if (Scopes.Groups.Count > 0)
                        Scopes.Groups.Add(new WhereGroupFct(WhereGroupFctEnum.And));
                    Scopes.Groups.AddRange(translator.Translate(scope));
                }
            }

            LoadParent(table, info);
            LoadChildren(table, info, info.Children);
            return info;
        }
        protected void LoadParent(TableInfo table, DatabaseBuilderInfo info)
        {
            if (table.Parent != null)
            {
                TableInfo parent = table.Parent;
                string alias = CreateAlias(parent);
                info.Parents[parent] = alias;
                LoadParent(parent, info);
            }
        }
        protected void LoadChildren(TableInfo table, DatabaseBuilderInfo info, List<DatabaseBuilderInfoChild> list)
        {
            foreach (TableInfo child in table.Children)
            {
                DatabaseBuilderInfoChild childInfo = new(CreateAlias(child), child);
                list.Add(childInfo);
                LoadChildren(child, info, childInfo.Children);
            }
        }

        public void LoadLinks(List<string> pathSplitted, List<Type> types, bool addLinksToMembers)
        {
            string currentPath = "";
            for (int i = 0; i < pathSplitted.Count; i++)
            {
                DatabaseBuilderInfo parentInfo = InfoByPath[currentPath];
                if (types[i].GetInterfaces().Contains(typeof(IStorable)))
                {
                    if (i > 0)
                    {
                        currentPath += ".";
                    }
                    currentPath += pathSplitted[i];


                    if (!InfoByPath.ContainsKey(currentPath))
                    {
                        KeyValuePair<TableMemberInfoSql?, string> memberInfo = parentInfo.GetTableMemberInfoAndAlias(pathSplitted[i]);
                        if (memberInfo.Key != null)
                        {

                            DatabaseBuilderInfo currentTable = LoadTable(GetTableInfo(types[i]), currentPath);
                            parentInfo.joins[memberInfo.Key] = currentTable;
                            if (addLinksToMembers)
                            {
                                parentInfo.Members.Add(memberInfo.Key, new DatabaseBuilderInfoMember(memberInfo.Key, memberInfo.Value, Storage));
                                if (i == pathSplitted.Count - 1)
                                {
                                    AllMembersByPath[currentPath] = true;
                                }
                            }
                        }
                        else
                        {
                            throw new Exception("Can't query " + pathSplitted[i] + " on " + parentInfo.TableInfo.Type.Name);
                        }
                    }
                    else if (addLinksToMembers)
                    {
                        KeyValuePair<TableMemberInfoSql?, string> memberInfo = parentInfo.GetTableMemberInfoAndAlias(pathSplitted[i]);
                        if (memberInfo.Key != null && !parentInfo.Members.ContainsKey(memberInfo.Key))
                        {
                            parentInfo.Members.Add(memberInfo.Key, new DatabaseBuilderInfoMember(memberInfo.Key, memberInfo.Value, Storage));
                            if (i == pathSplitted.Count - 1)
                            {
                                AllMembersByPath[currentPath] = true;
                            }
                        }
                    }
                }
                else
                {
                    Type? listType = TableMemberInfoSql.IsListTypeUsable(types[i]);
                    if (listType != null)
                    {
                        if (i > 0)
                        {
                            currentPath += ".";
                        }
                        currentPath += pathSplitted[i];

                        if (!InfoByPath.ContainsKey(currentPath))
                        {
                            TableMemberInfoSql? memberInfo = parentInfo.GetTableMemberInfo(pathSplitted[i]);
                            if (memberInfo is ITableMemberInfoSqlLinkMultiple multiple && multiple.TableLinked != null)
                            {
                                if (!parentInfo.joinsNM.ContainsKey(multiple))
                                {
                                    parentInfo.joinsNM.Add(multiple, CreateAlias(parentInfo.TableInfo, multiple.TableLinked));
                                }
                            }
                            else
                            {
                                throw new Exception("Can't query " + pathSplitted[i] + " on " + parentInfo.TableInfo.Type.Name);
                            }
                        }
                    }
                }
            }
        }

        protected void WhereGeneric(Expression<Func<T, bool>> expression)
        {
            if (Wheres != null)
            {
                throw new Exception("Can't use twice the where action");
            }
            ReplaceWhereByParameters = false;
            LambdaTranslator<T> translator = new(this);
            Wheres = translator.Translate(expression);
        }
        protected void WhereGenericWithParameters(Expression<Func<T, bool>> expression)
        {
            if (Wheres != null)
            {
                throw new Exception("Can't use twice the where action");
            }
            ReplaceWhereByParameters = true;
            LambdaTranslator<T> translator = new(this);
            Wheres = translator.Translate(expression);
        }
        protected void PrepareGeneric(params object[] objects)
        {
            List<ParamsInfo> toSet = WhereParamsInfo.Values.ToList();
            foreach (object obj in objects)
            {
                foreach (ParamsInfo info in toSet)
                {
                    if (obj.GetType() == info.TypeLvl0)
                    {
                        info.SetValue(obj);
                        OnVariableSet(info, obj);
                        toSet.Remove(info);
                        // set by order first
                        break;
                    }
                }
            }
            if (toSet.Count > 0)
            {
                List<ParamsInfo> toSetClone = toSet.ToList();
                foreach (ParamsInfo info in toSetClone)
                {
                    foreach (object obj in objects)
                    {
                        if (obj.GetType() == info.TypeLvl0)
                        {
                            info.SetValue(obj);
                            OnVariableSet(info, obj);
                            toSet.Remove(info);
                            // set if same variable used by multiple params
                            break;
                        }
                    }
                }
                if (toSet.Count > 0)
                {
                    throw new Exception("Can't found a value to set for variables : " + string.Join(", ", toSet.Select(t => t.Name)));
                }
            }
        }

        protected virtual void OnVariableSet(ParamsInfo param, object fromObject)
        {

        }
        protected void SetVariableGeneric(string name, object value)
        {
            foreach (KeyValuePair<string, ParamsInfo> paramInfo in WhereParamsInfo)
            {
                if (paramInfo.Value.IsNameSimilar(name))
                {
                    paramInfo.Value.SetValue(value);
                    OnVariableSet(paramInfo.Value, value);
                }
            }
        }
        protected void FieldsGeneric()
        {
            string fullPath = "";
            if (AllMembersByPath[fullPath])
            {
                Storage.LoadAllTableFieldsQuery(InfoByPath[fullPath].TableInfo, InfoByPath[fullPath].Alias, InfoByPath[fullPath], new List<string>(), new List<Type>(), this);
                AllMembersByPath[fullPath] = false;
            }
        }
        protected string FieldGeneric<X>(Expression<Func<T, X>> expression)
        {
            // TODO add WhereGroupFctSqlEnum management to get for example lowercase
            // the strucutre must be Lambda => Convert? => (member x times)
            if (expression is LambdaExpression lambdaExpression)
            {
                Expression? exp = lambdaExpression.Body;
                if (lambdaExpression.Body is UnaryExpression convertExpression)
                {
                    exp = convertExpression.Operand;
                }

                List<WhereGroupFctSqlEnum>? transformators = null;

                while (exp is MethodCallExpression callExpression)
                {
                    WhereGroupFctSqlEnum? _fct = LambdaTranslator.GetFctSql(callExpression);
                    if (_fct is WhereGroupFctSqlEnum fct)
                    {
                        if (transformators == null) transformators = new();
                        transformators.Add(fct);
                    }
                    else
                    {
                        throw new NotImplementedException("Can't find a method for " + callExpression.Method.Name + " on type " + LambdaTranslator.ExtractType(callExpression));
                    }
                    if (callExpression.Object != null)
                    {
                        exp = callExpression.Object;
                    }
                    else if (callExpression.Arguments.Count > 0)
                    {
                        exp = callExpression.Arguments[0];
                    }
                    else
                    {
                        exp = null;
                    }
                }



                if (exp is MemberExpression memberExpression)
                {
                    List<Type> types = new();
                    List<string> names = new();

                    types.Insert(0, memberExpression.Type);
                    names.Insert(0, memberExpression.Member.Name);

                    Expression? temp = memberExpression.Expression;
                    while (temp is MemberExpression temp2)
                    {
                        types.Insert(0, temp2.Type);
                        names.Insert(0, temp2.Member.Name);
                        temp = temp2.Expression;
                    }

                    LoadLinks(names, types, false);

                    string fullPath = string.Join(".", names.SkipLast(1));
                    KeyValuePair<TableMemberInfoSql?, string> memberInfo = InfoByPath[fullPath].GetTableMemberInfoAndAlias(memberExpression.Member.Name);
                    if (memberInfo.Key != null)
                    {
                        AllMembersByPath[fullPath] = false;

                        if (memberInfo.Key is ITableMemberInfoSqlLink)
                        {
                            AllMembersByPath[string.Join(".", names)] = true;
                        }
                        InfoByPath[fullPath].Members[memberInfo.Key] = new DatabaseBuilderInfoMember(memberInfo.Key, memberInfo.Value, Storage, transformators);
                    }
                    else
                    {
                        // if we can't find the members info maybe it's a reverse link
                        TableReverseMemberInfo? reversMemberInfo = InfoByPath[fullPath].GetReverseTableMemberInfo(memberExpression.Member.Name);
                        if (reversMemberInfo != null && !InfoByPath[fullPath].ReverseLinks.Contains(reversMemberInfo))
                        {
                            InfoByPath[fullPath].ReverseLinks.Add(reversMemberInfo);
                        }
                    }
                    return fullPath != "" ? fullPath + "." + memberExpression.Member.Name : memberExpression.Member.Name;
                }
            }

            throw new Exception();
        }
        protected string IgnoreGeneric<X>(Expression<Func<T, X>> expression)
        {
            // TODO add WhereGroupFctSqlEnum management to get for example lowercase
            // the strucutre must be Lambda => Convert? => (member x times)
            if (expression is LambdaExpression lambdaExpression)
            {
                Expression exp = lambdaExpression.Body;
                if (lambdaExpression.Body is UnaryExpression convertExpression)
                {
                    exp = convertExpression.Operand;
                }

                if (exp is MemberExpression memberExpression)
                {
                    List<Type> types = new();
                    List<string> names = new();

                    types.Insert(0, memberExpression.Type);
                    names.Insert(0, memberExpression.Member.Name);

                    Expression? temp = memberExpression.Expression;
                    while (temp is MemberExpression temp2)
                    {
                        types.Insert(0, temp2.Type);
                        names.Insert(0, temp2.Member.Name);
                        temp = temp2.Expression;
                    }


                    LoadLinks(names, types, false);

                    string fullPath = string.Join(".", names.SkipLast(1));
                    if (AllMembersByPath[fullPath])
                    {
                        Storage.LoadAllTableFieldsQuery(InfoByPath[fullPath].TableInfo, InfoByPath[fullPath].Alias, InfoByPath[fullPath], new List<string>(), new List<Type>(), this);
                        AllMembersByPath[fullPath] = false;
                    }
                    KeyValuePair<TableMemberInfoSql?, string> memberInfo = InfoByPath[fullPath].GetTableMemberInfoAndAlias(memberExpression.Member.Name);
                    if (memberInfo.Key != null)
                    {
                        if (InfoByPath[fullPath].Members.ContainsKey(memberInfo.Key))
                        {
                            InfoByPath[fullPath].Members.Remove(memberInfo.Key);
                        }
                    }
                    else
                    {
                        // // if we can't find the members info maybe it's a reverse link
                        // TableReverseMemberInfo? reversMemberInfo = InfoByPath[fullPath].GetReverseTableMemberInfo(memberExpression.Member.Name);
                        // if (reversMemberInfo != null && !InfoByPath[fullPath].ReverseLinks.Contains(reversMemberInfo))
                        // {
                        //     InfoByPath[fullPath].ReverseLinks.Add(reversMemberInfo);
                        // }
                    }
                    return fullPath != "" ? fullPath + "." + memberExpression.Member.Name : memberExpression.Member.Name;
                }
            }

            throw new Exception();
        }
        protected void SortGeneric<X>(Expression<Func<T, X>> expression, Sort sort)
        {
            // TODO add WhereGroupFctSqlEnum management to get for example lowercase
            // the strucutre must be Lambda => Convert? => (member x times)
            if (expression is LambdaExpression lambdaExpression)
            {
                if (Sorting == null)
                {
                    Sorting = new List<SortInfo>();
                }
                Expression exp = lambdaExpression.Body;
                if (lambdaExpression.Body is UnaryExpression convertExpression)
                {
                    exp = convertExpression.Operand;
                }

                if (exp is MemberExpression memberExpression)
                {
                    List<Type> types = new();
                    List<string> names = new();

                    types.Insert(0, memberExpression.Type);
                    names.Insert(0, memberExpression.Member.Name);

                    Expression? temp = memberExpression.Expression;
                    while (temp is MemberExpression temp2)
                    {
                        types.Insert(0, temp2.Type);
                        names.Insert(0, temp2.Member.Name);
                        temp = temp2.Expression;
                    }

                    LoadLinks(names, types, false);

                    string fullPath = string.Join(".", names.SkipLast(1));
                    KeyValuePair<TableMemberInfoSql?, string> memberInfo = InfoByPath[fullPath].GetTableMemberInfoAndAlias(memberExpression.Member.Name);
                    if (memberInfo.Key != null)
                    {
                        Sorting.Add(new SortInfo(memberInfo.Key, memberInfo.Value, sort));
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                    return;
                }
            }

            throw new Exception();
        }


        protected void GroupGeneric<X>(Expression<Func<T, X>> expression)
        {
            // TODO add WhereGroupFctSqlEnum management to get for example lowercase
            // the strucutre must be Lambda => Convert? => (member x times)
            if (expression is LambdaExpression lambdaExpression)
            {
                if (Groups == null)
                {
                    Groups = new();
                }
                Expression exp = lambdaExpression.Body;
                if (lambdaExpression.Body is UnaryExpression convertExpression)
                {
                    exp = convertExpression.Operand;
                }

                if (exp is MemberExpression memberExpression)
                {
                    List<Type> types = new();
                    List<string> names = new();

                    types.Insert(0, memberExpression.Type);
                    names.Insert(0, memberExpression.Member.Name);

                    Expression? temp = memberExpression.Expression;
                    while (temp is MemberExpression temp2)
                    {
                        types.Insert(0, temp2.Type);
                        names.Insert(0, temp2.Member.Name);
                        temp = temp2.Expression;
                    }

                    LoadLinks(names, types, false);

                    string fullPath = string.Join(".", names.SkipLast(1));
                    KeyValuePair<TableMemberInfoSql?, string> memberInfo = InfoByPath[fullPath].GetTableMemberInfoAndAlias(memberExpression.Member.Name);
                    if (memberInfo.Key != null)
                    {
                        Groups.Add(new GroupInfo(memberInfo.Key, memberInfo.Value));
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                    return;
                }
            }

            throw new Exception();
        }


        protected void IncludeGeneric(Expression<Func<T, IStorable>> expression)
        {
            // the strucutre must be Lambda => Convert? => (member x times)
            if (expression is LambdaExpression lambdaExpression)
            {
                Expression exp = lambdaExpression.Body;
                if (lambdaExpression.Body is UnaryExpression convertExpression)
                {
                    exp = convertExpression.Operand;
                }

                if (exp is MemberExpression memberExpression)
                {
                    List<Type> types = new();
                    List<string> names = new();

                    types.Insert(0, memberExpression.Type);
                    names.Insert(0, memberExpression.Member.Name);

                    Expression? temp = memberExpression.Expression;
                    while (temp is MemberExpression temp2)
                    {
                        types.Insert(0, temp2.Type);
                        names.Insert(0, temp2.Member.Name);
                        temp = temp2.Expression;
                    }

                    LoadLinks(names, types, false);
                    return;
                }
            }
            throw new Exception();
        }

        protected void LimitGeneric(int? limit)
        {
            LimitSize = limit;
        }

        protected void OffsetGeneric(int? offset)
        {
            OffsetSize = offset;
        }

        public bool MustLoadMembers(List<string> path)
        {
            string mergedPath = string.Join(".", path);
            return AllMembersByPath.ContainsKey(mergedPath) && AllMembersByPath[mergedPath] == true;
        }

        protected void MergeScopeAndWhere()
        {
            if (Scopes == null) return;

            if (Wheres == null)
            {
                Wheres = new List<IWhereRootGroup>() { Scopes };
                return;
            }

            var group = new WhereGroup();
            group.Groups.AddRange(Scopes);
            group.Groups.Add(new WhereGroupFct(WhereGroupFctEnum.And));
            group.Groups.AddRange(Wheres);
            Wheres = new List<IWhereRootGroup>() { group };

        }
    }
}
