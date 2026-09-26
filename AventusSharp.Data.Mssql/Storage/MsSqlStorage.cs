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
using AventusSharp.Data.Migrations;
using System.Threading.Tasks;
using AventusSharp.Data.Storage.Relational;

namespace AventusSharp.Data.Storage.Mssql;

public class MsSqlStorage : DefaultDBStorage<MsSqlStorage>
{
    private bool useDatabase = true;
    protected bool CreateDatabase { get; set; }
    protected MsSqlMigrationProvider MigrationProvider { get; }
    public MsSqlStorage(StorageCredentials info, bool createDatabase = true) : base(info)
    {
        DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fff";
        CreateDatabase = createDatabase;
        MigrationProvider = new MsSqlMigrationProvider(this);
    }

    protected SqlConnectionStringBuilder GetStringBuilder(bool useDatabase)
    {
        SqlConnectionStringBuilder builder = new()
        {
            UserID = Username,
            Password = Password,
            DataSource = Host,
            TrustServerCertificate = credentials.TrustServerCertificate
        };
        if (Port != null)
        {
            builder.DataSource = $"{Host},{Port}";
        }
        if (useDatabase)
            builder.InitialCatalog = Database;

        return builder;
    }
    public override DbConnection GetConnection()
    {
        SqlConnectionStringBuilder builder = GetStringBuilder(useDatabase);
        return new SqlConnection(builder.ConnectionString);
    }
    protected override IMigrationProvider DefineMigrationProvider()
    {
        return MigrationProvider;
    }
    public override async Task<VoidWithError> ConnectWithError()
    {
        VoidWithError result = new();
        try
        {
            IsConnectedOneTime = true;
            using (DbConnection connection = GetConnection())
            {
                await connection.OpenAsync();
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
                        SqlConnectionStringBuilder builder = GetStringBuilder(false);
                        using (DbConnection connection = new SqlConnection(builder.ConnectionString))
                        {
                            useDatabase = false;
                            connection.Open();
                            (await Execute("CREATE DATABASE " + QuoteIdentifier(Database) + ";")).Print();
                            useDatabase = true;
                        }
                        ;



                        SqlConnectionStringBuilder builderFull = GetStringBuilder(true);
                        using (DbConnection connection = new SqlConnection(builderFull.ConnectionString))
                        {
                            connection.Open();
                        }
                    }
                    catch (Exception e2)
                    {
                        useDatabase = true;
                        result.Errors.Add(new DataError(DataErrorCode.UnknownError, e2));
                    }
                }
                else
                {
                    result.Errors.Add(new DataError(DataErrorCode.UnknownError, e));
                }
            }
            else
            {
                result.Errors.Add(new DataError(DataErrorCode.UnknownError, e));
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
            result.Errors.Add(new DataError(DataErrorCode.UnknownError, e));
        }
        return result;
    }
    public override DbParameter GetDbParameter()
    {
        return new SqlParameter();
    }

    public override async Task<ResultWithError<bool>> ResetStorage()
    {
        ResultWithError<bool> result = new();
        if (DataRuntime.IsExportCommand)
        {
            result.Result = true;
            return result;
        }

        string sql = "SELECT TABLE_NAME as name " +
                     "FROM INFORMATION_SCHEMA.TABLES " +
                     "WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_CATALOG = '" + this.Database + "';";

        ResultWithError<List<Dictionary<string, string?>>> queryResult = await Query(sql);
        if (!queryResult.Success || queryResult.Result == null)
        {
            result.Errors.AddRange(queryResult.Errors);
            return result;
        }

        string dropAllCmd = "EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';";
        foreach (Dictionary<string, string?> line in queryResult.Result)
        {
            dropAllCmd += "DROP TABLE " + QuoteIdentifier(line["name"]!) + ";";
        }
        dropAllCmd += "EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';";

        VoidWithError executeResult = await Execute(dropAllCmd);
        if (!executeResult.Success)
        {
            result.Errors.AddRange(executeResult.Errors);
            return result;
        }

        result.Result = true;
        return result;
    }

    #region table
    protected override List<string> PrepareSQLCreateTable(TableInfo table)
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
    protected override string PrepareSQLTableRename(string oldName, string newName)
    {
        return "RENAME TABLE = " + QuoteIdentifier(oldName) + " TO " + QuoteIdentifier(newName) + "; ";
    }
    protected override string PrepareSQLTableDelete(string name)
    {
        return "DROP TABLE IF EXISTS " + QuoteIdentifier(name) + ";";
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
    protected override DatabaseCreateBuilderInfo PrepareSQLForBulkCreate<X>(DatabaseCreateBuilder<X> createBuilder, int nbItems, bool withId)
    {
        return Queries.BulkCreate.PrepareSQL(createBuilder, nbItems, withId);
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

    #region graph
    public override string DiagramType()
    {
        return "mssql";
    }
    #endregion

    #region migrations
    protected override async Task<ResultWithError<List<MigrationForeignKey>>> GetMigrationForeignKeys()
    {
        ResultWithError<List<MigrationForeignKey>> result = new() { Result = new() };
        string sql = "SELECT child.name AS table_name, parent.name AS referenced_table, fk.name, "
            + "SCHEMA_NAME(child.schema_id) AS schema_name "
            + "FROM sys.foreign_keys fk JOIN sys.tables child ON child.object_id = fk.parent_object_id "
            + "JOIN sys.tables parent ON parent.object_id = fk.referenced_object_id "
            + "WHERE parent.schema_id = SCHEMA_ID()";
        var rows = await result.ExtractAsync(() => Query(sql));
        if (rows == null) return result;
        var schema = await result.ExtractAsync(() => Query("SELECT SCHEMA_NAME() AS name"));
        if (schema == null) return result;
        foreach (var row in rows)
        {
            string table = row["table_name"]!;
            if (row["schema_name"] != schema[0]["name"]) table = row["schema_name"] + "." + table;
            string dropSql = "ALTER TABLE " + QuoteIdentifier(row["schema_name"]!) + "."
                + QuoteIdentifier(row["table_name"]!) + " DROP CONSTRAINT " + QuoteIdentifier(row["name"]!);
            result.Result.Add(new(table, row["referenced_table"]!, dropSql));
        }
        return result;
    }

    protected override Task<VoidWithError> RenameMigrationProperty(string table, IMigrationProperty property)
    {
        string sql = "EXEC sp_rename " + FormatMigrationDefault(QuoteIdentifier(table) + "." + QuoteIdentifier(property.OldName!))
            + ", " + FormatMigrationDefault(property.Name) + ", 'COLUMN'";
        return Execute(sql);
    }
    protected override async Task<VoidWithError> UpdateMigrationProperty(string table, IMigrationProperty property)
    {
        VoidWithError result = new();

        string sql = "SELECT d.name FROM sys.default_constraints d JOIN sys.columns c "
            + "ON c.object_id = d.parent_object_id AND c.column_id = d.parent_column_id "
            + "WHERE d.parent_object_id = OBJECT_ID(" + FormatMigrationDefault(QuoteIdentifier(table))
            + ") AND c.name = " + FormatMigrationDefault(property.Name);
        List<Dictionary<string, string?>>? defaults = await result.ExtractAsync(() => Query(sql));
        if (defaults == null) return result;

        sql = "SELECT i.index_id, i.name, i.type_desc, i.is_unique, i.filter_definition, i.is_disabled, "
            + "i.fill_factor, i.is_padded, i.ignore_dup_key, i.allow_row_locks, i.allow_page_locks, d.name AS filegroup, "
            + "p.data_compression_desc FROM sys.indexes i JOIN sys.partitions p "
            + "ON p.object_id = i.object_id AND p.index_id = i.index_id AND p.partition_number = 1 "
            + "JOIN sys.data_spaces d ON d.data_space_id = i.data_space_id "
            + "WHERE i.object_id = OBJECT_ID(" + FormatMigrationDefault(QuoteIdentifier(table)) + ") "
            + "AND i.is_primary_key = 0 AND i.is_unique_constraint = 0 AND i.type IN (1, 2) AND d.type = 'FG' "
            + "AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c "
            + "ON c.object_id = ic.object_id AND c.column_id = ic.column_id "
            + "WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND c.name = "
            + FormatMigrationDefault(property.Name) + ") ORDER BY i.type";
        List<Dictionary<string, string?>>? indexes = await result.ExtractAsync(() => Query(sql));
        if (indexes == null) return result;

        List<string> restoreIndexes = new();
        foreach (var index in indexes)
        {
            sql = "SELECT c.name, ic.is_descending_key, ic.is_included_column FROM sys.index_columns ic "
                + "JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id "
                + "WHERE ic.object_id = OBJECT_ID(" + FormatMigrationDefault(QuoteIdentifier(table))
                + ") AND ic.index_id = " + index["index_id"] + " ORDER BY ic.key_ordinal, ic.index_column_id";
            List<Dictionary<string, string?>>? columns = await result.ExtractAsync(() => Query(sql));
            if (columns == null) return result;

            List<string> keys = new();
            List<string> included = new();
            foreach (var column in columns)
            {
                string columnName = QuoteIdentifier(column["name"]!);
                if (column["is_included_column"] == "True")
                    included.Add(columnName);
                else
                    keys.Add(columnName + (column["is_descending_key"] == "True" ? " DESC" : " ASC"));
            }

            string definition = "CREATE ";
            if (index["is_unique"] == "True") definition += "UNIQUE ";
            definition += index["type_desc"] + " INDEX " + QuoteIdentifier(index["name"]!)
                + " ON " + QuoteIdentifier(table) + " (" + string.Join(", ", keys) + ")";
            if (included.Count > 0) definition += " INCLUDE (" + string.Join(", ", included) + ")";
            if (!string.IsNullOrEmpty(index["filter_definition"])) definition += " WHERE " + index["filter_definition"];
            definition += " WITH (PAD_INDEX = " + SqlIndexOption(index["is_padded"])
                + ", IGNORE_DUP_KEY = " + SqlIndexOption(index["ignore_dup_key"])
                + ", ALLOW_ROW_LOCKS = " + SqlIndexOption(index["allow_row_locks"])
                + ", ALLOW_PAGE_LOCKS = " + SqlIndexOption(index["allow_page_locks"])
                + ", DATA_COMPRESSION = " + index["data_compression_desc"];
            if (index["fill_factor"] != "0") definition += ", FILLFACTOR = " + index["fill_factor"];
            definition += ") ON " + QuoteIdentifier(index["filegroup"]!);
            restoreIndexes.Add(definition);
            if (index["is_disabled"] == "True")
                restoreIndexes.Add($"ALTER INDEX {QuoteIdentifier(index["name"]!)} ON {QuoteIdentifier(table)} DISABLE");
        }

        // Drop nonclustered indexes first; recreate the clustered index first.
        for (int i = indexes.Count - 1; i >= 0; i--)
        {
            var index = indexes[i];
            await result.RunAsync(() => Execute($"DROP INDEX {QuoteIdentifier(index["name"]!)} ON {QuoteIdentifier(table)}"));
        }

        foreach (var constraint in defaults)
        {
            await result.RunAsync(() => Execute($"ALTER TABLE {QuoteIdentifier(table)} DROP CONSTRAINT {QuoteIdentifier(constraint["name"]!)}"));
        }

        await result.RunAsync(() => Execute($"ALTER TABLE {QuoteIdentifier(table)} ALTER COLUMN {QuoteIdentifier(property.Name)} {GetMigrationColumnType(property)} {(property.Options.Nullable ? "NULL" : "NOT NULL")}"));

        if (property.Options.Default != null)
        {
            await result.RunAsync(() => Execute($"ALTER TABLE {QuoteIdentifier(table)} ADD DEFAULT {FormatMigrationDefault(property.Options.Default)} FOR {QuoteIdentifier(property.Name)}"));
        }

        foreach (string definition in restoreIndexes)
        {
            await result.RunAsync(() => Execute(definition));
        }

        if (property.Options.Index || property.Options.Unique)
        {
            string name = Utils.CheckConstraint((property.Options.Unique ? "UC_" : "IND_") + property.Name + "_" + table);
            string sqlIndex = "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID("
                + FormatMigrationDefault(QuoteIdentifier(table)) + ") AND name = " + FormatMigrationDefault(name) + ") "
                + $"CREATE {(property.Options.Unique ? "UNIQUE " : "")}INDEX {QuoteIdentifier(name)} "
                + $"ON {QuoteIdentifier(table)} ({QuoteIdentifier(property.Name)})";

            await result.RunAsync(() => Execute(sqlIndex));
        }
        return result;
    }

    private static string SqlIndexOption(string? value)
    {
        if (value == "True") return "ON";
        return "OFF";
    }
    #endregion
    public override string QuoteIdentifier(string identifier)
    {
        return "[" + identifier.Replace("]", "]]") + "]";
    }

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
        if (dbType == DbType.Int16) { return "smallint"; }
        if (dbType == DbType.Int32) { return "int"; }
        if (dbType == DbType.Int64) { return "bigint"; }
        if (dbType == DbType.Double) { return "float"; }
        if (dbType == DbType.Boolean) { return "bit"; }
        if (dbType == DbType.DateTime) { return "datetime"; }
        if (dbType == DbType.Date) { return "date"; }
        if (dbType == DbType.Time) { return "time"; }
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
