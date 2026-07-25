using AventusSharp.Data.Attributes;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Storage.Default.TableMember;
using AventusSharp.Routes;
using AventusSharp.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace AventusSharp.Data.Manager.DB;

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

    protected bool _noScope { get; set; }
    protected List<IScope>? ManualScopes { get; set; }
    protected List<IScope>? Scopes { get; set; }

    public List<GenericError> Errors { get; private set; } = new List<GenericError>();

    internal List<TableMemberInfoSql> Included { get; private set; } = new List<TableMemberInfoSql>();
    internal Dictionary<string, DatabaseSubBuilder> SubQueries { get; private set; } = new();


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

        if (path == "" && table.Scopes.Count > 0)
        {
            if (Scopes == null) Scopes = new();

            foreach (var scope in table.Scopes)
            {
                Scopes.Add(scope);
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

    protected void WhereGeneric(Expression<Func<T, bool>> expression)
    {
        AddWhereGeneric(expression, WhereGroupFctEnum.And);
    }

    protected void OrWhereGeneric(Expression<Func<T, bool>> expression)
    {
        AddWhereGeneric(expression, WhereGroupFctEnum.Or);
    }

    private void AddWhereGeneric(
        Expression<Func<T, bool>> expression,
        WhereGroupFctEnum link)
    {
        try
        {
            ReplaceWhereByParameters = false;
            LambdaTranslator<T> translator = new(this);
            TranslateResult translateResult = translator.Translate(expression);
            if (!translateResult.IsExternal)
            {
                if (Wheres == null)
                {
                    Wheres = translateResult.Wheres;
                }
                else
                {
                    WhereGroup group = new();
                    group.Groups.AddRange(Wheres);
                    group.Groups.Add(new WhereGroupFct(link));
                    group.Groups.AddRange(translateResult.Wheres);
                    Wheres = [group];
                }
            }
            else
            {
                throw new NotImplementedException("Missing implementation to where external subquery");
            }
        }
        catch (Exception exception)
        {
            Errors.Add(new DataError(DataErrorCode.UnknowError, exception));
        }
    }
    protected void WhereGenericWithParameters(Expression<Func<T, bool>> expression)
    {
        try
        {
            if (Wheres != null)
            {
                throw new Exception("Can't use twice the where action");
            }
            ReplaceWhereByParameters = true;
            LambdaTranslator<T> translator = new(this);
            TranslateResult translateResult = translator.Translate(expression);
            if (!translateResult.IsExternal)
            {
                Wheres = translateResult.Wheres;
            }
            else
            {
                throw new NotImplementedException("Missing implementation to where external subquery");
            }
        }
        catch (Exception exception)
        {
            Errors.Add(new DataError(DataErrorCode.UnknowError, exception));
        }
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
        }
    }
    protected void ResetPreparedParametersGeneric()
    {
        foreach (ParamsInfo parameter in WhereParamsInfo.Values)
        {
            parameter.ResetValue();
        }
    }

    protected virtual void OnVariableSet(ParamsInfo param, object fromObject)
    {

    }
    protected List<GenericError> GetRunErrors()
    {
        List<GenericError> result = Errors.ToList();
        if (!ReplaceWhereByParameters)
        {
            return result;
        }

        List<string> missingParameters = WhereParamsInfo.Values
            .Where(parameter => !parameter.IsSet)
            .Select(parameter => parameter.Name)
            .Distinct()
            .ToList();
        if (missingParameters.Count > 0)
        {
            result.Add(new DataError(
                DataErrorCode.ValidationError,
                "Missing values for prepared query parameters: " +
                string.Join(", ", missingParameters)));
        }
        return result;
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
        Storage.LoadAllTableFieldsQuery(
            InfoByPath[fullPath].TableInfo,
            InfoByPath[fullPath].Alias,
            InfoByPath[fullPath],
            new List<string>(),
            new List<Type>(),
            this);
        AllMembersByPath[fullPath] = false;
    }
    protected string FieldGeneric<X>(Expression<Func<T, X>> expression)
    {
        return FieldGeneric((LambdaExpression)expression);
    }
    protected string FieldGeneric(LambdaExpression lambdaExpression)
    {
        LambdaIncludeResult lambdaResult = LambdaInclude(
            lambdaExpression,
            fields: null,
            addToMembers: true
        );
        return string.Join(".", lambdaResult.Steps.Select(p => p.Name));
    }
    protected string IgnoreGeneric<X>(Expression<Func<T, X>> expression)
    {
        return IgnoreGeneric((LambdaExpression)expression);
    }
    protected string IgnoreGeneric(LambdaExpression lambdaExpression)
    {

        LambdaIncludeResult lambdaResult = LambdaInclude(
            lambdaExpression,
            fields: null,
            addToMembers: false
        );

        string fullPath = string.Join(".", lambdaResult.Steps.SkipLast(1).Select(p => p.Name));
        string lastName = lambdaResult.Steps.Last().Name;
        if (!lambdaResult.IsExternal)
        {
            if (AllMembersByPath[fullPath])
            {
                Storage.LoadAllTableFieldsQuery(InfoByPath[fullPath].TableInfo, InfoByPath[fullPath].Alias, InfoByPath[fullPath], new List<string>(), new List<Type>(), this);
                AllMembersByPath[fullPath] = false;
            }
            if (InfoByPath.ContainsKey(fullPath))
            {
                KeyValuePair<TableMemberInfoSql?, string> memberInfo = InfoByPath[fullPath].GetTableMemberInfoAndAlias(lastName);
                if (memberInfo.Key != null)
                {
                    if (InfoByPath[fullPath].Members.ContainsKey(memberInfo.Key))
                    {
                        InfoByPath[fullPath].Members.Remove(memberInfo.Key);
                    }
                }
            }
        }
        else
        {
            // TODO ignore in sub query
            throw new NotImplementedException("Missing implementation to ignore in subquery");
        }


        return fullPath != "" ? fullPath + "." + lastName : lastName;

    }
    protected void SortGeneric<X>(Expression<Func<T, X>> expression, Sort sort)
    {
        SortGeneric((LambdaExpression)expression, sort);

    }
    protected void SortGeneric(LambdaExpression lambdaExpression, Sort sort)
    {

        if (Sorting == null)
        {
            Sorting = new List<SortInfo>();
        }

        LambdaIncludeResult lambdaResult = LambdaInclude(
            lambdaExpression,
            fields: null,
            addToMembers: false
        );
        if (!lambdaResult.IsExternal)
        {
            string fullPath = string.Join(".", lambdaResult.Steps.SkipLast(1).Select(p => p.Name));
            string lastName = lambdaResult.Steps.Last().Name;
            KeyValuePair<TableMemberInfoSql?, string> memberInfo = InfoByPath[fullPath].GetTableMemberInfoAndAlias(lastName);
            if (memberInfo.Key != null)
            {
                Sorting.Add(new SortInfo(memberInfo.Key, memberInfo.Value, sort));
            }
            else
            {
                throw new Exception("This kind of sort should be impossible");
            }
        }
        else
        {
            // TODO : add sort after loading
            throw new NotImplementedException("Missing implementation to sort after loading");
        }
    }

    protected void GroupGeneric<X>(Expression<Func<T, X>> expression)
    {
        GroupGeneric((LambdaExpression)expression);
    }
    protected void GroupGeneric(LambdaExpression lambdaExpression)
    {

        if (Groups == null)
        {
            Groups = new();
        }

        LambdaIncludeResult lambdaResult = LambdaInclude(
           lambdaExpression,
           fields: null,
           addToMembers: false
       );

        if (!lambdaResult.IsExternal)
        {
            string fullPath = string.Join(".", lambdaResult.Steps.SkipLast(1).Select(p => p.Name));
            string lastName = lambdaResult.Steps.Last().Name;
            KeyValuePair<TableMemberInfoSql?, string> memberInfo = InfoByPath[fullPath].GetTableMemberInfoAndAlias(lastName);
            if (memberInfo.Key != null)
            {
                Groups.Add(new GroupInfo(memberInfo.Key, memberInfo.Value));
            }
            else
            {
                throw new Exception("This kind of group should be impossible");
            }
        }
        else
        {
            // TODO : add sort after loading
            throw new NotImplementedException("Missing implementation to group after loading");
        }

    }

    protected void IncludeGeneric<Y>(Expression<Func<T, Y?>> expression, List<LambdaExpression>? fields, List<Scope<Y>>? scopes) where Y : IStorable
    {
        IncludeGeneric(expression, fields, scopes?.ConvertList<IScope>());
    }
    protected void IncludeGeneric(LambdaExpression lambdaExpression, List<LambdaExpression>? fields, List<IScope>? scopes)
    {
        LambdaIncludeResult lambdaResult = LambdaInclude(
           lambdaExpression,
           fields: fields,
           addToMembers: true,
           scopes
        );

        string relationPath = string.Join(".", lambdaResult.Steps.Select(step => step.Name));
        if (!lambdaResult.IsExternal && InfoByPath.TryGetValue(relationPath, out DatabaseBuilderInfo? relationInfo))
        {
            List<IScope> scopesToApply = scopes ?? relationInfo.TableInfo.Scopes.ToList();
            if (scopesToApply.Count > 0)
            {
                LambdaExpression relationExpression = LambdaTranslator.MergePart<T>(lambdaResult.Steps);
                foreach (IScope scope in scopesToApply)
                {
                    LambdaExpression? scopeExpression = scope.Where(RouterMiddleware.ContextScope);
                    if (scopeExpression == null)
                    {
                        continue;
                    }

                    LambdaExpression merged = LambdaTranslator.LambdaMerge(
                        relationExpression,
                        scopeExpression);
                    AddWhereGeneric(
                        Expression.Lambda<Func<T, bool>>(
                            merged.Body,
                            (ParameterExpression)merged.Parameters[0]),
                        WhereGroupFctEnum.And);
                }
            }
        }

        string fullPath = string.Join(".", lambdaResult.Steps.SkipLast(1).Select(p => p.Name));
        string lastName = lambdaResult.Steps.Last().Name;

        if (InfoByPath.ContainsKey(fullPath))
        {
            TableMemberInfoSql? memberInfo = InfoByPath[fullPath].GetTableMemberInfo(lastName);
            if (memberInfo != null)
            {
                Included.Add(memberInfo);
            }
        }
    }

    public LambdaIncludeResult LambdaInclude(LambdaExpression lambdaExpression, List<LambdaExpression>? fields, bool addToMembers, List<IScope>? scopes = null)
    {
        List<LambdaStep> lambdaParts = LambdaTranslator.ExtractPart(lambdaExpression);
        return LambdaInclude(lambdaParts, fields, addToMembers, scopes);
    }
    public LambdaIncludeResult LambdaInclude(List<LambdaStep> lambdaParts, List<LambdaExpression>? fields, bool addToMembers, List<IScope>? scopes = null)
    {
        bool isExternal = false;
        DatabaseBuilderInfo parentInfo = InfoByPath[""];
        string fullPath = "";
        for (int i = 0; i < lambdaParts.Count; i++)
        {
            LambdaStep lambdaPart = lambdaParts[i];
            Type? listType = TableMemberInfoSql.IsListTypeUsable(lambdaPart.Type);
            if (lambdaPart.Type.GetInterfaces().Contains(typeof(IStorable)) || listType != null)
            {
                string parentPath = fullPath;
                if (i > 0)
                {
                    fullPath += ".";
                }
                fullPath += lambdaPart.Name;

                TableMemberInfoSql? memberInfoLink = parentInfo.GetTableMemberInfo(lambdaPart.Name);
                if (memberInfoLink != null)
                {
                    if (DM is IDatabaseDM databaseDM && databaseDM.IsSameStorage(memberInfoLink.DM))
                    {
                        if (memberInfoLink is ITableMemberInfoSqlLinkMultiple multiple && multiple.TableLinked != null)
                        {
                            if (!parentInfo.joinsNM.ContainsKey(multiple))
                            {
                                parentInfo.joinsNM.Add(multiple, CreateAlias(parentInfo.TableInfo, multiple.TableLinked));
                            }
                        }
                        else if (!InfoByPath.ContainsKey(fullPath))
                        {
                            KeyValuePair<TableMemberInfoSql?, string> memberInfoWithAlias = parentInfo.GetTableMemberInfoAndAlias(lambdaPart.Name);
                            if (memberInfoWithAlias.Key != null)
                            {
                                DatabaseBuilderInfo currentTable = LoadTable(GetTableInfo(lambdaPart.Type), fullPath);
                                parentInfo.joins[memberInfoWithAlias.Key] = currentTable;
                                if (addToMembers)
                                {
                                    parentInfo.Members.Add(memberInfoWithAlias.Key, new DatabaseBuilderInfoMember(memberInfoWithAlias.Key, memberInfoWithAlias.Value, Storage));
                                    AllMembersByPath[fullPath] =
                                        fields == null && i == lambdaParts.Count - 1;
                                }
                            }
                        }
                        if (addToMembers && fields != null && i == lambdaParts.Count - 1)
                        {
                            LambdaExpression baseExp = LambdaTranslator.MergePart<T>(lambdaParts);
                            foreach (var field in fields)
                            {
                                FieldGeneric(LambdaTranslator.LambdaMerge(baseExp, field));
                            }
                        }
                        parentInfo = InfoByPath[fullPath];
                    }
                    else
                    {
                        isExternal = true;
                        List<string> namesTemp = new List<string>();
                        for (; i < lambdaParts.Count; i++)
                        {
                            namesTemp.Add(lambdaParts[i].Name);
                        }
                        if (SubQueries.ContainsKey(fullPath))
                        {
                            SubQueries[fullPath].ExtendExternalStorage(namesTemp, fields, scopes);
                        }
                        else
                        {
                            DatabaseSubBuilder subQuery = DatabaseSubBuilder.Make(parentInfo.TableInfo.Type, listType ?? lambdaPart.Type);
                            VoidWithError prepareInfo = subQuery.PrepareExternalStorage(namesTemp, fields, scopes);
                            if (prepareInfo.Success)
                            {
                                SubQueries.Add(fullPath, subQuery);
                            }
                            else
                            {
                                throw prepareInfo.Errors[0].GetException();
                            }
                        }
                    }
                    continue;
                }

                TableReverseMemberInfo? reverseInfo = parentInfo.GetReverseTableMemberInfo(lambdaPart.Name);
                if (reverseInfo != null)
                {
                    isExternal = true;
                    List<string> namesTemp = new List<string>();
                    for (; i < lambdaParts.Count; i++)
                    {
                        namesTemp.Add(lambdaParts[i].Name);
                    }

                    if (SubQueries.ContainsKey(fullPath))
                    {
                        SubQueries[fullPath].ExtendReverseLink(namesTemp, fields, scopes);
                    }
                    else
                    {
                        DatabaseSubBuilder subQuery = DatabaseSubBuilder.Make(parentInfo.TableInfo.Type, listType ?? lambdaPart.Type);
                        VoidWithError prepareInfo = subQuery.PrepareReverseLink(namesTemp, fields, scopes);
                        if (prepareInfo.Success)
                        {
                            SubQueries.Add(fullPath, subQuery);
                        }
                        else
                        {
                            throw prepareInfo.Errors[0].GetException();
                        }
                    }
                    continue;
                }

                throw new Exception("How can a IStorable not be in the table. Send your Lambda to an admin");
            }

            // add to field
            KeyValuePair<TableMemberInfoSql?, string> memberInfo = parentInfo.GetTableMemberInfoAndAlias(lambdaPart.Name);
            if (memberInfo.Key != null)
            {
                if (addToMembers)
                {
                    AllMembersByPath[fullPath] = false;
                    parentInfo.Members[memberInfo.Key] = new DatabaseBuilderInfoMember(memberInfo.Key, memberInfo.Value, Storage, lambdaPart.Transformators);
                }
                continue;
            }

            throw new Exception("Can't understand what you mean for the query");

        }

        LambdaIncludeResult result = new LambdaIncludeResult()
        {
            Steps = lambdaParts,
            IsExternal = isExternal
        };

        return result;
    }

    protected void LimitGeneric(int? limit)
    {
        if (limit < 0)
        {
            Errors.Add(new DataError(
                DataErrorCode.ValidationError,
                "Limit must be greater than or equal to zero"));
            return;
        }
        LimitSize = limit;
    }

    protected void OffsetGeneric(int? offset)
    {
        if (offset < 0)
        {
            Errors.Add(new DataError(
                DataErrorCode.ValidationError,
                "Offset must be greater than or equal to zero"));
            return;
        }
        OffsetSize = offset;
    }

    public bool MustLoadMembers(List<string> path)
    {
        string mergedPath = string.Join(".", path);
        return AllMembersByPath.ContainsKey(mergedPath) && AllMembersByPath[mergedPath] == true;
    }

    protected void WithoutScopeGeneric()
    {
        _noScope = true;
    }

    protected void WithScopeGeneric<X>() where X : IScope, new()
    {
        WithScopeGeneric(TypeTools.CreateNewObj<X>());
    }
    protected void WithScopeGeneric(IScope scope)
    {
        if (ManualScopes == null) ManualScopes = new();
        ManualScopes.Add(scope);
    }
    protected void MergeScopeAndWhere()
    {
        if (_noScope)
        {
            return;
        }
        List<IScope>? scopes = ManualScopes ?? Scopes;
        if (scopes == null) return;
        scopes = scopes.ToList();

        LambdaTranslator<T> translator = new(this);
        WhereGroup whereGroup = new();
        bool hasScope = false;
        foreach (var scope in scopes)
        {
            var scopeFct = scope.Where(RouterMiddleware.ContextScope);
            if (scopeFct != null)
            {
                hasScope = true;
                if (whereGroup.Groups.Count > 0)
                    whereGroup.Groups.Add(new WhereGroupFct(WhereGroupFctEnum.And));

                var translateResult = translator.Translate(scopeFct);
                if (!translateResult.IsExternal)
                {
                    whereGroup.Groups.AddRange(translateResult.Wheres);
                }
                else
                {
                    throw new NotImplementedException("Missing implementation to scope with external subquery");
                }
            }
        }

        if (!hasScope) return;

        if (Wheres == null)
        {
            Wheres = new List<IWhereRootGroup>() { whereGroup };
            return;
        }




        var group = new WhereGroup();
        group.Groups.AddRange(whereGroup);
        group.Groups.Add(new WhereGroupFct(WhereGroupFctEnum.And));
        group.Groups.AddRange(Wheres);
        Wheres = new List<IWhereRootGroup>() { group };

    }
}


public class LambdaIncludeResult
{
    public required List<LambdaStep> Steps { get; set; }
    public required bool IsExternal { get; set; }
}
