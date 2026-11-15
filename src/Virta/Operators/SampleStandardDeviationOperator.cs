using System.Numerics;

namespace Virta.Operators;

/// <summary>Stores centered moments for numerically stable variance aggregation.</summary>
/// <param name="Count">The number of observations.</param>
/// <param name="Mean">The arithmetic mean.</param>
/// <param name="SumOfSquaredDeviations">The sum of squared distances from the mean.</param>
public readonly record struct StandardDeviationPartial<T>(long Count, T Mean, T SumOfSquaredDeviations);

/// <summary>Computes sample standard deviation; fewer than two observations return zero.</summary>
public readonly struct SampleStandardDeviationOperator<T>
    : IInvertibleBinaryOperator<T, StandardDeviationPartial<T>, T>
    where T : IFloatingPointIeee754<T>
{
    /// <inheritdoc />
    public static StandardDeviationPartial<T> Identity => new(0, T.Zero, T.Zero);

    /// <inheritdoc />
    public static StandardDeviationPartial<T> Lift(T value)
    {
        if (!T.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Observations must be finite.");
        }

        return new(1, value, T.Zero);
    }

    /// <inheritdoc />
    public static StandardDeviationPartial<T> Combine(
        StandardDeviationPartial<T> left, StandardDeviationPartial<T> right)
    {
        if (left.Count == 0)
        {
            return right;
        }

        if (right.Count == 0)
        {
            return left;
        }

        long count = left.Count + right.Count;
        T difference = right.Mean - left.Mean;
        T rightWeight = T.CreateChecked(right.Count) / T.CreateChecked(count);
        T mean = left.Mean + difference * rightWeight;
        T squaredDeviations = left.SumOfSquaredDeviations + right.SumOfSquaredDeviations
            + difference * difference * T.CreateChecked(left.Count) * rightWeight;

        return new(count, mean, squaredDeviations);
    }

    /// <inheritdoc />
    public static T Lower(StandardDeviationPartial<T> partial)
        => partial.Count < 2
            ? T.Zero
            : T.Sqrt(partial.SumOfSquaredDeviations / T.CreateChecked(partial.Count - 1));

    /// <inheritdoc />
    public static StandardDeviationPartial<T> RemovePrefix(
        StandardDeviationPartial<T> combined, StandardDeviationPartial<T> prefix)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(prefix.Count, combined.Count, nameof(prefix));
        if (prefix.Count == 0)
        {
            return combined;
        }

        long remainingCount = combined.Count - prefix.Count;
        if (remainingCount == 0)
        {
            return Identity;
        }

        T mean = combined.Mean
            + (combined.Mean - prefix.Mean) * T.CreateChecked(prefix.Count) / T.CreateChecked(remainingCount);
        T difference = mean - prefix.Mean;
        T squaredDeviations = combined.SumOfSquaredDeviations - prefix.SumOfSquaredDeviations
            - difference * difference * T.CreateChecked(prefix.Count)
                * (T.CreateChecked(remainingCount) / T.CreateChecked(combined.Count));

        // Subtracting centered moments can leave a negative rounding residual.
        return new(remainingCount, mean, T.Max(T.Zero, squaredDeviations));
    }
}
