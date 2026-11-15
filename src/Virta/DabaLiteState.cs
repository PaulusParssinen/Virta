using System.Diagnostics;
using System.Runtime.CompilerServices;

using Virta.Operators;
using Virta.Queues;

namespace Virta;

internal struct DabaLiteState<TInput, TPartial, TOutput, TOperator>
    where TOperator : struct, IBinaryOperator<TInput, TPartial, TOutput>
{
    private int _left;
    private int _right;
    private int _accumulator;
    private int _back;
    private TPartial _middleAggregate;
    private TPartial _backAggregate;

    public DabaLiteState()
    {
        _middleAggregate = TOperator.Identity;
        _backAggregate = TOperator.Identity;
    }

    // RATIONALE: Expose the caller's queue type through this shared heap/stack layer for devirtualization.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Insert<TQueue>(ref TQueue queue, TInput value)
        where TQueue : IDabaQueue<TPartial>, allows ref struct
    {
        TPartial lifted = TOperator.Lift(value);
        TPartial aggregate = TOperator.Combine(_backAggregate, lifted);
        queue.PushBack(lifted);
        _backAggregate = aggregate;
        Step(ref queue);
    }

    // RATIONALE: Expose queue removal and cursor updates together across the shared heap/stack layer.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Evict<TQueue>(ref TQueue queue)
        where TQueue : IDabaQueue<TPartial>, allows ref struct
    {
        queue.PopFront();
        _left--;
        _right--;
        _accumulator--;
        _back--;
        Step(ref queue);
    }

    // RATIONALE: Favor queue devirtualization through the wrapper instead of a shared generic query call.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly TOutput Query<TQueue>(ref TQueue queue)
        where TQueue : IDabaQueue<TPartial>, allows ref struct
    {
        return queue.Count == 0
            ? TOperator.Lower(TOperator.Identity)
            : TOperator.Lower(TOperator.Combine(queue.Front, _backAggregate));
    }

    // RATIONALE: This branch-heavy scheduler exceeds trivial-inline heuristics but benefits from the caller's queue type.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Step<TQueue>(ref TQueue queue)
        where TQueue : IDabaQueue<TPartial>, allows ref struct
    {
        if (_left == _back)
        {
            _left = 0;
            _right = _back;
            _accumulator = queue.Count;
            _back = queue.Count;
            _middleAggregate = _backAggregate;
            _backAggregate = TOperator.Identity;
        }

        if (_back != 0)
        {
            if (_accumulator != _right)
            {
                TPartial previousSuffix = GetSuffixAggregate(ref queue);
                _accumulator--;
                ref TPartial partial = ref queue[_accumulator];
                partial = TOperator.Combine(partial, previousSuffix);
            }

            if (_left != _right)
            {
                ref TPartial partial = ref queue[_left];
                partial = TOperator.Combine(partial, _middleAggregate);
                _left++;
            }
            else
            {
                _left++;
                _right++;
                _accumulator++;
                _middleAggregate = GetSuffixAggregate(ref queue);
            }
        }
        else
        {
            _backAggregate = TOperator.Identity;
            _middleAggregate = TOperator.Identity;
        }

        AssertInvariant(queue.Count);
    }

    // RATIONALE: Expose suffix access inside the scheduler so the queue lookup can specialize with its caller.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly TPartial GetSuffixAggregate<TQueue>(ref TQueue queue)
        where TQueue : IDabaQueue<TPartial>, allows ref struct
        => _accumulator == _back ? TOperator.Identity : queue[_accumulator];

    [Conditional("DEBUG")]
    private readonly void AssertInvariant(int count)
    {
        Debug.Assert(0 <= _left && _left <= _right && _right <= _accumulator
            && _accumulator <= _back && _back <= count);
        Debug.Assert(_right - _left == _accumulator - _right);
        Debug.Assert(count == 0 || _back - _left + 1 == _back - (count - _back));
    }
}
