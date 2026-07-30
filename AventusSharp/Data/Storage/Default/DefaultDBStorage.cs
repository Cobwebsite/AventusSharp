using AventusSharp.Chart;
using AventusSharp.Data.Attributes;
using AventusSharp.Data.Manager;
using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Manager.DB.Builders;
using AventusSharp.Data.Migrations;
using AventusSharp.Data.Storage.Default.TableMember;
using AventusSharp.Tools;
using K4os.Compression.LZ4.Internal;
using Microsoft.Extensions.Logging;
using MySqlX.XDevAPI.Relational;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AventusSharp.Data.Storage.Default
{
    public class StorageCredentials
    {
        public string host;
        public uint? port;
        public string username;
        public string password;
        public string database;
        public bool trustServerCertificate = false;
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

    public static class DBStorage
    {
        internal static Dictionary<Type, IDBStorage> listStorage = new();

        public static T? Get<T>() where T : IDBStorage
        {
            if (listStorage.ContainsKey(typeof(T)))
            {
                return (T)listStorage[typeof(T)];
            }
            return default;
        }

        public static IDBStorage? GetFrom<T>() where T : IStorable
        {
            IGenericDM dm = GenericDM.Get<T>();
            if (dm is IDatabaseDM database)
            {
                return database.Storage;
            }
            return null;
        }

        public static List<IDBStorage> GetAll()
        {
            return listStorage.Values.ToList();
        }
    }

    public abstract class DefaultDBStorage<T> : IDBStorage where T : IDBStorage
    {
        protected string host { get => credentials.host; }
        protected uint? port { get => credentials.port; }
        protected string username { get => credentials.username; }
        protected string password { get => credentials.password; }
        protected string database { get => credentials.database; }

        protected StorageCredentials credentials;

        protected bool addCreatedAndUpdatedDate;

        private static bool IsAutomaticTimestamp(ParamsInfo parameter)
        {
            string? memberName = parameter.MembersList.LastOrDefault()?.Name;
            if (memberName == nameof(IStorableTimestamp.CreatedDate)
                || memberName == nameof(IStorableTimestamp.UpdatedDate))
            {
                return true;
            }

            return Regex.IsMatch(
                parameter.Name,
                "(^|[._])(?:CreatedDate|UpdatedDate)$");
        }

        protected virtual DateTime GetCurrentDateTime() => DateTime.Now;
        private bool linksCreated;
        private AsyncLocal<DbTransactionContext?> _transactionScope = new();
        private DbTransactionContext? transactionScope
        {
            get => _transactionScope.Value;
            set => _transactionScope.Value = value;
        }
        public bool IsConnectedOneTime { get; protected set; }
        public bool Debug { get; set; }

        [StringSyntax(StringSyntaxAttribute.DateTimeFormat)]
        public string? DateTimeFormat { get; set; }
        public bool ReadOnly { get; set; }


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
            credentials = info;
            addCreatedAndUpdatedDate = info.addCreatedAndUpdatedDate;
            if (!DBStorage.listStorage.ContainsKey(GetType()))
            {
                DBStorage.listStorage.Add(GetType(), this);
            }
        }

        #region connection
        public async Task<bool> Connect()
        {
            return (await ConnectWithError()).Success;
        }
        public virtual async Task<VoidWithError> ConnectWithError()
        {
            VoidWithError result = new();
            try
            {
                using DbConnection connection = GetConnection();
                await connection.OpenAsync();
                IsConnectedOneTime = true;
            }
            catch (Exception e)
            {
                result.Errors.Add(new DataError(DataErrorCode.UnknownError, e));
            }
            return result;
        }

        public abstract Task<ResultWithError<bool>> ResetStorage();
        public abstract DbConnection GetConnection();
        public abstract ResultWithDataError<DbCommand> CreateCmd(string sql);
        public abstract DbParameter GetDbParameter();
        public async Task Close()
        {
            try
            {
                if (transactionScope != null)
                {
                    await transactionScope.Rollback();
                    await transactionScope.DisposeAsync();
                }
            }
            catch (Exception e)
            {
                new DataError(DataErrorCode.UnknownError, e.Message).Print();
            }
        }

        public async Task<VoidWithError> Execute(string sql, [CallerFilePath] string callerPath = "", [CallerLineNumber] int callerNo = 0)
        {
            ResultWithDataError<DbCommand> commandResult = CreateCmd(sql);
            if (commandResult.Result != null)
            {
                VoidWithError result = await Execute(commandResult.Result, dataParameters: null, callerPath, callerNo);
                commandResult.Result.Dispose();
                return result;
            }
            VoidWithError noCommand = new();
            noCommand.Errors.AddRange(commandResult.Errors);
            return noCommand;
        }

        public Task<VoidWithError> Execute(DbCommand command, Dictionary<string, object?> parameters, [CallerFilePath] string callerPath = "", [CallerLineNumber] int callerNo = 0)
        {
            return Execute(command, new List<Dictionary<string, object?>>() { parameters }, callerPath, callerNo);
        }
        public async Task<VoidWithError> Execute(DbCommand command, List<Dictionary<string, object?>>? dataParameters, [CallerFilePath] string callerPath = "", [CallerLineNumber] int callerNo = 0)
        {
            VoidWithError result = new();
            if (ReadOnly && !command.CommandText.ToLower().StartsWith("select"))
            {
                result.Errors.Add(new DataError(DataErrorCode.IsReadOnly, "Can't execute the command " + command.CommandText + " because the connection is readonly"));
                return result;
            }
            try
            {

                if (transactionScope == null)
                {
                    return await RunInsideTransaction(async () =>
                    {
                        return await Execute(command, dataParameters, callerPath, callerNo);
                    });
                }

                DbConnection? connection = transactionScope.Connection;
                if (connection == null)
                {
                    result.Errors.Add(new DataError(DataErrorCode.NoConnectionInsideStorage, "The storage " + GetType().Name, " doesn't have a connection"));
                    return result;
                }


                try
                {
                    command.Transaction = transactionScope.transaction;
                    command.Connection = connection;
                    try
                    {
                        if (dataParameters != null)
                        {
                            foreach (Dictionary<string, object?> parameters in dataParameters)
                            {
                                VoidWithError parameterResult = ApplyCommandParameters(command, parameters);
                                result.Errors.AddRange(parameterResult.Errors);
                                if (!parameterResult.Success)
                                    return result;

                                printCommand(command.CommandText, parameters);
                                await command.ExecuteNonQueryAsync();
                            }
                        }
                        else
                        {
                            printCommand(command.CommandText);
                            await command.ExecuteNonQueryAsync();
                        }
                    }
                    catch (Exception e)
                    {
                        result.Errors.Add(new DataError(DataErrorCode.UnknownError, e.Message, callerPath, callerNo));
                    }
                }
                catch (Exception e)
                {
                    result.Errors.Add(new DataError(DataErrorCode.UnknownError, e.Message, callerPath, callerNo));
                }
                finally
                {
                }
            }
            catch (Exception e)
            {
                result.Errors.Add(new DataError(DataErrorCode.UnknownError, e));
            }
            return result;
        }

        public async Task<VoidWithError> ExecuteNoTransaction(string sql, [CallerFilePath] string callerPath = "", [CallerLineNumber] int callerNo = 0)
        {
            ResultWithDataError<DbCommand> commandResult = CreateCmd(sql);
            if (commandResult.Result != null)
            {
                VoidWithError result = await ExecuteNoTransaction(commandResult.Result, dataParameters: null, callerPath, callerNo);
                commandResult.Result.Dispose();
                return result;
            }
            VoidWithError noCommand = new();
            noCommand.Errors.AddRange(commandResult.Errors);
            return noCommand;
        }

        public Task<VoidWithError> ExecuteNoTransaction(DbCommand command, Dictionary<string, object?> parameters, [CallerFilePath] string callerPath = "", [CallerLineNumber] int callerNo = 0)
        {
            return ExecuteNoTransaction(command, new List<Dictionary<string, object?>>() { parameters }, callerPath, callerNo);
        }
        public async Task<VoidWithError> ExecuteNoTransaction(DbCommand command, List<Dictionary<string, object?>>? dataParameters, [CallerFilePath] string callerPath = "", [CallerLineNumber] int callerNo = 0)
        {
            VoidWithError result = new();
            if (ReadOnly && !command.CommandText.ToLower().StartsWith("select"))
            {
                result.Errors.Add(new DataError(DataErrorCode.IsReadOnly, "Can't execute the command " + command.CommandText + " because the connection is readonly"));
                return result;
            }
            try
            {
                DbConnection connection = GetConnection();
                await connection.OpenAsync();

                try
                {
                    command.Connection = connection;
                    try
                    {
                        if (dataParameters != null)
                        {
                            foreach (Dictionary<string, object?> parameters in dataParameters)
                            {
                                VoidWithError parameterResult = ApplyCommandParameters(command, parameters);
                                result.Errors.AddRange(parameterResult.Errors);
                                if (!parameterResult.Success)
                                    return result;

                                printCommand(command.CommandText, parameters);
                                await command.ExecuteNonQueryAsync();
                            }
                        }
                        else
                        {
                            printCommand(command.CommandText);
                            await command.ExecuteNonQueryAsync();
                        }
                    }
                    catch (Exception e)
                    {
                        result.Errors.Add(new DataError(DataErrorCode.UnknownError, e.Message, callerPath, callerNo));
                    }
                }
                catch (Exception e)
                {
                    result.Errors.Add(new DataError(DataErrorCode.UnknownError, e.Message, callerPath, callerNo));
                }
                finally
                {
                    await connection.CloseAsync();
                }
            }
            catch (Exception e)
            {
                result.Errors.Add(new DataError(DataErrorCode.UnknownError, e));
            }
            return result;
        }

        public async Task<ResultWithError<List<X>>> Query<X>(string sql, [CallerFilePath] string callerPath = "", [CallerLineNumber] int callerNo = 0)
        {
            ResultWithDataError<DbCommand> commandResult = CreateCmd(sql);
            if (commandResult.Result != null)
            {
                ResultWithError<List<X>> result = await Query<X>(commandResult.Result, null, callerPath, callerNo);
                commandResult.Result.Dispose();
                return result;
            }
            ResultWithError<List<X>> noCommand = new();
            noCommand.Errors.AddRange(commandResult.Errors);
            return noCommand;
        }
        public async Task<ResultWithError<List<X>>> Query<X>(DbCommand command, List<Dictionary<string, object?>>? dataParameters, [CallerFilePath] string callerPath = "", [CallerLineNumber] int callerNo = 0)
        {
            ResultWithError<List<X>> result = new();
            var dico = result.ExtractAsync(() => Query(command, dataParameters, callerPath, callerNo));

            if (dico != null)
            {

            }

            return result;

        }
        public async Task<ResultWithError<List<Dictionary<string, string?>>>> Query(string sql, [CallerFilePath] string callerPath = "", [CallerLineNumber] int callerNo = 0)
        {
            ResultWithDataError<DbCommand> commandResult = CreateCmd(sql);
            if (commandResult.Result != null)
            {
                ResultWithError<List<Dictionary<string, string?>>> result = await Query(commandResult.Result, null, callerPath, callerNo);
                commandResult.Result.Dispose();
                return result;
            }
            ResultWithError<List<Dictionary<string, string?>>> noCommand = new();
            noCommand.Errors.AddRange(commandResult.Errors);
            return noCommand;
        }
        public async Task<ResultWithError<List<Dictionary<string, string?>>>> Query(DbCommand command, List<Dictionary<string, object?>>? dataParameters, [CallerFilePath] string callerPath = "", [CallerLineNumber] int callerNo = 0)
        {
            ResultWithError<List<Dictionary<string, string?>>> result = new()
            {
                Result = new List<Dictionary<string, string?>>()
            };
            await result.RunAsync(() => QueryStream(command, dataParameters, (line) =>
            {
                result.Result.Add(line);
                return Task.FromResult(new VoidWithError());
            }));
            return result;
        }

        public async Task<VoidWithError> QueryStream(string sql, Func<Dictionary<string, string?>, Task<VoidWithError>> action, [CallerFilePath] string callerPath = "", [CallerLineNumber] int callerNo = 0)
        {
            ResultWithDataError<DbCommand> commandResult = CreateCmd(sql);
            if (commandResult.Result != null)
            {
                VoidWithError result = await QueryStream(commandResult.Result, null, action, callerPath, callerNo);
                commandResult.Result.Dispose();
                return result;
            }
            VoidWithError noCommand = new();
            noCommand.Errors.AddRange(commandResult.Errors);
            return noCommand;
        }
        public async Task<VoidWithError> QueryStream(DbCommand command, List<Dictionary<string, object?>>? dataParameters, Func<Dictionary<string, string?>, Task<VoidWithError>> action, [CallerFilePath] string callerPath = "", [CallerLineNumber] int callerNo = 0)
        {
            VoidWithError result = new();
            try
            {
                if (transactionScope == null)
                {
                    return await RunInsideTransaction(async () =>
                    {
                        return await QueryStream(command, dataParameters, action, callerPath, callerNo);
                    });
                }

                DbConnection? connection = transactionScope.Connection;
                if (connection == null)
                {
                    result.Errors.Add(new DataError(DataErrorCode.NoConnectionInsideStorage, "The storage " + GetType().Name, " doesn't have a connection"));
                    return result;
                }

                try
                {
                    command.Transaction = transactionScope.transaction;
                    command.Connection = connection;
                    if (dataParameters != null)
                    {
                        foreach (Dictionary<string, object?> parameters in dataParameters)
                        {
                            VoidWithError parameterResult = ApplyCommandParameters(command, parameters);
                            result.Errors.AddRange(parameterResult.Errors);
                            if (!parameterResult.Success)
                                return result;

                            printCommand(command.CommandText, parameters);

                            using (IDataReader reader = await command.ExecuteReaderAsync())
                            {
                                while (reader.Read())
                                {
                                    Dictionary<string, string?> temp = new();
                                    for (int i = 0; i < reader.FieldCount; i++)
                                    {
                                        if (!temp.ContainsKey(reader.GetName(i)))
                                        {
                                            if (!reader.IsDBNull(i))
                                            {
                                                string? valueString = reader.GetValue(i).ToString();
                                                valueString ??= "";
                                                temp.Add(reader.GetName(i), valueString);
                                            }
                                            else
                                            {
                                                temp.Add(reader.GetName(i), null);
                                            }
                                        }
                                    }
                                    await result.RunAsync(() => action(temp));
                                }
                                reader.Close();
                                reader.Dispose();
                            }
                        }
                    }
                    else
                    {
                        printCommand(command.CommandText, null);
                        using (IDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (reader.Read())
                            {
                                Dictionary<string, string?> temp = new();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    if (!temp.ContainsKey(reader.GetName(i)))
                                    {
                                        if (!reader.IsDBNull(i))
                                        {
                                            string? valueString = reader.GetValue(i).ToString();
                                            valueString ??= "";
                                            temp.Add(reader.GetName(i), valueString);
                                        }
                                        else
                                        {
                                            temp.Add(reader.GetName(i), null);
                                        }
                                    }
                                }
                                await result.RunAsync(() => action(temp));
                            }
                            reader.Close();
                            reader.Dispose();
                        }
                    }
                }
                catch (Exception e)
                {
                    DataError error = new DataError(DataErrorCode.UnknownError, e.Message + "\nSQL: " + command.CommandText);
                    error.Details.Add(command.CommandText);
                    result.Errors.Add(error);
                }
            }
            catch (Exception e)
            {
                result.Errors.Add(new DataError(DataErrorCode.UnknownError, e.Message + "\nSQL: " + command.CommandText, callerPath, callerNo));
            }

            return result;
        }

        private static VoidWithError ApplyCommandParameters(DbCommand command, Dictionary<string, object?> values)
        {
            VoidWithError result = new();
            foreach (DbParameter parameter in command.Parameters)
            {
                if (!values.ContainsKey(parameter.ParameterName))
                {
                    result.Errors.Add(new DataError(DataErrorCode.ValidationError, $"The parameter {parameter.ParameterName} is missing"));
                }
            }
            foreach (string name in values.Keys)
            {
                if (!command.Parameters.Contains(name))
                {
                    result.Errors.Add(new DataError(DataErrorCode.ValidationError, $"The parameter {name} is not defined in the command"));
                }
            }
            if (!result.Success)
                return result;

            foreach (KeyValuePair<string, object?> value in values)
            {
                command.Parameters[value.Key].Value = value.Value ?? DBNull.Value;
            }
            return result;
        }


        public async Task<ResultWithError<DbTransactionContext>> BeginTransaction()
        {
            ResultWithError<DbTransactionContext> result = new();

            try
            {

                if (transactionScope == null)
                {
                    DbConnection connection = GetConnection();
                    try
                    {
                        await connection.OpenAsync();
                    }
                    catch
                    {
                        result.Errors.Add(new DataError(DataErrorCode.StorageDisconnected, "The storage " + GetType().Name + "(" + ToString() + ") can't connect to the database"));
                        return result;
                    }
                    DbTransaction transaction = await connection.BeginTransactionAsync();
                    result.Result = new DbTransactionContext(transaction, EndTransaction);
                }
                else
                {
                    transactionScope.count++;
                    result.Result = transactionScope;
                }
            }
            catch (Exception e)
            {
                result.Errors.Add(new DataError(DataErrorCode.UnknownError, e));
            }


            return result;
        }

        protected async Task EndTransaction()
        {
            if (transactionScope != null)
            {
                DbConnection? connection = transactionScope.Connection;
                await transactionScope.transaction.DisposeAsync();
                if (connection != null)
                {
                    await connection.DisposeAsync();
                }
            }
        }

        public TransactionContext? getTransactionScope() => transactionScope;
        public void setTransactionScope(TransactionContext? context)
        {
            if (context == null)
                transactionScope = null;
            else if (context is DbTransactionContext dbTransactionContext)
                transactionScope = dbTransactionContext;

        }


        private void printCommand(string queryWithParam, Dictionary<string, object?>? parameters = null)
        {
            if (Debug)
            {
                if (parameters != null)
                {
                    foreach (KeyValuePair<string, object?> parameter in parameters)
                    {
                        queryWithParam = queryWithParam.Replace(parameter.Key, parameter.Key + "(" + parameter.Value?.ToString() + ")");
                    }
                }
                AventusLogger.Instance.LogInformation(queryWithParam);

            }
        }
        #endregion

        #region init
        public IMigrationProvider GetMigrationProvider()
        {
            return MigrationFactory.Register(DefineMigrationProvider());
        }
        protected abstract IMigrationProvider DefineMigrationProvider();
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
                        if (memberInfo.IsPrimary && classInfo.Primary == null)
                        {
                            classInfo.Primary = memberInfo;
                        }
                    }
                    classInfo.AddMembersFirst(membersToAdd);
                    if (addCreatedAndUpdatedDate)
                    {
                        if (createdDate != null)
                        {
                            classInfo.AddMember(createdDate);
                        }
                        if (updatedDate != null)
                        {
                            classInfo.AddMember(updatedDate);
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
                        classInfo.AddMemberFirst(parentLink);
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

        protected async Task<VoidWithError> PrepareGeneric(StorableAction action, string sql, Dictionary<ParamsInfo, QueryParameterType> parameters, IStorable? item, Func<DbCommand, List<Dictionary<string, object?>>, Task<VoidWithError>> run)
        {
            VoidWithError result = new();
            List<GenericError> errors = new();

            if (item != null)
            {
                errors.AddRange(item.IsValid(action));
            }
            if (errors.Count > 0)
            {
                foreach (DataError error in errors)
                {
                    result.Errors.Add(error);
                }
                return result;
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
                    string replacement = paramNames.Count == 0
                        ? "(NULL)"
                        : "(" + string.Join(",", paramNames) + ")";
                    sqlToExecute = sqlToExecute.Replace(
                        "@" + parameterInfo.Key.Name,
                        replacement);
                }
                else
                {
                    parametersToUse.Add(parameterInfo.Key, parameterInfo.Value);
                }
            }
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
                    if (IsAutomaticTimestamp(parameterInfo.Key))
                    {
                        parameterInfo.Key.Value = GetCurrentDateTime();
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
                    errors.AddRange(await parameterInfo.Key.IsValueValid(action));

                }
                parametersValue["@" + parameterInfo.Key.Name] = TransformValueForFct(parameterInfo.Key);
            }
            if (errors.Count > 0)
            {
                foreach (DataError error in errors)
                {
                    result.Errors.Add(error);
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
                        foreach (object? o in enumerable.Cast<object?>().Distinct())
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

                await run(cmd, parametersFinal);
            }
            cmd.Dispose();
            return result;

        }

        protected async Task<ResultWithError<List<Dictionary<string, string?>>>> QueryGeneric(StorableAction action, string sql, Dictionary<ParamsInfo, QueryParameterType> parameters, IStorable? item = null)
        {
            ResultWithError<List<Dictionary<string, string?>>> result = new ResultWithError<List<Dictionary<string, string?>>>();
            await result.RunAsync(() =>
                PrepareGeneric(action, sql, parameters, item, async (cmd, parametersFinal) =>
                {
                    result = await Query(cmd, parametersFinal);
                    return new VoidWithError();
                })
            );
            return result;
        }
        protected async Task<VoidWithError> QueryStreamGeneric(StorableAction action, string sql, Dictionary<ParamsInfo, QueryParameterType> parameters, IStorable? item, Func<Dictionary<string, string?>, Task<VoidWithError>> transform)
        {
            VoidWithError result = new VoidWithError();
            VoidWithError prepareResult = await PrepareGeneric(action, sql, parameters, item, async (cmd, parametersFinal) =>
            {
                result = await QueryStream(cmd, parametersFinal, transform);
                return new VoidWithError();
            });
            if (!prepareResult.Success)
            {
                result.Errors.AddRange(prepareResult.Errors);
            }
            return result;
        }
        // protected async Task<ResultWithError<List<Dictionary<string, string?>>>> QueryGeneric(StorableAction action, string sql, Dictionary<ParamsInfo, QueryParameterType> parameters, IStorable? item = null)
        // {
        //     List<GenericError> errors = new();

        //     if (item != null)
        //     {
        //         errors.AddRange(item.IsValid(action));
        //     }
        //     if (errors.Count > 0)
        //     {
        //         ResultWithError<List<Dictionary<string, string?>>> queryResultTemp = new()
        //         {
        //             Result = new List<Dictionary<string, string?>>()
        //         };
        //         foreach (DataError error in errors)
        //         {
        //             queryResultTemp.Errors.Add(error);
        //         }
        //         return queryResultTemp;
        //     }

        //     string sqlToExecute = sql;
        //     Dictionary<ParamsInfo, QueryParameterType> parametersToUse = new();
        //     // check if parameters list
        //     foreach (KeyValuePair<ParamsInfo, QueryParameterType> parameterInfo in parameters)
        //     {
        //         if (parameterInfo.Key.Value is IList list)
        //         {
        //             List<string> paramNames = new();
        //             for (int i = 0; i < list.Count; i++)
        //             {
        //                 paramNames.Add("@" + parameterInfo.Key.Name + "_" + i);

        //                 parametersToUse.Add(new ParamsInfo()
        //                 {
        //                     DbType = parameterInfo.Key.DbType,
        //                     MembersList = parameterInfo.Key.MembersList,
        //                     Name = parameterInfo.Key.Name + "_" + i,
        //                     TypeLvl0 = parameterInfo.Key.TypeLvl0,
        //                     Value = list[i],
        //                 }, parameterInfo.Value);
        //             }
        //             sqlToExecute = sqlToExecute.Replace("@" + parameterInfo.Key.Name, "(" + string.Join(",", paramNames) + ")");
        //         }
        //         else
        //         {
        //             parametersToUse.Add(parameterInfo.Key, parameterInfo.Value);
        //         }
        //     }
        //     ResultWithError<List<Dictionary<string, string?>>> result = new();
        //     ResultWithDataError<DbCommand> cmdResult = CreateCmd(sqlToExecute);
        //     result.Errors.AddRange(cmdResult.Errors);
        //     if (!result.Success || cmdResult.Result == null)
        //     {
        //         return result;
        //     }
        //     DbCommand cmd = cmdResult.Result;
        //     Dictionary<string, object?> parametersValue = new();
        //     foreach (KeyValuePair<ParamsInfo, QueryParameterType> parameterInfo in parametersToUse)
        //     {
        //         DbParameter parameter = GetDbParameter();
        //         parameter.ParameterName = "@" + parameterInfo.Key.Name;
        //         parameter.DbType = parameterInfo.Key.DbType;
        //         cmd.Parameters.Add(parameter);
        //         if (parameterInfo.Value == QueryParameterType.GrabValue)
        //         {
        //             if (Regex.IsMatch(parameterInfo.Key.Name, "(^|\\.)UpdatedDate$") || Regex.IsMatch(parameterInfo.Key.Name, "(^|\\.)CreatedDate$"))
        //             {
        //                 parameterInfo.Key.Value = DateTime.Now;
        //                 if (item != null)
        //                 {
        //                     parameterInfo.Key.SetCurrentValueOnObject(item);
        //                 }
        //             }
        //             else if (item != null)
        //             {
        //                 parameterInfo.Key.TypeLvl0 = item.GetType();
        //                 parameterInfo.Key.SetValue(item);
        //             }
        //             else
        //             {
        //                 parameterInfo.Key.Value = null;
        //             }
        //             errors.AddRange(await parameterInfo.Key.IsValueValid(action));

        //         }
        //         parametersValue["@" + parameterInfo.Key.Name] = TransformValueForFct(parameterInfo.Key);
        //     }
        //     ResultWithError<List<Dictionary<string, string?>>> queryResult;
        //     if (errors.Count > 0)
        //     {
        //         queryResult = new ResultWithError<List<Dictionary<string, string?>>>
        //         {
        //             Result = new List<Dictionary<string, string?>>()
        //         };
        //         foreach (DataError error in errors)
        //         {
        //             queryResult.Errors.Add(error);
        //         }
        //     }
        //     else
        //     {
        //         //write all combinaisons if one of the parameter is a list
        //         List<Dictionary<string, object?>> parametersFinal = new();

        //         Action<int, Dictionary<string, object?>> combinaisons = (int i, Dictionary<string, object?> current) => { };
        //         combinaisons = (int i, Dictionary<string, object?> current) =>
        //         {
        //             if (i == parametersValue.Count)
        //             {
        //                 parametersFinal.Add(current);
        //                 return;
        //             }
        //             KeyValuePair<string, object?> parameterValue = parametersValue.ElementAt(i);

        //             if (parameterValue.Value is IList enumerable)
        //             {
        //                 foreach (object o in enumerable)
        //                 {
        //                     Dictionary<string, object?> clone = current.ToDictionary(t => t.Key, t => t.Value);
        //                     clone.Add(parameterValue.Key, o);
        //                     combinaisons(i + 1, clone);
        //                 }
        //             }
        //             else
        //             {
        //                 current.Add(parameterValue.Key, parameterValue.Value);
        //                 combinaisons(i + 1, current);
        //             }
        //         };

        //         combinaisons(0, new());

        //         queryResult = await Query(cmd, parametersFinal);
        //     }
        //     cmd.Dispose();
        //     return queryResult;

        // }


        protected async Task<VoidWithError> BulkQueryGeneric(StorableAction action, string sql, Dictionary<ParamsInfo, QueryParameterType> parameters, List<IStorable> items)
        {
            List<GenericError> errors = new();

            string sqlToExecute = sql;

            VoidWithError result = new();
            ResultWithDataError<DbCommand> cmdResult = CreateCmd(sqlToExecute);
            result.Errors.AddRange(cmdResult.Errors);
            if (!result.Success || cmdResult.Result == null)
            {
                return result;
            }
            DbCommand cmd = cmdResult.Result;
            Dictionary<string, object?> parametersValue = new();
            foreach (KeyValuePair<ParamsInfo, QueryParameterType> parameterInfo in parameters)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    IStorable item = items[i];
                    DbParameter parameter = GetDbParameter();
                    parameter.ParameterName = "@" + parameterInfo.Key.Name + "__" + i;
                    parameter.DbType = parameterInfo.Key.DbType;
                    cmd.Parameters.Add(parameter);
                    if (parameterInfo.Value == QueryParameterType.GrabValue)
                    {
                        if (IsAutomaticTimestamp(parameterInfo.Key))
                        {
                            parameterInfo.Key.Value = GetCurrentDateTime();
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
                        errors.AddRange(await parameterInfo.Key.IsValueValid(action));

                    }
                    parametersValue["@" + parameterInfo.Key.Name + "__" + i] = TransformValueForFct(parameterInfo.Key);
                }

            }
            VoidWithError queryResult;
            if (errors.Count > 0)
            {
                queryResult = new VoidWithError();
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

                    current.Add(parameterValue.Key, parameterValue.Value);
                    combinaisons(i + 1, current);
                };

                combinaisons(0, new());

                queryResult = await Execute(cmd, parametersFinal);
            }
            cmd.Dispose();
            return queryResult;

        }

        #region Get
        protected abstract DatabaseQueryBuilderInfo PrepareSQLForQuery<X>(DatabaseQueryBuilder<X> queryBuilder) where X : IStorable;
        public async Task<ResultWithError<List<X>>> QueryFromBuilder<X>(DatabaseQueryBuilder<X> queryBuilder) where X : IStorable
        {
            ResultWithError<List<X>> result = new();

            if (queryBuilder.info == null)
            {
                queryBuilder.info = PrepareSQLForQuery(queryBuilder);
            }
            string sql = queryBuilder.info.Sql;

            ResultWithError<List<Dictionary<string, string?>>> queryResult = await QueryGeneric(StorableAction.Read, sql, queryBuilder.WhereParamsInfo.ToDictionary(p => p.Value, p => QueryParameterType.Normal));
            result.Errors.AddRange(queryResult.Errors);
            if (queryResult.Success && queryResult.Result != null)
            {
                result.Result = new List<X>();
                DatabaseBuilderInfo baseInfo = queryBuilder.InfoByPath[""];

                for (int i = 0; i < queryResult.Result.Count; i++)
                {
                    Dictionary<string, string?> itemFields = queryResult.Result[i];
                    ResultWithError<object> resultTemp = await CreateObject(baseInfo, itemFields, false);
                    if (resultTemp.Success && resultTemp.Result != null)
                    {
                        if (resultTemp.Result is X oCasted)
                        {
                            await queryBuilder.DM.OnItemLoaded(oCasted);
                            result.Result.Add(oCasted);
                        }
                        else
                        {
                            result.Errors.Add(new DataError(DataErrorCode.UnknownError, "Impossible to cast " + resultTemp.Result.GetType().Name + " into " + typeof(X).Name));
                        }
                    }
                    else
                    {
                        result.Errors.AddRange(resultTemp.Errors);
                    }

                }

                foreach (var subquery in queryBuilder.SubQueries)
                {
                    await result.RunAsync(() => subquery.Value.Run(result.Result));
                }
            }

            return result;
        }
        public async Task<VoidWithError> QueryStreamFromBuilder<X>(DatabaseQueryBuilder<X> queryBuilder, Func<X, Task<VoidWithError>> action) where X : IStorable
        {

            if (queryBuilder.info == null)
            {
                queryBuilder.info = PrepareSQLForQuery(queryBuilder);
            }
            string sql = queryBuilder.info.Sql;

            DatabaseBuilderInfo baseInfo = queryBuilder.InfoByPath[""];
            VoidWithError queryResult = await QueryStreamGeneric(StorableAction.Read, sql, queryBuilder.WhereParamsInfo.ToDictionary(p => p.Value, p => QueryParameterType.Normal), null, async (ligne) =>
            {
                VoidWithError result = new VoidWithError();
                object? objectTemp = await result.ExtractAsync(() => CreateObject(baseInfo, ligne, false));
                if (objectTemp != null)
                {
                    if (objectTemp is X oCasted)
                    {
                        await result.RunAsync(() => action(oCasted));
                    }
                    else
                    {
                        result.Errors.Add(new DataError(DataErrorCode.UnknownError, "Impossible to cast " + objectTemp.GetType().Name + " into " + typeof(X).Name));
                    }
                }
                return result;
            });
            return queryResult;
        }

        protected async Task<ResultWithError<object>> CreateObject(DatabaseBuilderInfo info, Dictionary<string, string?> itemFields, bool allowNull)
        {
            ResultWithError<object> result = new ResultWithError<object>();
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

            bool hasValue = false;
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
                        if (itemFields[key] != null)
                            hasValue = true;
                        memberInfo.ApplySqlValue(o, itemFields[key]);
                    }
                    else if (memberInfo is TableMemberInfoSql1N memberInfo1N)
                    {
                        if (!string.IsNullOrEmpty(itemFields[key]))
                        {
                            if (member.Value.UseDM)
                            {
                                string idValue = itemFields[key] ?? string.Empty;
                                IGenericDM? dm = memberInfo1N.TableLinked?.DM;
                                if (dm != null)
                                {
                                    object? oTemp = await dm.GetById(int.Parse(idValue));
                                    if (oTemp != null)
                                        hasValue = true;
                                    memberInfo.SetValue(o, oTemp);
                                }

                            }
                            else if (info.joins.ContainsKey(memberInfo))
                            {
                                // loaded from the query
                                ResultWithError<object> oTemp = await CreateObject(info.joins[memberInfo], itemFields, memberInfo.IsNullable);
                                if (oTemp.Success)
                                {
                                    if (oTemp.Result != null)
                                    {
                                        memberInfo.SetValue(o, oTemp.Result);
                                        hasValue = true;
                                    }
                                    else if (memberInfo.IsNullable)
                                        memberInfo.SetValue(o, null);
                                    else
                                        result.Errors.Add(new DataError(DataErrorCode.WrongType, "The property " + memberInfo.Name + " is not null but receiving a null from the db"));
                                }
                                else
                                {
                                    result.Errors.AddRange(oTemp.Errors);
                                }
                            }
                            else
                            {
                                result.Errors.Add(new DataError(DataErrorCode.UnknownError, "impossible?"));
                            }
                        }
                    }
                    else if (memberInfo is TableMemberInfoSqlNMInt tableMemberInfoSqlNMInt)
                    {
                        if (itemFields[key] != null)
                            hasValue = true;
                        tableMemberInfoSqlNMInt.ApplySqlValue(o, itemFields[key]);
                    }
                    else if (memberInfo is TableMemberInfoSqlNM tableMemberInfoSqlNM)
                    {
                        if (itemFields[key] != null)
                            hasValue = true;
                        tableMemberInfoSqlNM.ApplySqlValue(o, itemFields[key]);
                    }
                }
            }

            if (!hasValue && allowNull)
            {
                result.Result = null;
                return result;
            }

            result.Result = o;
            return result;
        }
        public void LoadAllTableFieldsQuery<X>(TableInfo tableInfo, string alias, DatabaseBuilderInfo baseInfo, List<string> path, List<Type> types, DatabaseGenericBuilder<X> queryBuilder) where X : IStorable
        {
            bool useShort = false;
            if (queryBuilder is DatabaseQueryBuilder<X> builder && builder.UseShortObject)
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
                    baseInfo.Members[tableInfo.TypeMember] = new DatabaseBuilderInfoMember(tableInfo.TypeMember, alias, this);
                }
                if (tableInfo.Primary != null)
                {
                    baseInfo.Members[tableInfo.Primary] = new DatabaseBuilderInfoMember(tableInfo.Primary, alias, this);
                }
            }
            else
            {
                foreach (TableMemberInfoSql member in tableInfo.Members)
                {
                    if (!member.IsAutoRead && !queryBuilder.Included.Contains(member))
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

                                List<LambdaStep> steps = LambdaStep.Create(path, types);
                                queryBuilder.LambdaInclude(steps, null, true);
                                path.RemoveAt(path.Count - 1);
                                types.RemoveAt(types.Count - 1);
                            }
                        }
                        else
                        {
                            baseInfo.Members[member] = info;
                        }
                    }
                    else
                    {
                        baseInfo.Members[member] = new DatabaseBuilderInfoMember(member, alias, this);
                    }
                }
            }

            // Reverse AutoRead expressions are rooted on X. For a joined/nested table,
            // injecting one here would build (for example) `x => x.Lamps` even though
            // Lamps belongs to x.Room. A reverse link on that related object must be
            // loaded from the related object itself.
            if (path.Count == 0)
            {
                foreach (TableReverseMemberInfo member in tableInfo.ReverseMembers)
                {
                    if (member.IsAutoRead)
                    {
                        if (queryBuilder is IQueryBuilder<X> qb)
                        {
                            ParameterExpression argParam = Expression.Parameter(typeof(X), "t");
                            Expression nameProperty = Expression.PropertyOrField(argParam, member.Name);
                            LambdaExpression lambda3 = Expression.Lambda(nameProperty, argParam);
                            qb.Include(lambda3);
                        }
                        baseInfo.ReverseLinks.Add(member);
                    }
                }
            }
        }
        #endregion


        #region Exist
        protected abstract DatabaseExistBuilderInfo PrepareSQLForExist<X>(DatabaseExistBuilder<X> queryBuilder) where X : IStorable;
        public async Task<ResultWithError<bool>> ExistFromBuilder<X>(DatabaseExistBuilder<X> queryBuilder) where X : IStorable
        {
            ResultWithError<bool> result = new();

            if (queryBuilder.info == null)
            {
                queryBuilder.info = PrepareSQLForExist(queryBuilder);
            }
            string sql = queryBuilder.info.Sql;

            ResultWithError<List<Dictionary<string, string?>>> queryResult = await QueryGeneric(StorableAction.Read, sql, queryBuilder.WhereParamsInfo.ToDictionary(p => p.Value, p => QueryParameterType.Normal));

            result.Errors.AddRange(queryResult.Errors);
            if (queryResult.Success && queryResult.Result != null && queryResult.Result.Count > 0 && queryResult.Result[0].ContainsKey("nb"))
            {
                result.Result = int.Parse(queryResult.Result[0]["nb"] ?? "0") > 0;
            }
            return result;
        }

        #endregion

        #region Table
        protected abstract List<string> PrepareSQLCreateTable(TableInfo table);
        protected abstract string PrepareSQLCreateIntermediateTable(TableMemberInfoSql tableMember);
        public async Task<VoidWithError> CreateTable(PyramidInfo pyramid, bool force)
        {
            VoidWithError result = new();
            if (!pyramid.isForceInherit)
            {
                if (force || pyramid.type.GetCustomAttribute<CreateTable>() != null)
                {
                    if (allTableInfos.ContainsKey(pyramid.type))
                    {
                        VoidWithError resultTemp = await CreateTable(allTableInfos[pyramid.type]);
                        result.Errors.AddRange(resultTemp.Errors);
                    }
                    else
                    {
                        result.Errors.Add(new DataError(DataErrorCode.TypeNotExistInsideStorage, "Can't find the type " + pyramid.type));
                    }
                }
            }
            else
            {
                foreach (PyramidInfo child in pyramid.children)
                {
                    VoidWithError resultTemp = await CreateTable(child, force);
                    result.Errors.AddRange(resultTemp.Errors);
                }
            }
            return result;
        }
        public async Task<VoidWithError> CreateTable(TableInfo table)
        {
            VoidWithError result = new();
            ResultWithError<bool> tableExist = await TableExist(table);
            result.Errors.AddRange(tableExist.Errors);

            if (tableExist.Success && !tableExist.Result)
            {
                List<string> sqls = PrepareSQLCreateTable(table);
                foreach (string sql in sqls)
                {
                    await result.RunAsync(() => Execute(sql));
                }

                // create intermediate table
                List<TableMemberInfoSql> members = table.Members.Where
                    (f => f is TableMemberInfoSqlNM || f is TableMemberInfoSqlNMInt).ToList();

                string? intermediateQuery = null;
                foreach (TableMemberInfoSql member in members)
                {
                    intermediateQuery = PrepareSQLCreateIntermediateTable(member);
                    await result.RunAsync(() => Execute(intermediateQuery));
                }
            }
            foreach (TableInfo child in table.Children)
            {
                await result.RunAsync(() => CreateTable(child));
            }
            return result;
        }
        public async Task<VoidWithError> CreateTable(IMigrationModel migration)
        {
            VoidWithError result = new();
            bool tableExist = await result.ExtractAsync(() => TableExist(TableInfo.GetSQLTableName(migration.Type)));
            if (tableExist || !result.Success) return result;

            TableInfo table = new TableInfo(migration.Type);
            foreach (KeyValuePair<string, IMigrationProperty> pair in migration.Properties)
            {
                var property = pair.Value;
                TableMemberInfoSql member;
                if (property is IMigrationPropertyRef propertyRef)
                {
                    member = new TableMemberInfoSql1N(propertyRef, table);
                }
                else
                {
                    member = new TableMemberInfoSqlBasic(property, table);
                }
                result.Run(() => table.PrepareMembers(member).ToGeneric());
                table.AddMember(member);
            }
            List<string> sqls = PrepareSQLCreateTable(table);
            foreach (string sql in sqls)
            {
                await result.RunAsync(() => Execute(sql));
            }

            return result;
        }
        public async Task<ResultWithError<bool>> TableExist(PyramidInfo pyramid)
        {
            if (allTableInfos.ContainsKey(pyramid.type))
            {
                return await TableExist(allTableInfos[pyramid.type]);
            }
            ResultWithError<bool> result = new();
            result.Errors.Add(new DataError(DataErrorCode.TypeNotExistInsideStorage, "Can't find the type " + pyramid.type));
            result.Result = false;
            return result;
        }
        protected abstract string PrepareSQLTableExist(string table);
        public Task<ResultWithError<bool>> TableExist(TableInfo table)
        {
            return TableExist(table.SqlTableName);
        }
        public async Task<ResultWithError<bool>> TableExist(string table)
        {
            ResultWithError<bool> result = new();
            string sql = PrepareSQLTableExist(table);
            ResultWithError<List<Dictionary<string, string?>>> queryResult = await Query(sql);
            result.Errors.AddRange(queryResult.Errors);

            if (queryResult.Success && queryResult.Result != null && queryResult.Result.Count == 1)
            {
                int nb = int.Parse(queryResult.Result.ElementAt(0)["nb"] ?? "0");
                result.Result = (nb != 0);
            }
            return result;
        }

        protected abstract string PrepareSQLTableRename(string oldName, string newName);
        public async Task<ResultWithError<bool>> TableRename(string oldName, string newName)
        {
            ResultWithError<bool> result = new();
            string sql = PrepareSQLTableRename(oldName, newName);
            await result.RunAsync(() => Execute(sql));
            result.Result = result.Success;
            return result;
        }

        protected abstract string PrepareSQLTableDelete(string name);
        public async Task<ResultWithError<bool>> TableDelete(string name)
        {
            ResultWithError<bool> result = new();
            string sql = PrepareSQLTableDelete(name);
            await result.RunAsync(() => Execute(sql));
            result.Result = result.Success;
            return result;
        }

        #endregion

        #region Create
        protected abstract DatabaseCreateBuilderInfo PrepareSQLForBulkCreate<X>(DatabaseCreateBuilder<X> createBuilder, int nbItems, bool withId) where X : IStorable;
        public async Task<VoidWithError> BulkCreateFromBuilder<X>(DatabaseCreateBuilder<X> createBuilder, List<X> items, bool withId) where X : IStorable
        {
            VoidWithError result = new();
            int bufferSize = 500;
            for (int i = 0; i < items.Count; i += bufferSize)
            {
                List<X> buffer = items.GetRange(i, Math.Min(bufferSize, items.Count - i));
                List<DatabaseCreateBuilderInfoQuery> queries;
                createBuilder.info = PrepareSQLForBulkCreate(createBuilder, buffer.Count, withId);

                queries = createBuilder.info.Queries;

                foreach (DatabaseCreateBuilderInfoQuery query in queries)
                {
                    string sql = query.Sql;
                    Dictionary<ParamsInfo, QueryParameterType> parametersCreate = new();
                    foreach (ParamsInfo parameterInfo in query.Parameters)
                    {
                        parameterInfo.Value = null;
                        parametersCreate.Add(parameterInfo, QueryParameterType.GrabValue);
                    }

                    List<IStorable> storables = buffer.Select(p => (IStorable)p).ToList();
                    VoidWithError createResult = await BulkQueryGeneric(StorableAction.Create, sql, parametersCreate, storables);

                    if (!createResult.Success)
                    {
                        result.Errors.AddRange(createResult.Errors);
                        return result;
                    }

                }

            }
            return result;
        }
        protected abstract DatabaseCreateBuilderInfo PrepareSQLForCreate<X>(DatabaseCreateBuilder<X> createBuilder) where X : IStorable;
        public async Task<VoidWithError> CreateFromBuilder<X>(DatabaseCreateBuilder<X> createBuilder, X item) where X : IStorable
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

            VoidWithError resultBefore = await CheckAutoCUDBeforeCreate(createBuilder.info.ToCheckBefore, item);
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

                ResultWithError<List<Dictionary<string, string?>>> createResult = await QueryGeneric(StorableAction.Create, sql, parametersCreate, item);

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
                VoidWithError resultReverse = await CheckReverseLinkAfterCreate(createBuilder.info.ReverseMembers, item, id);
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
        protected async Task<VoidWithError> CheckAutoCUDBeforeCreate<X>(List<TableMemberInfoSql> members, X item) where X : IStorable
        {
            VoidWithError result = new();
            Func<IStorable, TableMemberInfoSql, Task<bool>> manageStorable = async (storableLink, member) =>
            {
                if (storableLink.Id == 0 && member.IsAutoCreate)
                {
                    List<GenericError> resultCreateTemp = await storableLink.CreateWithError();
                    if (resultCreateTemp.Count != 0)
                    {
                        result.Errors.AddRange(resultCreateTemp);
                        return false;
                    }
                }
                else if (storableLink.Id != 0 && member.IsAutoUpdate)
                {
                    List<GenericError> resultUpdateTemp = await storableLink.UpdateWithError();
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
                        if (!await manageStorable(storableLink, member))
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
                                if (!await manageStorable(storableLink, member))
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
                                if (!await manageStorable(storableLink, member))
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
        protected async Task<VoidWithError> CheckReverseLinkAfterCreate<X>(List<TableReverseMemberInfo> reverseMembers, X item, int id) where X : IStorable
        {
            VoidWithError result = new();
            Func<IStorable, TableReverseMemberInfo, Task<bool>> manageStorable = async (reverseStorable, member) =>
            {
                if (reverseStorable.Id == 0 && member.IsAutoCreate)
                {
                    member.SetReverseId(reverseStorable, id);
                    List<GenericError> resultCreateTemp = await reverseStorable.CreateWithError();
                    if (resultCreateTemp.Count != 0)
                    {
                        result.Errors.AddRange(resultCreateTemp);
                        return false;
                    }
                }
                else if (member.IsAutoUpdate)
                {
                    member.SetReverseId(reverseStorable, id);
                    List<GenericError> resultUpdateTemp = await reverseStorable.UpdateWithError();
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
                            if (!await manageStorable(reverseStorable, reverseMember))
                            {
                                return result;
                            }
                        }
                    }
                }
                else if (reverseO is IStorable reverseStorable)
                {
                    if (!await manageStorable(reverseStorable, reverseMember))
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
        public async Task<ResultWithError<List<int>>> UpdateFromBuilder<X>(DatabaseUpdateBuilder<X> updateBuilder, X item) where X : IStorable
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
            ResultWithError<List<Dictionary<string, string?>>> queryResult = await QueryGeneric(StorableAction.Read, updateInfo.QuerySql, parametersQuery);
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

            List<IStorable> storableToDeleted = new List<IStorable>();
            await CheckAutoCUDBeforeUpdate(updateInfo.ToCheckBefore, item, list, updateBuilder.DM, storableToDeleted);

            foreach (TableReverseMemberInfo reverseMember in updateInfo.ReverseMembers)
            {
                Dictionary<int, IStorable> oldList = new Dictionary<int, IStorable>();
                foreach (int id in list)
                {
                    ResultWithDataError<List<IStorable>> resultTemp = await reverseMember.ReverseQuery(id);
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
                                await itemTemp.Create();
                            }
                        }
                        else if (oldList.ContainsKey(itemTemp.Id))
                        {
                            if (reverseMember.IsAutoUpdate)
                            {
                                await itemTemp.Update();
                            }
                            oldList.Remove(itemTemp.Id);
                        }
                    }
                }

                if (reverseMember.IsAutoDelete)
                {
                    foreach (KeyValuePair<int, IStorable> missing in oldList)
                    {
                        await missing.Value.Delete();
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
                ResultWithError<List<Dictionary<string, string?>>> updateResult = await QueryGeneric(StorableAction.Update, query.Sql, parametersUpdate, item);

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
                List<GenericError> resultError = await storable.DeleteWithError();
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

        protected async Task<VoidWithError> CheckAutoCUDBeforeUpdate<X>(List<TableMemberInfoSql> members, X item, List<int> listIdUpdate, IGenericDM DM, List<IStorable> storableToDeleted) where X : IStorable
        {
            VoidWithError result = new VoidWithError();
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
                    queryBuilder.Field(lambda);
                }
                else if (member is ITableMemberInfoSqlLinkMultiple)
                {
                    ParameterExpression argParam = Expression.Parameter(typeof(X), "t");
                    MemberExpression fieldProperty = Expression.Property(argParam, member.SqlName);
                    LambdaExpression lambda = Expression.Lambda(fieldProperty, argParam);
                    queryBuilder.Field(lambda);
                }
            }
            queryBuilder.Where(p => listIdUpdate.Contains(p.Id));
            ResultWithError<List<X>> resultTemp = await queryBuilder.RunWithError();
            if (!resultTemp.Success || resultTemp.Result == null)
            {
                result.Errors.AddRange(resultTemp.Errors);
                return result;
            }

            Func<IStorable, TableMemberInfoSql, Dictionary<int, IStorable>, Task<bool>> manageStorable = async (currentStorable, member, oldValues) =>
            {
                if (currentStorable.Id == 0 && member.IsAutoCreate)
                {
                    List<GenericError> resultError = await currentStorable.CreateWithError();
                    if (resultError.Count != 0)
                    {
                        result.Errors.AddRange(resultError);
                        return false;
                    }
                }
                else if (member.IsAutoUpdate)
                {
                    List<GenericError> resultError = await currentStorable.UpdateWithError();
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
                        if (!await manageStorable(currentStorable, member, oldValues))
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
                                if (!await manageStorable(currentStorable, member, oldValues))
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
                                if (!await manageStorable(currentStorable, member, oldValues))
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
        protected async Task<VoidWithError> CheckReverseLinkBeforeUpdate<X>(List<TableReverseMemberInfo> reverseMembers, X item, List<int> listIdUpdate) where X : IStorable
        {
            listIdUpdate = listIdUpdate.ToList();
            VoidWithError result = new VoidWithError();
            foreach (TableReverseMemberInfo reverseMember in reverseMembers)
            {
                Dictionary<int, IStorable> oldList = new Dictionary<int, IStorable>();
                foreach (int id in listIdUpdate)
                {
                    ResultWithDataError<List<IStorable>> resultTemp = await reverseMember.ReverseQuery(id);
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
                                List<GenericError> resultTemp = await itemTemp.CreateWithError();
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
                                List<GenericError> resultTemp = await itemTemp.UpdateWithError();
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
                        List<GenericError> resultTemp = await missing.Value.DeleteWithError();
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
        public async Task<VoidWithError> DeleteFromBuilder<X>(DatabaseDeleteBuilder<X> deleteBuilder, List<X> elementsToDelete) where X : IStorable
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
                ResultWithError<List<Dictionary<string, string?>>> deleteResultNM = await QueryGeneric(StorableAction.Delete, deleteNM.Key, parametersDeleteNM);
            }

            // delete reverse
            foreach (TableReverseMemberInfo reverseMemberInfo in deleteBuilder.info.ReverseMembers)
            {
                ResultWithDataError<List<IStorable>> resultTemp = await reverseMemberInfo.ReverseQuery(ids);
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
                        List<GenericError> errorsTemp = await item.UpdateWithError();
                        if (errorsTemp.Count > 0)
                        {
                            result.Errors.AddRange(errorsTemp);
                            return result;
                        }
                    }
                    else
                    {
                        List<GenericError> errorsTemp = await item.DeleteWithError();
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
            ResultWithError<List<Dictionary<string, string?>>> deleteResult = await QueryGeneric(StorableAction.Delete, sql, parametersDelete);
            if (!deleteResult.Success)
            {
                result.Errors.AddRange(deleteResult.Errors);
                return result;
            }

            #endregion

            // auto delete 1-n
            TableInfo deletedTable = deleteBuilder.InfoByPath[""].TableInfo;
            HashSet<(Type Type, int Id)> autoDeleted = [];
            foreach (TableMemberInfoSql member in deletedTable.Members.Where(member => member.IsAutoDelete))
            {
                foreach (X element in elementsToDelete)
                {
                    object? linkedValue = member.GetValue(element);
                    IEnumerable<IStorable> linkedItems = linkedValue switch
                    {
                        IStorable storable => [storable],
                        IEnumerable enumerable => enumerable.OfType<IStorable>(),
                        _ => []
                    };

                    foreach (IStorable linkedItem in linkedItems)
                    {
                        if (linkedItem.Id == 0 || !autoDeleted.Add((linkedItem.GetType(), linkedItem.Id)))
                        {
                            continue;
                        }
                        await result.RunAsync(async () =>
                        {
                            VoidWithError deleteLinked = new()
                            {
                                Errors = await linkedItem.DeleteWithError()
                            };
                            return deleteLinked;
                        });
                    }
                }
            }


            return result;
        }


        #endregion

        #region Migration
        public async Task<VoidWithError> ApplyMigration<X>(IMigrationModel model) where X : notnull, IStorable
        {
            VoidWithError result = new VoidWithError();
            Action checkMember = () =>
            {
                foreach (KeyValuePair<string, IMigrationProperty> member in model.Properties)
                {
                    if (member.Value.PropertyAction == MigrationPropertyAction.Update && member.Value.OldName != null)
                    {
                        // TODO: rename colonne
                    }
                    else if (member.Value.PropertyAction == MigrationPropertyAction.Delete)
                    {
                        // TODO: drop colonne
                    }
                    else if (member.Value.PropertyAction == MigrationPropertyAction.Create)
                    {
                        // TODO: add colonne
                        // check si la colonne existe, si c'est le cas il faut faire attention aux attributes
                    }
                }
            };
            if (model.ModelAction == null)
            {
                checkMember();
            }
            else if (model.ModelAction == MigrationModelAction.Update)
            {
                if (!string.IsNullOrEmpty(model.OldName))
                {
                    await result.RunAsync(() => TableRename(model.OldName, TableInfo.GetSQLTableName(model.Type)));
                }
                checkMember();
            }
            else if (model.ModelAction == MigrationModelAction.Create)
            {
                await result.RunAsync(() => CreateTable(model));
            }
            else if (model.ModelAction == MigrationModelAction.Delete)
            {
                await result.RunAsync(() => TableDelete(TableInfo.GetSQLTableName(model.Type)));
            }
            return new();
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
        public async Task<ResultWithError<Y>> RunInsideTransaction<Y>(Y? defaultValue, Func<Task<ResultWithError<Y>>> action)
        {
            ResultWithError<DbTransactionContext> transactionResult = (await BeginTransaction()).ToGeneric();
            if (!transactionResult.Success || transactionResult.Result == null)
            {
                ResultWithError<Y> resultError = new()
                {
                    Result = defaultValue,
                    Errors = transactionResult.Errors
                };
                return resultError;
            }
            transactionScope = transactionResult.Result;
            ResultWithError<Y> resultTemp;
            try
            {
                resultTemp = await action();
            }
            catch (Exception exception)
            {
                resultTemp = new ResultWithError<Y>()
                {
                    Result = defaultValue
                };
                resultTemp.Errors.Add(new DataError(DataErrorCode.UnknownError, exception));
            }
            if (resultTemp.Success)
            {
                ResultWithError<bool> commitResult = await transactionResult.Result.Commit();
                resultTemp.Errors.AddRange(commitResult.Errors);
            }
            else
            {
                ResultWithError<bool> rollbackResult = await transactionResult.Result.Rollback();
                resultTemp.Errors.AddRange(rollbackResult.Errors);
            }
            transactionScope = null;
            return resultTemp;
        }
        /// <summary>
        /// Run a function inside a transaction that ll be commit if no error otherwise rollback
        /// </summary>
        /// <typeparam name="Y"></typeparam>
        /// <param name="action"></param>
        /// <returns></returns>
        public Task<ResultWithError<Y>> RunInsideTransaction<Y>(Func<Task<ResultWithError<Y>>> action)
        {
            return RunInsideTransaction<Y>(default, action);
        }
        /// <summary>
        /// Run a function inside a transaction that ll be commit if no error otherwise rollback
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public async Task<VoidWithError> RunInsideTransaction(Func<Task<VoidWithError>> action)
        {
            ResultWithError<DbTransactionContext> transactionResult = (await BeginTransaction()).ToGeneric();
            if (!transactionResult.Success || transactionResult.Result == null)
            {
                VoidWithError resultError = new()
                {
                    Errors = transactionResult.Errors
                };
                return resultError;
            }
            transactionScope = transactionResult.Result;
            VoidWithError resultTemp;
            try
            {
                resultTemp = await action();
            }
            catch (Exception exception)
            {
                resultTemp = new VoidWithError();
                resultTemp.Errors.Add(new DataError(DataErrorCode.UnknownError, exception));
            }
            if (resultTemp.Success)
            {
                ResultWithError<bool> commitResult = await transactionResult.Result.Commit();
                resultTemp.Errors.AddRange(commitResult.Errors);
            }
            else
            {
                ResultWithError<bool> rollbackResult = await transactionResult.Result.Rollback();
                resultTemp.Errors.AddRange(rollbackResult.Errors);
            }
            transactionScope = null;
            return resultTemp;
        }


        public abstract string GetSqlColumnType(DbType dbType, TableMemberInfoSql tableMember);
        #endregion

        #region Graph
        public List<DiagramObject> GetDiagrams(DiagramConfigInternal config)
        {
            string diagramType = DiagramType();
            Dictionary<string, DiagramObject> diagrams = new();
            string mainName = config.MainName;
            if (config.GenerateMain)
            {
                diagrams[mainName] = new DiagramObject(mainName, diagramType);
            }

            foreach (var pair in allTableInfos)
            {
                var info = pair.Value;
                if (info.IsForceInherit) continue;
                if (pair.Key.IsInterface) continue;

                IEnumerable<Diagram> attrs = pair.Key.GetCustomAttributes<Diagram>();
                bool mainFound = false;
                string? area = pair.Key.Namespace;
                foreach (Diagram attr in attrs)
                {
                    string name = attr.Name ?? mainName;
                    if (name == mainName)
                    {
                        mainFound = true;
                    }
                    else
                    {
                        area = attr.Area;
                    }
                    if (!diagrams.ContainsKey(name))
                    {
                        diagrams.Add(name, new DiagramObject(name, diagramType));
                    }

                    (DiagramTable table, List<DiagramRelationship> rels) temp = CreateTableDiagram(info, attr);
                    if (area != null)
                    {
                        if (!diagrams[name].Areas.Exists(p => p.Name == area))
                        {
                            string[] colors = { "#ef4444", "#3b82f6", "#10b981", "#f59e0b", "#8b5cf6", "#ec4899" };
                            string color = colors[new Random().Next(colors.Length)];
                            if (attr.AreaColor != null)
                            {
                                color = attr.AreaColor;
                            }
                            diagrams[name].Areas.Add(new Area()
                            {
                                Name = area,
                                Color = color
                            });
                        }

                        temp.table.ParentAreaId = diagrams[name].Areas.Find(p => p.Name == area)!.Id;

                    }

                    diagrams[name].Tables.Add(temp.table);
                    diagrams[name].Relationships.AddRange(temp.rels);
                }

                if (!mainFound && config.GenerateMain)
                {
                    (DiagramTable table, List<DiagramRelationship> rels) temp = CreateTableDiagram(info);
                    if (area != null)
                    {
                        if (!diagrams[mainName].Areas.Exists(p => p.Name == area))
                        {
                            string[] colors = { "#ef4444", "#3b82f6", "#10b981", "#f59e0b", "#8b5cf6", "#ec4899" };
                            diagrams[mainName].Areas.Add(new Area()
                            {
                                Name = area,
                                Color = colors[new Random().Next(colors.Length)]
                            });
                        }
                        temp.table.ParentAreaId = diagrams[mainName].Areas.Find(p => p.Name == area)!.Id;

                    }

                    diagrams[mainName].Tables.Add(temp.table);
                    diagrams[mainName].Relationships.AddRange(temp.rels);
                }


            }


            foreach (var pair in diagrams)
            {
                DiagramObject diagram = pair.Value;

                List<DiagramRelationship> relationships = diagram.Relationships.ToList();
                foreach (DiagramRelationship relationship in relationships)
                {
                    if (
                        !diagram.Tables.Exists(p => p.Id == relationship.SourceTableId && p.Fields.Exists(f => f.Id == relationship.SourceFieldId)) ||
                        !diagram.Tables.Exists(p => p.Id == relationship.TargetTableId && p.Fields.Exists(f => f.Id == relationship.TargetFieldId))
                    )
                    {
                        diagram.Relationships.Remove(relationship);
                    }
                }

                diagram.LayoutDiagram();
            }


            return diagrams.Values.ToList();
        }
        public abstract string DiagramType();

        private (DiagramTable table, List<DiagramRelationship> rels) CreateTableDiagram(TableInfo info, Diagram? attr = null)
        {
            DiagramTable table = new DiagramTable()
            {
                Id = info.SqlTableName,
                Name = info.SqlTableName,
                Color = attr?.TableColor ?? "#3b82f6"
            };

            List<DiagramRelationship> rels = new();

            foreach (var member in info.Members)
            {
                if (member == null) continue;

                DbType? type = TableMemberInfoSql.GetDbType(member.MemberType, member);
                if (type == null) continue;

                string typeTxt = GetSqlColumnType((DbType)type, member);

                DiagramField field = new DiagramField()
                {
                    Id = info.SqlTableName + "." + member.SqlName,
                    Name = member.SqlName,
                    Type = new()
                    {
                        Id = typeTxt,
                        Name = typeTxt
                    },
                    PrimaryKey = member.IsPrimary,
                    Unique = member.IsUnique,
                    Nullable = member.IsNullable,
                };
                table.Fields.Add(field);

                if (member is ITableMemberInfoSqlLinkSingle rel && rel.TableLinked?.Primary != null && info.Primary != null)
                {
                    // TODO add relation name
                    DiagramRelationship relationship = new DiagramRelationship()
                    {
                        Name = table.Name + "_" + rel.TableLinked.SqlTableName,
                        SourceTableId = table.Name,
                        SourceFieldId = table.Name + "." + info.Primary.SqlName,
                        TargetTableId = rel.TableLinked.SqlTableName,
                        TargetFieldId = rel.TableLinked.SqlTableName + "." + rel.TableLinked.Primary.SqlName
                    };
                    DiagramRelation? relationAttr = member.memberInfo?.GetCustomAttribute<DiagramRelation>();
                    if (relationAttr != null)
                    {
                        relationship.Description = relationAttr.Description;
                    }
                    rels.Add(relationship);
                }
            }
            return (table, rels);
        }
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


    public class DbTransactionContext : TransactionContext
    {

        public DbTransaction transaction;


        public DbConnection Connection { get; }

        public DbTransactionContext(DbTransaction transaction, Func<Task> endTransaction) : base(endTransaction)
        {
            this.transaction = transaction;
            Connection = transaction.Connection ?? throw new Exception("Transaction without connection");
        }

        protected override async Task TransactionDispose()
        {
            await transaction.DisposeAsync();
        }

        protected override async Task TransactionRollback()
        {
            await transaction.RollbackAsync();
        }

        protected override async Task TransactionCommit()
        {
            await transaction.CommitAsync();
        }
    }
}
