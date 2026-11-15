# Operator contract

An operator is a value type implementing
`IBinaryOperator<TInput, TPartial, TOutput>`. Its static members perform
four operations:

1. `Identity` supplies an empty partial.
2. `Lift` converts an observation into a partial.
3. `Combine(left, right)` concatenates the represented subsequences in order.
4. `Lower` converts the completed partial into a query result.

`Combine` must be associative and `Identity` must be a two-sided identity.
Commutativity and invertibility are not required. Neither arguments nor shared
state may be mutated. Partial values can be shared between several stored
aggregates, so reference-containing partials must behave as immutable values.
Operators must support every intermediate partial produced from valid inputs.
`Lift` may reject an input before insertion; `Combine`, `Identity`, and `Lower`
must complete normally for valid partials.

## An order-sensitive operator

```csharp
using Virta;
using Virta.Operators;

public readonly struct ConcatenationOperator : IBinaryOperator<string, string, string>
{
    public static string Identity => "";
    public static string Lift(string value) => value;
    public static string Combine(string left, string right) => left + right;
    public static string Lower(string partial) => partial;
}
```

```csharp
using var window = new DabaLite<string, string, string, ConcatenationOperator>();
window.InsertRange("first", "second", "third");
window.Evict();
Console.WriteLine(window.Query()); // secondthird
```

String concatenation illustrates ordering. Its variable-sized results allocate
and take time proportional to their length; DABA Lite bounds the number of operator
calls, not the work performed inside an operator.

## Selecting an argument by a key

```csharp
public readonly struct LengthSelector : IKeySelector<string, int>
{
    public static int SelectKey(string argument) => argument.Length;
}
```

Use this selector with `ArgumentMaximumOperator<string, int, LengthSelector>`.
Its output has `HasValue`, `Argument`, and `Key` properties. Keys must be nonnull
and consistently comparable. Equal keys retain the left argument, preserving
first-occurrence semantics across different aggregation orders.

## Prefix removal

An invertible operator additionally implements
`IInvertibleBinaryOperator<TInput, TPartial, TOutput>.RemovePrefix`:

```text
RemovePrefix(Combine(prefix, remainder), prefix) == remainder
```

The removed partial must represent a leading subsequence of the aggregate.
Removing a suffix is a different operation for noncommutative operators.
The test-only [SubtractOnEvictWindow](../tests/Virta.Tests/Baselines/SubtractOnEvictWindow.cs)
always removes the oldest observation and resets the aggregate to the identity
when the last observation leaves. DABA Lite does not require prefix removal.

## Numerical representation

Choose the partial domain to represent the operation, not just the input.
Means carry a sum and count; geometric means carry a logarithm sum and count.
Sample standard deviation combines centered moments:

```text
count = left.count + right.count
difference = right.mean - left.mean
mean = left.mean + difference * right.count / count
squaredDeviations = left.squaredDeviations + right.squaredDeviations
                 + difference^2 * left.count * right.count / count
```

This avoids subtracting two large raw second moments. The sample output divides
by `count - 1` before taking the square root. Prefix removal reverses the centered
moment combination and floors negative rounding residuals at zero.

Floating-point operators obey their algebraic laws to floating-point precision.
Tests compare them with a relative tolerance. For exact algebraic validation,
the suite also uses integer addition and fixed-width order-sensitive fingerprints.
