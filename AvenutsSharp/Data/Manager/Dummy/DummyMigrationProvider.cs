using AventusSharp.Data.Migrations;
using AventusSharp.Tools;

namespace AventusSharp.Data.Manager.Dummy;

public class DummyMigrationProvider : MigrationProvider
{
    public override VoidWithError Init()
    {
        VoidWithError result = new();
        return result;
    }

    public override ResultWithError<bool> Can(string name)
    {
        ResultWithError<bool> result = new()
        {
            Result = true
        };
        return result;
    }
    public override VoidWithError Save(string name)
    {
        return new();
    }

    public override void BeforeUp(VoidWithError voidWithError)
    {
    }

    public override void AfterUp(VoidWithError voidWithError)
    {
    }

    public override VoidWithError ApplyMigration<X>(IMigrationModel model)
    {
        return new();
    }
}