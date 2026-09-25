using AventusSharp.Data;
using AventusSharp.Data.Attributes;
using AventusSharp.Data.Manager.DB;

namespace AventusSharpTest.Integration.Models;

[SqlName("no_cache_cycle_rooms")]
public sealed class NoCacheCycleRoom : Storable<NoCacheCycleRoom>
{
    public string Name { get; set; } = "";

    [ReverseLink(nameof(NoCacheCycleLamp.Room))]
    [AutoRead]
    public List<NoCacheCycleLamp> Lamps { get; set; } = [];
}

public sealed class NoCacheCycleRoomManager : DatabaseDM<NoCacheCycleRoomManager, NoCacheCycleRoom>
{
    protected override bool? UseLocalCache() => false;
}

[SqlName("no_cache_cycle_lamps")]
public sealed class NoCacheCycleLamp : Storable<NoCacheCycleLamp>
{
    public string Name { get; set; } = "";

    [AutoRead]
    public NoCacheCycleRoom Room { get; set; } = null!;
}

public sealed class NoCacheCycleLampManager : DatabaseDM<NoCacheCycleLampManager, NoCacheCycleLamp>
{
    protected override bool? UseLocalCache() => false;
}
