using System;
using System.Collections.Generic;
using AventusSharp.Routes;
using AventusSharp.Routes.Attributes;
using AventusSharp.Routes.Response;
using AventusSharp.Tools.Attributes;
using AventusSharp.Tools;
using AventusSharp.Routes.Request;

namespace CSharpExport.TestFixture.Routes;

[Prefix("/api")]
[FrontCacheClearable]
public class ApiRouter : Router
{
    [Get]
    [Path("/people/{id}")]
    public string GetPerson(int id) => id.ToString();

    [Post]
    [Path("/people")]
    [AddFormData(false)]
    public string CreatePerson(string name, int age) => name;

    [Get]
    [FctName("FetchText")]
    public TextResponse Text() => new("text");

    [Get]
    public byte[] Download() => [];

    [Post]
    public void Ping() { }

    [Get]
    [NoRoute]
    public string Hidden() => "hidden";

    [Get]
    [NoExport]
    public string PrivateClient() => "hidden";

    [Get]
    [Post]
    [Path("/first")]
    [Path("/second")]
    public string Multiple() => "multiple";

    [Get]
    [Path("/dynamic/[Segment]")]
    public string Dynamic() => "dynamic";

    private string Segment() => Environment.GetEnvironmentVariable("SEGMENT") ?? "fallback";

    [Get]
    [PathRegex("^/regex/.*$")]
    public string RegexRoute() => "regex";

    [Get]
    [Path("/profile")]
    public Json GetProfile() => new(new Profile { Label = "Ada" });

    [Get]
    [Path("/wrapped")]
    public ResultWithError<Profile> GetWrapped() => new() { Result = new Profile { Label = "Ada" } };

    [Post]
    [Path("/empty-result")]
    public VoidWithError EmptyResult() => new();

    [Get]
    [Path("/custom-result")]
    public SampleResult GetCustomResult() => new() { RequestId = "req-1" };

    [Post]
    [Path("/form")]
    [AddFormData]
    public string SendForm(string title) => title;

    [Post]
    public string Optional(string? value = null) => value ?? "empty";

    [Post]
    [Path("/upload")]
    public string Upload(HttpFile file) => file.FileName;

    [Post]
    [Path("/upload-many")]
    public string UploadMany(List<HttpFile> files) => files.Count.ToString();

    [Post]
    [Path("/upload-nested")]
    public string UploadNested(UploadPayload payload) => payload.File.FileName;

    [Get]
    [Path("/overload/int/{value}")]
    public string Overload(int value) => value.ToString();

    [Post]
    [Path("/overload/text")]
    public string Overload(string value) => value;

    [Post]
    [Path("/overload/list-int")]
    public string OverloadList(List<int> values) => values.Count.ToString();

    [Post]
    [Path("/overload/list-string")]
    public string OverloadList(List<string> values) => values.Count.ToString();

    [Post]
    [Path("/mixed/{id}")]
    [Path("/mixed/body")]
    public string Mixed(int id) => id.ToString();
}

[Export]
public class UploadPayload
{
    public string Name { get; set; } = "";
    public HttpFile File { get; set; } = null!;
}
