using AventusSharp.Data.Storage.Default;
using System.Collections.Generic;

namespace AventusSharp.Data.Storage.Mysql.Queries;

public class RenameColumn
{
    public static List<string> PrepareSQL(TableInfo table, string oldColumn, string newColumn, MySQLStorage storage)
    {

        return [$"ALTER TABLE `{table.SqlTableName}` RENAME COLUMN `{oldColumn}` TO `{newColumn}`"];
    }
}
