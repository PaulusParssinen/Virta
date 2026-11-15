using System.Buffers;
using System.Runtime.CompilerServices;

namespace Virta.Queues;

/// <summary>Provides a pooled circular array that grows and shrinks with the queue.</summary>
public sealed class ArrayPoolQueue<TElement> : IDabaQueue<TElement>, IDisposable
{
    private readonly ArrayPool<TElement> _pool;
    private readonly int _minimumCapacity;
    private TElement[] _buffer;
    private int _front;
    private int _count;
    private bool _isDisposed;

    /// <summary>Creates a queue with a positive minimum capacity and an optional pool.</summary>
    public ArrayPoolQueue(int minimumCapacity = 16, ArrayPool<TElement>? pool = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumCapacity);
        _pool = pool ?? ArrayPool<TElement>.Shared;
        _buffer = _pool.Rent(minimumCapacity);
        _minimumCapacity = _buffer.Length;
    }

    /// <inheritdoc />
    public int Count
    {
        get
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            return _count;
        }
    }

    /// <inheritdoc />
    public ref TElement this[int index]
    {
        get
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
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
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (_count == _buffer.Length)
        {
            Resize(checked(_buffer.Length * 2));
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
        if (_buffer.Length > _minimumCapacity && _count <= _buffer.Length / 4)
        {
            Resize(Math.Max(_minimumCapacity, _buffer.Length / 2));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _pool.Return(_buffer, RuntimeHelpers.IsReferenceOrContainsReferences<TElement>());
        _buffer = [];
        _count = 0;
        _isDisposed = true;
    }

    // RATIONALE: Keep wrapping and array access together so the caller can reuse buffer-length loads.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetPosition(int index)
    {
        uint position = (uint)_front + (uint)index;
        return (int)(position >= (uint)_buffer.Length ? position - (uint)_buffer.Length : position);
    }

    private void Resize(int capacity)
    {
        TElement[] replacement = _pool.Rent(capacity);
        int firstLength = Math.Min(_count, _buffer.Length - _front);
        _buffer.AsSpan(_front, firstLength).CopyTo(replacement);
        _buffer.AsSpan(0, _count - firstLength).CopyTo(replacement.AsSpan(firstLength));
        _pool.Return(_buffer, RuntimeHelpers.IsReferenceOrContainsReferences<TElement>());
        _buffer = replacement;
        _front = 0;
    }

    private void ThrowIfEmpty()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (_count == 0)
        {
            throw new InvalidOperationException("The queue is empty.");
        }
    }
}
