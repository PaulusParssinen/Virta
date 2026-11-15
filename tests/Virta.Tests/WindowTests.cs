using Virta.Operators;
using Virta.Queues;
using Virta.Tests.Baselines;

namespace Virta.Tests;

public sealed class WindowTests
{
    [Fact]
    public void SumWindowsMatchOrderedRecalculation()
        => VerifyWindows<int, int, int, SumOperator<int>>(index => index % 101 - 50, Assert.Equal);

    [Fact]
    public void MaximumWindowsMatchOrderedRecalculation()
        => VerifyWindows<int, int, int, MaximumOperator<int>>(index => index % 101 - 50, Assert.Equal);

    [Fact]
    public void MeanWindowsMatchOrderedRecalculation()
        => VerifyWindows<double, MeanPartial<double>, double, MeanOperator<double>>(
            index => index % 101 + 1, AssertClose);

    [Fact]
    public void GeometricMeanWindowsMatchOrderedRecalculation()
        => VerifyWindows<double, GeometricMeanPartial<double>, double, GeometricMeanOperator<double>>(
            index => index % 101 + 1, AssertClose);

    [Fact]
    public void StandardDeviationWindowsMatchOrderedRecalculation()
        => VerifyWindows<double, StandardDeviationPartial<double>, double, SampleStandardDeviationOperator<double>>(
            index => index % 101 + 1, AssertClose);

    [Fact]
    public void ArgumentMaximumWindowsPreserveTieOrder()
        => VerifyWindows<string, ArgumentMaximumPartial<string, int>, ArgumentMaximumPartial<string, int>,
            ArgumentMaximumOperator<string, int, OperatorTests.LengthSelector>>(
            index => index.ToString(), Assert.Equal);

    [Fact]
    public void NoncommutativeWindowsPreserveOrderThroughEveryTransition()
        => VerifyWindows<int, OrderedFingerprint, OrderedFingerprint, FingerprintOperator>(
            index => index, Assert.Equal);

    [Fact]
    public void ReferencePartialsSurviveDrainingAndRefilling()
    {
        var (windows, owners) = CreateWindows<string, string, string, ConcatenationOperator>();
        try
        {
            foreach (int size in new[] { 1, 2, 3, 7, 16, 31, 64, 127, 256 })
            {
                string[] values = Enumerable.Range(0, size).Select(index => $"{index};").ToArray();
                foreach (IFifoWindow<string, string> window in windows)
                {
                    window.InsertRange(values);
                    for (int index = 0; index < size; index++)
                    {
                        Assert.Equal(string.Concat(values.AsSpan(index).ToArray()), window.Query());
                        window.Evict();
                    }

                    Assert.Equal("", window.Query());
                    Assert.Throws<InvalidOperationException>(window.Evict);
                }
            }
        }
        finally
        {
            foreach (IDisposable owner in owners)
            {
                owner.Dispose();
            }
        }
    }

    [Fact]
    public void FullFixedWindowRejectsInsertWithoutChangingItsAggregate()
    {
        var window = new RingBufferDabaLite<int, int, int, SumOperator<int>>(1);
        window.Insert(7);
        Assert.Throws<InvalidOperationException>(() => window.Insert(9));
        Assert.Equal(7, window.Query());
        Assert.Equal(1, window.Count);
        window.Evict();
        window.Insert(11);
        Assert.Equal(11, window.Query());
    }

    [Fact]
    public void InvalidInputDoesNotChangeTheWindow()
    {
        using var window = new DabaLite<double, GeometricMeanPartial<double>, double, GeometricMeanOperator<double>>();
        window.Insert(4);
        Assert.Throws<ArgumentOutOfRangeException>(() => window.Insert(0));
        Assert.Equal(1, window.Count);
        Assert.Equal(4, window.Query());
        window.Insert(16);
        Assert.Equal(8, window.Query(), 12);
    }

    [Fact]
    public void RangeInsertionRetainsItsSuccessfulPrefixWhenAnInputFails()
    {
        using var window = new DabaLite<double, GeometricMeanPartial<double>, double, GeometricMeanOperator<double>>();
        Assert.Throws<ArgumentOutOfRangeException>(() => window.InsertRange(2, 8, -1, 16));
        Assert.Equal(2, window.Count);
        Assert.Equal(4, window.Query(), 12);
    }

    [Fact]
    public void DefaultWindowsReleaseOwnedStorageAndRejectOperationsAfterDisposal()
    {
        VerifyOwnedWindow(new DabaLite<int, int, int, SumOperator<int>>());
    }

    [Fact]
    public void BorrowedWindowsRejectNonemptyQueuesWithoutChangingThem()
    {
        using var queue = new ChunkedArrayQueue<int>();
        queue.PushBack(1);
        Assert.Throws<ArgumentException>(
            () => new DabaLite<int, int, int, SumOperator<int>, ChunkedArrayQueue<int>>(queue));
        Assert.Equal(1, queue.Front);
    }

    [Fact]
    public void BorrowedWindowsLeavePooledStorageLifetimeWithTheCaller()
    {
        using var queue = new ArrayPoolQueue<int>();
        IFifoWindow<int, int> window = new DabaLite<int, int, int, SumOperator<int>, ArrayPoolQueue<int>>(queue);
        Assert.False(window is IDisposable);
        window.InsertRange(3, 7);
        window.Evict();
        Assert.Equal(7, window.Query());

        Assert.Equal(1, queue.Count);
        queue.Dispose();
        Assert.Throws<ObjectDisposedException>(() => window.Query());
    }

    [Fact]
    public void ManagedWindowsHaveNoDisposalContract()
    {
        IFifoWindow<int, int>[] windows =
        [
            new RingBufferDabaLite<int, int, int, SumOperator<int>>(1),
            new RecalculationWindow<int, int, int, SumOperator<int>>(),
            new SubtractOnEvictWindow<int, int, int, SumOperator<int>>(),
            new TwoStacksWindow<int, int, int, SumOperator<int>>()
        ];

        foreach (IFifoWindow<int, int> window in windows)
        {
            Assert.False(window is IDisposable);
            window.Insert(5);
            Assert.Equal(5, window.Query());
            window.Evict();
            window.Insert(9);
            Assert.Equal(9, window.Query());
        }
    }

    [Fact]
    public void SubtractOnEvictUsesPrefixRemovalForNoncommutativeOperators()
    {
        var window = new SubtractOnEvictWindow<string, string, string, ConcatenationOperator>();
        window.InsertRange("first", "second", "third");
        window.Evict();
        Assert.Equal("secondthird", window.Query());
        window.Evict();
        Assert.Equal("third", window.Query());
        window.Evict();
        Assert.Equal("", window.Query());
    }

    [Fact]
    public void SubtractOnEvictMatchesRecalculationDuringSlidingUpdates()
    {
        var window = new SubtractOnEvictWindow<int, int, int, SumOperator<int>>();
        var expected = new Queue<int>();
        for (int index = 0; index < 10_000; index++)
        {
            window.Insert(index);
            expected.Enqueue(index);
            if (expected.Count > 127)
            {
                window.Evict();
                expected.Dequeue();
            }

            Assert.Equal(expected.Sum(), window.Query());
        }
    }

    [Fact]
    public void StandardDeviationPrefixRemovalMatchesSlidingRecalculation()
    {
        var window = new SubtractOnEvictWindow<double, StandardDeviationPartial<double>, double,
            SampleStandardDeviationOperator<double>>();
        var expected = new RecalculationWindow<double, StandardDeviationPartial<double>, double,
            SampleStandardDeviationOperator<double>>();
        for (int index = 0; index < 10_000; index++)
        {
            double value = index % 101 + 1;
            window.Insert(value);
            expected.Insert(value);
            if (expected.Count > 63)
            {
                window.Evict();
                expected.Evict();
            }

            AssertClose(expected.Query(), window.Query());
        }
    }

    private static void VerifyWindows<TInput, TPartial, TOutput, TOperator>(
        Func<int, TInput> createValue, Action<TOutput, TOutput> assertEqual)
        where TOperator : struct, IBinaryOperator<TInput, TPartial, TOutput>
    {
        var (windows, owners) = CreateWindows<TInput, TPartial, TOutput, TOperator>();
        var expected = new RecalculationWindow<TInput, TPartial, TOutput, TOperator>();
        var random = new Random(104729);
        try
        {
            for (int iteration = 0; iteration < 10_000; iteration++)
            {
                bool shouldInsert = expected.IsEmpty || (expected.Count < 512 && random.Next(5) < 3);
                TInput value = createValue(iteration);
                if (shouldInsert)
                {
                    expected.Insert(value);
                }
                else
                {
                    expected.Evict();
                }

                TOutput output = expected.Query();
                foreach (IFifoWindow<TInput, TOutput> window in windows)
                {
                    if (shouldInsert)
                    {
                        window.Insert(value);
                    }
                    else
                    {
                        window.Evict();
                    }

                    Assert.Equal(expected.Count, window.Count);
                    assertEqual(output, window.Query());
                }
            }
        }
        finally
        {
            foreach (IDisposable owner in owners)
            {
                owner.Dispose();
            }
        }
    }

    private static (IFifoWindow<TInput, TOutput>[] Windows, IDisposable[] Owners) CreateWindows<TInput, TPartial, TOutput, TOperator>()
        where TOperator : struct, IBinaryOperator<TInput, TPartial, TOutput>
    {
        var dabaLite = new DabaLite<TInput, TPartial, TOutput, TOperator>();
        var liteArrayQueue = new ArrayPoolQueue<TPartial>();

        return ([
            dabaLite,
            new DabaLite<TInput, TPartial, TOutput, TOperator, ArrayPoolQueue<TPartial>>(liteArrayQueue),
            new RingBufferDabaLite<TInput, TPartial, TOutput, TOperator>(4096),
            new TwoStacksWindow<TInput, TPartial, TOutput, TOperator>()
        ], [dabaLite, liteArrayQueue]);
    }

    private static void VerifyOwnedWindow<TWindow>(TWindow window)
        where TWindow : IFifoWindow<int, int>, IDisposable
    {
        using (window)
        {
            window.Insert(1);
            window.Dispose();
            window.Dispose();
            Assert.Throws<ObjectDisposedException>(() => window.Insert(2));
            Assert.Throws<ObjectDisposedException>(window.Evict);
            Assert.Throws<ObjectDisposedException>(() => window.Query());
            Assert.Throws<ObjectDisposedException>(() => window.InsertRange([]));
        }
    }

    private static void AssertClose(double expected, double actual)
    {
        Assert.True(Math.Abs(expected - actual) <= 1e-10 * Math.Max(1, Math.Abs(expected)),
            $"Expected {expected:R}, actual {actual:R}.");
    }

    internal readonly record struct OrderedFingerprint(ulong Hash, ulong Factor);

    internal readonly struct FingerprintOperator : IBinaryOperator<int, OrderedFingerprint, OrderedFingerprint>
    {
        public static OrderedFingerprint Identity => new(0, 1);
        public static OrderedFingerprint Lift(int value) => new((ulong)value, 31);
        public static OrderedFingerprint Combine(OrderedFingerprint left, OrderedFingerprint right)
            => new(unchecked(left.Hash * right.Factor + right.Hash), unchecked(left.Factor * right.Factor));
        public static OrderedFingerprint Lower(OrderedFingerprint partial) => partial;
    }

    internal readonly struct ConcatenationOperator : IInvertibleBinaryOperator<string, string, string>
    {
        public static string Identity => "";
        public static string Lift(string value) => value;
        public static string Combine(string left, string right) => left + right;
        public static string Lower(string partial) => partial;
        public static string RemovePrefix(string combined, string prefix) => combined[prefix.Length..];
    }
}
