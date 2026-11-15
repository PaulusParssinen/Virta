namespace Virta.Operators;

/// <summary>Selects a comparison key without allocating a delegate.</summary>
public interface IKeySelector<TArgument, TKey>
{
    /// <summary>Returns the argument's comparison key.</summary>
    static abstract TKey SelectKey(TArgument argument);
}

/// <summary>Represents a selected argument, or an empty result when HasValue is false.</summary>
/// <param name="HasValue">Whether an argument is present.</param>
/// <param name="Argument">The selected argument.</param>
/// <param name="Key">The selected comparison key.</param>
public readonly record struct ArgumentMaximumPartial<TArgument, TKey>(
    bool HasValue, TArgument? Argument, TKey? Key);

/// <summary>Selects the first argument having the greatest key, preserving insertion order for ties.</summary>
public readonly struct ArgumentMaximumOperator<TArgument, TKey, TKeySelector>
    : IBinaryOperator<TArgument, ArgumentMaximumPartial<TArgument, TKey>, ArgumentMaximumPartial<TArgument, TKey>>
    where TKey : notnull, IComparable<TKey>
    where TKeySelector : struct, IKeySelector<TArgument, TKey>
{
    /// <inheritdoc />
    public static ArgumentMaximumPartial<TArgument, TKey> Identity => default;

    /// <inheritdoc />
    public static ArgumentMaximumPartial<TArgument, TKey> Lift(TArgument value)
        => new(true, value, TKeySelector.SelectKey(value));

    /// <inheritdoc />
    public static ArgumentMaximumPartial<TArgument, TKey> Combine(
        ArgumentMaximumPartial<TArgument, TKey> left, ArgumentMaximumPartial<TArgument, TKey> right)
    {
        if (!left.HasValue)
        {
            return right;
        }

        return !right.HasValue || left.Key!.CompareTo(right.Key) >= 0 ? left : right;
    }

    /// <inheritdoc />
    public static ArgumentMaximumPartial<TArgument, TKey> Lower(ArgumentMaximumPartial<TArgument, TKey> partial)
        => partial;
}
