# Benchmarking Virta

The suite measures two distinct questions:

1. What does a sustained eviction/insertion/query update cost, relative to
   two stacks and prefix subtraction?
2. What happens when an eviction burst reaches the two-stack reversal boundary?

Benchmarks use the library directly and share the comparison implementations
from [Virta.Tests](../tests/Virta.Tests/Baselines). Every throughput benchmark
uses the same [workload loop](../benchmarks/Virta.Benchmarks/SlidingWindowWorkload.cs),
precomputed inputs, and BenchmarkDotNet consumer. Input conversion, initial
filling, and construction are outside the timed interval. Ring capacity is
supplied at construction and equals the window size.

Generated Markdown summaries and disassembly remain under the ignored
`benchmarks/results/` directory. This document retains the useful measurements.

## Reproduce

Run from the repository root:

```sh
dotnet build Virta.slnx -c Release

# All six operators, three window sizes, steady-state throughput and allocation.
DOTNET_TieredPGO=1 dotnet run --project benchmarks/Virta.Benchmarks -c Release --no-build -- \
  --filter '*ThroughputBenchmarks*' --job Short --launchCount 1 \
  --warmupCount 3 --iterationCount 8 --iterationTime 250 \
  --artifacts benchmarks/results/current

# Representative operators with dynamic PGO disabled, tiered compilation enabled.
DOTNET_TieredPGO=0 dotnet run --project benchmarks/Virta.Benchmarks -c Release --no-build -- \
  --filter '*ThroughputBenchmarks<Int32, Int32, Int32, SumOperator<Int32>>*' \
           '*ThroughputBenchmarks<Int32, Int32, Int32, MaximumOperator<Int32>>*' \
           '*ThroughputBenchmarks<Double, MeanPartial<Double>, Double, MeanOperator<Double>>*' \
  --job Short --launchCount 1 --warmupCount 3 --iterationCount 8 --iterationTime 250 \
  --artifacts benchmarks/results/without-pgo

# Eviction bursts beginning at the reversal boundary, after storage has warmed.
DOTNET_TieredPGO=1 dotnet run --project benchmarks/Virta.Benchmarks -c Release --no-build -- \
  --filter '*EvictionBurstBenchmarks*' --job Short --launchCount 1 \
  --warmupCount 3 --iterationCount 20 \
  --artifacts benchmarks/results/eviction-bursts

# Targeted generated-code inspection.
DOTNET_TieredPGO=1 dotnet run --project benchmarks/Virta.Benchmarks -c Release --no-build -- \
  --filter '*HotPathDisassembly*' --job Short --launchCount 1 \
  --warmupCount 3 --iterationCount 8 --iterationTime 250 \
  --artifacts benchmarks/results/disassembly
```

BenchmarkDotNet 0.16.0-preview.2 exports GitHub Markdown only. The disassembly
diagnoser also exports Markdown. The library has no BenchmarkDotNet dependency.

## Environment and units

Recorded on 2026-11-17:

- AMD Ryzen 7 9800X3D; 8 logical processors available to the process.
- Debian GNU/Linux 13, x64.
- .NET SDK 11.0.100; runtime 11.0.0; RyuJIT x86-64-v4.
- Concurrent workstation garbage collection.

Throughput values are BenchmarkDotNet arithmetic means in **nanoseconds per
eviction/insertion/query update**, normalized over 1,024 updates per invocation.
The throughput jobs use one launch, three warmup iterations, and eight measured
iterations targeting 250 ms each. They measure steady-state averages, not
individual-operation latency percentiles.

## Sustained sliding updates

PGO enabled. Pooled means the default chunked storage.

| Operator                  | Window size | Two stacks | Pooled DABA Lite | Fixed-ring DABA Lite |
| ------------------------- | ----------: | ---------: | ---------------: | -------------------: |
| Sum                       |       1,000 |      4.305 |           12.774 |               12.282 |
| Sum                       |      10,000 |      6.901 |           12.658 |               11.716 |
| Sum                       |   1,000,000 |      7.574 |           13.447 |               11.621 |
| Maximum                   |       1,000 |      4.259 |           15.268 |                9.369 |
| Maximum                   |      10,000 |      7.542 |           12.394 |               11.051 |
| Maximum                   |   1,000,000 |      7.546 |           14.005 |               12.486 |
| Mean                      |       1,000 |      6.198 |           14.896 |               12.035 |
| Mean                      |      10,000 |     14.900 |           14.969 |               12.109 |
| Mean                      |   1,000,000 |     14.337 |           16.715 |               13.836 |
| Geometric mean            |       1,000 |      13.92 |            21.79 |                28.16 |
| Geometric mean            |      10,000 |      22.36 |            21.95 |                18.50 |
| Geometric mean            |   1,000,000 |      23.66 |            24.67 |                20.03 |
| Sample standard deviation |       1,000 |      10.10 |            35.89 |                24.16 |
| Sample standard deviation |      10,000 |      33.40 |            35.83 |                24.24 |
| Sample standard deviation |   1,000,000 |      30.65 |            25.63 |                33.26 |
| Argument maximum          |       1,000 |      6.342 |           19.160 |               11.698 |
| Argument maximum          |      10,000 |     17.729 |           19.377 |               11.663 |
| Argument maximum          |   1,000,000 |     17.339 |           20.632 |               13.484 |

Two stacks has higher throughput for sum and maximum. The fixed ring usually
improves on pooled DABA Lite, but the result depends on the operator and partial:
at one million observations, pooled sample standard deviation measured 25.63 ns
versus 33.26 ns for the ring.

The tradeoff is update work: two stacks occasionally reverses the entire window;
DABA Lite performs a bounded number of combinations on every update.

## Prefix subtraction

The invertible-operator comparison uses the same timed loop and precomputed
inputs. These are separate runs with prefix subtraction as their baseline:

| Operator       | Window size | Subtract on eviction | Pooled DABA Lite |
| -------------- | ----------: | -------------------: | ---------------: |
| Sum            |       1,000 |                2.251 |           12.626 |
| Sum            |      10,000 |                2.279 |           13.101 |
| Sum            |   1,000,000 |                2.297 |           13.458 |
| Mean           |       1,000 |                6.485 |           14.580 |
| Mean           |      10,000 |                6.608 |           14.708 |
| Mean           |   1,000,000 |                6.569 |           15.393 |
| Geometric mean |       1,000 |                10.18 |            22.08 |
| Geometric mean |      10,000 |                10.07 |            21.76 |
| Geometric mean |   1,000,000 |                10.10 |            23.63 |

Prefix subtraction is faster for these invertible operators. DABA Lite's
additional work supports operators such as maximum and argument maximum that
cannot remove a prefix through an inverse.

## Eviction bursts at the reversal boundary

Each iteration fills the windows and advances them through one complete sliding
rotation outside the timed interval. This warms their storage and leaves the
two-stack front empty with a full back stack. The timed operation then performs
**256 evictions, each followed by a maximum query**, with no insertions.

The job uses one invocation per iteration, three warmup iterations, and twenty
measured iterations. Values are **microseconds for the entire burst**, not
per-operation percentiles:

| Initial window size | Two stacks | Pooled DABA Lite | Fixed-ring DABA Lite |
| ------------------: | ---------: | ---------------: | -------------------: |
|               1,000 |      2.328 |            3.524 |                1.879 |
|              10,000 |     15.669 |            3.147 |                2.196 |
|           1,000,000 |  4,120.942 |            3.101 |                1.960 |

This captures the reason to choose de-amortization: the million-element
two-stack reversal takes milliseconds, while both DABA Lite bursts remain in
single-digit microseconds. The stack arrays were already allocated before
measurement; this is rebuilding work rather than first-use growth.

## PGO and allocation

Representative one-million-element windows, nanoseconds per sliding update:

| Operator | Pooled, PGO | Pooled, no PGO | Ring, PGO | Ring, no PGO |
| -------- | ----------: | -------------: | --------: | -----------: |
| Sum      |      13.447 |         17.670 |    11.621 |       14.404 |
| Maximum  |      14.005 |         19.202 |    12.486 |       16.053 |
| Mean     |      16.715 |          20.25 |    13.836 |        16.12 |

Dynamic PGO improves these update paths. Disabling it leaves tiered compilation
enabled, so this comparison isolates PGO rather than changing both settings.

All **111 throughput configurations** reported **0 B per update**: 72 with PGO
and 39 without it. Construction and growth are outside those steady-state
measurements. Independent tests also assert zero thread-local allocated bytes
for warmed pooled windows, runtime-capacity rings, and stack-backed windows.

## Generated code and excluded measurements

The targeted disassembly confirms that integer combinations inline and ring
wrapping uses conditional subtraction. There is no capacity-policy type or
modulo operation.

The suite and this document exclude:

- The obsolete type-level capacity experiment.
- Duplicate sum-only steady-state and single-stopwatch sustained measurements.
- Per-operation stopwatch percentiles dominated by timer overhead.
- Process peak working sets accumulated across unrelated workloads.
- Timed recalculation, which remains a test oracle rather than a competitive
  sliding-window implementation.

The retained benchmarks cover average throughput, allocations, reversal-boundary
bursts, and generated code without duplicating queue or algorithm implementations.
