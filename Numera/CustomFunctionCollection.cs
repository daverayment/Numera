using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Numera;

/// <summary>
/// An immutable, ordered collection of <see cref="CustomFunction"/> definitions.
/// Preserves order of registration while providing case-insensitive O(1) lookup by name.
/// </summary>
[CollectionBuilder(typeof(CustomFunctionCollection), nameof(Create))]
public sealed class CustomFunctionCollection :
    IReadOnlyList<CustomFunction>,
    IReadOnlyDictionary<string, CustomFunction>
{
    /// <summary>
    /// The single comparer used for every function name lookup, forwarding to
    /// <see cref="Identifier.Comparer"/>.
    /// </summary>
    public static StringComparer NameComparer => Identifier.Comparer;

    /// <summary>
    /// Gets an empty custom function collection, which is the same instance every time.
    /// Safe to share.
    /// </summary>
    public static CustomFunctionCollection Empty { get; } = new();

    private readonly OrderedDictionary<string, CustomFunction>? _storage;

    /// <summary>
    /// The number of custom functions in the collection.
    /// </summary>
    public int Count => _storage?.Count ?? 0;

    /// <summary>
    /// The names of the custom functions in the collection.
    /// </summary>
    public IEnumerable<string> Names => _storage?.Keys is not null ? _storage.Keys : [];

    IEnumerable<string> IReadOnlyDictionary<string, CustomFunction>.Keys => Names;

    IEnumerable<CustomFunction> IReadOnlyDictionary<string, CustomFunction>.Values => this;

    /// <summary>
    /// Retrieves a custom function by name, case-insensitive. O(1).
    /// </summary>
    /// <param name="name">Function name.</param>
    /// <returns>The <see cref="CustomFunction"/> instance.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the function is not found.
    /// </exception>
    public CustomFunction this[string name] =>
        _storage?.TryGetValue(name, out var func) == true ?
            func :
            throw new KeyNotFoundException($"Custom function '{name}' not found.");

    /// <summary>
    /// Positional lookup by registration index. O(1).
    /// </summary>
    /// <param name="index">Index of the custom function in the collection.</param>
    /// <returns>The <see cref="CustomFunction"/> at the given index.</returns>
    /// <exception cref="IndexOutOfRangeException">Thrown if the index is out of range.
    /// </exception>
    public CustomFunction this[int index]
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
    /// Initializes an empty <see cref="CustomFunctionCollection"/>.
    /// </summary>
    public CustomFunctionCollection() { }

    /// <summary>
    /// Initializes a new collection from an existing sequence of
    /// <see cref="CustomFunction"/>s. If duplicate names are provided (comparing case-
    /// insensitively), later functions overwrite earlier ones.
    /// </summary>
    /// <param name="functions">The functions to include.</param>
    public CustomFunctionCollection(IEnumerable<CustomFunction>? functions)
    {
        if (functions is not null)
        {
            int capacity = functions is ICollection<CustomFunction> coll ? coll.Count : 1;
            var storage = new OrderedDictionary<string, CustomFunction>(capacity, NameComparer);

            foreach (var func in functions)
            {
                ArgumentNullException.ThrowIfNull(func);
                storage.Remove(func.Name);
                storage.Add(func.Name, func);
            }

            _storage = storage;
        }
    }

    private CustomFunctionCollection(OrderedDictionary<string, CustomFunction> storage)
    {
        _storage = storage;
    }

    /// <summary>
    /// Factory method enabling C# collection expression syntax (<c>[fn1, fn2]</c>).
    /// </summary>
    /// <param name="functions">The functions to include in the collection.</param>
    /// <returns>A new <see cref="CustomFunctionCollection"/> containing the specified
    /// functions.</returns>
    public static CustomFunctionCollection Create(ReadOnlySpan<CustomFunction> functions)
    {
        if (functions.IsEmpty)
        {
            return Empty;
        }

        var storage = new OrderedDictionary<string, CustomFunction>(functions.Length, NameComparer);
        foreach (var func in functions)
        {
            ArgumentNullException.ThrowIfNull(func);
            storage.Remove(func.Name);
            storage.Add(func.Name, func);
        }

        return new CustomFunctionCollection(storage);
    }

    /// <summary>
    /// Returns a new collection with the given custom function added or updated.
    /// </summary>
    /// <param name="function">The function to add or update.</param>
    /// <returns>A new <see cref="CustomFunctionCollection"/>.</returns>
    public CustomFunctionCollection Add(CustomFunction function)
    {
        ArgumentNullException.ThrowIfNull(function);
        ArgumentNullException.ThrowIfNull(function.Name, nameof(function.Name));

        var next = _storage is null ?
            new OrderedDictionary<string, CustomFunction>(1, NameComparer) :
            new OrderedDictionary<string, CustomFunction>(_storage, NameComparer);

        next.Remove(function.Name);
        next.Add(function.Name, function);

        return new CustomFunctionCollection(next);
    }

    // Fluent overloads.
    /// <summary>
    /// Adds a single-argument custom function.
    /// </summary>
    public CustomFunctionCollection Add(string name, Func<MathValue, MathValue> handler, string? description = null) =>
        Add(CustomFunction.Create(name, handler, description));

    /// <summary>
    /// Adds a two-argument custom function.
    /// </summary>
    public CustomFunctionCollection Add(string name, Func<MathValue, MathValue, MathValue> handler, string? description = null) =>
        Add(CustomFunction.Create(name, handler, description));

    /// <summary>
    /// Adds a three-argument custom function.
    /// </summary>
    public CustomFunctionCollection Add(string name, Func<MathValue, MathValue, MathValue, MathValue> handler, string? description = null) =>
        Add(CustomFunction.Create(name, handler, description));

    /// <summary>
    /// Adds a variadic custom function with a minimum number of arguments.
    /// </summary>
    public CustomFunctionCollection Add(string name, int minArgs, Func<IReadOnlyList<MathValue>, MathValue> handler, string? description = null) =>
        Add(CustomFunction.CreateVariadic(name, minArgs, handler, description));

    /// <summary>
    /// Determines whether the collection contains a function with the specified name.
    /// </summary>
    /// <param name="name">The function name to check.</param>
    public bool Contains(string name) => _storage?.ContainsKey(name) == true;

    bool IReadOnlyDictionary<string, CustomFunction>.ContainsKey(string key) => Contains(key);

    /// <summary>
    /// Attempts to retrieve a custom function by name.
    /// </summary>
    /// <param name="name">The function name to resolve.</param>
    /// <param name="function">When found, receives the custom function instance.</param>
    /// <returns><c>true</c> if found; otherwise <c>false</c>.</returns>
    public bool TryGet(string name, [NotNullWhen(true)] out CustomFunction? function)
    {
        if (_storage is not null && _storage.TryGetValue(name, out function))
        {
            return true;
        }

        function = null;
        return false;
    }

    bool IReadOnlyDictionary<string, CustomFunction>.TryGetValue(string key, [NotNullWhen(true)] out CustomFunction? value) =>
        TryGet(key, out value);

    /// <summary>
    /// Attempts to retrieve a custom function by span name.
    /// </summary>
    public bool TryGet(ReadOnlySpan<char> name, [NotNullWhen(true)] out CustomFunction? function)
    {
        if (_storage is not null)
        {
            return TryGet(name.ToString(), out function);
        }

        function = null;
        return false;
    }

    /// <inheritdoc/>
    public IEnumerator<CustomFunction> GetEnumerator()
    {
        if (_storage is not null)
        {
            foreach (var entry in _storage)
            {
                yield return entry.Value;
            }
        }
    }

    IEnumerator<KeyValuePair<string, CustomFunction>> IEnumerable<KeyValuePair<string, CustomFunction>>.GetEnumerator() =>
        _storage?.GetEnumerator() ?? Enumerable.Empty<KeyValuePair<string, CustomFunction>>().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
