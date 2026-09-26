using AventusSharp.Data;
using AventusSharp.Data.Attributes;

namespace AventusSharpTest.Integration.Models;

[SqlName("select")]
public sealed class QuotedIdentifierRecord : Storable<QuotedIdentifierRecord>
{
    [SqlName("from")]
    public string Value { get; set; } = "";
}
