using AventusSharp.Data;
using AventusSharp.Data.Attributes;
using AventusSharp.Tools.Attributes;
using Newtonsoft.Json;
using AventusSharp.Tools;

namespace CSharpExport.TestFixture;

[SqlName("export_people")]
public class Person : Storable<Person>
{
    [Size(1, 80)]
    [JsonProperty("fullName")]
    public string Name { get; set; } = "";

    public int Age { get; private set; }

    public JsonSerializerSettings? Settings { get; set; }

    [NoExport]
    public string Secret { get; set; } = "";

    public string DisplayName() => Name;
}

[NoExport]
public class Hidden : Storable<Hidden>
{
    public string Value { get; set; } = "";
}

[Export]
public class Profile
{
    [JsonProperty("label")]
    public string Label { get; set; } = "";

    [NoExport]
    public string InternalValue { get; set; } = "";
}

[Export]
public enum ProfileState
{
    Draft = 1,
    Published = 4,
    [NoExport]
    Internal = 8
}

public class SampleError : GenericError
{
    public SampleError(int code, string message) : base(code, message) { }
    public string Category { get; set; } = "";
}

public class SampleResult : VoidWithError<SampleError>
{
    public string RequestId { get; set; } = "";
}
