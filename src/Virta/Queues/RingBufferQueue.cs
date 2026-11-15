using System.Runtime.CompilerServices;

namespace Virta.Queues;

/// <summary>Provides fixed-capacity storage with no allocation after construction.</summary>
public sealed class RingBufferQueue<TElement> : IDabaQueue<TElement>
{
    private readonly TElement[] _buffer;
    private int _front;
    private int _count;

    /// <summary>Creates an empty queue with a positive capacity in elements.</summary>
    public RingBufferQueue(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _buffer = new TElement[capacity];
    }

    /// <inheritdoc />
    public int Count => _count;

    /// <inheritdoc />
    public ref TElement this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _count);
            return ref _buffer[GetPosition(index)];
        }
    }

    /// <inheritdoc />
    public ref readonly TElement Front
    {
        get
        {
            ThrowIfEmpty();
            return ref _buffer[_front];
        }
    }

    /// <inheritdoc />
    public void PushBack(TElement element)
    {
        if (_count == _buffer.Length)
        {
            throw new InvalidOperationException("The queue is full.");
        }

        _buffer[GetPosition(_count)] = element;
        _count++;
    }

    /// <inheritdoc />
    public void PopFront()
    {
        ThrowIfEmpty();
        if (RuntimeHelpers.IsReferenceOrContainsReferences<TElement>())
        {
            _buffer[_front] = default!;
        }

        _front = GetPosition(1);
        _count--;
    }

    // RATIONALE: Keep wrapping and array access together so the caller can reuse buffer-length loads.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetPosition(int index)
    {
        uint position = (uint)_front + (uint)index;
        return (int)(position >= (uint)_buffer.Length ? position - (uint)_buffer.Length : position);
    }

    private void ThrowIfEmpty()
    {
        if (_count == 0)
        {
            throw new InvalidOperationException("The queue is empty.");
        }
    }
}
