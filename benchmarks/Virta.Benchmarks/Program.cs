using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Running;

using Virta.Operators;

namespace Virta.Benchmarks;

internal static class Program
{
    public static void Main(string[] arguments)
    {
        var configuration = ManualConfig.CreateMinimumViable()
            .AddAnalyser(DefaultConfig.Instance.GetAnalysers().ToArray())
            .AddValidator(DefaultConfig.Instance.GetValidators().ToArray())
            .AddExporter(MarkdownExporter.GitHub);

        var summaries = BenchmarkSwitcher.FromTypes(
            [
                typeof(ThroughputBenchmarks<int, int, int, SumOperator<int>>),
                typeof(ThroughputBenchmarks<int, int, int, MaximumOperator<int>>),
                typeof(ThroughputBenchmarks<double, MeanPartial<double>, double, MeanOperator<double>>),
                typeof(ThroughputBenchmarks<double, GeometricMeanPartial<double>, double, GeometricMeanOperator<double>>),
                typeof(ThroughputBenchmarks<double, StandardDeviationPartial<double>, double, SampleStandardDeviationOperator<double>>),
                typeof(ThroughputBenchmarks<int, ArgumentMaximumPartial<int, int>, ArgumentMaximumPartial<int, int>,
                    ArgumentMaximumOperator<int, int, IdentityKeySelector>>),
                typeof(InvertibleThroughputBenchmarks<int, int, int, SumOperator<int>>),
                typeof(InvertibleThroughputBenchmarks<double, MeanPartial<double>, double, MeanOperator<double>>),
                typeof(InvertibleThroughputBenchmarks<double, GeometricMeanPartial<double>, double, GeometricMeanOperator<double>>),
                typeof(EvictionBurstBenchmarks),
                typeof(HotPathDisassembly)
            ]).Run(arguments, configuration);

        if (summaries.Any(summary => summary.HasCriticalValidationErrors
            || summary.Reports.Any(report => !report.Success)))
        {
            Environment.ExitCode = 1;
        }
    }
}
