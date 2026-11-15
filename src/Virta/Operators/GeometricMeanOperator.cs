using System.Numerics;

namespace Virta.Operators;

/// <summary>Stores a sum of logarithms and its observation count.</summary>
/// <param name="LogarithmSum">The sum of natural logarithms.</param>
/// <param name="Count">The number of observations.</param>
public readonly record struct GeometricMeanPartial<T>(T LogarithmSum, long Count);

/// <summary>Computes the geometric mean of positive finite observations; an empty window returns zero.</summary>
public readonly struct GeometricMeanOperator<T>
    : IInvertibleBinaryOperator<T, GeometricMeanPartial<T>, T>
    where T : IFloatingPointIeee754<T>
{
    /// <inheritdoc />
    public static GeometricMeanPartial<T> Identity => new(T.Zero, 0);

    /// <inheritdoc />
    /// <exception cref="ArgumentOutOfRangeException">The observation is not positive and finite.</exception>
    public static GeometricMeanPartial<T> Lift(T value)
    {
        if (!T.IsFinite(value) || value <= T.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Observations must be positive and finite.");
        }

        return new(T.Log(value), 1);
    }

    /// <inheritdoc />
    public static GeometricMeanPartial<T> Combine(GeometricMeanPartial<T> left, GeometricMeanPartial<T> right)
        => new(left.LogarithmSum + right.LogarithmSum, left.Count + right.Count);

    /// <inheritdoc />
    public static T Lower(GeometricMeanPartial<T> partial)
        => partial.Count == 0 ? T.Zero : T.Exp(partial.LogarithmSum / T.CreateChecked(partial.Count));

    /// <inheritdoc />
    public static GeometricMeanPartial<T> RemovePrefix(
        GeometricMeanPartial<T> combined, GeometricMeanPartial<T> prefix)
        => new(combined.LogarithmSum - prefix.LogarithmSum, combined.Count - prefix.Count);
}
