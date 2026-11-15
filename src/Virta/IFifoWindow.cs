namespace Virta;

/// <summary>Maintains an aggregate in insertion order and removes observations from the front.</summary>
/// <remarks>Storage ownership and disposal are defined by the concrete implementation.</remarks>
public interface IFifoWindow<TInput, TOutput>
{
    /// <summary>Gets the number of observations.</summary>
    int Count { get; }

    /// <summary>Gets whether the window contains no observations.</summary>
    bool IsEmpty { get; }

    /// <summary>Appends an observation.</summary>
    void Insert(TInput value);

    /// <summary>Appends observations in order, with the same behavior as repeated Insert calls.</summary>
    void InsertRange(params ReadOnlySpan<TInput> values);

    /// <summary>Removes the oldest observation; an empty window throws InvalidOperationException.</summary>
    void Evict();

    /// <summary>Returns the current aggregate, or the lowered identity for an empty window.</summary>
    TOutput Query();
}
