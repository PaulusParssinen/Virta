using System.Buffers;
using System.Runtime.CompilerServices;

namespace Virta.Queues;

/// <summary>
/// Provides constant-time indexed access to pooled chunks.
/// Growth copies the chunk directory rather than the stored elements.
/// </summary>
/// <remarks>Creates an empty queue using the supplied pool or the shared pool.</remarks>
public sealed class ChunkedArrayQueue<TElement>(ArrayPool<TElement>? pool = null) : IDabaQueue<TElement>, IDisposable
{
    private static readonly int ChunkCapacity = Math.Max(1, 512 / Unsafe.SizeOf<TElement>());
    private readonly ArrayPool<TElement> _pool = pool ?? ArrayPool<TElement>.Shared;
    private TElement[]?[] _chunks = new TElement[4][];
    private int _frontChunk;
    private int _frontOffset;
    private int _chunkCount;
    private int _count;
    private bool _isDisposed;

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
            return ref GetElement(index);
        }
    }

    /// <inheritdoc />
    public ref readonly TElement Front
    {
        get
        {
            ThrowIfEmpty();
            return ref GetElement(0);
        }
    }

    /// <inheritdoc />
    public void PushBack(TElement element)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        uint position = (uint)_frontOffset + (uint)_count;
        int chunkOffset = (int)(position / (uint)ChunkCapacity);
        if (chunkOffset == _chunkCount)
        {
            if (_chunkCount == _chunks.Length)
            {
                GrowDirectory();
            }

            _chunks[(_frontChunk + _chunkCount) & (_chunks.Length - 1)] = _pool.Rent(ChunkCapacity);
            _chunkCount++;
        }

        GetElement(_count) = element;
        _count++;
    }

    /// <inheritdoc />
    public void PopFront()
    {
        ThrowIfEmpty();
        if (RuntimeHelpers.IsReferenceOrContainsReferences<TElement>())
        {
            GetElement(0) = default!;
        }

        _count--;
        _frontOffset++;
        if (_frontOffset == ChunkCapacity)
        {
            _pool.Return(_chunks[_frontChunk]!, RuntimeHelpers.IsReferenceOrContainsReferences<TElement>());
            _chunks[_frontChunk] = null;
            _frontChunk = (_frontChunk + 1) & (_chunks.Length - 1);
            _chunkCount--;
            _frontOffset = 0;
        }

        if (_count == 0)
        {
            _frontOffset = 0;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        for (int index = 0; index < _chunkCount; index++)
        {
            int position = (_frontChunk + index) & (_chunks.Length - 1);
            _pool.Return(_chunks[position]!, RuntimeHelpers.IsReferenceOrContainsReferences<TElement>());
            _chunks[position] = null;
        }

        _count = 0;
        _chunkCount = 0;
        _isDisposed = true;
    }

    // RATIONALE: Fold chunk addressing into the caller's array access instead of returning through a helper call.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref TElement GetElement(int index)
    {
        uint position = (uint)_frontOffset + (uint)index;
        int chunkOffset = (int)(position / (uint)ChunkCapacity);
        int elementOffset = (int)(position % (uint)ChunkCapacity);
        return ref _chunks[(_frontChunk + chunkOffset) & (_chunks.Length - 1)]![elementOffset];
    }

    private void GrowDirectory()
    {
        var replacement = new TElement[checked(_chunks.Length * 2)][];
        for (int index = 0; index < _chunkCount; index++)
        {
            replacement[index] = _chunks[(_frontChunk + index) & (_chunks.Length - 1)]!;
        }

        _chunks = replacement;
        _frontChunk = 0;
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
