using Virta;
using Virta.Operators;
using Virta.Queues;

using var total = DabaLite.Create<int, int, int, SumOperator<int>>();
total.InsertRange(10, 20, 30);
total.Evict();
Require(total.Query() == 50);

using var maximum = DabaLite.Create<int, int, int, MaximumOperator<int>>();
maximum.InsertRange(3, 7, 5);
Require(maximum.Query() == 7);

var fixedWindow = new RingBufferDabaLite<int, int, int, SumOperator<int>>(64);
fixedWindow.InsertRange(1, 2, 3);
Require(fixedWindow.Query() == 6);

var stackWindow = new StackDabaLite<double, MeanPartial<double>, double, MeanOperator<double>>(
    stackalloc MeanPartial<double>[64]);
stackWindow.InsertRange(2, 4, 6);
Require(stackWindow.Query() == 4);

using var timestamped = new TimestampedDabaLite<long, int, int, int, SumOperator<int>>();
timestamped.Insert(100, 4);
timestamped.Insert(200, 8);
Require(timestamped.EvictThrough(100) == 1 && timestamped.Query() == 8);

using var geometricMean = new DabaLite<double, GeometricMeanPartial<double>, double, GeometricMeanOperator<double>>();
geometricMean.InsertRange(2, 8);
Require(Math.Abs(geometricMean.Query() - 4) < 1e-12);

using var deviation = new DabaLite<double, StandardDeviationPartial<double>, double, SampleStandardDeviationOperator<double>>();
deviation.InsertRange(1, 2, 3);
Require(deviation.Query() == 1);

using var longest = new DabaLite<string, ArgumentMaximumPartial<string, int>, ArgumentMaximumPartial<string, int>,
    ArgumentMaximumOperator<string, int, LengthSelector>>();
longest.InsertRange("one", "longest", "two");
Require(longest.Query().Argument == "longest");

using var pooledQueue = new ArrayPoolQueue<int>();
var pooled = new DabaLite<int, int, int, SumOperator<int>, ArrayPoolQueue<int>>(pooledQueue);
pooled.InsertRange(5, 10);
Require(pooled.Query() == 15);

Console.WriteLine("Virta example: all aggregation checks passed.");

static void Require(bool condition)
{
    if (!condition)
    {
        throw new InvalidOperationException("An example produced an incorrect aggregate.");
    }
}

internal readonly struct LengthSelector : IKeySelector<string, int>
{
    public static int SelectKey(string argument) => argument.Length;
}
