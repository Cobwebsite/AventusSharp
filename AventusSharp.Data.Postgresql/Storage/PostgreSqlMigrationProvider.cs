using AventusSharp.Data.Storage.Default;

namespace AventusSharp.Data.Storage.Postgresql;

public class PostgreSqlMigrationProvider : StorageMigrationProvider<PostgreSqlStorage>
{
    public PostgreSqlMigrationProvider(PostgreSqlStorage storage) : base(storage)
    {
    }
}