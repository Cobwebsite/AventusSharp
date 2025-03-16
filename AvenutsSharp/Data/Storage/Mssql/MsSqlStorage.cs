using System.Data.Common;
using AventusSharp.Data.Storage.Default;
using Microsoft.Data.SqlClient;
using AventusSharp.Tools;
using System;
using System.Collections.Generic;
using AventusSharp.Data.Storage.Default.TableMember;
using AventusSharp.Data.Manager.DB.Builders;
using AventusSharp.Data.Manager.DB;
using System.Data;
using AventusSharp.Data.Attributes;

namespace AventusSharp.Data.Storage.Mssql;

public class MsSqlStorage : DefaultDBStorage<MsSqlStorage>
{
    private bool useDatabase = true;
    protected bool CreateDatabase { get; set; }
    public MsSqlStorage(StorageCredentials info, bool createDatabase = true) : base(info)
    {
        CreateDatabase = createDatabase;
    }

    protected override DbConnection GetConnection()
    {
        SqlConnectionStringBuilder builder = new()
        {
            DataSource = host,
            UserID = username,
            Password = password,
        };
        if (useDatabase)
            builder.InitialCatalog = database;

        if (port != null)
        {
            builder.DataSource = $"{host},{port}";
        }
        return new SqlConnection(builder.ConnectionString);
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
            if (e is SqlException exception)
            {
                if (exception.Number == 1049 && CreateDatabase) // missing database
                {
                    try
                    {
                        SqlConnectionStringBuilder builder = new()
                        {
                            UserID = username,
                            Password = password,
                            DataSource = host
                        };
                        using (DbConnection connection = new SqlConnection(builder.ConnectionString))
                        {
                            useDatabase = false;
                            connection.Open();
                            Execute("CREATE DATABASE " + database + ";").Print();
                            useDatabase = true;
                        }
                        ;



                        SqlConnectionStringBuilder builderFull = new()
                        {
                            UserID = username,
                            Password = password,
                            InitialCatalog = database,
                            DataSource = host
                        };
                        using (DbConnection connection = new SqlConnection(builderFull.ConnectionString))
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
            SqlConnection mySqlConnection = (SqlConnection)GetConnection();
            SqlCommand command = mySqlConnection.CreateCommand();
            command.CommandType = CommandType.Text;
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
        return new SqlParameter();
    }

    public override ResultWithError<bool> ResetStorage()
    {
        ResultWithError<bool> result = new();

        string sql = "SELECT 'DROP TABLE [' + TABLE_NAME + '];' as query " +
                     "FROM INFORMATION_SCHEMA.TABLES " +
                     "WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_CATALOG = '" + this.database + "';";

        ResultWithError<List<Dictionary<string, string?>>> queryResult = Query(sql);
        if (!queryResult.Success || queryResult.Result == null)
        {
            result.Errors.AddRange(queryResult.Errors);
            return result;
        }

        string dropAllCmd = "EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';";
        foreach (Dictionary<string, string?> line in queryResult.Result)
        {
            dropAllCmd += line["query"];
        }
        dropAllCmd += "EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';";

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
        string sql = "SELECT COUNT(*) AS nb FROM INFORMATION_SCHEMA.TABLES " +
                     "WHERE TABLE_NAME = '" + table + "' AND TABLE_CATALOG = '" + GetDatabaseName() + "';";
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
