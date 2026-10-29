using System.Collections;

namespace Numera;

/// <summary>
/// An immutable, ordered collection of variables. Preserves chronological order of
/// assignment while providing case-insensitive O(1) lookup by name.
/// </summary>
/// <remarks>
/// Stores full <see cref="Variable"/> instances to preserve metadata such as
/// <see cref="Variable.Definition"/> across assignment updates and evaluations.
/// </remarks>
public sealed class VariableCollection : IReadOnlyList<Variable>
{
    /// <summary>
    /// The single comparer used for every variable name lookup, forwarding to
    /// <see cref="Identifier.Comparer"/>. Ordinal rather than culture-sensitive, to
    /// prevent issues with e.g. Turkish i's or other culture-specific case mappings.
    /// </summary>
    public static StringComparer NameComparer => Identifier.Comparer;

    /// <summary>
    /// Gets an empty variable collection, which is the same instance every time. Safe to
    /// share.
    /// </summary>
    public static VariableCollection Empty { get; } = [];

    /// Private storage mapping variable names to their full Variable instances.
    private readonly OrderedDictionary<string, Variable>? _storage;

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
    public IEnumerable<MathValue> Values => _storage?.Select(kvp => kvp.Value.Value) ?? [];

    /// <summary>
    /// Retrieve the variable value by its name, case-insensitive. O(1).
    /// </summary>
    /// <param name="name">Variable name.</param>
    /// <returns>The variable's <see cref="MathValue"/>, if the key is found; otherwise
    /// throws a <see cref="KeyNotFoundException"/> exception.
    /// </returns>
    /// <exception cref="KeyNotFoundException"></exception>
    public MathValue this[string name] =>
        _storage?.TryGetValue(name, out var variable) == true ?
        variable.Value :
        throw new KeyNotFoundException($"Variable '{name}' not found.");

    /// <summary>
    /// Positional lookup by chronological/recency order. O(1).
    /// </summary>
    /// <param name="index">Index of the variable in the collection.</param>
    /// <returns>The <see cref="Variable"/> at the given positional index.</returns>
    /// <exception cref="IndexOutOfRangeException"/>
    public Variable this[int index]
    {
        get
        {
            if (_storage is null || index < 0 || index >= _storage.Count)
            {
                throw new IndexOutOfRangeException($"Index {index} is out of range.");
            }

            return _storage.GetAt(index).Value;
        }
    }

    /// <summary>
    /// Create an empty collection.
    /// </summary>
    public VariableCollection() { }

    /// <summary>
    /// Create a new collection from an existing Variable enumerable. If the source
    /// contains duplicate names (comparing case-insensitively), later entries overwrite
    /// earlier ones.
    /// </summary>
    /// <param name="source">The source of variables to include in the collection.</param>
    public VariableCollection(IEnumerable<Variable>? source)
    {
        if (source is not null)
        {
            var storage = new OrderedDictionary<string, Variable>(NameComparer);
            foreach (var variable in source)
            {
                storage.Remove(variable.Name);
                storage.Add(variable.Name, variable);
            }

            _storage = storage;
        }
    }

    private VariableCollection(OrderedDictionary<string, Variable> storage)
    {
        _storage = storage;
    }

    /// <summary>
    /// Adds or updates a variable in the collection, returning a new collection.
    /// </summary>
    /// <param name="variable">The variable to add or update.</param>
    /// <returns>A new <see cref="VariableCollection"/> with the updated variable.
    /// </returns>
    public VariableCollection Add(Variable variable)
    {
        ArgumentNullException.ThrowIfNull(variable.Name, nameof(variable.Name));

        var next = _storage is null ?
            new OrderedDictionary<string, Variable>(1, NameComparer) :
            new OrderedDictionary<string, Variable>(_storage, NameComparer);

        next.Remove(variable.Name);
        next.Add(variable.Name, variable);

        return new VariableCollection(next);
    }

    /// <summary>
    /// Set the value of a variable in the collection, returning a new collection.
    /// </summary>
    /// <param name="name">The name of the variable.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>A new <see cref="VariableCollection"/> with the updated value.</returns>
    public VariableCollection Add(string name, MathValue value) =>
        Add(new Variable(name, value));

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
    public VariableCollection SetItems(IReadOnlyCollection<Variable> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        int capacity = _storage?.Count + items.Count ?? items.Count;
        var next = new OrderedDictionary<string, Variable>(capacity, NameComparer);

        if (_storage is not null)
        {
            foreach (var (key, value) in _storage)
            {
                next.Add(key, value);
            }
        }

        foreach (var variable in items)
        {
            next.Remove(variable.Name); // Move reassigned variables to the end.
            next.Add(variable.Name, variable);
        }

        return new VariableCollection(next);
    }

    /// <summary>
    /// Determines whether the collection contains a variable with the specified name.
    /// </summary>
    public bool ContainsKey(string name) => _storage?.ContainsKey(name) == true;

    /// <summary>
    /// Gets the value associated with the specified variable name.
    /// </summary>
    /// <param name="name">The identifier of the variable to get.</param>
    /// <param name="value">Set to the variable value, if found.</param>
    /// <returns><c>true</c> if the collection contains a variable with the supplied
    /// identifier; otherwise <c>false</c>.</returns>
    public bool TryGetValue(string name, out MathValue value)
    {
        if (_storage is not null && _storage.TryGetValue(name, out var variable))
        {
            value = variable.Value;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Gets the full <see cref="Variable"/> associated with the specified name.
    /// </summary>
    /// <param name="name">The identifier of the variable to get.</param>
    /// <param name="variable">Set to the variable instance, if found.</param>
    /// <returns><c>true</c> if found; otherwise <c>false</c>.</returns>
    public bool TryGetVariable(string name, out Variable variable)
    {
        if (_storage is not null)
        {
            return _storage.TryGetValue(name, out variable);
        }

        variable = default;
        return false;
    }

    /// <inheritdoc/>
    public IEnumerator<Variable> GetEnumerator()
    {
        if (_storage is not null)
        {
            foreach (var entry in _storage)
            {
                yield return entry.Value;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
