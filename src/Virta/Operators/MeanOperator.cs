using System.Numerics;

namespace Virta.Operators;

/// <summary>Stores a sum and its observation count.</summary>
/// <param name="Sum">The sum of the observations.</param>
/// <param name="Count">The number of observations.</param>
public readonly record struct MeanPartial<T>(T Sum, long Count);

/// <summary>Computes an arithmetic mean; an empty window returns zero.</summary>
public readonly struct MeanOperator<T> : IInvertibleBinaryOperator<T, MeanPartial<T>, T>
    where T : INumber<T>
{
    /// <inheritdoc />
    public static MeanPartial<T> Identity => new(T.Zero, 0);

    /// <inheritdoc />
    public static MeanPartial<T> Lift(T value) => new(value, 1);

    /// <inheritdoc />
    public static MeanPartial<T> Combine(MeanPartial<T> left, MeanPartial<T> right)
        => new(left.Sum + right.Sum, left.Count + right.Count);

    /// <inheritdoc />
    public static T Lower(MeanPartial<T> partial)
        => partial.Count == 0 ? T.Zero : partial.Sum / T.CreateChecked(partial.Count);

    /// <inheritdoc />
    public static MeanPartial<T> RemovePrefix(MeanPartial<T> combined, MeanPartial<T> prefix)
        => new(combined.Sum - prefix.Sum, combined.Count - prefix.Count);
}
