using Virta.Operators;

namespace Virta.Tests;

public sealed class TimestampedWindowTests
{
    [Fact]
    public void TimestampedWindowsEvictInclusivePrefixesAndPreserveEndpoints()
    {
        VerifyTimestamps(new TimestampedDabaLite<int, int, int, int, SumOperator<int>>());
    }

    [Fact]
    public void InvalidObservationDoesNotAppendATimestamp()
    {
        using var window = new TimestampedDabaLite<int, double, GeometricMeanPartial<double>, double,
            GeometricMeanOperator<double>>();
        window.Insert(1, 4);
        Assert.Throws<ArgumentOutOfRangeException>(() => window.Insert(2, 0));
        Assert.Equal(1, window.Count);
        Assert.Equal(1, window.NewestTimestamp);
        Assert.Equal(4, window.Query());
    }

    private static void VerifyTimestamps<TWindow>(TimestampedWindow<int, int, int, TWindow> window)
        where TWindow : class, IFifoWindow<int, int>, IDisposable
    {
        using (window)
        {
            Assert.Equal(0, window.EvictThrough(100));
            Assert.Throws<InvalidOperationException>(() => window.OldestTimestamp);
            Assert.Throws<InvalidOperationException>(() => window.NewestTimestamp);
            window.Insert(1, 10);
            window.Insert(2, 20);
            window.Insert(2, 30);
            window.Insert(4, 40);
            Assert.Equal(1, window.OldestTimestamp);
            Assert.Equal(4, window.NewestTimestamp);
            Assert.Equal(3, window.EvictThrough(2));
            Assert.Equal(40, window.Query());

            window.Insert(0, 50);
            Assert.Equal(0, window.EvictThrough(3));
            Assert.Equal(4, window.OldestTimestamp);
            Assert.Equal(0, window.NewestTimestamp);
            Assert.Equal(2, window.EvictThrough(4));
            Assert.True(window.IsEmpty);
            Assert.Equal(0, window.Query());
            Assert.Throws<InvalidOperationException>(window.Evict);
            window.Insert(5, 60);
            Assert.Equal(60, window.Query());
        }

        Assert.Throws<ObjectDisposedException>(() => window.Query());
        Assert.Throws<ObjectDisposedException>(() => window.OldestTimestamp);
        Assert.Throws<ObjectDisposedException>(() => window.NewestTimestamp);
    }
}
