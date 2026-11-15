using System.Numerics;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

using Virta.Operators;
using Virta.Tests.Baselines;

namespace Virta.Benchmarks;

[MemoryDiagnoser]
public class ThroughputBenchmarks<TInput, TPartial, TOutput, TOperator>
    where TInput : INumber<TInput>
    where TOperator : struct, IBinaryOperator<TInput, TPartial, TOutput>
{
    private const int OperationsPerInvocation = 1024;
    private readonly Consumer _consumer = new();
    private TwoStacksWindow<TInput, TPartial, TOutput, TOperator> _twoStacks = null!;
    private DabaLite<TInput, TPartial, TOutput, TOperator> _dabaLite = null!;
    private RingBufferDabaLite<TInput, TPartial, TOutput, TOperator> _ringBuffer = null!;
    private TInput[] _inputs = null!;

    [Params(1_000, 10_000, 1_000_000)]
    public int WindowSize { get; set; }

    [GlobalSetup]
    public void SetUp()
    {
        _twoStacks = new();
        _dabaLite = new();
        _ringBuffer = new(WindowSize);
        _inputs = Enumerable.Range(0, OperationsPerInvocation)
            .Select(index => TInput.CreateChecked(index % 101 + 1)).ToArray();

        for (int index = 0; index < WindowSize; index++)
        {
            TInput value = TInput.CreateChecked(index % 101 + 1);
            _twoStacks.Insert(value);
            _dabaLite.Insert(value);
            _ringBuffer.Insert(value);
        }
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = OperationsPerInvocation)]
    public TOutput UseTwoStacks()
        => SlidingWindowWorkload<TInput, TOutput>.Run(_twoStacks, _inputs, _consumer);

    [Benchmark(OperationsPerInvoke = OperationsPerInvocation)]
    public TOutput UseDabaLite()
        => SlidingWindowWorkload<TInput, TOutput>.Run(_dabaLite, _inputs, _consumer);

    [Benchmark(OperationsPerInvoke = OperationsPerInvocation)]
    public TOutput UseRingBufferDabaLite()
        => SlidingWindowWorkload<TInput, TOutput>.Run(_ringBuffer, _inputs, _consumer);

    [GlobalCleanup]
    public void CleanUp()
    {
        _dabaLite.Dispose();
    }
}

[MemoryDiagnoser]
public class InvertibleThroughputBenchmarks<TInput, TPartial, TOutput, TOperator>
    where TInput : INumber<TInput>
    where TOperator : struct, IInvertibleBinaryOperator<TInput, TPartial, TOutput>
{
    private const int OperationsPerInvocation = 1024;
    private readonly Consumer _consumer = new();
    private SubtractOnEvictWindow<TInput, TPartial, TOutput, TOperator> _subtraction = null!;
    private DabaLite<TInput, TPartial, TOutput, TOperator> _dabaLite = null!;
    private TInput[] _inputs = null!;

    [Params(1_000, 10_000, 1_000_000)]
    public int WindowSize { get; set; }

    [GlobalSetup]
    public void SetUp()
    {
        _subtraction = new();
        _dabaLite = new();
        _inputs = Enumerable.Range(0, OperationsPerInvocation)
            .Select(index => TInput.CreateChecked(index % 101 + 1)).ToArray();
        for (int index = 0; index < WindowSize; index++)
        {
            TInput value = TInput.CreateChecked(index % 101 + 1);
            _subtraction.Insert(value);
            _dabaLite.Insert(value);
        }
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = OperationsPerInvocation)]
    public TOutput SubtractOnEvict()
        => SlidingWindowWorkload<TInput, TOutput>.Run(_subtraction, _inputs, _consumer);

    [Benchmark(OperationsPerInvoke = OperationsPerInvocation)]
    public TOutput UseDabaLite()
        => SlidingWindowWorkload<TInput, TOutput>.Run(_dabaLite, _inputs, _consumer);

    [GlobalCleanup]
    public void CleanUp()
    {
        _dabaLite.Dispose();
    }
}

public readonly struct IdentityKeySelector : IKeySelector<int, int>
{
    public static int SelectKey(int argument) => argument;
}
