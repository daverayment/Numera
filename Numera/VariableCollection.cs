using System.Collections;

namespace Numera;

/// <summary>
/// An immutable, ordered collection of variable names and their values. Preserves
/// chronological order of assignment while providing case-insensitive O(1) lookup by
/// name.
/// </summary>
public sealed class VariableCollection :
    IReadOnlyDictionary<string, MathValue>, IReadOnlyList<KeyValuePair<string, MathValue>>
{
    /// <summary>
    /// The single comparer used for every variable name lookup, forwarding to
    /// <see cref="Identifier.Comparer"/>. Ordinal rather than culture-sensitive, to prevent
    /// issues with e.g. Turkish i's or other culture-specific case mappings.
    /// </summary>
    public static StringComparer NameComparer => Identifier.Comparer;

    /// <summary>
    /// Gets an empty variable collection, which is the same instance every time. Safe to
    /// share.
    /// </summary>
    public static VariableCollection Empty { get; } = [];

    /// Private storage.
    private readonly OrderedDictionary<string, MathValue>? _storage;

    /// <summary>
    /// The number of variables in the collection.
    /// </summary>
    public int Count => _storage?.Count ?? 0;

    /// <summary>
    /// The names of the variables in the collection, in chronological order of assignment
    /// or update.
    /// </summary>
    public IEnumerable<string> Keys => _storage?.Keys is not null ? _storage.Keys : [];

    /// <summary>
    /// The values of the variables in the collection, in chronological order of
    /// assignment or update.
    /// </summary>
    public IEnumerable<MathValue> Values => _storage?.Select(kvp => kvp.Value) ?? [];

    /// <summary>
    /// Retrieve the variable value by its name, case-insensitive. O(1).
    /// </summary>
    /// <param name="name">Variable name.</param>
    /// <returns>The variable's <see cref="MathValue"/>, if the key is found; otherwise
    /// throws a <see cref="KeyNotFoundException"/> exception.
    /// </returns>
    /// <exception cref="KeyNotFoundException"></exception>
    public MathValue this[string name] =>
        _storage?.TryGetValue(name, out var value) == true ?
        value :
        throw new KeyNotFoundException($"Variable '{name}' not found.");

    /// <summary>
    /// Positional lookup by chronological/recency order. O(1).
    /// </summary>
    /// <param name="index">Index of the variable in the collection.</param>
    /// <returns>The <see cref="KeyValuePair"/> of the variable's string identifier and
    /// <see cref="MathValue"/> value.</returns>
    /// <exception cref="IndexOutOfRangeException"/>
    public KeyValuePair<string, MathValue> this[int index]
    {
        get
        {
            if (_storage is null || index < 0 || index >= _storage.Count)
            {
                throw new IndexOutOfRangeException($"Index {index} is out of range.");
            }

            var entry = _storage.GetAt(index);
            return new KeyValuePair<string, MathValue>(entry.Key, entry.Value);
        }
    }

    /// <summary>
    /// Create an empty collection.
    /// </summary>
    public VariableCollection() { }

    /// <summary>
    /// Create a new collection from an existing enumerable. If the source contains
    /// duplicate keys (comparing case-insensitively), later entries overwrite earlier ones.
    /// </summary>
    public VariableCollection(IEnumerable<KeyValuePair<string, MathValue>>? source)
    {
        if (source is not null)
        {
            var storage = new OrderedDictionary<string, MathValue>(NameComparer);
            foreach (var (key, value) in source)
            {
                storage.Remove(key);
                storage.Add(key, value);
            }

            _storage = storage;
        }
    }

    private VariableCollection(OrderedDictionary<string, MathValue> storage)
    {
        _storage = storage;
    }

    /// <summary>
    /// Set the value of a variable in the collection, returning a new collection.
    /// </summary>
    /// <param name="name">The name of the variable.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>A new <see cref="VariableCollection"/> with the updated value.</returns>
    public VariableCollection Add(string name, MathValue value)
    {
        ArgumentNullException.ThrowIfNull(name, nameof(name));

        var next = _storage is null ?
            new OrderedDictionary<string, MathValue>(1, NameComparer) :
            new OrderedDictionary<string, MathValue>(_storage, NameComparer);

        next.Remove(name);
        next.Add(name, value);

        return new VariableCollection(next);
    }

    /// <summary>
    /// Returns a new <see cref="VariableCollection"/> with the given variables added or
    /// updated in order.
    /// </summary>
    /// <param name="items">The variables to add or update.</param>
    /// <returns>A new <see cref="VariableCollection"/> with the updated variables.
    /// </returns>
    /// <remarks>
    /// Existing variables with the same names as those in <paramref name="items"/> will
    /// be removed and re-added with the new values to preserve the MRU order of the new
    /// assignments.
    /// </remarks>
    public VariableCollection SetItems(IReadOnlyCollection<KeyValuePair<string, MathValue>> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        int capacity = _storage?.Count + items.Count ?? items.Count;
        var next = new OrderedDictionary<string, MathValue>(capacity, NameComparer);

        if (_storage is not null)
        {
            foreach (var (key, value) in _storage)
            {
                next.Add(key, value);
            }
        }

        foreach (var (key, value) in items)
        {
            next.Remove(key); // Move reassigned variables to the end.
            next.Add(key, value);
        }

        return new VariableCollection(next);
    }

    /// <inheritdoc/>
    public bool ContainsKey(string name) => _storage?.ContainsKey(name) == true;

    /// <summary>
    /// Gets the value associated with the specified key.
    /// </summary>
    /// <param name="key">The identifier of the variable to get.</param>
    /// <param name="value">Set to the variable value, if found.</param>
    /// <returns><c>true</c> if the collection contains a variable with the supplied
    /// identifier; otherwise <c>false</c>.</returns>
    public bool TryGetValue(string key, out MathValue value)
    {
        if (_storage is not null)
        {
            return _storage.TryGetValue(key, out value);
        }

        value = default;
        return false;
    }

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<string, MathValue>> GetEnumerator() =>
        _storage?.GetEnumerator() ?? Enumerable.Empty<KeyValuePair<string, MathValue>>().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

