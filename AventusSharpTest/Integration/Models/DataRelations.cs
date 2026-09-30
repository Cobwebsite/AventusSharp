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

    [ReverseLink(nameof(TestSensor.Room))]
    [AutoDelete(false)]
    public List<TestSensor> Sensors { get; set; } = [];
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

[SqlName("test_projection_parents")]
public sealed class TestProjectionParent : Storable<TestProjectionParent>
{
    public string Name { get; set; } = "";

    [ReverseLink(nameof(TestProjectionChild.Parent))]
    public TestProjectionChild? Child { get; set; }
}

[SqlName("test_projection_children")]
public sealed class TestProjectionChild : Storable<TestProjectionChild>
{
    public string Name { get; set; } = "";
    public string Detail { get; set; } = "";
    public TestProjectionParent Parent { get; set; } = null!;
}

[SqlName("test_projection_wrappers")]
public sealed class TestProjectionWrapper : Storable<TestProjectionWrapper>
{
    [AutoRead]
    public TestProjectionParent Parent { get; set; } = null!;
}

[SqlName("test_auto_projection_parents")]
public sealed class TestAutoProjectionParent : Storable<TestAutoProjectionParent>
{
    public string Name { get; set; } = "";
    [ReverseLink(nameof(TestAutoProjectionChild.Parent))]
    [AutoRead]
    public TestAutoProjectionChild? Child { get; set; }
}

[SqlName("test_auto_projection_children")]
public sealed class TestAutoProjectionChild : Storable<TestAutoProjectionChild>
{
    public string Name { get; set; } = "";
    public string Detail { get; set; } = "";
    public TestAutoProjectionParent Parent { get; set; } = null!;
}

[SqlName("test_auto_projection_wrappers")]
public sealed class TestAutoProjectionWrapper : Storable<TestAutoProjectionWrapper>
{
    [AutoRead]
    public TestAutoProjectionParent Parent { get; set; } = null!;
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

[SqlName("test_policy_rooms")]
public sealed class TestPolicyRoom : Storable<TestPolicyRoom>
{
    public string Name { get; set; } = "";

    [ReverseLink(nameof(TestPolicyOptionalCascade.Room))]
    [AutoDelete(false)]
    public List<TestPolicyOptionalCascade> OptionalCascades { get; set; } = [];

    [ReverseLink(nameof(TestPolicyNullableFallback.Room))]
    [AutoDelete]
    public List<TestPolicyNullableFallback> NullableFallbacks { get; set; } = [];

    [ReverseLink(nameof(TestPolicyRequiredFallback.Room))]
    [AutoDelete]
    public List<TestPolicyRequiredFallback> RequiredFallbacks { get; set; } = [];

    [ReverseLink(nameof(TestPolicyRestricted.Room))]
    [AutoDelete(false)]
    public List<TestPolicyRestricted> Restricted { get; set; } = [];

    [ReverseLink(nameof(TestPolicyInvalidSetNull.Room))]
    public List<TestPolicyInvalidSetNull> InvalidSetNulls { get; set; } = [];
}

[SqlName("test_policy_optional_cascades")]
public sealed class TestPolicyOptionalCascade : Storable<TestPolicyOptionalCascade>
{
    [AventusSharp.Data.Attributes.Nullable]
    [DeleteOnCascade]
    public TestPolicyRoom? Room { get; set; }
}

[SqlName("test_policy_nullable_fallbacks")]
public sealed class TestPolicyNullableFallback : Storable<TestPolicyNullableFallback>
{
    [AventusSharp.Data.Attributes.Nullable]
    public TestPolicyRoom? Room { get; set; }
}

[SqlName("test_policy_required_fallbacks")]
public sealed class TestPolicyRequiredFallback : Storable<TestPolicyRequiredFallback>
{
    public TestPolicyRoom Room { get; set; } = null!;
}

[SqlName("test_policy_restricted")]
public sealed class TestPolicyRestricted : Storable<TestPolicyRestricted>
{
    public TestPolicyRoom Room { get; set; } = null!;
}

[SqlName("test_policy_invalid_set_nulls")]
public sealed class TestPolicyInvalidSetNull : Storable<TestPolicyInvalidSetNull>
{
    [DeleteSetNull]
    public TestPolicyRoom Room { get; set; } = null!;
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

[SqlName("test_value_collections")]
public sealed class TestValueCollections : Storable<TestValueCollections>
{
    public List<int> Numbers { get; set; } = [];
    public List<string?> Labels { get; set; } = [];
    public List<PrimitiveRecordState> States { get; set; } = [];
    public List<PrimitiveRecordState?> OptionalStates { get; set; } = [];
    public List<int?> OptionalNumbers { get; set; } = [];
    public Dictionary<string, int?> Counts { get; set; } = [];
    public Dictionary<int, PrimitiveRecordState?> StateByKey { get; set; } = [];
    [AventusSharp.Data.Attributes.Nullable]
    public List<string>? NullableLabels { get; set; }
}

public sealed class TestValueCollectionsManager
    : DatabaseDM<TestValueCollectionsManager, TestValueCollections>
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
