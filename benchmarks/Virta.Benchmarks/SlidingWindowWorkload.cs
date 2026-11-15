using System.Runtime.CompilerServices;

using BenchmarkDotNet.Engines;

namespace Virta.Benchmarks;

internal static class SlidingWindowWorkload<TInput, TOutput>
{
    // RATIONALE: Expose each concrete window type at the benchmark call site instead of measuring shared interface dispatch.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TOutput Run<TWindow>(TWindow window, ReadOnlySpan<TInput> inputs, Consumer consumer)
        where TWindow : IFifoWindow<TInput, TOutput>
    {
        TOutput result = default!;
        foreach (TInput value in inputs)
        {
            window.Evict();
            window.Insert(value);
            result = window.Query();
            consumer.Consume(in result);
        }

        return result;
    }
}
