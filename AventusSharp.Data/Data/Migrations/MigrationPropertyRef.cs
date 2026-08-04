using System;

namespace AventusSharp.Data.Migrations;


public enum DeleteKind
{
    DeleteOnCascade,
    DeleteSetNull
}
public interface IMigrationPropertyRefOptions
{
    public DeleteKind? DeleteKind { get; set; }
    public string? KeyName { get; set; }
    Type? KeyKind { get; set; }
}
public class MigrationPropertyRefOptions<T> : MigrationPropertyOptions<T>, IMigrationPropertyRefOptions
{
    public DeleteKind? DeleteKind { get; set; }
    public string? KeyName { get; set; }
    internal Type? KeyKind { get; set; }
    Type? IMigrationPropertyRefOptions.KeyKind { get => KeyKind; set => KeyKind = value; }
}
public interface IMigrationPropertyRef : IMigrationProperty
{
    public new IMigrationPropertyRefOptions Options { get; }
}
public class MigrationPropertyRef<T, U> : MigrationProperty<T, U>, IMigrationPropertyRef where T : IStorable
{
    public MigrationPropertyRef(MigrationModel<T> table, MigrationPropertyRefOptions<U>? options) : base(table, options)
    {
    }

    public MigrationPropertyRef(MigrationModel<T> table, string name, MigrationPropertyRefOptions<U>? options) : base(table, name, options)
    {
    }

    public new IMigrationPropertyRefOptions Options => (IMigrationPropertyRefOptions)base.Options;
}


