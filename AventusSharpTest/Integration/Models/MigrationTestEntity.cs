using AventusSharp.Data;
using AventusSharp.Data.Attributes;
using AventusSharp.Data.Manager.DB;

namespace AventusSharpTest.Integration.Models;

[SqlName("migration_test_entities")]
public sealed class MigrationTestEntity : Storable<MigrationTestEntity>
{
    public string Name { get; set; } = "";
}

public sealed class MigrationTestEntityManager
    : DatabaseDM<MigrationTestEntityManager, MigrationTestEntity>
{
}
