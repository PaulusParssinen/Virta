namespace Virta.Operators;

/// <summary>Defines an aggregation that supports removing a leading subsequence.</summary>
public interface IInvertibleBinaryOperator<TInput, TPartial, TOutput>
    : IBinaryOperator<TInput, TPartial, TOutput>
{
    /// <summary>
    /// Removes a prefix: RemovePrefix(Combine(prefix, remainder), prefix) equals remainder.
    /// </summary>
    static abstract TPartial RemovePrefix(TPartial combined, TPartial prefix);
}
