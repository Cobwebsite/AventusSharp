using AventusSharp.Data.Storage.Default;

namespace AventusSharp.Data.Storage.Mssql;

public class MsSqlMigrationProvider : StorageMigrationProvider<MsSqlStorage>
{
    public MsSqlMigrationProvider(MsSqlStorage storage) : base(storage)
    {
    }
}