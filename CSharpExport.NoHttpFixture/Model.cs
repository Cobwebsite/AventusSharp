using AventusSharp.Data;

namespace CSharpExport.NoHttpFixture;

public sealed class Record : Storable<Record>
{
    public string Title { get; set; } = "";
}
