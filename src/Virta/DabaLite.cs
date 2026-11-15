using Virta.Operators;
using Virta.Queues;

namespace Virta;

/// <summary>Provides DABA Lite with pooled chunked storage.</summary>
public sealed class DabaLite<TInput, TPartial, TOutput, TOperator>
    : DabaLite<TInput, TPartial, TOutput, TOperator, ChunkedArrayQueue<TPartial>>, IDisposable
    where TOperator : struct, IBinaryOperator<TInput, TPartial, TOutput>
{
    /// <summary>Creates an empty window.</summary>
    public DabaLite() : base(new ChunkedArrayQueue<TPartial>()) { }

    /// <summary>Returns the owned storage to its pool and releases retained aggregate state.</summary>
    public void Dispose()
    {
        _queue.Dispose();
        _state = default;
    }
}

/// <summary>
/// Provides de-amortized aggregation with one partial value per observation and two scalar partials.
/// </summary>
/// <remarks>The supplied queue remains owned by the caller.</remarks>
public class DabaLite<TInput, TPartial, TOutput, TOperator, TQueue> : IFifoWindow<TInput, TOutput>
    where TOperator : struct, IBinaryOperator<TInput, TPartial, TOutput>
    where TQueue : class, IDabaQueue<TPartial>
{
    private protected TQueue _queue;
    private protected DabaLiteState<TInput, TPartial, TOutput, TOperator> _state = new();

    /// <summary>Borrows an empty queue. The caller must keep it alive and unmodified while the window is in use.</summary>
    public DabaLite(TQueue queue)
    {
        ArgumentNullException.ThrowIfNull(queue);
        if (queue.Count != 0)
        {
            throw new ArgumentException("The queue must be empty.", nameof(queue));
        }

        _queue = queue;
    }

    /// <inheritdoc />
    public int Count => _queue.Count;

    /// <inheritdoc />
    public bool IsEmpty => Count == 0;

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

/// <summary>Creates DABA Lite windows with the default pooled storage.</summary>
public static class DabaLite
{
    /// <summary>Creates an empty window using the specified operator.</summary>
    public static DabaLite<TInput, TPartial, TOutput, TOperator> Create<TInput, TPartial, TOutput, TOperator>()
        where TOperator : struct, IBinaryOperator<TInput, TPartial, TOutput>
        => new();
}
