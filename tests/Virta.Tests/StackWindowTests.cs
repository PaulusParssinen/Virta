using Virta.Operators;
using Virta.Tests.Baselines;
using static Virta.Tests.WindowTests;

namespace Virta.Tests;

public sealed class StackWindowTests
{
    [Fact]
    public void StackWindowMatchesNoncommutativeRecalculation()
    {
        var window = new StackDabaLite<int, OrderedFingerprint, OrderedFingerprint, FingerprintOperator>(
            stackalloc OrderedFingerprint[63]);
        var expected = new RecalculationWindow<int, OrderedFingerprint, OrderedFingerprint, FingerprintOperator>();
        Assert.Equal(FingerprintOperator.Identity, window.Query());
        var random = new Random(65537);
        for (int iteration = 0; iteration < 10_000; iteration++)
        {
            if (expected.IsEmpty || (expected.Count < 63 && random.Next(2) == 0))
            {
                window.Insert(iteration);
                expected.Insert(iteration);
            }
            else
            {
                window.Evict();
                expected.Evict();
            }

            Assert.Equal(expected.Query(), window.Query());
            Assert.Equal(expected.Count, window.Count);
        }
    }

    [Fact]
    public void StackWindowPreservesStateAfterCapacityFailure()
    {
        var window = new StackDabaLite<int, int, int, SumOperator<int>>(stackalloc int[1]);
        window.Insert(7);
        bool hasThrown = false;
        try
        {
            window.Insert(9);
        }
        catch (InvalidOperationException)
        {
            hasThrown = true;
        }

        Assert.True(hasThrown);
        Assert.Equal(7, window.Query());
        window.Evict();
        window.Insert(11);
        Assert.Equal(11, window.Query());
        window.Evict();
        Assert.Equal(0, window.Query());
    }

    [Fact]
    public void StackWindowClearsEvictedReferencesAndReusesCallerStorage()
    {
        string[] buffer = new string[8];
        var window = new StackDabaLite<string, string, string, ConcatenationOperator>(buffer);
        window.InsertRange("first", "second");
        Assert.Equal("firstsecond", window.Query());
        window.Evict();
        Assert.Null(buffer[0]);
        Assert.Equal("second", window.Query());
        window.Evict();
        Assert.All(buffer, Assert.Null);
        window.Insert("third");
        Assert.Equal("third", window.Query());
    }

    [Fact]
    public void EmptyAndDefaultStackWindowsRejectOperations()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = new StackDabaLite<int, int, int, SumOperator<int>>(Span<int>.Empty);
        });
        Assert.Throws<InvalidOperationException>(() =>
        {
            StackDabaLite<int, int, int, SumOperator<int>> window = default;
            window.Insert(1);
        });
        Assert.Throws<InvalidOperationException>(() =>
        {
            var window = new StackDabaLite<int, int, int, SumOperator<int>>(stackalloc int[2]);
            window.Evict();
        });
    }
}
