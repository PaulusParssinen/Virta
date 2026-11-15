using Virta.Operators;

namespace Virta.Tests;

public sealed class ComplexityTests
{
    [Fact]
    public void DabaLiteHasBoundedCombinationCounts()
    {
        using var window = new DabaLite<int, int, int, CountingOperator>();
        VerifyBounds(window, 3, 2);
    }

    [Fact]
    public void FixedAndWarmedChunkedWindowsAllocateNoMemoryDuringSlidingUpdates()
    {
        VerifyNoAllocations(new RingBufferDabaLite<int, int, int, SumOperator<int>>(4096));
        using var dabaLite = new DabaLite<int, int, int, SumOperator<int>>();
        VerifyNoAllocations(dabaLite);
    }

    [Fact]
    public void StackWindowAllocatesNoMemoryDuringConstructionOrSlidingUpdates()
    {
        RunStackWindow();
        long before = GC.GetAllocatedBytesForCurrentThread();
        int result = RunStackWindow();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.NotEqual(0, result);
    }

    private static void VerifyBounds(IFifoWindow<int, int> window, int insertionBound, int evictionBound)
    {
        for (int iteration = 0; iteration < 20_000; iteration++)
        {
            CountingOperator.CombinationCount = 0;
            window.Insert(iteration);
            Assert.InRange(CountingOperator.CombinationCount, 0, insertionBound);
            CountingOperator.CombinationCount = 0;
            _ = window.Query();
            Assert.InRange(CountingOperator.CombinationCount, 0, 1);
        }

        while (!window.IsEmpty)
        {
            CountingOperator.CombinationCount = 0;
            window.Evict();
            Assert.InRange(CountingOperator.CombinationCount, 0, evictionBound);
        }
    }

    private static void VerifyNoAllocations(IFifoWindow<int, int> window)
    {
        for (int index = 0; index < 1024; index++)
        {
            window.Insert(index);
        }

        RunSlidingUpdates(window);
        long before = GC.GetAllocatedBytesForCurrentThread();
        int result = RunSlidingUpdates(window);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.NotEqual(0, result);
    }

    private static int RunSlidingUpdates(IFifoWindow<int, int> window)
    {
        int result = 0;
        for (int index = 0; index < 20_000; index++)
        {
            window.Evict();
            window.Insert(index);
            result = unchecked(result + window.Query());
        }

        return result;
    }

    private static int RunStackWindow()
    {
        var window = new StackDabaLite<int, int, int, SumOperator<int>>(stackalloc int[64]);
        Span<int> initialValues = stackalloc int[4];
        for (int index = 0; index < initialValues.Length; index++)
        {
            initialValues[index] = index + 1;
        }

        window.InsertRange(initialValues);
        int result = 0;
        for (int index = 0; index < 20_000; index++)
        {
            window.Evict();
            window.Insert(index);
            result = unchecked(result + window.Query());
        }

        return result;
    }

    private readonly struct CountingOperator : IBinaryOperator<int, int, int>
    {
        public static int CombinationCount { get; set; }
        public static int Identity => 0;
        public static int Lift(int value) => value;
        public static int Combine(int left, int right)
        {
            CombinationCount++;
            return unchecked(left + right);
        }

        public static int Lower(int partial) => partial;
    }
}
