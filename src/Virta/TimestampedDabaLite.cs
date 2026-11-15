using Virta.Operators;

namespace Virta;

/// <summary>Provides timestamped DABA Lite with inclusive prefix eviction.</summary>
public sealed class TimestampedDabaLite<TTimestamp, TInput, TPartial, TOutput, TOperator>
    : TimestampedWindow<TTimestamp, TInput, TOutput, DabaLite<TInput, TPartial, TOutput, TOperator>>
    where TTimestamp : IComparable<TTimestamp>
    where TOperator : struct, IBinaryOperator<TInput, TPartial, TOutput>
{
    /// <summary>Creates an empty timestamped window.</summary>
    public TimestampedDabaLite() : base(new DabaLite<TInput, TPartial, TOutput, TOperator>()) { }
}
