using BenchmarkDotNet.Attributes;

using Virta.Operators;

namespace Virta.Benchmarks;

[DisassemblyDiagnoser(maxDepth: 6, printSource: true)]
public class HotPathDisassembly
{
    private readonly RingBufferDabaLite<int, int, int, SumOperator<int>> _window = new(1024);

    [GlobalSetup]
    public void SetUp()
    {
        for (int index = 0; index < 1024; index++)
        {
            _window.Insert(index);
        }
    }

    [Benchmark]
    public int Slide()
    {
        _window.Evict();
        _window.Insert(17);
        return _window.Query();
    }
}
