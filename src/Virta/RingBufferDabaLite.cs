using Virta.Operators;
using Virta.Queues;

namespace Virta;

/// <summary>Provides fixed-capacity DABA Lite with no storage allocation after construction.</summary>
public sealed class RingBufferDabaLite<TInput, TPartial, TOutput, TOperator>
    : DabaLite<TInput, TPartial, TOutput, TOperator, RingBufferQueue<TPartial>>
    where TOperator : struct, IBinaryOperator<TInput, TPartial, TOutput>
{
    /// <summary>Creates an empty window with a positive capacity; insertion at capacity throws InvalidOperationException.</summary>
    public RingBufferDabaLite(int capacity) : base(new RingBufferQueue<TPartial>(capacity)) { }
}
