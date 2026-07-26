using AventusSharp.Data;
using AventusSharp.Data.Attributes;

namespace AventusSharpTest.Integration.Models;

public interface ITestActuator : IStorable
{
    string Name { get; set; }
}

[SqlName("test_actuators")]
public abstract class TestActuator<T> : Storable<T>, ITestActuator
    where T : ITestActuator
{
    [Size(1, 100)]
    public string Name { get; set; } = "";
}

[SqlName("test_dimmers")]
public sealed class TestDimmer : TestActuator<TestDimmer>
{
    public int Level { get; set; }
}

[SqlName("test_relays")]
public sealed class TestRelay : TestActuator<TestRelay>
{
    public bool IsClosed { get; set; }
}

public interface ITestForcedAsset : IStorable
{
    string Label { get; set; }
}

[ForceInherit]
public abstract class TestForcedAsset<T> : Storable<T>, ITestForcedAsset
    where T : ITestForcedAsset
{
    [Size(1, 100)]
    public string Label { get; set; } = "";
}

[SqlName("test_forced_cameras")]
public sealed class TestForcedCamera : TestForcedAsset<TestForcedCamera>
{
    public int Resolution { get; set; }
}

[SqlName("test_forced_speakers")]
public sealed class TestForcedSpeaker : TestForcedAsset<TestForcedSpeaker>
{
    public int Volume { get; set; }
}

public interface ITestForcedNetworkAsset : ITestForcedAsset
{
    string IpAddress { get; set; }
}

[ForceInherit]
public abstract class TestForcedNetworkAsset<T> : TestForcedAsset<T>, ITestForcedNetworkAsset
    where T : ITestForcedNetworkAsset
{
    public string IpAddress { get; set; } = "";
}

[SqlName("test_forced_gateways")]
public sealed class TestForcedGateway : TestForcedNetworkAsset<TestForcedGateway>
{
    public int PortCount { get; set; }
}

[SqlName("test_forced_asset_bindings")]
public sealed class TestForcedAssetBinding : Storable<TestForcedAssetBinding>
{
    public string Name { get; set; } = "";

    [AutoRead]
    public TestForcedGateway Gateway { get; set; } = null!;
}
