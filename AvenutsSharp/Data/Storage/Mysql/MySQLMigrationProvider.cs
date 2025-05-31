using AventusSharp.Data.Storage.Default;

namespace AventusSharp.Data.Storage.Mysql;

public class MySQLMigrationProvider : StorageMigrationProvider<MySQLStorage>
{
    public MySQLMigrationProvider(MySQLStorage storage) : base(storage)
    {
    }
}