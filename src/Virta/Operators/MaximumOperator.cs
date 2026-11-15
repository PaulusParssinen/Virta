using System.Numerics;

namespace Virta.Operators;

/// <summary>Computes the maximum; the empty result is negative infinity or the numeric minimum.</summary>
public readonly struct MaximumOperator<T> : IBinaryOperator<T, T, T>
    where T : INumber<T>, IMinMaxValue<T>
{
    /// <inheritdoc />
    public static T Identity => T.CreateSaturating(double.NegativeInfinity);

    /// <inheritdoc />
    public static T Lift(T value) => value;

    /// <inheritdoc />
    public static T Combine(T left, T right) => T.Max(left, right);

    /// <inheritdoc />
    public static T Lower(T partial) => partial;
}
