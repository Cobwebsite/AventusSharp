using AventusSharp.Data.Storage.Default;
using AventusSharp.Tools;
using System.Threading.Tasks;

namespace AventusSharp.Data.Storage.Sqlite;

public class SqliteMigrationProvider : StorageMigrationProvider<SqliteStorage>
{
    private readonly SqliteStorage storage;
    public SqliteMigrationProvider(SqliteStorage storage) : base(storage)
    {
        this.storage = storage;
    }

    public override async Task AfterUp(VoidWithError result)
    {
        if (result.Success)
        {
            var check = await storage.Query("PRAGMA foreign_key_check");
            result.Errors.AddRange(check.Errors);
            if (check.Result?.Count > 0)
                result.Errors.Add(new DataError(DataErrorCode.ValidationError, "The migration violates a foreign key."));
        }
        await base.AfterUp(result);
    }
}
