using Virta.Operators;

namespace Virta.Tests.Baselines;

/// <summary>Provides amortized constant-time updates and constant-time queries using two stacks.</summary>
public sealed class TwoStacksWindow<TInput, TPartial, TOutput, TOperator> : IFifoWindow<TInput, TOutput>
    where TOperator : struct, IBinaryOperator<TInput, TPartial, TOutput>
{
    private readonly Stack<Entry> _front = new();
    private readonly Stack<Entry> _back = new();

    /// <inheritdoc />
    public int Count => _front.Count + _back.Count;

    /// <inheritdoc />
    public bool IsEmpty => Count == 0;

    /// <inheritdoc />
    public void Insert(TInput value)
    {
        TPartial lifted = TOperator.Lift(value);
        TPartial previous = _back.Count == 0 ? TOperator.Identity : _back.Peek().Aggregate;
        _back.Push(new Entry(lifted, TOperator.Combine(previous, lifted)));
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
        if (_front.Count == 0)
        {
            while (_back.Count > 0)
            {
                TPartial value = _back.Peek().Value;
                TPartial previous = _front.Count == 0 ? TOperator.Identity : _front.Peek().Aggregate;
                _front.Push(new Entry(value, TOperator.Combine(value, previous)));
                _back.Pop();
            }
        }

        _front.Pop();
    }

    /// <inheritdoc />
    public TOutput Query()
    {
        TPartial front = _front.Count == 0 ? TOperator.Identity : _front.Peek().Aggregate;
        TPartial back = _back.Count == 0 ? TOperator.Identity : _back.Peek().Aggregate;
        return TOperator.Lower(TOperator.Combine(front, back));
    }

    private readonly record struct Entry(TPartial Value, TPartial Aggregate);
}
