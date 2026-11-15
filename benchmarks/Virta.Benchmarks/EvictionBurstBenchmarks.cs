using BenchmarkDotNet.Attributes;

using Virta.Operators;
using Virta.Tests.Baselines;

namespace Virta.Benchmarks;

public class EvictionBurstBenchmarks
{
    private TwoStacksWindow<int, int, int, MaximumOperator<int>> _twoStacks = null!;
    private DabaLite<int, int, int, MaximumOperator<int>> _dabaLite = null!;
    private RingBufferDabaLite<int, int, int, MaximumOperator<int>> _ringBuffer = null!;

    [Params(1_000, 10_000, 1_000_000)]
    public int WindowSize { get; set; }

    [IterationSetup]
    public void SetUp()
    {
        _twoStacks = new();
        _dabaLite = new();
        _ringBuffer = new(WindowSize);
        for (int index = 0; index < WindowSize; index++)
        {
            int value = index % 101 + 1;
            _twoStacks.Insert(value);
            _dabaLite.Insert(value);
            _ringBuffer.Insert(value);
        }

        for (int index = 0; index < WindowSize; index++)
        {
            int value = index % 101 + 1;
            _twoStacks.Evict();
            _twoStacks.Insert(value);
            _dabaLite.Evict();
            _dabaLite.Insert(value);
            _ringBuffer.Evict();
            _ringBuffer.Insert(value);
        }
    }

    [Benchmark(Baseline = true)]
    public int UseTwoStacks() => Evict(_twoStacks);

    [Benchmark]
    public int UseDabaLite() => Evict(_dabaLite);

    [Benchmark]
    public int UseRingBufferDabaLite() => Evict(_ringBuffer);

    [IterationCleanup]
    public void CleanUp() => _dabaLite.Dispose();

    private static int Evict<TWindow>(TWindow window)
        where TWindow : IFifoWindow<int, int>
    {
        int result = 0;
        for (int index = 0; index < 256; index++)
        {
            window.Evict();
            result += window.Query();
        }

        return result;
    }
}
