using AventusSharp.Data.Storage.Default;
using System.Collections.Generic;

namespace AventusSharp.Data.Storage.Mysql.Queries;

public class DropColumn
{
    public static List<string> PrepareSQL(TableInfo table, string column, MySQLStorage storage)
    {

        return [$"ALTER TABLE {storage.QuoteIdentifier(table.SqlTableName)} DROP COLUMN {storage.QuoteIdentifier(column)}"];
    }
}
