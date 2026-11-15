using Virta.Operators;

namespace Virta.Tests;

public sealed class OperatorTests
{
    [Fact]
    public void ExactOperatorsObeyIdentityAssociativityAndPrefixRemoval()
    {
        VerifyMonoid<int, int, int, SumOperator<int>>(2, 5, 9);
        VerifyMonoid<int, int, int, MaximumOperator<int>>(2, 5, 9);
        VerifyMonoid<decimal, MeanPartial<decimal>, decimal, MeanOperator<decimal>>(2, 5, 9);
        VerifyInverse<int, int, int, SumOperator<int>>(2, 5);
        VerifyInverse<decimal, MeanPartial<decimal>, decimal, MeanOperator<decimal>>(2, 5);
        Assert.Equal(2.5m, MeanOperator<decimal>.Lower(new(5m, 2)));
        Assert.Equal(2, MeanOperator<int>.Lower(new(5, 2)));
    }

    [Fact]
    public void MaximumHasAnIdentityForNegativeInfinityAndIntegralMinima()
    {
        Assert.Equal(double.NegativeInfinity, MaximumOperator<double>.Identity);
        Assert.Equal(double.NegativeInfinity,
            MaximumOperator<double>.Combine(MaximumOperator<double>.Identity, double.NegativeInfinity));
        Assert.Equal(int.MinValue, MaximumOperator<int>.Identity);
        Assert.Equal(decimal.MinValue, MaximumOperator<decimal>.Identity);
        Assert.Equal(0U, MaximumOperator<uint>.Identity);
    }

    [Fact]
    public void GeometricMeanCombinesLogarithmsAndRemovesPrefixes()
    {
        var first = GeometricMeanOperator<double>.Lift(2);
        var second = GeometricMeanOperator<double>.Lift(8);
        var combined = GeometricMeanOperator<double>.Combine(first, second);
        Assert.Equal(4, GeometricMeanOperator<double>.Lower(combined), 12);
        Assert.Equal(8, GeometricMeanOperator<double>.Lower(
            GeometricMeanOperator<double>.RemovePrefix(combined, first)), 12);
        Assert.Equal(first, GeometricMeanOperator<double>.Combine(first, GeometricMeanOperator<double>.Identity));
        Assert.Equal(first, GeometricMeanOperator<double>.Combine(GeometricMeanOperator<double>.Identity, first));
        Assert.Equal(0, GeometricMeanOperator<double>.Lower(GeometricMeanOperator<double>.Identity));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void GeometricMeanRejectsObservationsOutsideItsDomain(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GeometricMeanOperator<double>.Lift(value));
    }

    [Fact]
    public void StandardDeviationUsesCenteredMomentsForLargeOffsets()
    {
        var partial = SampleStandardDeviationOperator<double>.Identity;
        foreach (double value in new[] { 1e12 + 1, 1e12 + 2, 1e12 + 3 })
        {
            partial = SampleStandardDeviationOperator<double>.Combine(
                partial, SampleStandardDeviationOperator<double>.Lift(value));
        }

        Assert.Equal(1, SampleStandardDeviationOperator<double>.Lower(partial), 12);
        Assert.Equal(partial, SampleStandardDeviationOperator<double>.Combine(
            partial, SampleStandardDeviationOperator<double>.Identity));
        Assert.Equal(0, SampleStandardDeviationOperator<double>.Lower(SampleStandardDeviationOperator<double>.Lift(5)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SampleStandardDeviationOperator<double>.Lift(double.NaN));
    }

    [Fact]
    public void StandardDeviationRemovesPrefixesAndResetsTheEmptyAggregate()
    {
        VerifyInverse<double, StandardDeviationPartial<double>, double,
            SampleStandardDeviationOperator<double>>(2, 8);
        var prefix = SampleStandardDeviationOperator<double>.Combine(
            SampleStandardDeviationOperator<double>.Lift(1),
            SampleStandardDeviationOperator<double>.Lift(2));
        var remainder = new StandardDeviationPartial<double>(3, 4, 2);
        var combined = SampleStandardDeviationOperator<double>.Combine(prefix, remainder);
        Assert.Equal(remainder, SampleStandardDeviationOperator<double>.RemovePrefix(combined, prefix));
        Assert.Equal(combined, SampleStandardDeviationOperator<double>.RemovePrefix(
            combined, SampleStandardDeviationOperator<double>.Identity));
        Assert.Equal(SampleStandardDeviationOperator<double>.Identity,
            SampleStandardDeviationOperator<double>.RemovePrefix(combined, combined));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SampleStandardDeviationOperator<double>.RemovePrefix(prefix, combined));
    }

    [Fact]
    public void ArgumentMaximumSelectsTheFirstTieAndRepresentsEmptyResults()
    {
        VerifyMonoid<string, ArgumentMaximumPartial<string, int>, ArgumentMaximumPartial<string, int>,
            ArgumentMaximumOperator<string, int, LengthSelector>>("a", "long", "same");
        var first = ArgumentMaximumOperator<string, int, LengthSelector>.Lift("long");
        var second = ArgumentMaximumOperator<string, int, LengthSelector>.Lift("same");
        Assert.Equal("long", ArgumentMaximumOperator<string, int, LengthSelector>.Combine(first, second).Argument);
        Assert.False(ArgumentMaximumOperator<string, int, LengthSelector>.Identity.HasValue);
    }

    private static void VerifyMonoid<TInput, TPartial, TOutput, TOperator>(
        TInput first, TInput second, TInput third)
        where TOperator : struct, IBinaryOperator<TInput, TPartial, TOutput>
    {
        TPartial left = TOperator.Lift(first);
        TPartial middle = TOperator.Lift(second);
        TPartial right = TOperator.Lift(third);
        Assert.Equal(left, TOperator.Combine(TOperator.Identity, left));
        Assert.Equal(left, TOperator.Combine(left, TOperator.Identity));
        Assert.Equal(TOperator.Combine(TOperator.Combine(left, middle), right),
            TOperator.Combine(left, TOperator.Combine(middle, right)));
    }

    private static void VerifyInverse<TInput, TPartial, TOutput, TOperator>(TInput first, TInput second)
        where TOperator : struct, IInvertibleBinaryOperator<TInput, TPartial, TOutput>
    {
        TPartial prefix = TOperator.Lift(first);
        TPartial remainder = TOperator.Lift(second);
        Assert.Equal(remainder, TOperator.RemovePrefix(TOperator.Combine(prefix, remainder), prefix));
    }

    internal readonly struct LengthSelector : IKeySelector<string, int>
    {
        public static int SelectKey(string argument) => argument.Length;
    }
}
