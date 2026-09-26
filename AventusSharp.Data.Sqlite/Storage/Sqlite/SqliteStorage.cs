

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Threading.Tasks;
using AventusSharp.Data.Attributes;
using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Manager.DB.Builders;
using AventusSharp.Data.Migrations;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Storage.Default.TableMember;
using AventusSharp.Data.Storage.Relational;
using AventusSharp.Tools;
using Microsoft.Data.Sqlite;

namespace AventusSharp.Data.Storage.Sqlite;

public class SqliteCredentials : StorageCredentials
{
    public SqliteCredentials(string database)
    {
        Database = database;
    }
}
public class SqliteStorage : DefaultDBStorage<SqliteStorage>
{
    protected bool CreateDatabase { get; set; }
    protected SqliteMigrationProvider MigrationProvider { get; }
    public SqliteStorage(string path, bool createDatabase = true) : base(new SqliteCredentials(path))
    {
        CreateDatabase = createDatabase;
        MigrationProvider = new SqliteMigrationProvider(this);
    }

    public override DbConnection GetConnection()
    {
        SqliteConnectionStringBuilder builder = new()
        {
            DataSource = Database,
            ForeignKeys = true,
        };

        SqliteConnection connection = new(builder.ConnectionString);
        connection.CreateFunction<string?, int?>("YEAR", value => DatePart(value, part => part.Year));
        connection.CreateFunction<string?, int?>("MONTH", value => DatePart(value, part => part.Month));
        connection.CreateFunction<string?, int?>("DAY", value => DatePart(value, part => part.Day));
        connection.CreateFunction<string?, int?>("HOUR", value => DatePart(value, part => part.Hour));
        connection.CreateFunction<string?, int?>("MINUTE", value => DatePart(value, part => part.Minute));
        connection.CreateFunction<string?, int?>("SECOND", value => DatePart(value, part => part.Second));
        return connection;
    }

    private static int? DatePart(string? value, Func<DateTime, int> selector)
    {
        string[] supportedFormats =
        [
            "yyyy-MM-dd",
            "yyyy-MM-dd HH-mm-ss",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm:ss.fffffff"
        ];
        if (DateTime.TryParseExact(
                value,
                supportedFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out DateTime date)
            || DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces,
            out date))
        {
            return selector(date);
        }
        return null;
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
            if (e is SqliteException exception)
            {
                result.Errors.Add(new DataError(DataErrorCode.UnknownError, e));
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
            SqliteConnection mySqlConnection = (SqliteConnection)GetConnection();
            SqliteCommand command = mySqlConnection.CreateCommand();
            command.CommandType = System.Data.CommandType.Text;
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
        return new SqliteParameter();
    }

    public override async Task<ResultWithError<bool>> ResetStorage()
    {
        ResultWithError<bool> result = new();
        if (Environment.GetCommandLineArgs().Contains("--export-info"))
        {
            result.Result = true;
            return result;
        }

        // Récupérer toutes les tables existantes
        string sql = "SELECT name " +
                     "FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";

        ResultWithError<List<Dictionary<string, string?>>> queryResult = await Query(sql);
        if (!queryResult.Success || queryResult.Result == null)
        {
            result.Errors.AddRange(queryResult.Errors);
            return result;
        }

        string dropAllCmd = "PRAGMA foreign_keys = OFF;";
        foreach (Dictionary<string, string?> line in queryResult.Result)
        {
            dropAllCmd += "DROP TABLE IF EXISTS " + QuoteIdentifier(line["name"]!) + ";";
        }
        dropAllCmd += "PRAGMA foreign_keys = ON;";

        // PRAGMA foreign_keys cannot be changed while a transaction is active.
        VoidWithError executeResult = await ExecuteNoTransaction(dropAllCmd);
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
        string sql = "SELECT COUNT(*) AS nb FROM sqlite_master " +
                     "WHERE type='table' AND name = '" + table + "';";
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
        return "sqlite";
    }
    #endregion

    #region migrations
    public override async Task<ResultWithError<DbTransactionContext>> BeginMigrationTransaction()
    {
        if (getTransactionScope() != null) return await BeginTransaction();
        ResultWithError<DbTransactionContext> result = new();
        var connection = GetConnection();
        try
        {
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA foreign_keys = OFF";
            await command.ExecuteNonQueryAsync();
            var transaction = await connection.BeginTransactionAsync();
            result.Result = new DbTransactionContext(transaction, async () =>
            {
                await transaction.DisposeAsync();
                await connection.DisposeAsync();
            });
        }
        catch (Exception exception)
        {
            await connection.DisposeAsync();
            result.Errors.Add(new DataError(DataErrorCode.StorageDisconnected, exception));
        }
        return result;
    }

    protected override Task<VoidWithError> RenameMigrationProperty(string table, IMigrationProperty property)
    {
        return Execute($"ALTER TABLE {QuoteIdentifier(table)} RENAME COLUMN {QuoteIdentifier(property.OldName!)} TO {QuoteIdentifier(property.Name)}");
    }

    protected override async Task<VoidWithError> UpdateMigrationProperty(string table, IMigrationProperty property)
    {
        VoidWithError result = new();
        List<Dictionary<string, string?>>? foreignKeys = await result.ExtractAsync(() => Query("PRAGMA foreign_keys"));
        if (foreignKeys == null) return result;

        if (getTransactionScope() == null || foreignKeys.Single()["foreign_keys"] != "0")
        {
            result.Errors.Add(new DataError(DataErrorCode.ValidationError,
                "SQLite column updates require a migration transaction with foreign keys disabled."));
            return result;
        }
        List<Dictionary<string, string?>>? schema = await result.ExtractAsync(() =>
            Query("SELECT sql FROM sqlite_schema WHERE type = 'table' AND name = " + FormatMigrationDefault(table))
        );
        if (schema == null) return result;

        if (schema.Count != 1)
        {
            result.Errors.Add(new DataError(DataErrorCode.ValidationError, "The migration table does not exist: " + table));
            return result;
        }
        string original = schema[0]["sql"]!;
        int start = original.IndexOf('(');
        List<string> parts = SplitDefinitions(original[(start + 1)..], out string suffix);
        int changed = parts.FindIndex(part => Tokens(part).FirstOrDefault() is string token && Unquote(token).Equals(property.Name, StringComparison.OrdinalIgnoreCase));
        if (changed < 0)
        {
            result.Errors.Add(new DataError(DataErrorCode.ValidationError, "The migration column does not exist: " + property.Name));
            return result;
        }

        List<Dictionary<string, string?>>? values = await result.ExtractAsync(() =>
            Query($"SELECT {QuoteIdentifier(property.Name)} AS value FROM {QuoteIdentifier(table)}")
        );
        if (values == null) return result;

        Type type = System.Nullable.GetUnderlyingType(property.Type) ?? property.Type;
        foreach (var row in values)
        {
            string? value = row["value"];
            try
            {
                if (value == null && !property.Options.Nullable)
                    throw new InvalidOperationException("Existing NULL values prevent a NOT NULL migration.");
                if (value != null && type == typeof(string) && property.Options.Size is { SizeType: null } size && value.Length > size.Max)
                    throw new InvalidOperationException("Existing values exceed the requested column size.");
                if (value != null && type != typeof(string))
                {
                    if (type.IsEnum) Enum.Parse(type, value);
                    else if (type == typeof(bool))
                    {
                        if (value != "0" && value != "1") bool.Parse(value);
                    }
                    else if (type == typeof(TimeSpan)) TimeSpan.Parse(value, CultureInfo.InvariantCulture);
                    else if (type == typeof(TimeOnly)) TimeOnly.Parse(value, CultureInfo.InvariantCulture);
                    else System.Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
                }
            }
            catch (Exception exception)
            {
                result.Errors.Add(new DataError(DataErrorCode.ValidationError, exception.Message));
                return result;
            }
        }

        string newType = GetMigrationColumnType(property);
        List<string> tokens = Tokens(parts[changed]);
        int constraints = tokens.FindIndex(1, token => IsConstraint(token));
        if (constraints < 0) constraints = tokens.Count;
        List<string> preserved = tokens.Skip(constraints).ToList();
        if (preserved.Any(token => token.Equals("AUTOINCREMENT", StringComparison.OrdinalIgnoreCase)))
        {
            if (type != typeof(int) && type != typeof(long))
            {
                result.Errors.Add(new DataError(DataErrorCode.ValidationError, "An autoincrement column must remain an integer."));
                return result;
            }
            newType = "INTEGER";
        }
        for (int i = 0; i < preserved.Count; i++)
        {
            if (
                preserved[i].Equals("NOT", StringComparison.OrdinalIgnoreCase) &&
                i + 1 < preserved.Count && preserved[i + 1].Equals("NULL", StringComparison.OrdinalIgnoreCase)
            )
            {
                preserved.RemoveRange(i, 2);
                i--;
            }
            else if (preserved[i].Equals("NULL", StringComparison.OrdinalIgnoreCase)
                && (i == 0 || !preserved[i - 1].Equals("SET", StringComparison.OrdinalIgnoreCase)))
            {
                preserved.RemoveAt(i--);
            }
            else if (preserved[i].Equals("DEFAULT", StringComparison.OrdinalIgnoreCase))
            {
                int count = i + 1 < preserved.Count ? 2 : 1;
                preserved.RemoveRange(i, count);
                i--;
            }
        }

        parts[changed] = QuoteIdentifier(property.Name) + " " + newType
            + (property.Options.Nullable ? "" : " NOT NULL")
            + (property.Options.Default == null ? "" : " DEFAULT " + FormatMigrationDefault(property.Options.Default))
            + " " + string.Join(" ", preserved);

        if (property.Options.Unique && !preserved.Any(token => token.Equals("UNIQUE", StringComparison.OrdinalIgnoreCase)))
            parts[changed] += " UNIQUE";

        List<Dictionary<string, string?>>? dependencies = await result.ExtractAsync(() =>
            Query("SELECT sql FROM sqlite_schema WHERE tbl_name = " + FormatMigrationDefault(table)
                + " AND type IN ('index', 'trigger') AND sql IS NOT NULL")
        );
        List<Dictionary<string, string?>>? columns = await result.ExtractAsync(() =>
             Query($"PRAGMA table_xinfo({QuoteIdentifier(table)})")
        );
        if (dependencies == null || columns == null) return result;

        string temporary = "__migration_" + Guid.NewGuid().ToString("N");
        string? sequence = null;
        if (original.Contains("AUTOINCREMENT", StringComparison.OrdinalIgnoreCase))
        {
            List<Dictionary<string, string?>>? sequences = await result.ExtractAsync(() =>
                Query("SELECT seq FROM sqlite_sequence WHERE name = " + FormatMigrationDefault(table))
            );
            if (sequences == null) return result;

            sequence = sequences.FirstOrDefault()?["seq"];
        }
        List<string> columnNames = new();
        List<string> sourceExpressions = new();
        for (int i = 0; i < columns.Count; i++)
        {
            Dictionary<string, string?> column = columns[i];
            if (column["hidden"] != "0")
            {
                continue;
            }

            string columnName = QuoteIdentifier(column["name"]!);
            columnNames.Add(columnName);
            if (
                column["name"] == property.Name &&
                type != typeof(string) &&
                !type.IsEnum &&
                type != typeof(DateTime) &&
                type != typeof(TimeSpan) &&
                type != typeof(TimeOnly)
            )
            {
                sourceExpressions.Add($"CAST({columnName} AS {newType})");
            }
            else
            {
                sourceExpressions.Add(columnName);
            }
        }
        string names = string.Join(", ", columnNames);
        string source = string.Join(", ", sourceExpressions);
        
        await result.RunAsync(() => Execute($"CREATE TABLE {QuoteIdentifier(temporary)} ({string.Join(", ", parts)}){suffix}"));
        await result.RunAsync(() => Execute($"INSERT INTO {QuoteIdentifier(temporary)} ({names}) SELECT {source} FROM {QuoteIdentifier(table)}"));
        await result.RunAsync(() => Execute($"DROP TABLE {QuoteIdentifier(table)}"));
        await result.RunAsync(() => Execute($"ALTER TABLE {QuoteIdentifier(temporary)} RENAME TO {QuoteIdentifier(table)}"));

        if (sequence != null)
        {
            await result.RunAsync(() => Execute("UPDATE sqlite_sequence SET seq = MAX(seq, "
               + long.Parse(sequence, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)
               + ") WHERE name = " + FormatMigrationDefault(table)));
        }


        foreach (var dependency in dependencies)
        {
            await result.RunAsync(() => Execute(dependency["sql"]!));
        }
        if (property.Options.Index)
        {
            string name = Utils.CheckConstraint("IND_" + property.Name + "_" + table);
            await result.RunAsync(() => Execute($"CREATE INDEX IF NOT EXISTS {QuoteIdentifier(name)} ON {QuoteIdentifier(table)} ({QuoteIdentifier(property.Name)})"));
        }
        return result;
    }

    private static bool IsConstraint(string token)
    {
        return new[] {
            "CONSTRAINT",
            "PRIMARY",
            "NOT",
            "NULL",
            "UNIQUE",
            "CHECK",
            "DEFAULT",
            "COLLATE",
            "REFERENCES",
            "GENERATED"
        }.Contains(token, StringComparer.OrdinalIgnoreCase);
    }

    private static string Unquote(string token)
    {
        if (token.Length < 2)
        {
            return token;
        }
        if (token[0] is '"' or '`' or '[')
        {
            string closingQuote = token[^1].ToString();
            return token[1..^1].Replace(closingQuote + closingQuote, closingQuote);
        }
        return token;
    }

    // Quoted strings/identifiers and parenthesized expressions are kept as whole tokens.
    private static List<string> Tokens(string text)
    {
        List<string> tokens = [];
        for (int i = 0; i < text.Length;)
        {
            if (char.IsWhiteSpace(text[i])) { i++; continue; }
            int start = i;
            if (text[i] is '\'' or '"' or '`' or '[')
            {
                char close = text[i] == '[' ? ']' : text[i];
                i++;
                while (i < text.Length)
                {
                    if (text[i++] != close) continue;
                    if (i < text.Length && text[i] == close) { i++; continue; }
                    break;
                }
            }
            else if (text[i] == '(')
            {
                int depth = 0;
                do
                {
                    if (text[i] is '\'' or '"' or '`' or '[')
                    {
                        char close = text[i++] == '[' ? ']' : text[i - 1];
                        while (i < text.Length)
                        {
                            if (text[i++] != close) continue;
                            if (i < text.Length && text[i] == close) { i++; continue; }
                            break;
                        }
                        continue;
                    }
                    if (text[i] == '(') depth++;
                    else if (text[i] == ')') depth--;
                    i++;
                } while (i < text.Length && depth > 0);
            }
            else
                while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != '(') i++;
            tokens.Add(text[start..i]);
        }
        return tokens;
    }

    private static List<string> SplitDefinitions(string text, out string suffix)
    {
        List<string> parts = [];
        int start = 0;
        int depth = 0;
        char? quote = null;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quote != null)
            {
                if (c != quote) continue;
                if (i + 1 < text.Length && text[i + 1] == quote) { i++; continue; }
                quote = null;
            }
            else if (c is '\'' or '"' or '`' or '[') quote = c == '[' ? ']' : c;
            else if (c == '(') depth++;
            else if (c == ')' && depth-- == 0)
            {
                parts.Add(text[start..i].Trim());
                suffix = text[(i + 1)..];
                return parts;
            }
            else if (c == ',' && depth == 0)
            {
                parts.Add(text[start..i].Trim());
                start = i + 1;
            }
        }
        throw new InvalidOperationException("Invalid SQLite table definition.");
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
