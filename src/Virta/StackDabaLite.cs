using Virta.Operators;
using Virta.Queues;

namespace Virta;

/// <summary>
/// Provides DABA Lite over caller-owned storage without heap allocation.
/// Keep one active window per buffer and do not copy an active window.
/// </summary>
public ref struct StackDabaLite<TInput, TPartial, TOutput, TOperator> : IFifoWindow<TInput, TOutput>
    where TOperator : struct, IBinaryOperator<TInput, TPartial, TOutput>
{
    private SpanQueue<TPartial> _queue;
    private DabaLiteState<TInput, TPartial, TOutput, TOperator> _state;

    /// <summary>Uses a nonempty span as fixed-capacity storage. Existing contents are ignored.</summary>
    public StackDabaLite(Span<TPartial> buffer)
    {
        _queue = new SpanQueue<TPartial>(buffer);
        _state = new();
    }

    /// <inheritdoc />
    public readonly int Count => _queue.Count;

    /// <inheritdoc />
    public readonly bool IsEmpty => Count == 0;

    /// <inheritdoc />
    public void Insert(TInput value) => _state.Insert(ref _queue, value);

    /// <inheritdoc />
    public void InsertRange(params ReadOnlySpan<TInput> values)
    {
        _ = Count;
        foreach (TInput value in values)
        {
            Insert(value);
        }
    }

    /// <inheritdoc />
    public void Evict() => _state.Evict(ref _queue);

    /// <inheritdoc />
    public TOutput Query() => _state.Query(ref _queue);
}
