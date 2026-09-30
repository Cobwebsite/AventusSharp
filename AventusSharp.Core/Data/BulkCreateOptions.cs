using AventusSharp.Tools;

namespace AventusSharp.Data;

/// <summary>Options shared by bulk creation entry points.</summary>
public sealed class BulkCreateOptions
{
    /// <summary>Insert the identifiers already assigned to the models.</summary>
    public bool WithId { get; init; }
    /// <summary>Requested maximum number of models per insert batch. The storage may lower it to fit its SQL parameter limit.</summary>
    public int BatchSize { get; init; } = 500;

    internal VoidWithError Validate()
    {
        VoidWithError result = new();
        if (BatchSize <= 0)
        {
            result.Errors.Add(new DataError(DataErrorCode.ValidationError, "Batch size must be positive."));
        }
        return result;
    }
}
