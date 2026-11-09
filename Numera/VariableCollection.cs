using System.Collections;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Numera;

/// <summary>
/// An immutable, ordered collection of variables. Preserves chronological order of
/// assignment while providing case-insensitive O(1) lookup by name.
/// </summary>
/// <remarks>
/// Stores full <see cref="Variable"/> instances to preserve metadata such as
/// <see cref="Variable.Definition"/> across assignment updates and evaluations.
/// </remarks>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
[CollectionBuilder(typeof(VariableCollection), nameof(Create))]
public sealed class VariableCollection : IReadOnlyList<Variable>
{
    private static readonly VariableCollection _empty = new();

    /// <summary>
    /// Gets an empty variable collection, which is the same instance every time. Safe to
    /// share.
    /// </summary>
    public static VariableCollection Empty { get; } = _empty;

    /// <summary>
    /// The single comparer used for every variable name lookup, forwarding to
    /// <see cref="Identifier.Comparer"/>. Ordinal rather than culture-sensitive, to
    /// prevent issues with e.g. Turkish i's or other culture-specific case mappings.
    /// </summary>
    public static StringComparer NameComparer => Identifier.Comparer;

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
    /// The variables in the collection, in chronological order of assignment or update.
    /// </summary>
    public IEnumerable<Variable> Values => _storage?.Select(kvp => kvp.Value) ?? [];

    /// <summary>
    /// Retrieve the variable value by its name, case-insensitive. O(1).
    /// </summary>
    /// <param name="name">Variable name.</param>
    /// <returns>The <see cref="Variable"/> if the key is found; otherwise throws a
    /// <see cref="KeyNotFoundException"/> exception.
    /// </returns>
    /// <exception cref="KeyNotFoundException">Thrown if the supplied name is not found in
    /// the collection.</exception>
    public Variable this[string name] =>
        _storage?.TryGetValue(name, out var variable) == true ?
            variable :
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
    private VariableCollection() { }

    private VariableCollection(OrderedDictionary<string, Variable> storage)
    {
        _storage = storage;
    }

    /// <summary>
    /// Creates a new <see cref="VariableCollection"/> from a span of
    /// <see cref="Variable"/> items.
    /// </summary>
    /// <param name="items">The span of variables to include in the collection.</param>
    /// <returns>A new <see cref="VariableCollection"/> containing the specified
    /// variables.</returns>
    /// <remarks>
    /// <para>
    /// If the source contains duplicate names (comparing case-insensitively), later
    /// entries overwrite earlier ones.</para>
    /// <para>
    /// For sequences of variables that are already in a collection, use
    /// <see cref="VariableCollectionExtensions.ToVariableCollection"/> or
    /// <see cref="CreateRange(IEnumerable{Variable}?)"/>.
    /// </para>
    /// </remarks>
    public static VariableCollection Create(ReadOnlySpan<Variable> items)
    {
        if (items.IsEmpty)
        {
            return _empty;
        }

        var storage = new OrderedDictionary<string, Variable>(items.Length, NameComparer);
        foreach (var variable in items)
        {
            Put(storage, variable, nameof(items));
        }

        return new VariableCollection(storage);
    }

    /// <summary>
    /// Creates a new <see cref="VariableCollection"/> from an enumerable collection of
    /// <see cref="Variable"/>.
    /// </summary>
    /// <param name="source">The source of <see cref="Variable"/> items.</param>
    /// <returns>A new <see cref="VariableCollection"/> containing the specified
    /// variables.</returns>
    /// <remarks>
    /// <para>
    /// If the source contains duplicate names (comparing case-insensitively), later
    /// entries overwrite earlier ones.
    /// </para>
    /// <para>
    /// Also available fluently as
    /// <see cref="VariableCollectionExtensions.ToVariableCollection"/>. For a fixed set
    /// of values, prefer a collection expression or
    /// <see cref="Create(ReadOnlySpan{Variable})"/>.
    /// </para>
    /// <para>
    /// If the supplied source is already a <see cref="VariableCollection"/>, it
    /// is simply returned as-is because it is already in an immutable state.
    /// </para>
    /// </remarks>
    public static VariableCollection CreateRange(IEnumerable<Variable> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source is VariableCollection existing)
        {
            // Already an immutable collection, so just return.
            return existing;
        }

        if (source.TryGetCount(out int count) && count == 0) // avoids enumeration if possible
        {
            return _empty;
        }

        var storage = new OrderedDictionary<string, Variable>(count, NameComparer);
        foreach (var variable in source)
        {
            Put(storage, variable, nameof(source));
        }

        return storage.Count == 0 ? _empty : new VariableCollection(storage);
    }

    /// <summary>
    /// Returns a new collection with the variable added, or, if a variable with the same
    /// name already exists, replaced and moved to the end (most recent).
    /// </summary>
    /// <param name="variable">The variable to add or update.</param>
    /// <returns>A new <see cref="VariableCollection"/> with the added/updated variable.
    /// </returns>
    [Pure]
    public VariableCollection SetItem(Variable variable)
    {
        var next = CopyStorage(extraCapacity: 1);
        Put(next, variable, nameof(variable));
        return new VariableCollection(next);
    }

    /// <summary>
    /// Returns a new collection with the variable added, or, if a variable with the same
    /// name already exists, replaced and moved to the end (most recent).
    /// </summary>
    /// <param name="name">The name of the variable.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>A new <see cref="VariableCollection"/> with the added/updated variable.
    /// </returns>
    [Pure]
    public VariableCollection SetItem(string name, MathValue value) =>
        SetItem(new Variable(name, value));

    /// <summary>
    /// Returns a new <see cref="VariableCollection"/> with the given variables added or
    /// updated in order, as if they were assigned in sequence by calling
    /// <see cref="SetItem(Variable)"/>, but with a single copy.
    /// </summary>
    /// <param name="items">The variables to add or update.</param>
    /// <returns>A new <see cref="VariableCollection"/> with the updated variables.
    /// </returns>
    /// <remarks>
    /// Existing variables with the same names as those in <paramref name="items"/> will
    /// be removed and re-added with the new values to preserve the MRU order of the new
    /// assignments.
    /// </remarks>
    [Pure]
    public VariableCollection SetItems(IReadOnlyCollection<Variable> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
        {
            return this;
        }

        var next = CopyStorage(items.Count);
        foreach (var variable in items)
        {
            Put(next, variable, nameof(items));
        }

        return new VariableCollection(next);
    }

    /// <summary>
    /// Returns a new collection with each variable in <paramref name="items"/> added or
    /// updated in order, as if they were assigned in sequence by calling
    /// <see cref="SetItem(Variable)"/>, but with a single copy. Returns this instance if
    /// <paramref name="items"/> is empty.
    /// </summary>
    /// <param name="items">The variables to add or update.</param>
    /// <returns>A new <see cref="VariableCollection"/> with the updated variables.</returns>
    [Pure]
    public VariableCollection SetItems(IEnumerable<Variable> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.TryGetCount(out int count) && count == 0) // avoids enumeration if possible
        {
            return this;
        }

        var next = CopyStorage(count);
        bool anyAdded = false;
        foreach (var variable in items)
        {
            Put(next, variable, nameof(items));
            anyAdded = true;
        }

        return anyAdded ? new VariableCollection(next) : this;
    }

    /// <inheritdoc cref="SetItems(IEnumerable{Variable})"/>
    [Pure]
    public VariableCollection SetItems(ReadOnlySpan<Variable> items)
    {
        if (items.IsEmpty)
        {
            return this;
        }

        var next = CopyStorage(items.Length);
        foreach (var variable in items)
        {
            Put(next, variable, nameof(items));
        }

        return new VariableCollection(next);
    }

    private OrderedDictionary<string, Variable> CopyStorage(int extraCapacity)
    {
        var next = new OrderedDictionary<string, Variable>(
            Count + extraCapacity, NameComparer);

        if (_storage is not null)
        {
            foreach (var (key, value) in _storage)
            {
                next.Add(key, value);
            }
        }

        return next;
    }

    private static void Put(
        OrderedDictionary<string, Variable> storage, Variable variable, string paramName)
    {
        if (variable.Name is null)
        {
            throw new ArgumentException(
                "Variable name cannot be null (default instance?).", paramName);
        }

        storage.Remove(variable.Name); // Move reassigned variables to the end.
        storage.Add(variable.Name, variable);
    }

    /// <summary>
    /// Returns a new collection without the named variables (case-insensitive). Names
    /// that are not present in the collection are ignored. Returns this instance if no
    /// variables are removed.
    /// </summary>
    /// <param name="names">The names of the variables to remove.</param>
    /// <returns>A new <see cref="VariableCollection"/> without the specified variables.
    /// </returns>
    /// <remarks>
    /// Other variables are untouched, including any whose
    /// <see cref="Variable.Expression"/> references a removed variable: values are
    /// snapshots, not live bindings.
    /// </remarks>
    [Pure]
    public VariableCollection Without(params ReadOnlySpan<string> names)
    {
        if (_storage is null || names.IsEmpty)
        {
            return this;
        }

        OrderedDictionary<string, Variable>? next = null;
        foreach (var name in names)
        {
            ArgumentNullException.ThrowIfNull(name);

            if (next is null)
            {
                if (!_storage.ContainsKey(name))
                {
                    continue;
                }

                next = CopyStorage(extraCapacity: 0);
            }

            next.Remove(name);
        }

        return next is null ?
            this :
            (next.Count == 0 ? _empty : new VariableCollection(next));
    }

    /// <summary>
    /// Determines whether the collection contains a variable with the specified name.
    /// </summary>
    public bool ContainsKey(string name) => _storage?.ContainsKey(name) == true;

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

    private string DebuggerDisplay =>
        Count == 0 ? "{Empty}" : $"Count = {Count}: {ToString()}";

    /// <inheritdoc/>
    public override string ToString()
    {
        if (Count == 0)
        {
            return "(empty)";
        }

        static string GetDefinition(Variable v) =>
            (v.Definition != null && v.Definition != v.Value.ToString()) ?
                $" ({v.Definition})" :
                "";

        return string.Join(", ", this.Select(v => $"{v.Name} = {v.Value}{GetDefinition(v)}"));
    }
}
