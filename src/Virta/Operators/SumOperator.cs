using System.Numerics;

namespace Virta.Operators;

/// <summary>Computes a sum using the numeric type's addition and subtraction.</summary>
public readonly struct SumOperator<T> : IInvertibleBinaryOperator<T, T, T>
    where T : INumber<T>
{
    /// <inheritdoc />
    public static T Identity => T.Zero;

    /// <inheritdoc />
    public static T Lift(T value) => value;

    /// <inheritdoc />
    public static T Combine(T left, T right) => left + right;

    /// <inheritdoc />
    public static T Lower(T partial) => partial;

    /// <inheritdoc />
    public static T RemovePrefix(T combined, T prefix) => combined - prefix;
}
