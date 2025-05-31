using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using AventusSharp.Data.Attributes;
using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Manager.DB.Builders;
using AventusSharp.Data.Migrations;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Storage.Default.TableMember;
using AventusSharp.Tools;
using Npgsql;

namespace AventusSharp.Data.Storage.Postgresql;

public class PostgreSqlStorage : DefaultDBStorage<PostgreSqlStorage>
{
    private bool useDatabase = true;
    protected bool CreateDatabase { get; set; }
    protected PostgreSqlMigrationProvider MigrationProvider { get; }
    public PostgreSqlStorage(StorageCredentials info, bool createDatabase = true) : base(info)
    {
        CreateDatabase = createDatabase;
        MigrationProvider = new PostgreSqlMigrationProvider(this);
    }

    protected override DbConnection GetConnection()
    {
        NpgsqlConnectionStringBuilder builder = new()
        {
            Host = host,
            Username = username,
            Password = password,
        };
        if (useDatabase)
            builder.Database = database;

        if (port != null)
        {
            builder.Port = (int)port;
        }
        return new NpgsqlConnection(builder.ConnectionString);
    }

    protected override IMigrationProvider DefineMigrationProvider()
    {
        return MigrationProvider;
    }

    public override VoidWithError ConnectWithError()
    {
        VoidWithError result = new();
        try
        {
            IsConnectedOneTime = true;
            using (DbConnection connection = GetConnection())
            {
                connection.Open();
                IsConnectedOneTime = true;
            }
        }
        catch (Exception e)
        {
            if (e is NpgsqlException exception)
            {
                if (exception.ErrorCode == 1049 && CreateDatabase) // missing database
                {
                    try
                    {
                        NpgsqlConnectionStringBuilder builder = new()
                        {
                            Username = username,
                            Password = password,
                            Host = host
                        };
                        using (DbConnection connection = new NpgsqlConnection(builder.ConnectionString))
                        {
                            useDatabase = false;
                            connection.Open();
                            Execute("CREATE DATABASE " + database + ";").Print();
                            useDatabase = true;
                        }
                        ;



                        NpgsqlConnectionStringBuilder builderFull = new()
                        {
                            Username = username,
                            Password = password,
                            Host = host,
                            Database = database
                        };
                        using (DbConnection connection = new NpgsqlConnection(builderFull.ConnectionString))
                        {
                            connection.Open();
                        }
                    }
                    catch (Exception e2)
                    {
                        useDatabase = true;
                        result.Errors.Add(new DataError(DataErrorCode.UnknowError, e2));
                    }
                }
                else
                {
                    result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
                }
            }
            else
            {
                result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
            }
        }

        return result;
    }

    public override ResultWithDataError<DbCommand> CreateCmd(string sql)
    {
        ResultWithDataError<DbCommand> result = new();
        try
        {
            NpgsqlConnection mySqlConnection = (NpgsqlConnection)GetConnection();
            NpgsqlCommand command = mySqlConnection.CreateCommand();
            command.CommandType = System.Data.CommandType.Text;
            command.CommandText = sql;
            result.Result = command;
        }
        catch (Exception e)
        {
            result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
        }
        return result;
    }
    public override DbParameter GetDbParameter()
    {
        return new NpgsqlParameter();
    }

    public override ResultWithError<bool> ResetStorage()
    {
        ResultWithError<bool> result = new();

        string sql = "SELECT 'DROP TABLE IF EXISTS \"' || tablename || '\" CASCADE;' as query " +
                     "FROM pg_tables WHERE schemaname = 'public';";

        ResultWithError<List<Dictionary<string, string?>>> queryResult = Query(sql);
        if (!queryResult.Success || queryResult.Result == null)
        {
            result.Errors.AddRange(queryResult.Errors);
            return result;
        }

        string dropAllCmd = "";
        foreach (Dictionary<string, string?> line in queryResult.Result)
        {
            dropAllCmd += line["query"];
        }

        VoidWithError executeResult = Execute(dropAllCmd);
        if (!executeResult.Success)
        {
            result.Errors.AddRange(executeResult.Errors);
            return result;
        }

        result.Result = true;
        return result;
    }

    #region table
    protected override string PrepareSQLCreateTable(TableInfo table)
    {
        return Queries.CreateTable.GetQuery(table, this);
    }
    protected override string PrepareSQLCreateIntermediateTable(TableMemberInfoSql tableMember)
    {
        return Queries.CreateIntermediateTable.GetQuery(tableMember, this);
    }
    protected override string PrepareSQLTableExist(string table)
    {
        string sql = "SELECT COUNT(*) AS nb FROM information_schema.tables " +
                     "WHERE table_name = '" + table + "' AND table_schema = 'public';";
        return sql;
    }
    #endregion

    #region query
    protected override DatabaseQueryBuilderInfo PrepareSQLForQuery<X>(DatabaseQueryBuilder<X> queryBuilder)
    {
        return Queries.Query.PrepareSQL(queryBuilder, this);
    }

    #endregion

    #region exist
    protected override DatabaseExistBuilderInfo PrepareSQLForExist<X>(DatabaseExistBuilder<X> queryBuilder)
    {
        return Queries.Exist.PrepareSQL(queryBuilder, this);
    }
    #endregion

    #region create
    protected override DatabaseCreateBuilderInfo PrepareSQLForCreate<X>(DatabaseCreateBuilder<X> createBuilder)
    {
        return Queries.Create.PrepareSQL(createBuilder);
    }
    #endregion

    #region update
    protected override DatabaseUpdateBuilderInfo PrepareSQLForUpdate<X>(DatabaseUpdateBuilder<X> updateBuilder)
    {
        return Queries.Update.PrepareSQL(updateBuilder, this);
    }

    #endregion

    #region delete
    protected override DatabaseDeleteBuilderInfo PrepareSQLForDelete<X>(DatabaseDeleteBuilder<X> deleteBuilder)
    {
        return Queries.Delete.PrepareSQL(deleteBuilder, this);
    }

    #endregion


    protected override object? TransformValueForFct(ParamsInfo paramsInfo)
    {
        if (paramsInfo.Value is string casted)
        {
            if (paramsInfo.FctMethodCall == WhereGroupFctEnum.StartsWith)
            {
                return casted + "%";
            }
            if (paramsInfo.FctMethodCall == WhereGroupFctEnum.EndsWith)
            {
                return "%" + casted;
            }
            if (paramsInfo.FctMethodCall == WhereGroupFctEnum.ContainsStr)
            {
                return "%" + casted + "%";
            }
        }
        return paramsInfo.Value;
    }

    public override string GetSqlColumnType(DbType dbType, TableMemberInfoSql? tableMember = null)
    {
        if (dbType == DbType.Int32) { return "int"; }
        if (dbType == DbType.Double) { return "float"; }
        if (dbType == DbType.Boolean) { return "bit"; }
        if (dbType == DbType.DateTime) { return "datetime"; }
        if (dbType == DbType.Date) { return "date"; }
        if (dbType == DbType.String)
        {
            if (tableMember is ITableMemberInfoSizable basic && basic.SizeAttr != null)
            {
                if (basic.SizeAttr.SizeType == null) return "varchar(" + basic.SizeAttr.Max + ")";
                else if (basic.SizeAttr.SizeType == SizeEnum.MaxVarChar) return "TEXT";
                else if (basic.SizeAttr.SizeType == SizeEnum.Text) return "TEXT";
                else if (basic.SizeAttr.SizeType == SizeEnum.MediumText) return "MEDIUMTEXT";
                else if (basic.SizeAttr.SizeType == SizeEnum.LongText) return "LONGTEXT";
            }
            return "varchar(255)";
        }
        throw new NotImplementedException();
    }
}
