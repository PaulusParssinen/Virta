namespace Virta.Operators;

/// <summary>
/// Defines an associative, order-preserving aggregation with a two-sided identity.
/// Implementations must not mutate their arguments or shared state.
/// </summary>
public interface IBinaryOperator<TInput, TPartial, TOutput>
{
    /// <summary>Gets the identity element.</summary>
    static abstract TPartial Identity { get; }

    /// <summary>Converts an input into its partial aggregate.</summary>
    static abstract TPartial Lift(TInput value);

    /// <summary>Combines consecutive left and right subsequences in that order.</summary>
    static abstract TPartial Combine(TPartial left, TPartial right);

    /// <summary>Converts a partial aggregate, including the identity, into an output.</summary>
    static abstract TOutput Lower(TPartial partial);
}
