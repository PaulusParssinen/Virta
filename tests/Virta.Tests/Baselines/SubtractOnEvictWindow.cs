using Virta.Operators;

namespace Virta.Tests.Baselines;

/// <summary>Maintains one aggregate using an operator that can remove prefixes.</summary>
public sealed class SubtractOnEvictWindow<TInput, TPartial, TOutput, TOperator> : IFifoWindow<TInput, TOutput>
    where TOperator : struct, IInvertibleBinaryOperator<TInput, TPartial, TOutput>
{
    private readonly Queue<TPartial> _values = new();
    private TPartial _aggregate = TOperator.Identity;

    /// <inheritdoc />
    public int Count => _values.Count;

    /// <inheritdoc />
    public bool IsEmpty => Count == 0;

    /// <inheritdoc />
    public void Insert(TInput value)
    {
        TPartial lifted = TOperator.Lift(value);
        TPartial aggregate = TOperator.Combine(_aggregate, lifted);
        _values.Enqueue(lifted);
        _aggregate = aggregate;
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
        TPartial prefix = _values.Peek();
        TPartial aggregate = _values.Count == 1
            ? TOperator.Identity
            : TOperator.RemovePrefix(_aggregate, prefix);
        _values.Dequeue();
        _aggregate = aggregate;
    }

    /// <inheritdoc />
    public TOutput Query()
    {
        return TOperator.Lower(_aggregate);
    }
}
