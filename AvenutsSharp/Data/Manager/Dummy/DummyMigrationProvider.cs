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
}