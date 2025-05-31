using AventusSharp.Data.Attributes;
using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Manager.DB.Builders;
using AventusSharp.Data.Migrations;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Storage.Default.TableMember;
using AventusSharp.Tools;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace AventusSharp.Data.Storage.Mysql
{
    public class MySQLStorage : DefaultDBStorage<MySQLStorage>
    {
        private bool useDatabase = true;
        protected bool CreateDatabase { get; set; }
        protected MySQLMigrationProvider MigrationProvider { get; }
        public MySQLStorage(StorageCredentials info, bool createDatabase = true) : base(info)
        {
            CreateDatabase = createDatabase;
            MigrationProvider = new MySQLMigrationProvider(this);
        }

        protected override DbConnection GetConnection()
        {
            MySqlConnectionStringBuilder builder = new()
            {
                Server = host,
                UserID = username,
                Password = password,
            };
            if (useDatabase)
                builder.Database = database;

            if (port != null)
            {
                builder.Port = (uint)port;
            }
            return new MySqlConnection(builder.ConnectionString);
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
                if (e is MySqlException exception)
                {
                    if (exception.Number == 1049 && CreateDatabase) // missing database
                    {
                        try
                        {
                            MySqlConnectionStringBuilder builder = new()
                            {
                                Server = host,
                                UserID = username,
                                Password = password,

                            };
                            using (DbConnection connection = new MySqlConnection(builder.ConnectionString))
                            {
                                useDatabase = false;
                                connection.Open();
                                Execute("CREATE DATABASE " + database + ";").Print();
                                useDatabase = true;
                            }
                            ;



                            MySqlConnectionStringBuilder builderFull = new()
                            {
                                Server = host,
                                UserID = username,
                                Password = password,
                                Database = database
                            };
                            using (DbConnection connection = new MySqlConnection(builderFull.ConnectionString))
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
                MySqlConnection mySqlConnection = (MySqlConnection)GetConnection();
                MySqlCommand command = mySqlConnection.CreateCommand();
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
            return new MySqlParameter();
        }

        public override ResultWithError<bool> ResetStorage()
        {
            ResultWithError<bool> result = new();
            string sql = "SELECT concat('DROP TABLE IF EXISTS `', table_name, '`;') as query FROM information_schema.tables WHERE table_schema = '" + this.database + "'; ";
            ResultWithError<List<Dictionary<string, string?>>> queryResult = Query(sql);
            if (!queryResult.Success || queryResult.Result == null)
            {
                result.Errors.AddRange(queryResult.Errors);
                return result;
            }

            string dropAllCmd = "SET FOREIGN_KEY_CHECKS = 0;";
            foreach (Dictionary<string, string?> line in queryResult.Result)
            {
                dropAllCmd += line["query"];
            }
            dropAllCmd += "SET FOREIGN_KEY_CHECKS = 1;";

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
            string sql = "SELECT COUNT(*) nb FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '" + table + "' and TABLE_SCHEMA = '" + GetDatabaseName() + "'; ";
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
}
