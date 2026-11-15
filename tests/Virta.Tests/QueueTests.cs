using System.Buffers;

using Virta.Queues;

namespace Virta.Tests;

public sealed class QueueTests
{
    [Theory]
    [InlineData("Chunked")]
    [InlineData("Pooled")]
    [InlineData("Ring")]
    [InlineData("NonPowerOfTwoRing")]
    public void QueuesPreserveOrderAndMutableReferencesAcrossBoundaries(string storage)
    {
        var (queue, owner) = CreateQueue<int>(storage);
        using var storageOwner = owner;
        var expected = new Queue<int>();
        var random = new Random(7919);
        for (int iteration = 0; iteration < 20_000; iteration++)
        {
            if (expected.Count == 0 || (expected.Count < 2000 && random.Next(3) != 0))
            {
                queue.PushBack(iteration);
                expected.Enqueue(iteration);
            }
            else
            {
                Assert.Equal(expected.Dequeue(), queue.Front);
                queue.PopFront();
            }

            Assert.Equal(expected.Count, queue.Count);
            if (expected.Count > 0)
            {
                Assert.Equal(expected.Peek(), queue.Front);
                Assert.Equal(expected.Last(), queue[queue.Count - 1]);
                int index = random.Next(expected.Count);
                Assert.Equal(expected.ElementAt(index), queue[index]);
            }
        }

        while (expected.Count > 0)
        {
            Assert.Equal(expected.Dequeue(), queue.Front);
            queue.PopFront();
        }

        queue.PushBack(7);
        ref readonly int front = ref queue.Front;
        queue[0] = 9;
        Assert.Equal(9, front);
        Assert.Equal(9, queue.Front);
    }

    [Theory]
    [InlineData("Chunked")]
    [InlineData("Pooled")]
    [InlineData("Ring")]
    public void EmptyInvalidAndDisposedOperationsThrow(string storage)
    {
        var (queue, owner) = CreateQueue<int>(storage);
        using var storageOwner = owner;
        Assert.Throws<InvalidOperationException>(() => queue.PopFront());
        Assert.Throws<InvalidOperationException>(() => { _ = queue.Front; });
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = queue[0]; });
        queue.PushBack(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = queue[-1]; });
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = queue[1]; });
        if (owner is not null)
        {
            owner.Dispose();
            owner.Dispose();
            Assert.Throws<ObjectDisposedException>(() => queue.PushBack(2));
            Assert.Throws<ObjectDisposedException>(() => queue.PopFront());
            Assert.Throws<ObjectDisposedException>(() => { _ = queue[0]; });
        }
        else
        {
            Assert.False(queue is IDisposable);
        }
    }

    [Fact]
    public void FullRingRejectsInsertionWithoutChangingContents()
    {
        var queue = new RingBufferQueue<int>(1);
        queue.PushBack(1);
        Assert.Throws<InvalidOperationException>(() => queue.PushBack(2));
        Assert.Equal(1, queue.Count);
        Assert.Equal(1, queue.Front);
        queue.PopFront();
        queue.PushBack(3);
        Assert.Equal(3, queue.Front);
        Assert.Throws<ArgumentOutOfRangeException>(() => new RingBufferQueue<int>(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RingBufferQueue<int>(-1));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PooledQueuesReturnAllStorageAndClearReferences(bool isChunked)
    {
        var pool = new TrackingPool<string>();
        if (isChunked)
        {
            VerifyReturnedStorage(new ChunkedArrayQueue<string>(pool), pool);
        }
        else
        {
            VerifyReturnedStorage(new ArrayPoolQueue<string>(16, pool), pool);
        }
    }

    private static void VerifyReturnedStorage<TQueue>(TQueue queue, TrackingPool<string> pool)
        where TQueue : IDabaQueue<string>, IDisposable
    {
        using var storageOwner = queue;
        for (int index = 0; index < 1000; index++)
        {
            queue.PushBack(index.ToString());
        }

        for (int index = 0; index < 995; index++)
        {
            queue.PopFront();
        }

        queue.Dispose();
        Assert.Empty(pool.Outstanding);
        Assert.True(pool.ReturnCount > 1);
    }

    private static (IDabaQueue<TElement> Queue, IDisposable? Owner) CreateQueue<TElement>(string storage)
    {
        switch (storage)
        {
            case "Chunked":
                var chunked = new ChunkedArrayQueue<TElement>();
                return (chunked, chunked);
            case "Pooled":
                var pooled = new ArrayPoolQueue<TElement>();
                return (pooled, pooled);
            case "Ring":
                return (new RingBufferQueue<TElement>(4096), null);
            case "NonPowerOfTwoRing":
                return (new RingBufferQueue<TElement>(4093), null);
            default:
                throw new ArgumentException("Unknown storage.", nameof(storage));
        }
    }

    private sealed class TrackingPool<T> : ArrayPool<T>
    {
        public HashSet<T[]> Outstanding { get; } = [];
        public int ReturnCount { get; private set; }

        public override T[] Rent(int minimumLength)
        {
            var buffer = new T[minimumLength];
            Outstanding.Add(buffer);
            return buffer;
        }

        public override void Return(T[] array, bool clearArray = false)
        {
            Assert.True(clearArray);
            Assert.True(Outstanding.Remove(array));
            ReturnCount++;
        }
    }
}
