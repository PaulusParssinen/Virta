# Virta

Virta is a .NET 11 library for first-in, first-out sliding-window aggregation.
It implements **DABA Lite** (De-Amortized Banker's Aggregator Lite) with
statically dispatched operators. It preserves insertion order and performs a
constant number of aggregation operations per insertion, eviction, and query.

## Quick start

Reference `src/Virta/Virta.csproj`, or build the NuGet package with
`dotnet pack src/Virta -c Release`.

```csharp
using Virta;
using Virta.Operators;

using var window = DabaLite.Create<int, int, int, SumOperator<int>>();
window.InsertRange(10, 20, 30);
Console.WriteLine(window.Query()); // 60
window.Evict();
Console.WriteLine(window.Query()); // 50
```

Windows expose `Insert`, `InsertRange`, `Evict`, `Query`, `Count`, and `IsEmpty`
through `IFifoWindow<TInput, TOutput>`.

### Fixed-capacity windows

```csharp
var window =
    new RingBufferDabaLite<int, int, int, MaximumOperator<int>>(4096);
window.InsertRange(3, 7, 5);
Console.WriteLine(window.Query()); // 7
```

Every slot is usable. Insertion at capacity throws `InvalidOperationException`
without changing the window. Capacity is supplied at construction and can be any
positive element count; the backing array never resizes.

### Caller-owned storage

```csharp
var window = new StackDabaLite<double, MeanPartial<double>, double, MeanOperator<double>>(
    stackalloc MeanPartial<double>[64]);
window.InsertRange(2, 4, 6);
Console.WriteLine(window.Query()); // 4
```

`StackDabaLite` is a `ref struct` and shares the same scheduling implementation as
heap-backed DABA Lite. The supplied span determines capacity. Keep one active
window per buffer and do not copy an active window. The window borrows the span
and requires no disposal. Eviction clears reference-containing slots; the caller
can clear the entire buffer after the window is no longer in use.

### Timestamped windows

```csharp
using var window = new TimestampedDabaLite<long, int, int, int, SumOperator<int>>();
window.Insert(100, 4);
window.Insert(200, 8);
window.EvictThrough(100);
Console.WriteLine(window.OldestTimestamp); // 200
Console.WriteLine(window.Query());         // 8
```

`OldestTimestamp` and `NewestTimestamp` describe insertion endpoints, not timestamp
minima or maxima.
`EvictThrough` removes the consecutive prefix at or below the inclusive cutoff
and stops at the first newer timestamp. It returns the number removed.
For time-based windows, insert observations in timestamp order.

## Operators

| Operator                                                 | Partial state                            | Empty result                         |
| -------------------------------------------------------- | ---------------------------------------- | ------------------------------------ |
| `SumOperator<T>`                                         | Numeric value                            | Zero                                 |
| `MaximumOperator<T>`                                     | Numeric value                            | Negative infinity or numeric minimum |
| `MeanOperator<T>`                                        | Sum and count                            | Zero                                 |
| `GeometricMeanOperator<T>`                               | Logarithm sum and count                  | Zero                                 |
| `SampleStandardDeviationOperator<T>`                     | Count, mean, centered squared deviations | Zero                                 |
| `ArgumentMaximumOperator<TArgument, TKey, TKeySelector>` | Selected argument and key                | `HasValue == false`                  |

Geometric means accept positive finite observations. Sample standard deviation
accepts finite observations and returns zero for a singleton. Argument maximum
selects the first observation when keys tie. Numeric operations use the selected
type's arithmetic: integer means use integer division, and floating-point
reassociation changes rounding.

Sum, mean, geometric mean, and sample standard deviation implement prefix removal,
which the test-only subtraction baseline exercises. DABA Lite requires no inverse.
Standard deviation uses centered moments to retain precision when observations
have large offsets.

See [the operator contract](docs/OPERATORS.md) for custom operators and selectors.

## Storage and ownership

The default window uses `ChunkedArrayQueue<TElement>`, backed by `ArrayPool<TElement>`.
Elements remain in their chunks as the window grows. A circular directory gives
constant-time indexed access; directory growth copies chunk references.
`ArrayPoolQueue<TElement>` supplies an alternative resizable circular array.
Custom storage implements `IDabaQueue<TElement>`.
Its `Front` property returns a read-only reference; the indexer supplies mutable
references for rebuilding aggregates and reading any other position.

```csharp
using var queue = new ArrayPoolQueue<int>();
var window =
    new DabaLite<int, int, int, SumOperator<int>, ArrayPoolQueue<int>>(queue);
```

Queue-injected windows borrow an empty queue. The caller retains ownership and
disposes pooled storage after its window is no longer in use. Do not independently
modify or dispose the queue while the window is active.

The default `DabaLite` convenience type creates and owns a pooled queue,
so it implements `IDisposable`. Timestamped windows own their aggregation window
and pooled timestamp storage and also implement `IDisposable`. Dispose these
owners to return rented arrays promptly.

`IFifoWindow` and `IDabaQueue` define operations, not resource ownership.
Managed ring buffers and stack-backed windows require no disposal.
Generic helpers that own disposable instances must add an
explicit `IDisposable` constraint.

Windows and queues are single-owner mutable objects; synchronize access when
sharing them between threads. References returned by a queue remain valid until
its next mutation or release of the underlying storage.

An empty query returns the operator's lowered identity. Evicting an empty window
or reading an empty timestamp endpoint throws `InvalidOperationException`.
Operations on disposed pooled storage or its owning window throw
`ObjectDisposedException`.
`InsertRange` is equivalent to repeated insertion: a failure retains the
successfully inserted prefix.

## Complexity

Here, `n` is the observation count and space counts partial aggregate values.

| Implementation                   | Insert                | Evict                 | Query | Partial storage |
| -------------------------------- | --------------------- | --------------------- | ----- | --------------- |
| Recalculation                    | O(1) amortized        | O(1)                  | O(n)  | n               |
| Subtract on eviction             | O(1) amortized        | O(1)                  | O(1)  | n + 1           |
| Two stacks                       | O(1) amortized        | O(1) amortized        | O(1)  | 2n              |
| DABA Lite                        | O(1) aggregation work | O(1) aggregation work | O(1)  | n + 2           |
| Fixed-capacity / stack DABA Lite | O(1)                  | O(1)                  | O(1)  | capacity + 2    |

These bounds assume constant-cost operators. Dynamic storage has amortized
growth costs; fixed-capacity storage performs no resizing. Timestamped windows
add one timestamp per observation, and eviction of `b` observations takes O(b).
Arrays also contain unused capacity and queue metadata.

Recalculation, subtraction, and two-stack baselines live in
[Virta.Tests](tests/Virta.Tests/Baselines) and are not included in the library
package. The benchmarks reference the test project to reuse those implementations.

The [algorithm guide](docs/ALGORITHM.md) describes the invariants and links the
original research. The [benchmark guide](docs/BENCHMARKS.md) provides measured
results, allocation checks, and commands for reproducing the library benchmarks.

## Build and validate

[Curb](https://github.com/nullean/curb) formats C# source during builds and
checks formatting during Release builds, including CI.

```sh
dotnet build Virta.slnx
dotnet build Virta.slnx -c Release
dotnet test tests/Virta.Tests -c Release
dotnet run --project samples/Virta.Example -c Release
dotnet publish samples/Virta.Example -c Release -r linux-x64 --self-contained
dotnet pack src/Virta -c Release -o artifacts/packages
```

The tests cover all supplied operators, queue growth and wrapping, deterministic
10,000-operation sequences, noncommutative aggregation, capacity failures,
timestamp prefixes, combine-call bounds, and steady-state allocations.
The executable example checks representative public APIs under both the JIT and
NativeAOT. The library has no runtime package dependencies.

## Workflow dependencies and releases

The workflows use readable action versions pinned to verified commits in
[actions.lock](.github/workflows/actions.lock). Generate the lockfile with
GitHub's official tool rather than editing it:

```sh
gh extension install github/gh-actions-lock --pin v0.4.1
gh actions-lock
gh actions-lock --verify
```

[Dependabot](.github/dependabot.yml) updates action versions and their lockfile
entries together. The [verification workflow](.github/workflows/verify-lockfile.yml)
checks the lockfile on pull requests and pushes.

Tagged [builds](.github/workflows/build.yml) attest the NuGet packages, upload the
provenance bundle, and publish using short-lived NuGet OIDC credentials.
Configure NuGet trusted publishing for the `build.yml` workflow and the
`PaulusParssinen` account. To verify a downloaded release package:

```sh
gh attestation verify Virta.0.1.0.nupkg --repo PaulusParssinen/Virta
```

## License

[Apache License 2.0](LICENSE). See [NOTICE](NOTICE) for research attribution.
