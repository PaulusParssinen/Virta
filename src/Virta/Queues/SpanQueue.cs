using System.Runtime.CompilerServices;

namespace Virta.Queues;

internal ref struct SpanQueue<TElement> : IDabaQueue<TElement>
{
    private readonly Span<TElement> _buffer;
    private int _front;
    private int _count;

    public SpanQueue(Span<TElement> buffer)
    {
        ArgumentOutOfRangeException.ThrowIfZero(buffer.Length);
        _buffer = buffer;
    }

    public readonly int Count
    {
        get
        {
            ThrowIfUninitialized();
            return _count;
        }
    }

    public readonly ref TElement this[int index]
    {
        get
        {
            ThrowIfUninitialized();
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _count);
            return ref _buffer[GetPosition(index)];
        }
    }

    public readonly ref readonly TElement Front
    {
        get
        {
            ThrowIfEmpty();
            return ref _buffer[_front];
        }
    }

    public void PushBack(TElement element)
    {
        ThrowIfUninitialized();
        if (_count == _buffer.Length)
        {
            throw new InvalidOperationException("The window is full.");
        }

        _buffer[GetPosition(_count)] = element;
        _count++;
    }

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

    private readonly int GetPosition(int index)
    {
        uint position = (uint)_front + (uint)index;
        return (int)(position >= (uint)_buffer.Length ? position - (uint)_buffer.Length : position);
    }

    private readonly void ThrowIfUninitialized()
    {
        if (_buffer.IsEmpty)
        {
            throw new InvalidOperationException("The window has not been initialized.");
        }
    }

    private readonly void ThrowIfEmpty()
    {
        ThrowIfUninitialized();
        if (_count == 0)
        {
            throw new InvalidOperationException("The window is empty.");
        }
    }
}
