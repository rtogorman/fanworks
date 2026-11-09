namespace DuctSizer.Core;

/// <summary>
/// A last-in, first-out history with a fixed capacity: pushing beyond the capacity discards the oldest entry.
/// Used for undo/redo so memory stays bounded through a long editing session (finding 14 of the v0.92 review).
/// </summary>
public sealed class BoundedHistory<T>
{
    private readonly LinkedList<T> _entries = new();

    /// <param name="capacity">Maximum number of entries kept (at least 1).</param>
    public BoundedHistory(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        Capacity = capacity;
    }

    /// <summary>Maximum number of entries kept.</summary>
    public int Capacity { get; }

    /// <summary>Number of entries currently held.</summary>
    public int Count => _entries.Count;

    /// <summary>Adds an entry, discarding the oldest one when the history is full.</summary>
    public void Push(T entry)
    {
        _entries.AddLast(entry);
        while (_entries.Count > Capacity)
            _entries.RemoveFirst();
    }

    /// <summary>Removes and returns the most recent entry.</summary>
    /// <exception cref="InvalidOperationException">The history is empty.</exception>
    public T Pop()
    {
        var last = _entries.Last ?? throw new InvalidOperationException("The history is empty.");
        _entries.RemoveLast();
        return last.Value;
    }

    /// <summary>Removes every entry.</summary>
    public void Clear() => _entries.Clear();
}
