using Virta.Queues;

namespace Virta;

/// <summary>
/// Adds timestamp endpoints and inclusive prefix eviction to a first-in, first-out window.
/// Timestamps remain in insertion order; they are not sorted.
/// </summary>
public abstract class TimestampedWindow<TTimestamp, TInput, TOutput, TWindow> : IDisposable
    where TTimestamp : IComparable<TTimestamp>
    where TWindow : class, IFifoWindow<TInput, TOutput>, IDisposable
{
    private readonly TWindow _window;
    private readonly ChunkedArrayQueue<TTimestamp> _timestamps = new();

    /// <summary>Takes exclusive ownership of an empty aggregation window.</summary>
    protected TimestampedWindow(TWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (!window.IsEmpty)
        {
            throw new ArgumentException("The window must be empty.", nameof(window));
        }

        _window = window;
    }

    /// <summary>Gets the number of observations.</summary>
    public int Count => _window.Count;

    /// <summary>Gets whether the window is empty.</summary>
    public bool IsEmpty => Count == 0;

    /// <summary>Gets the first inserted timestamp; an empty window throws InvalidOperationException.</summary>
    public TTimestamp OldestTimestamp => _timestamps.Front;

    /// <summary>Gets the last inserted timestamp; an empty window throws InvalidOperationException.</summary>
    public TTimestamp NewestTimestamp
    {
        get
        {
            int count = _timestamps.Count;
            if (count == 0)
            {
                throw new InvalidOperationException("The window is empty.");
            }

            return _timestamps[count - 1];
        }
    }

    /// <summary>Appends an observation and its timestamp in insertion order.</summary>
    public void Insert(TTimestamp timestamp, TInput value)
    {
        ArgumentNullException.ThrowIfNull(timestamp);
        _window.Insert(value);
        _timestamps.PushBack(timestamp);
    }

    /// <summary>Removes the oldest observation; an empty window throws InvalidOperationException.</summary>
    public void Evict()
    {
        _window.Evict();
        _timestamps.PopFront();
    }

    /// <summary>Returns the aggregate of the current observations.</summary>
    public TOutput Query() => _window.Query();

    /// <summary>
    /// Removes the consecutive prefix whose timestamps are at most the cutoff.
    /// Stops at the first newer timestamp and returns the number removed.
    /// </summary>
    public int EvictThrough(TTimestamp inclusiveCutoff)
    {
        ArgumentNullException.ThrowIfNull(inclusiveCutoff);
        int removed = 0;
        while (!IsEmpty && OldestTimestamp.CompareTo(inclusiveCutoff) <= 0)
        {
            Evict();
            removed++;
        }

        return removed;
    }

    /// <summary>Disposes both the aggregation storage and the timestamp storage.</summary>
    public void Dispose()
    {
        _window.Dispose();
        _timestamps.Dispose();
    }
}
