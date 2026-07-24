using AventusSharp.Data.Storage.Default;
using System.Collections.Generic;

namespace AventusSharp.Data.Storage.Mysql.Queries;

public class DropColumn
{
    public static List<string> PrepareSQL(TableInfo table, string column, MySQLStorage storage)
    {

        return [$"ALTER TABLE `{table.SqlTableName}` DROP COLUMN `{column}`"];
    }
}
