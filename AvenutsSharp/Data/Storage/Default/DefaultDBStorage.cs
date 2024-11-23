using AventusSharp.Data.Manager;
using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Manager.DB.Builders;
using AventusSharp.Data.Storage.Default.TableMember;
using AventusSharp.Data.Storage.Mysql.Queries;
using AventusSharp.Routes.Attributes;
using AventusSharp.Tools;
using MySql.Data.MySqlClient;
using MySqlX.XDevAPI.Common;
using Org.BouncyCastle.Asn1.Mozilla;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Transactions;

namespace AventusSharp.Data.Storage.Default
{
    public class StorageCredentials
    {
        public string host;
        public uint? port;
        public string username;
        public string password;
        public string database;
        public bool addCreatedAndUpdatedDate = true;

        public StorageCredentials(string host, string username, string password, string database)
        {
            this.host = host;
            this.username = username;
            this.password = password;
            this.database = database;
        }

        public StorageCredentials(string host, uint port, string username, string password, string database) : this(host, username, password, database)
        {
            this.port = port;
        }
    }

    public abstract class DefaultDBStorage<T> : IDBStorage where T : IDBStorage
    {
        protected string host;
        protected uint? port;
        protected string username;
        protected string password;
        protected string database;
        protected bool addCreatedAndUpdatedDate;
        private bool linksCreated;
        private TransactionContext? transactionContext;
        private SemaphoreSlim locker = new SemaphoreSlim(1, 1);
        public bool IsConnectedOneTime { get; protected set; }
        public bool Debug { get; set; }


        private readonly Dictionary<Type, TableInfo> allTableInfos = new();
        public TableInfo? GetTableInfo(Type type)
        {
            if (allTableInfos.ContainsKey(type))
            {
                return allTableInfos[type];
            }
            return null;
        }
        public string GetDatabaseName() => database;

        public DefaultDBStorage(StorageCredentials info)
        {
            host = info.host;
            port = info.port;
            username = info.username;
            password = info.password;
            database = info.database;
            addCreatedAndUpdatedDate = info.addCreatedAndUpdatedDate;
        }

        #region connection
        public bool Connect()
        {
            return ConnectWithError().Success;
        }
        public virtual VoidWithError ConnectWithError()
        {
            VoidWithError result = new();
            try
            {
                using DbConnection connection = GetConnection();
                connection.Open();
                IsConnectedOneTime = true;
            }
            catch (Exception e)
            {
                result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
            }
            return result;
        }

        public abstract ResultWithError<bool> ResetStorage();
        protected abstract DbConnection GetConnection();
        public abstract ResultWithDataError<DbCommand> CreateCmd(string sql);
        public abstract DbParameter GetDbParameter();
        public void Close()
        {
            try
            {
                if (transactionContext != null)
                {
                    transactionContext.Rollback();
                    transactionContext.Dispose();
                }
            }
            catch (Exception e)
            {
                new DataError(DataErrorCode.UnknowError, e.Message).Print();
            }
        }

        public VoidWithError Execute(string sql, [CallerFilePath] string callerPath = "", [CallerLineNumber] int callerNo = 0)
        {
            ResultWithDataError<DbCommand> commandResult = CreateCmd(sql);
            if (commandResult.Result != null)
            {
                VoidWithError result = Execute(commandResult.Result, dataParameters: null, callerPath, callerNo);
                commandResult.Result.Dispose();
                return result;
            }
            VoidWithError noCommand = new();
            noCommand.Errors.AddRange(commandResult.Errors);
            return noCommand;
        }

        public VoidWithError Execute(DbCommand command, Dictionary<string, object?> parameters, [CallerFilePath] string callerPath = "", [CallerLineNumber] int callerNo = 0)
        {
            return Execute(command, new List<Dictionary<string, object?>>() { parameters }, callerPath, callerNo);
        }
        public VoidWithError Execute(DbCommand command, List<Dictionary<string, object?>>? dataParameters, [CallerFilePath] string callerPath = "", [CallerLineNumber] int callerNo = 0)
        {
            VoidWithError result = new();
            try
            {
                lock (locker)
                {
                    if (transactionContext == null)
                    {
                        return RunInsideTransaction(() =>
                        {
                            return Execute(command, dataParameters, callerPath, callerNo);
                        });
                    }

                    locker.WaitAsync().GetAwaiter().GetResult();
                    DbConnection? connection = transactionContext.Connection;
                    if (connection == null)
                    {
                        result.Errors.Add(new DataError(DataErrorCode.NoConnectionInsideStorage, "The storage " + GetType().Name, " doesn't have a connection"));
                        return result;
                    }


                    try
                    {
                        command.Transaction = transactionContext.transaction;
                        command.Connection = connection;
                        try
                        {
                            if (dataParameters != null)
                            {
                                foreach (Dictionary<string, object?> parameters in dataParameters)
                                {
                                    foreach (KeyValuePair<string, object?> parameter in parameters)
                                    {
                                        command.Parameters[parameter.Key].Value = parameter.Value;
                                    }
                                    if (Debug)
                                    {
                                        Console.WriteLine();
                                        string queryWithParam = command.CommandText;
                                        foreach (KeyValuePair<string, object?> parameter in parameters)
                                        {
                                            queryWithParam = queryWithParam.Replace(parameter.Key, parameter.Key + "(" + parameter.Value?.ToString() + ")");
                                        }
                                        Console.WriteLine(queryWithParam);
                                        Console.WriteLine();
                                    }
                                    command.ExecuteNonQuery();
                                }
                            }
                            else
                            {
                                if (Debug)
                                {
                                    Console.WriteLine();
                                    Console.WriteLine(command.CommandText);
                                    Console.WriteLine();
                                }
                                command.ExecuteNonQuery();
                            }
                        }
                        catch (Exception e)
                        {
                            result.Errors.Add(new DataError(DataErrorCode.UnknowError, e.Message, callerPath, callerNo));
                        }
                    }
                    catch (Exception e)
                    {
                        result.Errors.Add(new DataError(DataErrorCode.UnknowError, e.Message, callerPath, callerNo));
                    }
                    finally
                    {
                    }
                    locker.Release();
                }
            }
            catch (Exception e)
            {
                result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
            }
            return result;
        }

        public ResultWithError<List<Dictionary<string, string?>>> Query(string sql, [CallerFilePath] string callerPath = "", [CallerLineNumber] int callerNo = 0)
        {
            ResultWithDataError<DbCommand> commandResult = CreateCmd(sql);
            if (commandResult.Result != null)
            {
                ResultWithError<List<Dictionary<string, string?>>> result = Query(commandResult.Result, null, callerPath, callerNo);
                commandResult.Result.Dispose();
                return result;
            }
            ResultWithError<List<Dictionary<string, string?>>> noCommand = new();
            noCommand.Errors.AddRange(commandResult.Errors);
            return noCommand;
        }
        public ResultWithError<List<Dictionary<string, string?>>> Query(DbCommand command, List<Dictionary<string, object?>>? dataParameters, [CallerFilePath] string callerPath = "", [CallerLineNumber] int callerNo = 0, int loop = 0)
        {
            ResultWithError<List<Dictionary<string, string?>>> result = new()
            {
                Result = new List<Dictionary<string, string?>>()
            };
            try
            {
                lock (locker)
                {
                    if (transactionContext == null)
                    {
                        return RunInsideTransaction(() =>
                        {
                            return Query(command, dataParameters, callerPath, callerNo);
                        });
                    }

                    locker.WaitAsync().GetAwaiter().GetResult();
                    DbConnection? connection = transactionContext.Connection;
                    if (connection == null)
                    {
                        result.Errors.Add(new DataError(DataErrorCode.NoConnectionInsideStorage, "The storage " + GetType().Name, " doesn't have a connection"));
                        return result;
                    }

                    try
                    {
                        command.Transaction = transactionContext.transaction;
                        command.Connection = connection;
                        if (dataParameters != null)
                        {
                            foreach (Dictionary<string, object?> parameters in dataParameters)
                            {
                                foreach (KeyValuePair<string, object?> parameter in parameters)
                                {
                                    command.Parameters[parameter.Key].Value = parameter.Value;
                                }

                                if (Debug)
                                {
                                    Console.WriteLine();
                                    string queryWithParam = command.CommandText;
                                    foreach (KeyValuePair<string, object?> parameter in parameters)
                                    {
                                        queryWithParam = queryWithParam.Replace(parameter.Key, parameter.Key + "(" + parameter.Value?.ToString() + ")");
                                    }
                                    Console.WriteLine(queryWithParam);
                                    Console.WriteLine();
                                }
                                using (IDataReader reader = command.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        Dictionary<string, string?> temp = new();
                                        for (int i = 0; i < reader.FieldCount; i++)
                                        {
                                            if (!temp.ContainsKey(reader.GetName(i)))
                                            {
                                                if (reader[reader.GetName(i)] != null)
                                                {
                                                    string? valueString = reader[reader.GetName(i)].ToString();
                                                    valueString ??= "";
                                                    temp.Add(reader.GetName(i), valueString);
                                                }
                                                else
                                                {
                                                    temp.Add(reader.GetName(i), null);
                                                }
                                            }
                                        }
                                        result.Result.Add(temp);
                                    }
                                    reader.Close();
                                    reader.Dispose();
                                }
                            }
                        }
                        else
                        {
                            if (Debug)
                            {
                                Console.WriteLine();
                                Console.WriteLine(command.CommandText);
                                Console.WriteLine();
                            }
                            using (IDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    Dictionary<string, string?> temp = new();
                                    for (int i = 0; i < reader.FieldCount; i++)
                                    {
                                        if (!temp.ContainsKey(reader.GetName(i)))
                                        {
                                            if (reader[reader.GetName(i)] != null)
                                            {
                                                string? valueString = reader[reader.GetName(i)].ToString();
                                                valueString ??= "";
                                                temp.Add(reader.GetName(i), valueString);
                                            }
                                            else
                                            {
                                                temp.Add(reader.GetName(i), null);
                                            }
                                        }
                                    }
                                    result.Result.Add(temp);
                                }
                                reader.Close();
                                reader.Dispose();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        // The DataReader is still attach to the connection => not possible
                        // If we rerun the function it seems to be ok
                        if (e.Message.StartsWith("There is already an open DataReader") && loop < 5)
                        {
                            locker.Release();
                            return Query(command, dataParameters, callerPath, callerNo, loop + 1);
                        }
                        DataError error = new DataError(DataErrorCode.UnknowError, e.Message + "\nSQL: " + command.CommandText);
                        error.Details.Add(command.CommandText);
                        result.Errors.Add(error);
                    }
                    locker.Release();
                }
            }
            catch (Exception e)
            {
                result.Errors.Add(new DataError(DataErrorCode.UnknowError, e.Message + "\nSQL: " + command.CommandText, callerPath, callerNo));
            }

            return result;
        }

        public ResultWithError<TransactionContext> BeginTransaction()
        {
            ResultWithError<TransactionContext> result = new();

            try
            {
                lock (locker)
                {
                    locker.WaitAsync().GetAwaiter().GetResult();
                    if (transactionContext == null)
                    {
                        DbConnection connection = GetConnection();
                        try
                        {
                            connection.Open();
                        }
                        catch
                        {
                            result.Errors.Add(new DataError(DataErrorCode.StorageDisconnected, "The storage " + GetType().Name + "(" + ToString() + ") can't connect to the database"));
                            locker.Release();
                            return result;
                        }
                        DbTransaction transaction = connection.BeginTransaction();
                        transactionContext = new TransactionContext(transaction, EndTransaction, RunInsideLocker);
                        result.Result = transactionContext;
                    }
                    else
                    {
                        transactionContext.count++;
                        result.Result = transactionContext;
                    }
                    locker.Release();
                }
            }
            catch (Exception e)
            {
                result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
            }


            return result;
        }

        protected void EndTransaction()
        {
            if (transactionContext != null)
            {
                transactionContext.transaction.Dispose();
                if (transactionContext.transaction.Connection != null)
                {
                    transactionContext.transaction.Connection.Dispose();
                }
                transactionContext = null;
            }
        }

        protected void RunInsideLocker(Action fct)
        {
            lock (locker)
            {
                locker.WaitAsync().GetAwaiter().GetResult();
                fct();
                locker.Release();
            }
        }


        #endregion

        #region init
        public VoidWithError CreateLinks()
        {
            VoidWithError result = new VoidWithError();
            if (!linksCreated)
            {
                linksCreated = true;
                foreach (TableInfo info in allTableInfos.Values.ToList())
                {
                    result = info.LoadDM();
                    if (!result.Success)
                    {
                        return result;
                    }
                    foreach (TableMemberInfoSql memberInfo in info.Members)
                    {
                        if (memberInfo is ITableMemberInfoSqlLink memberInfoSqlLink)
                        {
                            if (memberInfoSqlLink.TableLinked == null && memberInfoSqlLink.TableLinkedType != null)
                            {
                                if (allTableInfos.ContainsKey(memberInfoSqlLink.TableLinkedType))
                                {
                                    memberInfoSqlLink.TableLinked = allTableInfos[memberInfoSqlLink.TableLinkedType];
                                }
                                else
                                {
                                    result.Errors.Add(new DataError(DataErrorCode.TypeNotFound, "Can't find the type " + memberInfoSqlLink.TableLinkedType + " to create link with " + memberInfo.Name + " on " + memberInfo.TableInfo.Name));
                                }
                            }
                        }
                    }
                    foreach (TableReverseMemberInfo reversMember in info.ReverseMembers)
                    {
                        if (reversMember.ReverseLinkType != null && allTableInfos.ContainsKey(reversMember.ReverseLinkType))
                        {
                            VoidWithDataError resultTemp = reversMember.PrepareReverseLink(allTableInfos[reversMember.ReverseLinkType]);
                            if (!resultTemp.Success)
                            {
                                result.Errors.AddRange(resultTemp.Errors);
                            }
                        }
                        else
                        {
                            result.Errors.Add(new DataError(DataErrorCode.TypeNotFound, "Can't find the type " + reversMember.ReverseLinkType + " to create revserse link with " + reversMember.Name + " on " + reversMember.TableInfo.Name));
                        }
                    }
                    if (!result.Success)
                    {
                        return result;
                    }
                }
            }
            return result;
        }
        public VoidWithDataError AddPyramid(PyramidInfo pyramid)
        {
            linksCreated = false;
            return AddPyramidLoop(pyramid, null, null, false);
        }
        private VoidWithDataError AddPyramidLoop(PyramidInfo pyramid, TableInfo? parent, List<TableMemberInfoSql>? membersToAdd, bool typeMemberCreated)
        {
            VoidWithDataError resultTemp;
            TableInfo classInfo = new(pyramid);
            resultTemp = classInfo.Init();
            if (!resultTemp.Success)
            {
                return resultTemp;
            }
            if (pyramid.isForceInherit)
            {
                membersToAdd ??= new List<TableMemberInfoSql>();
                membersToAdd.AddRange(classInfo.Members);
                foreach (PyramidInfo child in pyramid.children)
                {
                    resultTemp = AddPyramidLoop(child, parent, membersToAdd, typeMemberCreated);
                    if (!resultTemp.Success)
                    {
                        return resultTemp;
                    }
                }
            }
            else
            {
                if (membersToAdd != null)
                {
                    // merge parent members
                    // force created and updated date to the end
                    TableMemberInfoSql? createdDate = null;
                    TableMemberInfoSql? updatedDate = null;
                    foreach (TableMemberInfoSql memberInfo in membersToAdd.ToList())
                    {
                        memberInfo.ChangeTableInfo(classInfo);
                        if (memberInfo.Name == TypeTools.GetMemberName((StorableTimestamp<IStorableTimestamp> s) => s.CreatedDate))
                        {
                            membersToAdd.Remove(memberInfo);
                            memberInfo.IsUpdatable = false;
                            createdDate = memberInfo;
                        }
                        else if (memberInfo.Name == TypeTools.GetMemberName((StorableTimestamp<IStorableTimestamp> s) => s.UpdatedDate))
                        {
                            membersToAdd.Remove(memberInfo);
                            updatedDate = memberInfo;
                        }
                        if (memberInfo.IsPrimary)
                        {
                            classInfo.Primary = memberInfo;
                        }
                    }
                    classInfo.Members.InsertRange(0, membersToAdd);
                    if (addCreatedAndUpdatedDate)
                    {
                        if (createdDate != null)
                        {
                            classInfo.Members.Add(createdDate);
                        }
                        if (updatedDate != null)
                        {
                            classInfo.Members.Add(updatedDate);
                        }
                    }
                }
                if (classInfo.IsAbstract && !typeMemberCreated)
                {
                    classInfo.AddTypeMember();
                    typeMemberCreated = true;
                }
                allTableInfos[pyramid.type] = classInfo;
                if (pyramid.aliasType != null)
                {
                    allTableInfos[pyramid.aliasType] = classInfo;
                }
                if (parent != null)
                {
                    classInfo.Parent = parent;
                    parent.Children.Add(classInfo);
                    if (parent.Primary != null)
                    {
                        TableMemberInfoSqlParent parentLink = new TableMemberInfoSqlParent(parent.Primary.memberInfo, parent.Primary.TableInfo, false);
                        parentLink.TableLinked = parent;
                        VoidWithDataError prepareResult = parentLink.PrepareForSQL();
                        if (!prepareResult.Success)
                        {
                            return prepareResult;
                        }
                        classInfo.Members.Insert(0, parentLink);
                        classInfo.Primary = parentLink;
                    }
                }
                foreach (PyramidInfo child in pyramid.children)
                {
                    resultTemp = AddPyramidLoop(child, classInfo, null, typeMemberCreated);
                    if (!resultTemp.Success)
                    {
                        return resultTemp;
                    }
                }
            }

            return new VoidWithDataError();
        }
        #endregion

        #region actions
        protected enum QueryParameterType
        {
            Normal,
            GrabValue
        }
        protected abstract object? TransformValueForFct(ParamsInfo paramsInfo);
        protected ResultWithError<List<Dictionary<string, string?>>> QueryGeneric(StorableAction action, string sql, Dictionary<ParamsInfo, QueryParameterType> parameters, IStorable? item = null)
        {
            List<GenericError> errors = new();

            if (item != null)
            {
                errors.AddRange(item.IsValid(action));
            }
            if (errors.Count > 0)
            {
                ResultWithError<List<Dictionary<string, string?>>> queryResultTemp = new()
                {
                    Result = new List<Dictionary<string, string?>>()
                };
                foreach (DataError error in errors)
                {
                    queryResultTemp.Errors.Add(error);
                }
                return queryResultTemp;
            }

            string sqlToExecute = sql;
            Dictionary<ParamsInfo, QueryParameterType> parametersToUse = new();
            // check if parameters list
            foreach (KeyValuePair<ParamsInfo, QueryParameterType> parameterInfo in parameters)
            {
                if (parameterInfo.Key.Value is IList list)
                {
                    List<string> paramNames = new();
                    for (int i = 0; i < list.Count; i++)
                    {
                        paramNames.Add("@" + parameterInfo.Key.Name + "_" + i);
                        parametersToUse.Add(new ParamsInfo()
                        {
                            DbType = parameterInfo.Key.DbType,
                            MembersList = parameterInfo.Key.MembersList,
                            Name = parameterInfo.Key.Name + "_" + i,
                            TypeLvl0 = parameterInfo.Key.TypeLvl0,
                            Value = list[i],
                        }, parameterInfo.Value);
                    }
                    sqlToExecute = sqlToExecute.Replace("@" + parameterInfo.Key.Name, "(" + string.Join(",", paramNames) + ")");
                }
                else
                {
                    parametersToUse.Add(parameterInfo.Key, parameterInfo.Value);
                }
            }
            ResultWithError<List<Dictionary<string, string?>>> result = new();
            ResultWithDataError<DbCommand> cmdResult = CreateCmd(sqlToExecute);
            result.Errors.AddRange(cmdResult.Errors);
            if (!result.Success || cmdResult.Result == null)
            {
                return result;
            }
            DbCommand cmd = cmdResult.Result;
            Dictionary<string, object?> parametersValue = new();
            foreach (KeyValuePair<ParamsInfo, QueryParameterType> parameterInfo in parametersToUse)
            {
                DbParameter parameter = GetDbParameter();
                parameter.ParameterName = "@" + parameterInfo.Key.Name;
                parameter.DbType = parameterInfo.Key.DbType;
                cmd.Parameters.Add(parameter);
                if (parameterInfo.Value == QueryParameterType.GrabValue)
                {
                    if (Regex.IsMatch(parameterInfo.Key.Name, "(^|\\.)UpdatedDate$") || Regex.IsMatch(parameterInfo.Key.Name, "(^|\\.)CreatedDate$"))
                    {
                        parameterInfo.Key.Value = DateTime.Now;
                        if (item != null)
                        {
                            parameterInfo.Key.SetCurrentValueOnObject(item);
                        }
                    }
                    else if (item != null)
                    {
                        parameterInfo.Key.TypeLvl0 = item.GetType();
                        parameterInfo.Key.SetValue(item);
                    }
                    else
                    {
                        parameterInfo.Key.Value = null;
                    }

                    errors.AddRange(parameterInfo.Key.IsValueValid());
                }
                parametersValue["@" + parameterInfo.Key.Name] = TransformValueForFct(parameterInfo.Key);
            }
            ResultWithError<List<Dictionary<string, string?>>> queryResult;
            if (errors.Count > 0)
            {
                queryResult = new ResultWithError<List<Dictionary<string, string?>>>
                {
                    Result = new List<Dictionary<string, string?>>()
                };
                foreach (DataError error in errors)
                {
                    queryResult.Errors.Add(error);
                }
            }
            else
            {
                //write all combinaisons if one of the parameter is a list
                List<Dictionary<string, object?>> parametersFinal = new();

                Action<int, Dictionary<string, object?>> combinaisons = (int i, Dictionary<string, object?> current) => { };
                combinaisons = (int i, Dictionary<string, object?> current) =>
                {
                    if (i == parametersValue.Count)
                    {
                        parametersFinal.Add(current);
                        return;
                    }
                    KeyValuePair<string, object?> parameterValue = parametersValue.ElementAt(i);

                    if (parameterValue.Value is IList enumerable)
                    {
                        foreach (object o in enumerable)
                        {
                            Dictionary<string, object?> clone = current.ToDictionary(t => t.Key, t => t.Value);
                            clone.Add(parameterValue.Key, o);
                            combinaisons(i + 1, clone);
                        }
                    }
                    else
                    {
                        current.Add(parameterValue.Key, parameterValue.Value);
                        combinaisons(i + 1, current);
                    }
                };

                combinaisons(0, new());

                queryResult = Query(cmd, parametersFinal);
            }
            cmd.Dispose();
            return queryResult;

        }


        #region Get
        protected abstract DatabaseQueryBuilderInfo PrepareSQLForQuery<X>(DatabaseQueryBuilder<X> queryBuilder) where X : IStorable;
        public ResultWithError<List<X>> QueryFromBuilder<X>(DatabaseQueryBuilder<X> queryBuilder) where X : IStorable
        {
            ResultWithError<List<X>> result = new();

            if (queryBuilder.info == null)
            {
                queryBuilder.info = PrepareSQLForQuery(queryBuilder);
            }
            string sql = queryBuilder.info.Sql;


            ResultWithError<List<Dictionary<string, string?>>> queryResult = QueryGeneric(StorableAction.Read, sql, queryBuilder.WhereParamsInfo.ToDictionary(p => p.Value, p => QueryParameterType.Normal));

            result.Errors.AddRange(queryResult.Errors);
            if (queryResult.Success && queryResult.Result != null)
            {
                result.Result = new List<X>();
                DatabaseBuilderInfo baseInfo = queryBuilder.InfoByPath[""];

                for (int i = 0; i < queryResult.Result.Count; i++)
                {
                    Dictionary<string, string?> itemFields = queryResult.Result[i];
                    ResultWithDataError<object> resultTemp = CreateObject(baseInfo, itemFields);
                    if (resultTemp.Success && resultTemp.Result != null)
                    {
                        if (resultTemp.Result is X oCasted)
                        {
                            queryBuilder.DM.OnItemLoaded(oCasted);
                            result.Result.Add(oCasted);
                        }
                        else
                        {
                            result.Errors.Add(new DataError(DataErrorCode.UnknowError, "Impossible to cast " + resultTemp.Result.GetType().Name + " into " + typeof(X).Name));
                        }
                    }
                    else
                    {
                        result.Errors.AddRange(resultTemp.Errors);
                    }

                }
            }

            return result;
        }
        protected ResultWithDataError<object> CreateObject(DatabaseBuilderInfo info, Dictionary<string, string?> itemFields)
        {
            ResultWithDataError<object> result = new ResultWithDataError<object>();
            string rootAlias = info.Alias;
            TableInfo rootTableInfo = info.TableInfo;

            while (rootTableInfo.Parent != null)
            {
                rootTableInfo = rootTableInfo.Parent;
            }
            if (rootTableInfo != info.TableInfo)
            {
                rootAlias = info.Parents[rootTableInfo];
            }

            object o;
            if (info.TableInfo.IsAbstract)
            {
                string fieldTypeName = rootAlias + "*" + TableInfo.TypeIdentifierName;
                if (!itemFields.ContainsKey(fieldTypeName))
                {
                    result.Errors.Add(new DataError(DataErrorCode.NoTypeIdentifierFoundInsideQuery, "Can't find the field " + TableInfo.TypeIdentifierName));
                    return result;
                }

                ResultWithDataError<Type> typeToCreate = TypeTools.GetTypeDataObject(itemFields[fieldTypeName] ?? "");
                if (!typeToCreate.Success || typeToCreate.Result == null)
                {
                    result.Errors.AddRange(typeToCreate.Errors);
                    return result;
                }
                o = TypeTools.CreateNewObj(typeToCreate.Result);
            }
            else
            {
                o = TypeTools.CreateNewObj(info.TableInfo.Type);
            }

            // TODO : optimize this method by storing needed values
            foreach (KeyValuePair<TableMemberInfoSql, DatabaseBuilderInfoMember> member in info.Members)
            {
                string alias = member.Value.Alias;
                TableMemberInfoSql memberInfo = member.Key;
                string key = alias + "*" + memberInfo.SqlName;
                if (itemFields.ContainsKey(key))
                {
                    if (memberInfo is TableMemberInfoSqlBasic || memberInfo is TableMemberInfoSql1NInt || memberInfo is CustomTableMember)
                    {
                        memberInfo.ApplySqlValue(o, itemFields[key]);
                    }
                    else if (memberInfo is TableMemberInfoSql1N memberInfo1N)
                    {
                        if (itemFields[key] != "")
                        {
                            if (member.Value.UseDM)
                            {
                                string idValue = itemFields[key] ?? string.Empty;
                                object? oTemp = memberInfo1N.TableLinked?.DM?.GetById(int.Parse(idValue));
                                memberInfo.SetValue(o, oTemp);
                            }
                            else if (info.joins.ContainsKey(memberInfo))
                            {
                                // loaded from the query
                                ResultWithDataError<object> oTemp = CreateObject(info.joins[memberInfo], itemFields);
                                if (oTemp.Success && oTemp.Result != null)
                                {
                                    memberInfo.SetValue(o, oTemp.Result);
                                }
                                else
                                {
                                    result.Errors.AddRange(oTemp.Errors);
                                }
                            }
                            else
                            {
                                result.Errors.Add(new DataError(DataErrorCode.UnknowError, "impossible?"));
                            }
                        }
                    }
                    else if (memberInfo is TableMemberInfoSqlNMInt tableMemberInfoSqlNMInt)
                    {
                        tableMemberInfoSqlNMInt.ApplySqlValue(o, itemFields[key]);
                    }
                    else if (memberInfo is TableMemberInfoSqlNM tableMemberInfoSqlNM)
                    {
                        tableMemberInfoSqlNM.ApplySqlValue(o, itemFields[key]);
                    }
                }
            }

            // TODO : change it to make only one DB request based on the list
            foreach (TableReverseMemberInfo reverse in info.ReverseLinks)
            {
                if (o is IStorable storable)
                {
                    VoidWithDataError reverseResult = reverse.ReverseLoadAndSet(storable);
                    if (!reverseResult.Success)
                    {
                        result.Errors.AddRange(reverseResult.Errors);
                    }
                }
            }
            result.Result = o;
            return result;
        }
        public void LoadAllTableFieldsQuery<X>(TableInfo tableInfo, string alias, DatabaseBuilderInfo baseInfo, List<string> path, List<Type> types, DatabaseQueryBuilder<X> queryBuilder) where X : IStorable
        {
            bool useShort = false;
            if (queryBuilder.UseShortObject)
            {
                if (path.Count == 0)
                {
                    useShort = false;
                }
                else if (queryBuilder.InfoByPath[""].TableInfo.DM is IDatabaseDM DM)
                {
                    useShort = DM.IsShortLink(string.Join(".", path));
                }
            }

            if (useShort)
            {
                if (tableInfo.TypeMember != null)
                {
                    baseInfo.Members.Add(tableInfo.TypeMember, new DatabaseBuilderInfoMember(tableInfo.TypeMember, alias, this));
                }
                if (tableInfo.Primary != null)
                {
                    baseInfo.Members.Add(tableInfo.Primary, new DatabaseBuilderInfoMember(tableInfo.Primary, alias, this));
                }
            }
            else
            {
                foreach (TableMemberInfoSql member in tableInfo.Members)
                {
                    if (!member.IsAutoRead)
                    {
                        continue;
                    }
                    if (member is TableMemberInfoSql1N)
                    {
                        DatabaseBuilderInfoMember info = new(member, alias, this);
                        if (!info.UseDM)
                        {
                            if (member.MemberType != null)
                            {
                                path.Add(member.Name);
                                types.Add(member.MemberType);
                                queryBuilder.LoadLinks(path, types, true);
                                path.RemoveAt(path.Count - 1);
                                types.RemoveAt(types.Count - 1);
                            }
                        }
                        else
                        {
                            baseInfo.Members.Add(member, info);
                        }
                    }
                    else
                    {
                        baseInfo.Members.Add(member, new DatabaseBuilderInfoMember(member, alias, this));
                    }
                }
            }

            foreach (TableReverseMemberInfo member in tableInfo.ReverseMembers)
            {
                if (member.IsAutoRead)
                {
                    baseInfo.ReverseLinks.Add(member);
                }
            }
        }
        #endregion


        #region Exist
        protected abstract DatabaseExistBuilderInfo PrepareSQLForExist<X>(DatabaseExistBuilder<X> queryBuilder) where X : IStorable;
        public ResultWithError<bool> ExistFromBuilder<X>(DatabaseExistBuilder<X> queryBuilder) where X : IStorable
        {
            ResultWithError<bool> result = new();

            if (queryBuilder.info == null)
            {
                queryBuilder.info = PrepareSQLForExist(queryBuilder);
            }
            string sql = queryBuilder.info.Sql;

            ResultWithError<List<Dictionary<string, string?>>> queryResult = QueryGeneric(StorableAction.Read, sql, queryBuilder.WhereParamsInfo.ToDictionary(p => p.Value, p => QueryParameterType.Normal));

            result.Errors.AddRange(queryResult.Errors);
            if (queryResult.Success && queryResult.Result != null && queryResult.Result.Count > 0 && queryResult.Result[0].ContainsKey("nb"))
            {
                result.Result = int.Parse(queryResult.Result[0]["nb"] ?? "0") > 0;
            }
            return result;
        }

        #endregion

        #region Table
        protected abstract string PrepareSQLCreateTable(TableInfo table);
        protected abstract string PrepareSQLCreateIntermediateTable(TableMemberInfoSql tableMember);
        public VoidWithError CreateTable(PyramidInfo pyramid)
        {
            VoidWithError result = new();
            if (!pyramid.isForceInherit)
            {
                if (allTableInfos.ContainsKey(pyramid.type))
                {
                    VoidWithError resultTemp = CreateTable(allTableInfos[pyramid.type]);
                    result.Errors.AddRange(resultTemp.Errors);
                }
                else
                {
                    result.Errors.Add(new DataError(DataErrorCode.TypeNotExistInsideStorage, "Can't find the type " + pyramid.type));
                }
            }
            else
            {
                foreach (PyramidInfo child in pyramid.children)
                {
                    VoidWithError resultTemp = CreateTable(child);
                    result.Errors.AddRange(resultTemp.Errors);
                }
            }
            return result;
        }
        public VoidWithError CreateTable(TableInfo table)
        {
            VoidWithError result = new();
            ResultWithError<bool> tableExist = TableExist(table);
            result.Errors.AddRange(tableExist.Errors);
           
            if (tableExist.Success && !tableExist.Result)
            {
                string sql = PrepareSQLCreateTable(table);
                VoidWithError resultTemp = Execute(sql);
                result.Errors.AddRange(resultTemp.Errors);

                // create intermediate table
                List<TableMemberInfoSql> members = table.Members.Where
                    (f => f is TableMemberInfoSqlNM || f is TableMemberInfoSqlNMInt).ToList();

                string? intermediateQuery = null;
                foreach (TableMemberInfoSql member in members)
                {
                    intermediateQuery = PrepareSQLCreateIntermediateTable(member);
                    VoidWithError resultTempInter = Execute(intermediateQuery);
                    result.Errors.AddRange(resultTempInter.Errors);
                }
            }
            foreach (TableInfo child in table.Children)
            {
                VoidWithError resultTemp = CreateTable(child);
                result.Errors.AddRange(resultTemp.Errors);
            }
            return result;
        }

        public ResultWithError<bool> TableExist(PyramidInfo pyramid)
        {
            if (allTableInfos.ContainsKey(pyramid.type))
            {
                return TableExist(allTableInfos[pyramid.type]);
            }
            ResultWithError<bool> result = new();
            result.Errors.Add(new DataError(DataErrorCode.TypeNotExistInsideStorage, "Can't find the type " + pyramid.type));
            result.Result = false;
            return result;
        }
        protected abstract string PrepareSQLTableExist(string table);
        public ResultWithError<bool> TableExist(TableInfo table)
        {
            return TableExist(table.SqlTableName);
        }
        public ResultWithError<bool> TableExist(string table)
        {
            ResultWithError<bool> result = new();
            string sql = PrepareSQLTableExist(table);
            ResultWithError<List<Dictionary<string, string?>>> queryResult = Query(sql);
            result.Errors.AddRange(queryResult.Errors);

            if (queryResult.Success && queryResult.Result != null && queryResult.Result.Count == 1)
            {
                int nb = int.Parse(queryResult.Result.ElementAt(0)["nb"] ?? "0");
                result.Result = (nb != 0);
            }
            return result;
        }
        #endregion

        #region Create

        protected abstract DatabaseCreateBuilderInfo PrepareSQLForCreate<X>(DatabaseCreateBuilder<X> createBuilder) where X : IStorable;
        public VoidWithError CreateFromBuilder<X>(DatabaseCreateBuilder<X> createBuilder, X item) where X : IStorable
        {
            VoidWithError result = new();
            if (item == null)
            {
                result.Errors.Add(new DataError(DataErrorCode.NoItemProvided, "Please provide an item to use for creation"));
                return result;
            }
            List<DatabaseCreateBuilderInfoQuery> queries;
            if (createBuilder.info == null)
            {
                createBuilder.info = PrepareSQLForCreate(createBuilder);
            }
            queries = createBuilder.info.Queries;


            #region create

            VoidWithError resultBefore = CheckAutoCUDBeforeCreate(createBuilder.info.ToCheckBefore, item);
            if (!resultBefore.Success)
            {
                result.Errors.AddRange(resultBefore.Errors);
                return result;
            }

            int id = 0;
            foreach (DatabaseCreateBuilderInfoQuery query in queries)
            {
                string sql = query.Sql;
                Dictionary<ParamsInfo, QueryParameterType> parametersCreate = new();
                foreach (ParamsInfo parameterInfo in query.Parameters)
                {
                    parameterInfo.Value = null;
                    parametersCreate.Add(parameterInfo, QueryParameterType.GrabValue);
                }
                if (!query.HasPrimaryResult && query.PrimaryToSet != null)
                {
                    query.PrimaryToSet.Value = id;
                    parametersCreate.Add(query.PrimaryToSet, QueryParameterType.Normal);
                }

                ResultWithError<List<Dictionary<string, string?>>> createResult = QueryGeneric(StorableAction.Create, sql, parametersCreate, item);

                if (!createResult.Success)
                {
                    result.Errors.AddRange(createResult.Errors);
                    return result;
                }
                else if (query.HasPrimaryResult && createResult.Result != null)
                {
                    id = int.Parse(createResult.Result[0][Storable.Id] ?? "0");
                    item.Id = id;
                }
            }

            if (result.Errors.Count == 0)
            {
                VoidWithError resultReverse = CheckReverseLinkAfterCreate(createBuilder.info.ReverseMembers, item, id);
                if (!resultReverse.Success)
                {
                    result.Errors.AddRange(resultReverse.Errors);
                    return result;
                }
            }
            else
            {
                item.Id = 0;
            }
            #endregion

            return result;
        }
        /// <summary>
        /// Check auto CUD before insert item into DB
        /// </summary>
        /// <typeparam name="X"></typeparam>
        /// <param name="members"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        protected VoidWithError CheckAutoCUDBeforeCreate<X>(List<TableMemberInfoSql> members, X item) where X : IStorable
        {
            VoidWithError result = new();
            Func<IStorable, TableMemberInfoSql, bool> manageStorable = (storableLink, member) =>
            {
                if (storableLink.Id == 0 && member.IsAutoCreate)
                {
                    List<GenericError> resultCreateTemp = storableLink.CreateWithError();
                    if (resultCreateTemp.Count != 0)
                    {
                        result.Errors.AddRange(resultCreateTemp);
                        return false;
                    }
                }
                else if (storableLink.Id != 0 && member.IsAutoUpdate)
                {
                    List<GenericError> resultUpdateTemp = storableLink.UpdateWithError();
                    if (resultUpdateTemp.Count != 0)
                    {
                        result.Errors.AddRange(resultUpdateTemp);
                        return false;
                    }
                }
                return true;
            };
            foreach (TableMemberInfoSql member in members)
            {
                if (member is ITableMemberInfoSqlLinkSingle)
                {
                    object? o = member.GetValue(item);
                    if (o is IStorable storableLink)
                    {
                        if (!manageStorable(storableLink, member))
                        {
                            return result;
                        }
                    }
                }
                else if (member is ITableMemberInfoSqlLinkMultiple)
                {
                    object? o = member.GetValue(item);
                    if (o is IList listLink)
                    {
                        foreach (object itemLink in listLink)
                        {
                            if (itemLink is IStorable storableLink)
                            {
                                if (!manageStorable(storableLink, member))
                                {
                                    return result;
                                }
                            }
                        }
                    }
                    else if (o is IDictionary dicoLink)
                    {
                        foreach (DictionaryEntry? itemLink in dicoLink)
                        {
                            if (itemLink.Value.Value is IStorable storableLink)
                            {
                                if (!manageStorable(storableLink, member))
                                {
                                    return result;
                                }
                            }
                        }
                    }
                }
            }

            return result;
        }
        /// <summary>
        /// Check auto CUD for reverse link
        /// </summary>
        /// <typeparam name="X"></typeparam>
        /// <param name="reverseMembers"></param>
        /// <param name="item"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        protected VoidWithError CheckReverseLinkAfterCreate<X>(List<TableReverseMemberInfo> reverseMembers, X item, int id) where X : IStorable
        {
            VoidWithError result = new();
            Func<IStorable, TableReverseMemberInfo, bool> manageStorable = (reverseStorable, member) =>
            {
                if (reverseStorable.Id == 0 && member.IsAutoCreate)
                {
                    member.SetReverseId(reverseStorable, id);
                    List<GenericError> resultCreateTemp = reverseStorable.CreateWithError();
                    if (resultCreateTemp.Count != 0)
                    {
                        result.Errors.AddRange(resultCreateTemp);
                        return false;
                    }
                }
                else if (member.IsAutoUpdate)
                {
                    member.SetReverseId(reverseStorable, id);
                    List<GenericError> resultUpdateTemp = reverseStorable.UpdateWithError();
                    if (resultUpdateTemp.Count != 0)
                    {
                        result.Errors.AddRange(resultUpdateTemp);
                        return false;
                    }
                }
                return true;
            };
            foreach (TableReverseMemberInfo reverseMember in reverseMembers)
            {
                object? reverseO = reverseMember.GetValue(item);
                if (reverseO is IList reverseList)
                {
                    foreach (object reverseItem in reverseList)
                    {
                        if (reverseItem is IStorable reverseStorable)
                        {
                            if (!manageStorable(reverseStorable, reverseMember))
                            {
                                return result;
                            }
                        }
                    }
                }
                else if (reverseO is IStorable reverseStorable)
                {
                    if (!manageStorable(reverseStorable, reverseMember))
                    {
                        return result;
                    }
                }
            }
            return result;
        }
        #endregion

        #region Update
        protected abstract DatabaseUpdateBuilderInfo PrepareSQLForUpdate<X>(DatabaseUpdateBuilder<X> updateBuilder) where X : IStorable;
        public ResultWithError<List<int>> UpdateFromBuilder<X>(DatabaseUpdateBuilder<X> updateBuilder, X item) where X : IStorable
        {
            ResultWithError<List<int>> result = new()
            {
                Result = new List<int>()
            };
            if (item == null)
            {
                result.Errors.Add(new DataError(DataErrorCode.NoItemProvided, "Please provide an item to use for update"));
                return result;
            }
            DatabaseUpdateBuilderInfo updateInfo;
            if (updateBuilder.Query != null)
            {
                updateInfo = updateBuilder.Query;
            }
            else
            {
                updateInfo = PrepareSQLForUpdate(updateBuilder);
                updateBuilder.Query = updateInfo;
            }


            Dictionary<ParamsInfo, QueryParameterType> parametersQuery = new();
            foreach (KeyValuePair<string, ParamsInfo> parameterInfo in updateBuilder.WhereParamsInfo)
            {
                parametersQuery.Add(parameterInfo.Value, QueryParameterType.Normal);
            }

            #region query elements that will be updated
            ResultWithError<List<Dictionary<string, string?>>> queryResult = QueryGeneric(StorableAction.Read, updateInfo.QuerySql, parametersQuery);
            List<int> list = new();
            if (!queryResult.Success)
            {
                result.Errors.AddRange(queryResult.Errors);
                return result;
            }
            else if (queryResult.Result != null)
            {
                foreach (Dictionary<string, string?> row in queryResult.Result)
                {
                    if (row.ContainsKey(Storable.Id))
                    {
                        list.Add(int.Parse(row[Storable.Id] ?? "0"));
                    }
                }
            }

            CheckAutoCUDBeforeUpdate(updateInfo.ToCheckBefore, item, list, updateBuilder.DM, out List<IStorable> storableToDeleted);

            foreach (TableReverseMemberInfo reverseMember in updateInfo.ReverseMembers)
            {
                Dictionary<int, IStorable> oldList = new Dictionary<int, IStorable>();
                foreach (int id in list)
                {
                    ResultWithDataError<List<IStorable>> resultTemp = reverseMember.ReverseQuery(id);
                    if (resultTemp.Result != null)
                    {
                        foreach (IStorable itemTemp in resultTemp.Result)
                        {
                            if (!oldList.ContainsKey(itemTemp.Id))
                            {
                                oldList[itemTemp.Id] = itemTemp;
                            }
                        }
                    }
                }

                object? currentListO = reverseMember.GetValue(item);
                if (currentListO is IList currentList)
                {
                    foreach (IStorable itemTemp in currentList)
                    {
                        if (itemTemp.Id == 0 && reverseMember.IsAutoCreate)
                        {
                            foreach (int id in list)
                            {
                                itemTemp.Id = 0;
                                reverseMember.SetReverseId(itemTemp, id);
                                itemTemp.Create();
                            }
                        }
                        else if (oldList.ContainsKey(itemTemp.Id))
                        {
                            if (reverseMember.IsAutoUpdate)
                            {
                                itemTemp.Update();
                            }
                            oldList.Remove(itemTemp.Id);
                        }
                    }
                }

                if (reverseMember.IsAutoDelete)
                {
                    foreach (KeyValuePair<int, IStorable> missing in oldList)
                    {
                        missing.Value.Delete();
                    }
                }
            }
            #endregion

            #region update
            foreach (DatabaseUpdateBuilderInfoQuery query in updateInfo.Queries)
            {
                string sql = query.Sql;
                Dictionary<ParamsInfo, QueryParameterType> parametersUpdate = new();
                foreach (ParamsInfo parameterInfo in query.Parameters)
                {
                    parametersUpdate.Add(parameterInfo, QueryParameterType.Normal);
                }
                foreach (ParamsInfo parameterInfo in query.ParametersGrap)
                {
                    parametersUpdate.Add(parameterInfo, QueryParameterType.GrabValue);
                }
                ResultWithError<List<Dictionary<string, string?>>> updateResult = QueryGeneric(StorableAction.Update, query.Sql, parametersUpdate, item);

                if (!updateResult.Success)
                {
                    result.Errors.AddRange(updateResult.Errors);
                    return result;
                }

            }
            #endregion
            result.Result = list;

            foreach (IStorable storable in storableToDeleted)
            {
                List<GenericError> resultError = storable.DeleteWithError();
                if (resultError.Count != 0)
                {
                    result.Errors.AddRange(resultError);
                    return result;
                }
            }

            return result;
        }
        public void LoadAllTableFieldsUpdate<X>(TableInfo tableInfo, string alias, DatabaseBuilderInfo baseInfo) where X : IStorable
        {
            foreach (TableMemberInfoSql member in tableInfo.Members)
            {
                if (!member.IsUpdatable)
                {
                    continue;
                }
                baseInfo.Members.Add(member, new DatabaseBuilderInfoMember(member, alias, this));
            }

            foreach (TableReverseMemberInfo member in tableInfo.ReverseMembers)
            {
                if (member.IsAutoCreate || member.IsAutoUpdate || member.IsAutoDelete)
                {
                    baseInfo.ReverseLinks.Add(member);
                }
            }
        }

        protected VoidWithError CheckAutoCUDBeforeUpdate<X>(List<TableMemberInfoSql> members, X item, List<int> listIdUpdate, IGenericDM DM, out List<IStorable> storableToDeleted) where X : IStorable
        {
            VoidWithError result = new VoidWithError();
            storableToDeleted = new List<IStorable>();
            if (members.Count == 0)
            {
                return result;
            }
            listIdUpdate = listIdUpdate.ToList();

            // query all update link
            DatabaseQueryBuilder<X> queryBuilder = new DatabaseQueryBuilder<X>(this, DM);
            queryBuilder.Field(p => p.Id);
            foreach (TableMemberInfoSql member in members)
            {
                if (member is ITableMemberInfoSqlLinkSingle)
                {
                    ParameterExpression argParam = Expression.Parameter(typeof(X), "t");
                    MemberExpression fieldProperty = Expression.Property(argParam, member.SqlName);
                    LambdaExpression lambda = Expression.Lambda(fieldProperty, argParam);
                    queryBuilder.GetType().GetMethod("Field")?.MakeGenericMethod(fieldProperty.Type).Invoke(queryBuilder, new object[] { lambda });
                }
                else if (member is ITableMemberInfoSqlLinkMultiple)
                {
                    ParameterExpression argParam = Expression.Parameter(typeof(X), "t");
                    MemberExpression fieldProperty = Expression.Property(argParam, member.SqlName);
                    LambdaExpression lambda = Expression.Lambda(fieldProperty, argParam);
                    queryBuilder.GetType().GetMethod("Field")?.MakeGenericMethod(fieldProperty.Type).Invoke(queryBuilder, new object[] { lambda });
                }
            }
            queryBuilder.Where(p => listIdUpdate.Contains(p.Id));
            ResultWithError<List<X>> resultTemp = queryBuilder.RunWithError();
            if (!resultTemp.Success || resultTemp.Result == null)
            {
                result.Errors.AddRange(resultTemp.Errors);
                return result;
            }

            Func<IStorable, TableMemberInfoSql, Dictionary<int, IStorable>, bool> manageStorable = (currentStorable, member, oldValues) =>
            {
                if (currentStorable.Id == 0 && member.IsAutoCreate)
                {
                    List<GenericError> resultError = currentStorable.CreateWithError();
                    if (resultError.Count != 0)
                    {
                        result.Errors.AddRange(resultError);
                        return false;
                    }
                }
                else if (member.IsAutoUpdate)
                {
                    List<GenericError> resultError = currentStorable.UpdateWithError();
                    if (resultError.Count != 0)
                    {
                        result.Errors.AddRange(resultError);
                        return false;
                    }
                    if (oldValues.ContainsKey(currentStorable.Id))
                    {
                        oldValues.Remove(currentStorable.Id);
                    }
                }
                return true;
            };

            // merge into one item
            foreach (TableMemberInfoSql member in members)
            {
                if (member is ITableMemberInfoSqlLinkSingle)
                {
                    Dictionary<int, IStorable> oldValues = new Dictionary<int, IStorable>();
                    foreach (IStorable itemTemp in resultTemp.Result)
                    {
                        object? valueTemp = member.GetValue(itemTemp);
                        if (valueTemp is IStorable storableTemp && !oldValues.ContainsKey(storableTemp.Id))
                        {
                            oldValues[storableTemp.Id] = storableTemp;
                        }
                    }

                    object? currentValue = member.GetValue(item);
                    if (currentValue is IStorable currentStorable)
                    {
                        if (!manageStorable(currentStorable, member, oldValues))
                        {
                            return result;
                        }
                    }

                    if (member.IsAutoDelete)
                    {
                        foreach (KeyValuePair<int, IStorable> oldValuePair in oldValues)
                        {
                            storableToDeleted.Add(oldValuePair.Value);
                        }
                    }

                }
                else if (member is ITableMemberInfoSqlLinkMultiple)
                {
                    Dictionary<int, IStorable> oldValues = new Dictionary<int, IStorable>();
                    foreach (IStorable itemTemp in resultTemp.Result)
                    {
                        object? o = member.GetValue(itemTemp);
                        if (o is IList listLinkOld)
                        {
                            foreach (object itemLink in listLinkOld)
                            {
                                if (itemLink is IStorable storableTemp && !oldValues.ContainsKey(storableTemp.Id))
                                {
                                    oldValues[storableTemp.Id] = storableTemp;
                                }
                            }
                        }
                        else if (o is IDictionary dicoLinkOld)
                        {
                            foreach (DictionaryEntry? itemLink in dicoLinkOld)
                            {
                                if (itemLink.Value.Value is IStorable storableTemp && !oldValues.ContainsKey(storableTemp.Id))
                                {
                                    oldValues[storableTemp.Id] = storableTemp;
                                }
                            }
                        }

                    }

                    object? currentValue = member.GetValue(item);
                    if (currentValue is IList listLink)
                    {
                        foreach (object itemLink in listLink)
                        {
                            if (itemLink is IStorable currentStorable)
                            {
                                if (!manageStorable(currentStorable, member, oldValues))
                                {
                                    return result;
                                }
                            }
                        }

                    }
                    else if (currentValue is IDictionary dicoLink)
                    {
                        foreach (DictionaryEntry? itemLink in dicoLink)
                        {
                            if (itemLink.Value.Value is IStorable currentStorable)
                            {
                                if (!manageStorable(currentStorable, member, oldValues))
                                {
                                    return result;
                                }
                            }
                        }

                    }

                    if (member.IsAutoDelete)
                    {
                        foreach (KeyValuePair<int, IStorable> oldValuePair in oldValues)
                        {
                            storableToDeleted.Add(oldValuePair.Value);
                        }
                    }

                }
            }


            return result;
        }
        protected VoidWithError CheckReverseLinkBeforeUpdate<X>(List<TableReverseMemberInfo> reverseMembers, X item, List<int> listIdUpdate) where X : IStorable
        {
            listIdUpdate = listIdUpdate.ToList();
            VoidWithError result = new VoidWithError();
            foreach (TableReverseMemberInfo reverseMember in reverseMembers)
            {
                Dictionary<int, IStorable> oldList = new Dictionary<int, IStorable>();
                foreach (int id in listIdUpdate)
                {
                    ResultWithDataError<List<IStorable>> resultTemp = reverseMember.ReverseQuery(id);
                    if (resultTemp.Result != null)
                    {
                        foreach (IStorable itemTemp in resultTemp.Result)
                        {
                            if (!oldList.ContainsKey(itemTemp.Id))
                            {
                                oldList[itemTemp.Id] = itemTemp;
                            }
                        }
                    }
                }

                object? currentListO = reverseMember.GetValue(item);
                if (currentListO is IList currentList)
                {
                    foreach (IStorable itemTemp in currentList)
                    {
                        if (itemTemp.Id == 0 && reverseMember.IsAutoCreate)
                        {
                            foreach (int id in listIdUpdate)
                            {
                                itemTemp.Id = 0;
                                reverseMember.SetReverseId(itemTemp, id);
                                List<GenericError> resultTemp = itemTemp.CreateWithError();
                                if (resultTemp.Count != 0)
                                {
                                    result.Errors.AddRange(resultTemp);
                                    return result;
                                }
                            }
                        }
                        else if (oldList.ContainsKey(itemTemp.Id))
                        {
                            if (reverseMember.IsAutoUpdate)
                            {
                                List<GenericError> resultTemp = itemTemp.UpdateWithError();
                                if (resultTemp.Count != 0)
                                {
                                    result.Errors.AddRange(resultTemp);
                                    return result;
                                }
                            }
                            oldList.Remove(itemTemp.Id);
                        }
                    }
                }

                if (reverseMember.IsAutoDelete)
                {
                    foreach (KeyValuePair<int, IStorable> missing in oldList)
                    {
                        List<GenericError> resultTemp = missing.Value.DeleteWithError();
                        if (resultTemp.Count != 0)
                        {
                            result.Errors.AddRange(resultTemp);
                            return result;
                        }
                    }
                }
            }

            return result;
        }

        #endregion

        #region Delete
        protected abstract DatabaseDeleteBuilderInfo PrepareSQLForDelete<X>(DatabaseDeleteBuilder<X> deleteBuilder) where X : IStorable;
        public VoidWithError DeleteFromBuilder<X>(DatabaseDeleteBuilder<X> deleteBuilder, List<X> elementsToDelete) where X : IStorable
        {
            VoidWithError result = new();
            if (deleteBuilder.info == null)
            {
                deleteBuilder.info = PrepareSQLForDelete(deleteBuilder);
            }

            // delete n-m
            List<int> ids = elementsToDelete.Select(e => e.Id).ToList();
            foreach (KeyValuePair<string, Dictionary<string, ParamsInfo>> deleteNM in deleteBuilder.info.DeleteNM)
            {
                Dictionary<ParamsInfo, QueryParameterType> parametersDeleteNM = new();
                foreach (KeyValuePair<string, ParamsInfo> parameterInfo in deleteNM.Value)
                {
                    parameterInfo.Value.Value = ids;
                    parametersDeleteNM.Add(parameterInfo.Value, QueryParameterType.Normal);
                }
                ResultWithError<List<Dictionary<string, string?>>> deleteResultNM = QueryGeneric(StorableAction.Delete, deleteNM.Key, parametersDeleteNM);
            }

            // delete reverse
            foreach (TableReverseMemberInfo reverseMemberInfo in deleteBuilder.info.ReverseMembers)
            {
                ResultWithDataError<List<IStorable>> resultTemp = reverseMemberInfo.ReverseQuery(ids);
                if (!resultTemp.Success)
                {
                    result.Errors.AddRange(resultTemp.Errors);
                    return result;
                }

                if (resultTemp.Result == null)
                {
                    continue;
                }

                foreach (IStorable item in resultTemp.Result)
                {
                    // TODO manage update or delete : check attribute
                    if (reverseMemberInfo.reverseMember != null && reverseMemberInfo.reverseMember.IsNullable)
                    {
                        reverseMemberInfo.reverseMember?.SetValue(item, null);
                        List<GenericError> errorsTemp = item.UpdateWithError();
                        if (errorsTemp.Count > 0)
                        {
                            result.Errors.AddRange(errorsTemp);
                            return result;
                        }
                    }
                    else
                    {
                        List<GenericError> errorsTemp = item.DeleteWithError();
                        if (errorsTemp.Count > 0)
                        {
                            result.Errors.AddRange(errorsTemp);
                            return result;
                        }
                    }


                }
            }

            #region delete

            Dictionary<ParamsInfo, QueryParameterType> parametersDelete = new();
            foreach (KeyValuePair<string, ParamsInfo> parameterInfo in deleteBuilder.WhereParamsInfo)
            {
                parametersDelete.Add(parameterInfo.Value, QueryParameterType.Normal);
            }

            string sql = deleteBuilder.info.Sql;
            ResultWithError<List<Dictionary<string, string?>>> deleteResult = QueryGeneric(StorableAction.Delete, sql, parametersDelete);
            if (!deleteResult.Success)
            {
                result.Errors.AddRange(deleteResult.Errors);
                return result;
            }

            #endregion

            // auto delete 1-n


            return result;
        }


        #endregion

        #endregion

        #region Tools
        /// <summary>
        /// Order data but type
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public ResultWithError<Dictionary<TableInfo, IList>> GroupDataByType<X>(IList data)
        {
            Type typeX = typeof(X);
            if (allTableInfos.ContainsKey(typeX))
            {
                TableInfo table = allTableInfos[typeX];
                ResultWithError<Dictionary<TableInfo, IList>> result = new()
                {
                    Result = new Dictionary<TableInfo, IList>()
                };
                if (table.IsAbstract)
                {
                    Dictionary<Type, TableInfo> loadedType = new();
                    foreach (object item in data)
                    {
                        Type type = item.GetType();
                        if (!loadedType.ContainsKey(type))
                        {
                            TableInfo? tableInfo = GetTableInfo(type);
                            if (tableInfo == null)
                            {
                                result.Errors.Add(new DataError(DataErrorCode.TypeNotExistInsideStorage, "this must be impossible"));
                                return result;
                            }
                            else
                            {
                                loadedType.Add(type, tableInfo);
                                Type newListType = typeof(List<>).MakeGenericType(type);
                                IList newList = TypeTools.CreateNewObj<IList>(newListType);
                                result.Result.Add(tableInfo, newList);
                            }
                        }
                        result.Result[loadedType[type]].Add(item);
                    }
                }
                else
                {
                    result.Result.Add(table, data);
                }
                return result;
            }
            else
            {
                ResultWithError<Dictionary<TableInfo, IList>> result = new();
                result.Errors.Add(new DataError(DataErrorCode.TypeNotExistInsideStorage, "Can't find the type " + typeX + " inside the storage " + GetType().Name));
                return result;
            }


        }

        /// <summary>
        /// Run a function inside a transaction that ll be commit if no error otherwise rollback
        /// </summary>
        /// <typeparam name="Y"></typeparam>
        /// <param name="defaultValue"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public ResultWithError<Y> RunInsideTransaction<Y>(Y? defaultValue, Func<ResultWithError<Y>> action)
        {
            ResultWithError<TransactionContext> transactionResult = BeginTransaction().ToGeneric();
            if (!transactionResult.Success || transactionResult.Result == null)
            {
                ResultWithError<Y> resultError = new()
                {
                    Result = defaultValue,
                    Errors = transactionResult.Errors
                };
                return resultError;
            }
            ResultWithError<Y> resultTemp = action();
            if (resultTemp.Success)
            {
                ResultWithError<bool> commitResult = transactionResult.Result.Commit();
                resultTemp.Errors.AddRange(commitResult.Errors);
            }
            else
            {
                ResultWithError<bool> rollbackResult = transactionResult.Result.Rollback();
                resultTemp.Errors.AddRange(rollbackResult.Errors);
            }
            return resultTemp;
        }
        /// <summary>
        /// Run a function inside a transaction that ll be commit if no error otherwise rollback
        /// </summary>
        /// <typeparam name="Y"></typeparam>
        /// <param name="action"></param>
        /// <returns></returns>
        public ResultWithError<Y> RunInsideTransaction<Y>(Func<ResultWithError<Y>> action)
        {
            return RunInsideTransaction<Y>(default, action);
        }
        /// <summary>
        /// Run a function inside a transaction that ll be commit if no error otherwise rollback
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public VoidWithError RunInsideTransaction(Func<VoidWithError> action)
        {
            ResultWithError<TransactionContext> transactionResult = BeginTransaction().ToGeneric();
            if (!transactionResult.Success || transactionResult.Result == null)
            {
                VoidWithError resultError = new()
                {
                    Errors = transactionResult.Errors
                };
                return resultError;
            }
            VoidWithError resultTemp = action();
            if (resultTemp.Success)
            {
                ResultWithError<bool> commitResult = transactionResult.Result.Commit();
                if (commitResult.Result)
                {
                    transactionContext = null;
                }
                resultTemp.Errors.AddRange(commitResult.Errors);
            }
            else
            {
                ResultWithError<bool> rollbackResult = transactionResult.Result.Rollback();
                transactionContext = null;
                resultTemp.Errors.AddRange(rollbackResult.Errors);
            }
            return resultTemp;
        }


        public abstract string GetSqlColumnType(DbType dbType, TableMemberInfoSql tableMember);
        #endregion

        public override string ToString()
        {
            string result = username + "@" + host;
            if (port != null)
            {
                result += ":" + port;
            }
            result += "/" + database;
            return result;
        }
    }


    public class TransactionContext : IDisposable
    {

        public DbTransaction transaction;

        private bool isEnded = false;

        public DbConnection? Connection
        {
            get => transaction.Connection;
        }

        public int count;
        private Action<Action> _runInsideLocker;
        private Action _endTransaction;

        public TransactionContext(DbTransaction transaction, Action endTransaction, Action<Action> runInsideLocker)
        {
            this.transaction = transaction;
            _endTransaction = endTransaction;
            _runInsideLocker = runInsideLocker;
            count = 1;
            if (transaction.Connection == null) throw new Exception("Transaction without connection");
        }

        public ResultWithError<bool> Commit()
        {
            ResultWithError<bool> result = new ResultWithError<bool>();
            _runInsideLocker(() =>
            {
                if (isEnded)
                {
                    result.Result = false;
                    return;
                }
                count--;
                if (count <= 0)
                {
                    isEnded = true;
                    result = _Commit();
                    return;
                }
                result.Result = false;
            });
            return result;
        }

        private ResultWithError<bool> _Commit()
        {
            ResultWithError<bool> result = new();
            try
            {
                transaction.Commit();
                result.Result = true;
                _endTransaction();
            }
            catch (Exception e)
            {
                result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
            }
            return result;
        }


        public ResultWithError<bool> Rollback()
        {
            ResultWithError<bool> result = new ResultWithError<bool>();
            _runInsideLocker(() =>
            {
                if (isEnded)
                {
                    result.Result = false;
                    return;
                }
                isEnded = true;
                result = _Rollback();
            });
            return result;
        }

        private ResultWithError<bool> _Rollback()
        {
            ResultWithError<bool> result = new();
            try
            {
                transaction.Rollback();
                result.Result = true;
                _endTransaction();
            }
            catch (Exception e)
            {
                result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
            }
            return result;
        }

        public void Dispose()
        {
            _Rollback();
            transaction.Dispose();
        }
    }
}
