namespace AventusSharp.Data.Storage.Default;

public sealed class MigrationForeignKey
{
    public string Table { get; }
    public string ReferencedTable { get; }
    public string? DropSql { get; }

    public MigrationForeignKey(string table, string referencedTable, string? dropSql = null)
    {
        Table = table;
        ReferencedTable = referencedTable;
        DropSql = dropSql;
    }
}
