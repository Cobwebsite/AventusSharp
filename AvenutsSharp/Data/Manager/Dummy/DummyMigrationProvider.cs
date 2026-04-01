using System.Threading.Tasks;
using AventusSharp.Data.Migrations;
using AventusSharp.Tools;

namespace AventusSharp.Data.Manager.Dummy;

public class DummyMigrationProvider : MigrationProvider
{
    public override Task<VoidWithError> Init()
    {
        VoidWithError result = new();
        return Task.FromResult(result);
    }

    public override Task<ResultWithError<bool>> Can(string name)
    {
        ResultWithError<bool> result = new()
        {
            Result = true
        };
        return Task.FromResult(result);
    }
    public override Task<VoidWithError> Save(string name)
    {
        return Task.FromResult(new VoidWithError());
    }

    public override Task BeforeUp(VoidWithError voidWithError)
    {
        return Task.CompletedTask;
    }

    public override Task AfterUp(VoidWithError voidWithError)
    {
        return Task.CompletedTask;
    }

    public override Task<VoidWithError> ApplyMigration<X>(IMigrationModel model)
    {
        return Task.FromResult(new VoidWithError());
    }
}