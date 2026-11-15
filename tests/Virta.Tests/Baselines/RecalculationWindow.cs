using Virta.Operators;

namespace Virta.Tests.Baselines;

/// <summary>Provides a reference implementation that recomputes the aggregate on each query.</summary>
public sealed class RecalculationWindow<TInput, TPartial, TOutput, TOperator> : IFifoWindow<TInput, TOutput>
    where TOperator : struct, IBinaryOperator<TInput, TPartial, TOutput>
{
    private readonly Queue<TPartial> _values = new();

    /// <inheritdoc />
    public int Count => _values.Count;

    /// <inheritdoc />
    public bool IsEmpty => Count == 0;

    /// <inheritdoc />
    public void Insert(TInput value)
    {
        _values.Enqueue(TOperator.Lift(value));
    }

    /// <inheritdoc />
    public void InsertRange(params ReadOnlySpan<TInput> values)
    {
        foreach (TInput value in values)
        {
            Insert(value);
        }
    }

    /// <inheritdoc />
    public void Evict()
    {
        _values.Dequeue();
    }

    /// <inheritdoc />
    public TOutput Query()
    {
        TPartial aggregate = TOperator.Identity;
        foreach (TPartial value in _values)
        {
            aggregate = TOperator.Combine(aggregate, value);
        }

        return TOperator.Lower(aggregate);
    }
}
