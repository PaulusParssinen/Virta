namespace Virta.Queues;

/// <summary>
/// Provides mutable, front-relative indexed storage for a window.
/// Returned references are valid only until the next mutation or release of the underlying storage.
/// </summary>
public interface IDabaQueue<TElement>
{
    /// <summary>Gets the number of elements.</summary>
    int Count { get; }

    /// <summary>Gets an element by its zero-based offset from the front.</summary>
    ref TElement this[int index] { get; }

    /// <summary>Gets a read-only reference to the oldest element; an empty queue throws InvalidOperationException.</summary>
    ref readonly TElement Front { get; }

    /// <summary>Appends an element; a full fixed-capacity queue throws InvalidOperationException.</summary>
    void PushBack(TElement element);

    /// <summary>Removes the oldest element; an empty queue throws InvalidOperationException.</summary>
    void PopFront();
}
