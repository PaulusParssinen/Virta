# DABA Lite

Virta adapts DABA Lite from the
[IBM reference implementation](https://github.com/IBM/sliding-window-aggregators)
to .NET 11. The reference uses linked-chunk iterators; Virta uses front-relative
integer cursors and mutable references into storage.

## Cursor regions

Four cursors partition the logical queue:

```text
0 <= left <= right <= accumulator <= back <= count
```

| Region                 | Stored partial               |
| ---------------------- | ---------------------------- |
| `[0, left)`            | Suffix ending at `back`      |
| `[left, right)`        | Suffix ending at `right`     |
| `[right, accumulator)` | Original lifted value       |
| `[accumulator, back)`  | Suffix ending at `back`      |
| `[back, count)`        | Original lifted value       |

Two additional invariants describe remaining work:

```text
right - left == accumulator - right
back - left + 1 == back - (count - back), when count > 0
```

The completed front always contains the oldest observation. A query combines
its suffix aggregate with the back aggregate, preserving left-to-right order.

Each insertion or eviction performs one scheduling step:

1. If `left == back`, flip the completed back into a new front. Reset `left`
   to zero, `right` to the old `back`, and both remaining boundaries to `count`.
2. If the rebuilding middle is nonempty, move `accumulator` left and extend
   the suffix by one observation.
3. If `left != right`, complete one left-region aggregate and advance `left`.
   Otherwise advance `left`, `right`, and `accumulator` together.

Eviction decrements all four cursors after removing the queue's front.
The empty transition resets the boundaries and scalar aggregates.
Debug builds assert cursor ordering and both scheduling invariants after each
step. Tests independently compare query results against left-to-right
recalculation after every operation.

## Partial storage

The queue stores `TPartial` directly: no additional per-observation wrapper or
second aggregate. During rebuilding, each lifted value is overwritten by its
required suffix aggregate.

Two extra scalar partials replace the omitted information:

- `backAggregate` is the aggregate of newly appended observations.
- `middleAggregate` snapshots that aggregate at the flip and supplies the value
  needed when completing left-region suffixes.

When the three work cursors advance together, the middle scalar is refreshed
from the current suffix. Insertion uses at most three combinations; eviction
uses at most two; querying uses at most one.

These combination bounds hold independently of window size and include empty
transitions. The two scalar partials are part of the algorithm, not an optional
cache policy.

`DabaLiteState` contains the scheduler and two scalars. Generic methods accept
either a heap-backed queue or the internal span-backed queue through
`allows ref struct`. This shares the algorithm without storing a span in a
heap object, boxing the queue, or duplicating the scheduler.

## Storage

The default chunk capacity is `max(1, 512 / sizeof(TElement))`. A circular
directory maps an index to its chunk and offset in constant time. Directory
growth copies references, while elements remain in their existing arrays.
Drained chunks return to their pool. The directory retains its high-water
capacity for reuse.

The queue contract exposes a read-only front reference for queries and a mutable
indexer for rebuilding partials. It has no separate back property; timestamp
endpoints read the final element by index.

The pooled contiguous queue copies elements when growing or shrinking.
The fixed ring accepts its capacity at construction, wraps indices with
conditional subtraction, and never resizes. Span-backed windows use only
their supplied storage. Fixed-size numeric operators and warmed pooled chunks
produce zero managed allocations during steady sliding updates.

Storage ownership is independent of scheduling. Queue-injected windows borrow
their storage, and only pooled queues and their owning convenience windows
implement `IDisposable`. Managed rings and caller-owned spans have no disposed
state. The same scheduler serves both owning and borrowing window types.

## Implementation choices

- Relative cursors avoid a monotonically growing epoch and keep queue indices
  identical to their documented public meaning.
- Inlining hints favor exposing concrete queue types across shared generic
  scheduling layers and keeping address calculations at their array accesses.
- `InsertRange` retains one scheduling step per input. Sum lifting is an
  identity operation, so there is no arithmetic in that lift to vectorize.
- The scheduler has no local aggregate arrays to clear. It uses initialized
  scalar state rather than `SkipLocalsInit` or uninitialized memory.
- Standard deviation uses centered moments rather than raw sums of squares.

## Research

1. Tangwongsan, Hirzel, Schneider:
   [Low-Latency Sliding-Window Aggregation in Worst-Case Constant Time](https://doi.org/10.1145/3093742.3093925),
   DEBS 2017.
2. Tangwongsan, Hirzel, Schneider:
   [In-Order Sliding-Window Aggregation in Worst-Case Constant Time](https://doi.org/10.1007/s00778-021-00668-3),
   The VLDB Journal, 2021.
3. [Reference source and experiment infrastructure](https://github.com/IBM/sliding-window-aggregators).

The original research establishes the aggregation-operation bounds. Virta's
[benchmark guide](BENCHMARKS.md) reports measurements for the library's
implementations.
