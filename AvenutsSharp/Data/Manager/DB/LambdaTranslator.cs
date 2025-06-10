using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Storage.Default.TableMember;
using AventusSharp.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;

namespace AventusSharp.Data.Manager.DB
{
    public interface ILambdaTranslatable
    {
        public bool ReplaceWhereByParameters { get; }
        public IDBStorage Storage { get; }
        public Dictionary<string, ParamsInfo> WhereParamsInfo { get; }
        public Dictionary<string, DatabaseBuilderInfo> InfoByPath { get; }
        public void LoadLinks(List<string> pathSplitted, List<Type> types, bool addLinksToMembers);
    }
    public class LambdaTranslator<T> : ExpressionVisitor
    {
        private static List<Type> _dateTypes = new List<Type>() { typeof(DateTime), typeof(Datetime), typeof(Date) };
        public List<string> pathes = new();
        public List<WhereGroupFctSqlEnum> sqlFcts = new();
        public bool alreadyAdded = false;
        public List<Type> types = new();
        private readonly ILambdaTranslatable databaseBuilder;
        private readonly List<DataMemberInfo> variableAccess = new();

        private List<IWhereRootGroup> queryGroups = new();
        private List<IWhereRootGroup> queryGroupsBase = new();
        private IWhereRootGroup? currentGroup;
        private bool onParameter = false;
        private WhereGroupFctEnum fctMethodCall = WhereGroupFctEnum.None;
        private List<Expression?> tree = new List<Expression?>();

        private bool nextGroupNegate = false;
        private Expression? parentExpression
        {
            get
            {
                return tree.Count > 1 ? tree[tree.Count - 2] : null;
            }
        }

        public LambdaTranslator(ILambdaTranslatable databaseQueryBuilder)
        {
            this.databaseBuilder = databaseQueryBuilder;
        }

        private void AddToParentGroup(IWhereGroup item)
        {
            if (currentGroup is WhereGroup whereGroup)
                whereGroup.Groups.Add(item);
        }

        private void AddToParentGroupCheckBool(IWhereGroup item)
        {
            if (currentGroup is WhereGroup whereGroup)
            {
                if (whereGroup.Groups.Count > 0 && whereGroup.Groups[0] is WhereGroupSingleBool singleBool)
                {
                    WhereGroupField field = new(singleBool.Alias, singleBool.TableMemberInfo);
                    whereGroup.Groups.RemoveAt(0);
                    whereGroup.Groups.Insert(0, field);
                }
                whereGroup.Groups.Add(item);
            }

        }

        public List<IWhereRootGroup> Translate(Expression expression)
        {
            queryGroups = new List<IWhereRootGroup>();
            queryGroupsBase = new List<IWhereRootGroup>();
            Visit(expression);

            return queryGroupsBase;
        }

        [return: NotNullIfNotNull("node")]
        public override Expression? Visit(Expression? node)
        {
            tree.Add(node);
            Expression? result = base.Visit(node);
            tree.RemoveAt(tree.Count - 1);
            return result;
        }

        protected override Expression VisitUnary(UnaryExpression u)
        {
            switch (u.NodeType)
            {
                case ExpressionType.Not:
                    if (currentGroup is WhereGroup whereGroup)
                        whereGroup.Groups.Add(new WhereGroupFct(WhereGroupFctEnum.Not));
                    else
                        nextGroupNegate = true;
                    Visit(u.Operand);
                    break;
                case ExpressionType.Convert:
                    Visit(u.Operand);
                    break;
                default:
                    throw new NotSupportedException(string.Format("The unary operator '{0}' is not supported", u.NodeType));
            }

            return u;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            onParameter = true;
            return base.VisitParameter(node);
        }

        protected override Expression VisitBinary(BinaryExpression b)
        {
            WhereGroup newGroup = new();
            AddToParentGroup(newGroup);
            currentGroup = newGroup;
            if (nextGroupNegate)
            {
                currentGroup.negate = true;
                nextGroupNegate = false;
            }
            if (queryGroups.Count == 0)
            {
                queryGroupsBase.Add(newGroup);
            }
            queryGroups.Add(newGroup);

            Visit(b.Left);

            switch (b.NodeType)
            {
                case ExpressionType.And:
                case ExpressionType.AndAlso:
                    AddToParentGroup(new WhereGroupFct(WhereGroupFctEnum.And));
                    break;
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                    AddToParentGroup(new WhereGroupFct(WhereGroupFctEnum.Or));
                    break;
                case ExpressionType.Equal:
                    AddToParentGroupCheckBool(new WhereGroupFct(WhereGroupFctEnum.Equal));
                    break;
                case ExpressionType.NotEqual:
                    AddToParentGroupCheckBool(new WhereGroupFct(WhereGroupFctEnum.NotEqual));
                    break;
                case ExpressionType.LessThan:
                    AddToParentGroup(new WhereGroupFct(WhereGroupFctEnum.LessThan));
                    break;
                case ExpressionType.LessThanOrEqual:
                    AddToParentGroup(new WhereGroupFct(WhereGroupFctEnum.LessThanOrEqual));
                    break;
                case ExpressionType.GreaterThan:
                    AddToParentGroup(new WhereGroupFct(WhereGroupFctEnum.GreaterThan));
                    break;
                case ExpressionType.GreaterThanOrEqual:
                    AddToParentGroup(new WhereGroupFct(WhereGroupFctEnum.GreaterThanOrEqual));
                    break;
                default:
                    throw new NotSupportedException(string.Format("The binary operator '{0}' is not supported", b.NodeType));
            }

            Visit(b.Right);

            queryGroups.RemoveAt(queryGroups.Count - 1);
            currentGroup = queryGroups.LastOrDefault();

            return b;
        }

        protected override Expression VisitConstant(ConstantExpression c)
        {
            IQueryable? q = c.Value as IQueryable;
            if (q == null && c.Value == null)
            {
                AddToParentGroup(new WhereGroupConstantNull());
            }
            if (q == null && c.Value != null)
            {
                TypeCode code = Type.GetTypeCode(c.Value.GetType());
                switch (code)
                {
                    case TypeCode.Boolean:
                        AddToParentGroup(new WhereGroupConstantBool((bool)c.Value));
                        break;

                    case TypeCode.String:
                        AddToParentGroup(new WhereGroupConstantString((string)c.Value));
                        break;

                    case TypeCode.DateTime:
                        AddToParentGroup(new WhereGroupConstantDateTime((DateTime)c.Value));
                        break;

                    case TypeCode.Object:
                        if (c.Value.GetType().GetInterfaces().Contains(typeof(IList)))
                        {
                            IList list = (IList)c.Value;
                            List<string?> tupple = new();
                            foreach (object o in list)
                            {
                                if (o.IsNumber())
                                {
                                    tupple.Add(o.ToString());
                                }
                                else
                                {
                                    tupple.Add("'" + o.ToString() + "'");
                                }
                            }
                            if (tupple.Count == 0)
                            {
                                tupple.Add("''");
                            }
                            string valueTxt = "(" + string.Join(",", tupple) + ")";
                            AddToParentGroup(new WhereGroupConstantOther(valueTxt));
                            break;
                        }
                        else if (c.Value is Datetime datetime)
                        {
                            AddToParentGroup(new WhereGroupConstantString(datetime.ToString()));
                            break;
                        }
                        else if (c.Value is Date date)
                        {
                            AddToParentGroup(new WhereGroupConstantString(date.ToString()));
                            break;
                        }
                        throw new NotSupportedException(string.Format("The constant for '{0}' is not supported", c.Value));

                    default:
                        string value = c.Value?.ToString() ?? "";
                        AddToParentGroup(new WhereGroupConstantOther(value));
                        break;
                }
            }

            return c;
        }

        protected override Expression VisitMember(MemberExpression m)
        {
            bool isBase = types.Count == 0;
            if (isBase)
            {
                alreadyAdded = false;
                onParameter = false;
            }
            if (_dateTypes.Contains(m.Type))
            {
                if (pathes.Count > 0)
                {
                    WhereGroupFctSqlEnum? fct = null;
                    if (pathes[0] == "Year") fct = WhereGroupFctSqlEnum.Year;
                    else if (pathes[0] == "Month") fct = WhereGroupFctSqlEnum.Month;
                    else if (pathes[0] == "Day") fct = WhereGroupFctSqlEnum.Day;
                    else if (pathes[0] == "Hour") fct = WhereGroupFctSqlEnum.Hour;
                    else if (pathes[0] == "Minute") fct = WhereGroupFctSqlEnum.Minute;
                    else if (pathes[0] == "Second") fct = WhereGroupFctSqlEnum.Second;

                    if (fct is WhereGroupFctSqlEnum realFct)
                    {
                        pathes.RemoveAt(0);
                        types.RemoveAt(0);
                        isBase = types.Count == 0;
                        sqlFcts.Insert(0, realFct);
                    }
                }
            }

            pathes.Insert(0, m.Member.Name);
            types.Insert(0, m.Type);

            if (m.Expression != null)
            {
                if (m.Expression is ConstantExpression cst)
                {
                    object? container = cst.Value;
                    DataMemberInfo? memberInfo;
                    if ((memberInfo = DataMemberInfo.Create(m.Member)) != null)
                    {
                        if (container != null)
                        {
                            object? value = memberInfo.GetValue(container);
                            if (isBase)
                            {
                                if (databaseBuilder.ReplaceWhereByParameters)
                                {
                                    variableAccess.Add(memberInfo);
                                    SetInfoToQueryBuilder();
                                }
                                else
                                {
                                    Visit(Expression.Constant(value));
                                }
                            }
                            else
                            {
                                variableAccess.Add(memberInfo);
                                return Expression.Constant(value);
                            }
                        }
                        else
                        {
                            throw new Exception("Can't parse the full object because of null value found.");
                        }
                    }
                    else
                    {
                        Visit(m.Expression);
                    }
                }
                else
                {
                    Expression result = Visit(m.Expression);
                    if(isBase && alreadyAdded)
                    {
                        isBase = false;
                    }
                    if (result is ConstantExpression cstExpression)
                    {
                        DataMemberInfo? memberInfo;
                        if ((memberInfo = DataMemberInfo.Create(m.Member)) != null)
                        {
                            if (cstExpression.Value != null)
                            {
                                object? value = memberInfo.GetValue(cstExpression.Value);
                                if (isBase)
                                {
                                    if (databaseBuilder.ReplaceWhereByParameters)
                                    {
                                        variableAccess.Add(memberInfo);
                                        SetInfoToQueryBuilder();
                                    }
                                    else
                                    {
                                        Visit(Expression.Constant(value));
                                    }
                                }
                                else
                                {
                                    variableAccess.Add(memberInfo);
                                    return Expression.Constant(value);
                                }
                            }
                            else
                            {
                                throw new Exception("Can't parse the full object because of null value found.");
                            }
                        }
                    }
                }
            }
            if (isBase)
            {
                if (onParameter)
                {
                    databaseBuilder.LoadLinks(pathes, types, false);
                    string fullPath = string.Join(".", pathes.SkipLast(1));

                    KeyValuePair<TableMemberInfoSql?, string> memberInfo = databaseBuilder.InfoByPath[fullPath].GetTableMemberInfoAndAlias(m.Member.Name);
                    if (memberInfo.Key != null)
                    {
                        foreach (WhereGroupFctSqlEnum sqlFct in sqlFcts)
                        {
                            AddToParentGroup(new WhereGroupFctSql(sqlFct));
                            WhereGroup newGroup = new();
                            AddToParentGroup(newGroup);
                            currentGroup = newGroup;
                            queryGroups.Add(newGroup);
                        }
                        WhereGroupField field = new(memberInfo.Value, memberInfo.Key);
                        if (memberInfo.Key.MemberType == typeof(bool))
                        {
                            WhereGroupSingleBool newGroup = new(memberInfo.Value, memberInfo.Key);
                            if (nextGroupNegate)
                            {
                                newGroup.negate = true;
                                nextGroupNegate = false;
                            }
                            if (queryGroups.Count == 0)
                            {
                                queryGroupsBase.Add(newGroup);
                            }
                            else
                            {
                                AddToParentGroup(newGroup);
                            }
                        }
                        else
                        {
                            AddToParentGroup(field);
                        }
                        foreach (WhereGroupFctSqlEnum sqlFct in sqlFcts)
                        {
                            queryGroups.RemoveAt(queryGroups.Count - 1);
                            currentGroup = queryGroups.LastOrDefault();
                        }
                    }
                }
                pathes.Clear();
                types.Clear();
                sqlFcts.Clear();
                variableAccess.Clear();
                alreadyAdded = true;
            }
            return m;

            throw new NotSupportedException(string.Format("The member '{0}' is not supported", m.Member.Name));
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {

            List<Type> listAllowed = new List<Type>()
            {
                typeof(List<int>),
                typeof(List<long>),
                typeof(List<short>),
                typeof(List<decimal>),
                typeof(List<double>),
                typeof(List<float>),
                typeof(List<string>),
                typeof(List<bool>),
                typeof(List<Datetime>),
                typeof(List<Date>),
                typeof(List<DateTime>),
            };
            string methodName = node.Method.Name;
            Type? onType = node.Object?.Type;
            WhereGroupFctEnum? fct = null;
            WhereGroupFctSqlEnum? fctSql = null;
            bool reverse = false;
            if (onType == typeof(string))
            {
                if (methodName == "StartsWith")
                {
                    fct = WhereGroupFctEnum.StartsWith;
                }
                else if (methodName == "Contains")
                {
                    fct = WhereGroupFctEnum.ContainsStr;
                }
                else if (methodName == "EndsWith")
                {
                    fct = WhereGroupFctEnum.EndsWith;
                }
                else if (methodName == "ToLower")
                {
                    fctSql = WhereGroupFctSqlEnum.ToLower;
                }
                else if (methodName == "ToUpper")
                {
                    fctSql = WhereGroupFctSqlEnum.ToUpper;
                }
            }
            else if (onType == typeof(DateTime) || onType == typeof(Datetime))
            {
                if (methodName == "DateOnly")
                {
                    fctSql = WhereGroupFctSqlEnum.Date;
                }
            }
            else if (onType != null && listAllowed.Contains(onType))
            {
                if (methodName == "Contains")
                {
                    fct = WhereGroupFctEnum.ListContains;
                    reverse = true;
                }
            }
            else if (onType != null && isListUsable(onType))
            {
                if (methodName == "Contains")
                {
                    fct = WhereGroupFctEnum.Equal;
                }
            }
            else if (node.Method.DeclaringType == typeof(Enumerable))
            {
                fct = WhereGroupFctEnum.Link;
            }

            else if (methodName == "GetElement" && node.Method.DeclaringType == typeof(LambdaExtractVariables))
            {

                ConstantExpression on = transformToConstant(node.Object);
                if (node.Arguments.Count == 1)
                {
                    ConstantExpression param = transformToConstant(node.Arguments[0]);
                    object? result = node.Method.Invoke(on.Value, new object?[] { param.Value });
                    if (parentExpression is BinaryExpression && param.Value != null && result != null)
                    {
                        SetQuickInfoToQueryBuilder(param.Value.ToString() ?? "", result.GetType());
                    }
                    return Expression.Constant(result, node.Method.ReturnType);
                }
            }
            else if (methodName == "GetValueOrDefault" && node.Method.DeclaringType != null && node.Method.DeclaringType.IsGenericType && node.Method.DeclaringType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                Visit(node.Object);
                return node;
            }

            if (fct == null && fctSql == null)
            {
                throw new Exception("Method " + methodName + " isn't allowed");
            }

            WhereGroup newGroup = new();
            AddToParentGroup(newGroup);
            currentGroup = newGroup;
            if (nextGroupNegate)
            {
                currentGroup.negate = true;
                nextGroupNegate = false;
            }
            if (queryGroups.Count == 0)
            {
                queryGroupsBase.Add(newGroup);
            }
            queryGroups.Add(newGroup);

            if (fct != null)
            {
                fctMethodCall = (WhereGroupFctEnum)fct;
                if (reverse)
                {
                    foreach (Expression argument in node.Arguments)
                    {
                        Visit(argument);
                    }
                    AddToParentGroup(new WhereGroupFct(fctMethodCall));
                    Visit(node.Object);
                }
                else
                {
                    Visit(node.Object);
                    AddToParentGroup(new WhereGroupFct(fctMethodCall));
                    foreach (Expression argument in node.Arguments)
                    {
                        Visit(argument);
                    }
                }
                fctMethodCall = WhereGroupFctEnum.None;
            }
            else if (fctSql != null)
            {
                AddToParentGroup(new WhereGroupFctSql((WhereGroupFctSqlEnum)fctSql));
                WhereGroup newGroup2 = new();
                AddToParentGroup(newGroup2);
                currentGroup = newGroup2;
                queryGroups.Add(newGroup2);
                Visit(node.Object);
                queryGroups.RemoveAt(queryGroups.Count - 1);
                currentGroup = queryGroups.LastOrDefault();
            }

            queryGroups.RemoveAt(queryGroups.Count - 1);
            currentGroup = queryGroups.LastOrDefault();
            return node;
        }

        private ConstantExpression transformToConstant(Expression? expression)
        {
            if (expression is ConstantExpression constantExpression)
            {
                return constantExpression;
            }

            else if (expression is MemberExpression memberExpression)
            {
                ConstantExpression cst = transformToConstant(memberExpression.Expression);
                DataMemberInfo? member = DataMemberInfo.Create(memberExpression.Member);
                if (member != null && member.Type != null)
                {
                    object? on = member.GetValue(cst.Value);
                    return Expression.Constant(on, member.Type);
                }
            }
            throw new Exception("Can't transform to constant expression");
        }
        private void SetInfoToQueryBuilder()
        {
            List<DataMemberInfo> members = variableAccess;
            List<TableMemberInfoSql> result = new();

            foreach (DataMemberInfo member in members.ToList())
            {
                if (member.Type != null && member.Type.Name.StartsWith("<>"))
                {
                    members.Remove(member);
                }
            }
            if (members.Count == 0)
            {
                throw new Exception("No member found");
            }

            Type from = members[0].Type ?? throw new Exception("The first members for query " + string.Join(".", members.Select(m => m.Name) + " has no type"));
            string paramName = string.Join(".", variableAccess.Select(v => v.Name));

            if (databaseBuilder.WhereParamsInfo.ContainsKey(paramName))
            {
                // already present
                return;
            }

            Type? lastType = members.Last().Type;
            if (lastType != null && lastType.GetInterfaces().Contains(typeof(IList)))
            {
                lastType = lastType.GetGenericArguments()[0];
            }
            DbType dbType = TableMemberInfoSql.GetDbType(lastType) ?? throw new Exception("Can't find a type to use inside sql for type " + from.Name);
            TableInfo? tableInfo = databaseBuilder.Storage.GetTableInfo(from);

            for (int i = 1; i < members.Count; i++)
            {
                if (tableInfo == null)
                {
                    throw new Exception("Can't find a table for the type " + from.Name);
                }
                TableMemberInfoSql memberInfo = tableInfo.Members.Find(m => m.Name == members[i].Name) ?? throw new Exception("Can't find a sql field for the field " + members[i].Name + " on the type " + from.Name);
                result.Add(memberInfo);
                if (memberInfo is ITableMemberInfoSqlLink memberInfoLink)
                {
                    tableInfo = memberInfoLink.TableLinked;
                }
                else
                {
                    tableInfo = null;
                }
            }

            databaseBuilder.WhereParamsInfo.Add(paramName, new ParamsInfo()
            {
                DbType = (DbType)dbType,
                MembersList = result,
                Name = paramName,
                TypeLvl0 = from,
                Value = null,
                FctMethodCall = fctMethodCall,
            });

            AddToParentGroup(new WhereGroupConstantParameter(paramName));

        }

        private void SetQuickInfoToQueryBuilder(string paramName, Type type)
        {
            if (databaseBuilder.WhereParamsInfo.ContainsKey(paramName))
            {
                // already present
                return;
            }

            DbType dbType = TableMemberInfoSql.GetDbType(type) ?? throw new Exception("Can't find a type to use inside sql for type " + type.Name);

            databaseBuilder.WhereParamsInfo.Add(paramName, new ParamsInfo()
            {
                DbType = (DbType)dbType,
                MembersList = new List<TableMemberInfoSql>(),
                Name = paramName,
                TypeLvl0 = type,
                Value = null,
                FctMethodCall = fctMethodCall,
            });

            AddToParentGroup(new WhereGroupConstantParameter(paramName));

        }


        private bool isListUsable(Type type)
        {
            if (type.IsGenericType && type.GetInterfaces().Contains(typeof(IList)))
            {
                Type typeInList = type.GetGenericArguments()[0];
                if (typeInList != null && typeInList.GetInterfaces().Contains(typeof(IStorable)))
                {
                    return true;
                }
            }
            return false;
        }
    }


    public class LambdaToPath : ExpressionVisitor
    {
        private readonly List<string> Pathes = new();

        private static LambdaToPath? instance;
        private static readonly Mutex Mutex = new();


        public static string Translate(Expression expression)
        {
            Mutex.WaitOne();
            instance ??= new LambdaToPath();
            instance.Pathes.Clear();
            instance.Visit(expression);
            string result = string.Join(".", instance.Pathes);
            Mutex.ReleaseMutex();
            return result;
        }

        protected override Expression VisitMember(MemberExpression m)
        {
            Pathes.Insert(0, m.Member.Name);
            return base.VisitMember(m);
        }
    }

    public class LambdaExtractVariables : ExpressionVisitor
    {
        private List<Type> types = new();

        private Dictionary<string, object?> parameters;
        private bool isOnParam = false;
        private int changedLvl = 0;
        private DataMemberInfo? memberInfo;
        private MethodInfo GetElementFct;

        public LambdaExtractVariables(Dictionary<string, object?> parameters)
        {
            this.parameters = parameters;
            GetElementFct = this.GetType().GetMethod("GetElement") ?? throw new Exception("Impossible");
        }
        public Expression Extract(Expression exp)
        {
            return Visit(exp);
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (memberInfo != null && !isOnParam)
            {
                changedLvl = 1;
                if (!parameters.ContainsKey(memberInfo.Name))
                {
                    object? o = memberInfo.GetValue(node.Value);
                    if (o != null)
                    {
                        object? newObjTemp = Activator.CreateInstance(o.GetType());
                        parameters.Add(memberInfo.Name, newObjTemp);
                    }
                    else
                    {
                        throw new Exception("You must set a value inside the field" + memberInfo.Name);
                    }
                }
                ConstantExpression constant = Expression.Constant(this, typeof(LambdaExtractVariables));
                return constant;
            }
            return base.VisitConstant(node);
        }
        protected override Expression VisitParameter(ParameterExpression node)
        {
            isOnParam = true;
            return base.VisitParameter(node);
        }
        public X GetElement<X>(string name)
        {
            if (this.parameters.ContainsKey(name) && parameters[name] is X casted)
            {
                return casted;
            }
            throw new Exception("Can't find variable " + name);
        }

        protected override Expression VisitMember(MemberExpression m)
        {
            bool isBase = types.Count == 0;
            if (isBase)
            {
                isOnParam = false;
                changedLvl = 0;
            }
            types.Insert(0, m.Type);

            if (m.Expression != null)
            {
                DataMemberInfo? localMember = DataMemberInfo.Create(m.Member);
                memberInfo = localMember;
                Expression result = Visit(m.Expression);
                if (localMember != null)
                {
                    if (changedLvl == 1 && localMember.Type != null)
                    {
                        // remove first level to remove local variable call
                        MethodInfo method = GetElementFct.MakeGenericMethod(localMember.Type);
                        result = Expression.Call(result, method, Expression.Constant(localMember.Name));
                        changedLvl++;
                        return result;
                    }
                    else if (changedLvl == 2)
                    {
                        m = Expression.Property(result, localMember.Name);
                    }
                }
                memberInfo = null;
            }
            if (isBase)
            {
                types.Clear();
            }
            return m;

        }

    }
}
