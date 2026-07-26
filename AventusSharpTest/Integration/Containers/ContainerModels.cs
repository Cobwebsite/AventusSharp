using AventusSharp.Data;
using AventusSharp.Data.Attributes;
using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Storage.Default;

namespace AventusSharpTest.Integration.Containers;

public interface IContainerDevice
{
    string Name { get; set; }
    string Category { get; set; }
    string Room { get; set; }
    int Value { get; set; }
    bool Enabled { get; set; }
    string? Description { get; set; }
    DateTime InstalledAt { get; set; }
    TimeSpan Duration { get; set; }
}

[SqlName("mysql_devices")]
public sealed class MySqlDevice : StorableTimestamp<MySqlDevice>, IContainerDevice
{
    [Size(64)]
    public string Name { get; set; } = "";
    [Default("standard")]
    public string Category { get; set; } = "";
    public string Room { get; set; } = "";
    public int Value { get; set; }
    public bool Enabled { get; set; }
    [AventusSharp.Data.Attributes.Nullable]
    public string? Description { get; set; }
    public DateTime InstalledAt { get; set; }
    public TimeSpan Duration { get; set; }
}

public sealed class MySqlDeviceManager : DatabaseDM<MySqlDeviceManager, MySqlDevice>
{
    protected override IDBStorage? DefineStorage() => DatabaseContainers.MySql;
}

[SqlName("postgresql_devices")]
public sealed class PostgreSqlDevice : StorableTimestamp<PostgreSqlDevice>, IContainerDevice
{
    [Size(64)]
    public string Name { get; set; } = "";
    [Default("standard")]
    public string Category { get; set; } = "";
    public string Room { get; set; } = "";
    public int Value { get; set; }
    public bool Enabled { get; set; }
    [AventusSharp.Data.Attributes.Nullable]
    public string? Description { get; set; }
    public DateTime InstalledAt { get; set; }
    public TimeSpan Duration { get; set; }
}

public sealed class PostgreSqlDeviceManager : DatabaseDM<PostgreSqlDeviceManager, PostgreSqlDevice>
{
    protected override IDBStorage? DefineStorage() => DatabaseContainers.PostgreSql;
}

[SqlName("mssql_devices")]
public sealed class MsSqlDevice : StorableTimestamp<MsSqlDevice>, IContainerDevice
{
    [Size(64)]
    public string Name { get; set; } = "";
    [Default("standard")]
    public string Category { get; set; } = "";
    public string Room { get; set; } = "";
    public int Value { get; set; }
    public bool Enabled { get; set; }
    [AventusSharp.Data.Attributes.Nullable]
    public string? Description { get; set; }
    public DateTime InstalledAt { get; set; }
    public TimeSpan Duration { get; set; }
}

public sealed class MsSqlDeviceManager : DatabaseDM<MsSqlDeviceManager, MsSqlDevice>
{
    protected override IDBStorage? DefineStorage() => DatabaseContainers.MsSql;
}

[SqlName("mysql_relation_parents")]
public sealed class MySqlRelationParent : Storable<MySqlRelationParent>
{
    public string Name { get; set; } = "";
}

public sealed class MySqlRelationParentManager
    : DatabaseDM<MySqlRelationParentManager, MySqlRelationParent>
{
    protected override IDBStorage? DefineStorage() => DatabaseContainers.MySql;
}

[SqlName("mysql_relation_children")]
public sealed class MySqlRelationChild : Storable<MySqlRelationChild>
{
    public string Name { get; set; } = "";

    [AutoRead]
    [DeleteOnCascade]
    public MySqlRelationParent Parent { get; set; } = null!;
}

public sealed class MySqlRelationChildManager
    : DatabaseDM<MySqlRelationChildManager, MySqlRelationChild>
{
    protected override IDBStorage? DefineStorage() => DatabaseContainers.MySql;
}

[SqlName("mysql_relation_groups")]
public sealed class MySqlRelationGroup : Storable<MySqlRelationGroup>
{
    public string Name { get; set; } = "";

    [AutoRead]
    public List<MySqlRelationChild> Children { get; set; } = [];
}

public sealed class MySqlRelationGroupManager
    : DatabaseDM<MySqlRelationGroupManager, MySqlRelationGroup>
{
    protected override IDBStorage? DefineStorage() => DatabaseContainers.MySql;
}

[SqlName("postgresql_relation_parents")]
public sealed class PostgreSqlRelationParent : Storable<PostgreSqlRelationParent>
{
    public string Name { get; set; } = "";
}

public sealed class PostgreSqlRelationParentManager
    : DatabaseDM<PostgreSqlRelationParentManager, PostgreSqlRelationParent>
{
    protected override IDBStorage? DefineStorage() => DatabaseContainers.PostgreSql;
}

[SqlName("postgresql_relation_children")]
public sealed class PostgreSqlRelationChild : Storable<PostgreSqlRelationChild>
{
    public string Name { get; set; } = "";

    [AutoRead]
    [DeleteOnCascade]
    public PostgreSqlRelationParent Parent { get; set; } = null!;
}

public sealed class PostgreSqlRelationChildManager
    : DatabaseDM<PostgreSqlRelationChildManager, PostgreSqlRelationChild>
{
    protected override IDBStorage? DefineStorage() => DatabaseContainers.PostgreSql;
}

[SqlName("postgresql_relation_groups")]
public sealed class PostgreSqlRelationGroup : Storable<PostgreSqlRelationGroup>
{
    public string Name { get; set; } = "";

    [AutoRead]
    public List<PostgreSqlRelationChild> Children { get; set; } = [];
}

public sealed class PostgreSqlRelationGroupManager
    : DatabaseDM<PostgreSqlRelationGroupManager, PostgreSqlRelationGroup>
{
    protected override IDBStorage? DefineStorage() => DatabaseContainers.PostgreSql;
}

[SqlName("mssql_relation_parents")]
public sealed class MsSqlRelationParent : Storable<MsSqlRelationParent>
{
    public string Name { get; set; } = "";
}

public sealed class MsSqlRelationParentManager
    : DatabaseDM<MsSqlRelationParentManager, MsSqlRelationParent>
{
    protected override IDBStorage? DefineStorage() => DatabaseContainers.MsSql;
}

[SqlName("mssql_relation_children")]
public sealed class MsSqlRelationChild : Storable<MsSqlRelationChild>
{
    public string Name { get; set; } = "";

    [AutoRead]
    [DeleteOnCascade]
    public MsSqlRelationParent Parent { get; set; } = null!;
}

public sealed class MsSqlRelationChildManager
    : DatabaseDM<MsSqlRelationChildManager, MsSqlRelationChild>
{
    protected override IDBStorage? DefineStorage() => DatabaseContainers.MsSql;
}

[SqlName("mssql_relation_groups")]
public sealed class MsSqlRelationGroup : Storable<MsSqlRelationGroup>
{
    public string Name { get; set; } = "";

    [AutoRead]
    public List<MsSqlRelationChild> Children { get; set; } = [];
}

public sealed class MsSqlRelationGroupManager
    : DatabaseDM<MsSqlRelationGroupManager, MsSqlRelationGroup>
{
    protected override IDBStorage? DefineStorage() => DatabaseContainers.MsSql;
}
