using AventusSharp.Data;
using AventusSharp.Data.Attributes;
using AventusSharp.Data.Manager.DB;
using System.Linq.Expressions;
using System.Data;
using Microsoft.AspNetCore.Http;
using AventusSharp.Data.Storage.Default.TableMember;
using AventusSharp.Tools;
using AventusSharp.Hosting;

namespace AventusSharpTest.Integration.Models;

[SqlName("devices")]
public sealed class Device : Storable<Device>
{
    [Size(1, 100)]
    public string Name { get; set; } = "";

    public string Room { get; set; } = "";

    public int Brightness { get; set; }

    public double PowerConsumption { get; set; }

    public bool IsOnline { get; set; }

    public Date InstalledOn { get; set; } = new();

    public Datetime LastSeen { get; set; } = new();

    [NotInDB]
    public string RuntimeState { get; set; } = "";
}

public sealed class DeviceManager : DatabaseDM<DeviceManager, Device>
{
}

[SqlName("cache_probe_records")]
public sealed class CacheProbeRecord : Storable<CacheProbeRecord>
{
    public string Name { get; set; } = "";
    public int Value { get; set; }
}

public sealed class CacheProbeRecordManager
    : DatabaseDM<CacheProbeRecordManager, CacheProbeRecord>
{
}

[SqlName("concurrent_cache_probe_records")]
public sealed class ConcurrentCacheProbeRecord : Storable<ConcurrentCacheProbeRecord>
{
    public string Name { get; set; } = "";
    public int Value { get; set; }
}

public sealed class ConcurrentCacheProbeRecordManager
    : DatabaseDM<ConcurrentCacheProbeRecordManager, ConcurrentCacheProbeRecord>
{
}

[VisibleScopedRecord]
[SqlName("scoped_records")]
public sealed class ScopedRecord : Storable<ScopedRecord>
{
    public string Name { get; set; } = "";
    public int Value { get; set; }
    public bool IsVisible { get; set; }
}

public sealed class ScopedRecordManager : DatabaseDM<ScopedRecordManager, ScopedRecord>
{
}

public sealed class VisibleScopedRecordAttribute : Scope<ScopedRecord>
{
    public override Expression<Func<ScopedRecord, bool>>? Where(IAventusContext? context) =>
        item => item.IsVisible;
}

public sealed class HighValueScopedRecord : Scope<ScopedRecord>
{
    public override Expression<Func<ScopedRecord, bool>>? Where(IAventusContext? context) =>
        item => item.Value >= 50;
}

[VisibleIncludedRecord]
[SqlName("included_scoped_records")]
public sealed class IncludedScopedRecord : Storable<IncludedScopedRecord>
{
    public string Name { get; set; } = "";
    public bool IsVisible { get; set; }
}

public sealed class VisibleIncludedRecordAttribute : Scope<IncludedScopedRecord>
{
    public override Expression<Func<IncludedScopedRecord, bool>>? Where(IAventusContext? context) =>
        item => item.IsVisible;
}

public sealed class NamedIncludedRecordScope : Scope<IncludedScopedRecord>
{
    public override Expression<Func<IncludedScopedRecord, bool>>? Where(IAventusContext? context) =>
        item => item.Name.StartsWith("Manual");
}

[SqlName("included_scoped_holders")]
public sealed class IncludedScopedHolder : Storable<IncludedScopedHolder>
{
    public string Name { get; set; } = "";
    public IncludedScopedRecord Record { get; set; } = null!;
}

[SqlName("timestamped_records")]
public sealed class TimestampedRecord : StorableTimestamp<TimestampedRecord>
{
    public string Name { get; set; } = "";
}

public sealed class TimestampedRecordManager
    : DatabaseDM<TimestampedRecordManager, TimestampedRecord>
{
}

[SqlName("attribute_records")]
public sealed class AttributeRecord : Storable<AttributeRecord>
{
    [Default(7)]
    public int Priority { get; set; }

    [Default("standard")]
    public string Category { get; set; } = "";

    [LowercaseInMemory]
    public string Code { get; set; } = "";

    [EvenValue]
    public int EvenValue { get; set; }

    [AventusSharp.Data.Attributes.Nullable]
    [NotNullable("RequiredText must be provided")]
    public string? RequiredText { get; set; }
}

public sealed class AttributeRecordManager : DatabaseDM<AttributeRecordManager, AttributeRecord>
{
}

[SqlName("failing_bulk_transform_records")]
public sealed class FailingBulkTransformRecord : Storable<FailingBulkTransformRecord>
{
    [LowercaseInMemory]
    public string NormalizedBeforeFailure { get; set; } = "";

    [ThrowingFromSql]
    public string FailingValue { get; set; } = "";
}

public sealed class FailingBulkTransformRecordManager
    : DatabaseDM<FailingBulkTransformRecordManager, FailingBulkTransformRecord>
{
}

public sealed class ThrowingFromSqlAttribute : SqlTransform<string>
{
    public override object? ToSql(string value, TableMemberInfoSql member) =>
        value.ToUpperInvariant();

    public override string FromSql(string? value, TableMemberInfoSql member)
    {
        if (string.Equals(value, "TRIGGER", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("intentional FromSql failure");
        }
        return value?.ToLowerInvariant() ?? "";
    }

    public override DbType? GetDbType(TableMemberInfoSql member) => DbType.String;
}

public sealed class LowercaseInMemoryAttribute : SqlTransform<string>
{
    public override object? ToSql(string value, TableMemberInfoSql member) =>
        value.ToUpperInvariant();

    public override string FromSql(string? value, TableMemberInfoSql member) =>
        value?.ToLowerInvariant() ?? "";

    public override DbType? GetDbType(TableMemberInfoSql member) => DbType.String;
}

public sealed class EvenValueAttribute : ValidationAttribute
{
    public override Task<ValidationResult> IsValid(object? value, ValidationContext context)
    {
        if (value is int number && number % 2 != 0)
        {
            return Task.FromResult(new ValidationResult(
                $"{context.FieldName} must be even during {context.Action}",
                context.FieldName));
        }
        return Task.FromResult(ValidationResult.Success);
    }
}

[SqlName("transformed_bool_records")]
public sealed class TransformedBoolRecord : Storable<TransformedBoolRecord>
{
    public string Name { get; set; } = "";

    [BooleanAsYesNo]
    public bool Deleted { get; set; }
}

public sealed class TransformedBoolRecordManager
    : DatabaseDM<TransformedBoolRecordManager, TransformedBoolRecord>
{
}

public sealed class BooleanAsYesNoAttribute : SqlTransform<bool>
{
    public override object? ToSql(bool value, TableMemberInfoSql member) =>
        value ? "Y" : "N";

    public override bool FromSql(string? value, TableMemberInfoSql member) =>
        string.Equals(value, "Y", StringComparison.OrdinalIgnoreCase);

    public override DbType? GetDbType(TableMemberInfoSql member) => DbType.String;
}

[SqlName("transformed_number_records")]
public sealed class TransformedNumberRecord : Storable<TransformedNumberRecord>
{
    public string Name { get; set; } = "";

    [OffsetNumber]
    public int Number { get; set; }

    [OffsetNumber]
    public int OtherNumber { get; set; }
}

public sealed class TransformedNumberRecordManager
    : DatabaseDM<TransformedNumberRecordManager, TransformedNumberRecord>
{
}

public sealed class OffsetNumberAttribute : SqlTransform<int>
{
    public override object? ToSql(int value, TableMemberInfoSql member) =>
        (long)value + 10_000L;

    public override int FromSql(string? value, TableMemberInfoSql member) =>
        checked((int)(long.Parse(value ?? "10000") - 10_000L));

    public override DbType? GetDbType(TableMemberInfoSql member) => DbType.Int64;
}

[SqlName("throwing_query_transform_records")]
public sealed class ThrowingQueryTransformRecord
    : Storable<ThrowingQueryTransformRecord>
{
    public string Name { get; set; } = "";

    [ThrowingQueryTransform]
    public int Number { get; set; }
}

public sealed class ThrowingQueryTransformRecordManager
    : DatabaseDM<ThrowingQueryTransformRecordManager,
        ThrowingQueryTransformRecord>
{
}

public sealed class ThrowingQueryTransformAttribute : SqlTransform<int>
{
    public override object? ToSql(int value, TableMemberInfoSql member)
    {
        if (value == 13)
        {
            throw new InvalidOperationException("Query transform rejected 13");
        }
        return value;
    }

    public override int FromSql(string? value, TableMemberInfoSql member) =>
        int.Parse(value ?? "0");

    public override DbType? GetDbType(TableMemberInfoSql member) => DbType.Int32;
}

public enum PrimitiveRecordState
{
    Unknown,
    Ready,
    Disabled
}

[SqlName("primitive_records")]
public sealed class PrimitiveRecord : Storable<PrimitiveRecord>
{
    public short SmallNumber { get; set; }
    public long LargeNumber { get; set; }
    public float SingleNumber { get; set; }
    public double DoubleNumber { get; set; }
    public decimal DecimalNumber { get; set; }
    public char Letter { get; set; }
    public PrimitiveRecordState State { get; set; }
    public TimeSpan Duration { get; set; }
}

public sealed class PrimitiveRecordManager
    : DatabaseDM<PrimitiveRecordManager, PrimitiveRecord>
{
}

[SqlName("nullable_primitive_records")]
public sealed class NullablePrimitiveRecord : Storable<NullablePrimitiveRecord>
{
    public int? Number { get; set; }
    public decimal? Amount { get; set; }
    public bool? Enabled { get; set; }
    public PrimitiveRecordState? State { get; set; }
    public TimeSpan? Duration { get; set; }
}

public sealed class NullablePrimitiveRecordManager
    : DatabaseDM<NullablePrimitiveRecordManager, NullablePrimitiveRecord>
{
}
