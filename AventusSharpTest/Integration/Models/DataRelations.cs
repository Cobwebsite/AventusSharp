using AventusSharp.Data;
using AventusSharp.Data.Attributes;
using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.CustomTableMembers;

namespace AventusSharpTest.Integration.Models;

[SqlName("test_rooms")]
public sealed class TestRoom : Storable<TestRoom>
{
    [Unique]
    [Size(1, 100)]
    public string Name { get; set; } = "";

    [AventusSharp.Data.Attributes.Index]
    public string Code { get; set; } = "";

    [AventusSharp.Data.Attributes.Nullable]
    public string? Description { get; set; }

    [ReverseLink(nameof(TestLamp.Room))]
    [AutoRead]
    public List<TestLamp> Lamps { get; set; } = [];
}

public sealed class TestRoomManager : DatabaseDM<TestRoomManager, TestRoom>
{
}

[SqlName("test_lamps")]
public sealed class TestLamp : Storable<TestLamp>
{
    [Size(1, 100)]
    public string Name { get; set; } = "";

    [AutoRead]
    [DeleteOnCascade]
    public TestRoom Room { get; set; } = null!;
}

public sealed class TestLampManager : DatabaseDM<TestLampManager, TestLamp>
{
}

[SqlName("test_scenes")]
public sealed class TestScene : Storable<TestScene>
{
    [Size(1, 100)]
    public string Name { get; set; } = "";

    [AutoRead]
    public List<TestLamp> Lamps { get; set; } = [];
}

public sealed class TestSceneManager : DatabaseDM<TestSceneManager, TestScene>
{
}

[SqlName("test_sensors")]
public sealed class TestSensor : Storable<TestSensor>
{
    public string Name { get; set; } = "";

    [AventusSharp.Data.Attributes.Nullable]
    [DeleteSetNull]
    [AutoRead]
    public TestRoom? Room { get; set; }
}

public sealed class TestSensorManager : DatabaseDM<TestSensorManager, TestSensor>
{
}

[SqlName("test_lazy_links")]
public sealed class TestLazyLink : Storable<TestLazyLink>
{
    public string Name { get; set; } = "";
    public TestRoom Room { get; set; } = null!;
}

public sealed class TestLazyLinkManager : DatabaseDM<TestLazyLinkManager, TestLazyLink>
{
}

[SqlName("test_owned_profiles")]
public sealed class TestOwnedProfile : Storable<TestOwnedProfile>
{
    [Unique]
    public string Label { get; set; } = "";
}

public sealed class TestOwnedProfileManager : DatabaseDM<TestOwnedProfileManager, TestOwnedProfile>
{
}

[SqlName("test_owners")]
public sealed class TestOwnedEntity : Storable<TestOwnedEntity>
{
    public string Name { get; set; } = "";

    [AutoCRUD]
    public TestOwnedProfile Profile { get; set; } = null!;
}

public sealed class TestOwnedEntityManager : DatabaseDM<TestOwnedEntityManager, TestOwnedEntity>
{
}

[SqlName("test_csv_items")]
public sealed class TestCsvItem : Storable<TestCsvItem>
{
    public string Name { get; set; } = "";
    public int Quantity { get; set; }
}

public sealed class TestCsvItemManager : DatabaseDM<TestCsvItemManager, TestCsvItem>
{
}

[SqlName("test_specialized_data")]
public sealed class TestSpecializedData : Storable<TestSpecializedData>
{
    public StorableListInt Numbers { get; set; } = [];
    public StorableListShort ShortNumbers { get; set; } = [];
    public StorableListLong LongNumbers { get; set; } = [];
    public StorableListFloat FloatNumbers { get; set; } = [];
    public StorableListDouble DoubleNumbers { get; set; } = [];
    public StorableListBool Flags { get; set; } = [];
    public StorableListString Labels { get; set; } = [];
    public TestDocumentFile Document { get; set; } = new();
}

public sealed class TestSpecializedDataManager
    : DatabaseDM<TestSpecializedDataManager, TestSpecializedData>
{
}

public sealed class TestDocumentFile
    : AventusSharp.Data.CustomTableMembers.AventusFile<TestSpecializedData>
{
    protected override AventusSharp.Tools.ResultWithError<string> DefineSavePath(
        TestSpecializedData instance,
        AventusSharp.Routes.Request.HttpFile file)
    {
        return new AventusSharp.Tools.ResultWithError<string>
        {
            Result = Path.Combine(Path.GetTempPath(), "aventus-sharp-tests", file.FileName)
        };
    }
}
