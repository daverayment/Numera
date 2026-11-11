using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Numera;

/// <summary>
/// An immutable, ordered collection of <see cref="CustomFunction"/> definitions.
/// Preserves order of registration while providing case-insensitive O(1) lookup by name.
/// Creation or adding functions throws <see cref="ArgumentException"/> if duplicate names
/// are provided.
/// </summary>
[CollectionBuilder(typeof(CustomFunctionCollection), nameof(Create))]
public sealed class CustomFunctionCollection : IReadOnlyList<CustomFunction>
{
    /// <summary>
    /// The single comparer used for every function name lookup, forwarding to
    /// <see cref="Identifier.Comparer"/>.
    /// </summary>
    public static StringComparer NameComparer => Identifier.Comparer;

    // Note: not "= []", as that would call Create() while this is still null.
    private static readonly CustomFunctionCollection _empty = new();

    /// <summary>
    /// Gets an empty custom function collection, which is the same instance every time.
    /// Safe to share.
    /// </summary>
    public static CustomFunctionCollection Empty { get; } = _empty;

    private readonly OrderedDictionary<string, CustomFunction>? _storage;

    /// <summary>
    /// The number of custom functions in the collection.
    /// </summary>
    public int Count => _storage?.Count ?? 0;

    /// <summary>
    /// The names of the custom functions in the collection.
    /// </summary>
    public IEnumerable<string> Names => this.Select(f => f.Name);

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
    private CustomFunctionCollection() { }

    private CustomFunctionCollection(OrderedDictionary<string, CustomFunction> storage)
    {
        _storage = storage;
    }

    /// <summary>
    /// Factory method enabling C# collection expression syntax (<c>[fn1, fn2]</c>).
    /// </summary>
    /// <param name="items">The functions to include in the collection.</param>
    /// <returns>A new <see cref="CustomFunctionCollection"/> containing the specified
    /// functions.</returns>
    /// <exception cref="ArgumentException">Thrown if duplicate function names are
    /// provided.</exception>
    public static CustomFunctionCollection Create(ReadOnlySpan<CustomFunction> items)
    {
        if (items.IsEmpty)
        {
            return _empty;
        }

        var storage = new OrderedDictionary<string, CustomFunction>(items.Length, NameComparer);
        foreach (var function in items)
        {
            AddNew(storage, function, nameof(items));
        }

        return new CustomFunctionCollection(storage);
    }

    /// <summary>
    /// Creates a new collection from an enumerable of <see cref="CustomFunction"/>s.
    /// </summary>
    /// <param name="source">The source enumerable of custom functions.</param>
    /// <returns>A new <see cref="CustomFunctionCollection"/> containing the specified
    /// functions.</returns>
    /// <exception cref="ArgumentException">Thrown if duplicate function names are
    /// provided.</exception>
    public static CustomFunctionCollection CreateRange(IEnumerable<CustomFunction> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source is CustomFunctionCollection existing)
        {
            // Already an immutable collection, return as-is.
            return existing;
        }

        if (source.TryGetCount(out int count) && count == 0) // avoids enumeration if possible
        {
            return _empty;
        }

        var storage = new OrderedDictionary<string, CustomFunction>(count, NameComparer);
        foreach (var function in source)
        {
            AddNew(storage, function, nameof(source));
        }

        return storage.Count == 0 ? _empty : new CustomFunctionCollection(storage);
    }

    /// <summary>
    /// Returns a new collection with the given custom function added or updated.
    /// </summary>
    /// <param name="function">The function to add or update.</param>
    /// <returns>A new <see cref="CustomFunctionCollection"/>.</returns>
    [Pure]
    public CustomFunctionCollection SetItem(CustomFunction function)
    {
        ArgumentNullException.ThrowIfNull(function);

        var next = CopyStorage(extraCapacity: 1);
        next[function.Name] = function;
        return new CustomFunctionCollection(next);
    }

    // Fluent overloads.

    /// <summary>
    /// Returns a new collection with a zero-argument custom function added or updated,
    /// keyed by the specified name.
    /// </summary>
    /// <param name="name">The name of the custom function.</param>
    /// <param name="handler">The function handler.</param>
    /// <param name="description">An optional description of the function.</param>
    /// <returns>A new <see cref="CustomFunctionCollection"/> with the added/updated
    /// function.</returns>
    /// <exception cref="ArgumentNullException">The name or handler is null.</exception>
    /// <exception cref="ArgumentException">The name is invalid or reserved.</exception>
    [Pure]
    public CustomFunctionCollection SetItem(string name, Func<MathValue> handler, string? description = null) =>
        SetItem(CustomFunction.Create(name, handler, description));

    /// <summary>
    /// Returns a new collection with the given custom function added or updated, keyed by
    /// the specified name. 
    /// </summary>
    /// <param name="name">The name of the custom function.</param>
    /// <param name="handler">The function handler.</param>
    /// <param name="description">An optional description of the function.</param>
    /// <returns>A new <see cref="CustomFunctionCollection"/> with the added/updated
    /// function.</returns>
    /// <exception cref="ArgumentException"></exception>
    [Pure]
    public CustomFunctionCollection SetItem(string name, Func<MathValue, MathValue> handler, string? description = null) =>
        SetItem(CustomFunction.Create(name, handler, description));

    /// <summary>
    /// Returns a new collection with the given custom function added or updated, keyed by
    /// the specified name. 
    /// </summary>
    /// <param name="name">The name of the custom function.</param>
    /// <param name="handler">The function handler.</param>
    /// <param name="description">An optional description of the function.</param>
    /// <returns>A new <see cref="CustomFunctionCollection"/> with the added/updated
    /// function.</returns>
    /// <exception cref="ArgumentException"></exception>
    [Pure]
    public CustomFunctionCollection SetItem(string name, Func<MathValue, MathValue, MathValue> handler, string? description = null) =>
        SetItem(CustomFunction.Create(name, handler, description));

    /// <summary>
    /// Returns a new collection with the given custom function added or updated, keyed by
    /// the specified name. 
    /// </summary>
    /// <param name="name">The name of the custom function.</param>
    /// <param name="handler">The function handler.</param>
    /// <param name="description">An optional description of the function.</param>
    /// <returns>A new <see cref="CustomFunctionCollection"/> with the added/updated
    /// function.</returns>
    /// <exception cref="ArgumentException"></exception>
    [Pure]
    public CustomFunctionCollection SetItem(string name, Func<MathValue, MathValue, MathValue, MathValue> handler, string? description = null) =>
        SetItem(CustomFunction.Create(name, handler, description));

    /// <summary>
    /// Returns a new collection with the given variadic custom function added or updated, keyed by
    /// the specified name. 
    /// </summary>
    /// <param name="name">The name of the custom function.</param>
    /// <param name="minArgs">The minimum number of arguments.</param>
    /// <param name="handler">The function handler.</param>
    /// <param name="description">An optional description of the function.</param>
    /// <returns>A new <see cref="CustomFunctionCollection"/> with the added/updated
    /// function.</returns>
    /// <exception cref="ArgumentException"></exception>
    [Pure]
    public CustomFunctionCollection SetItem(string name, int minArgs, Func<IReadOnlyList<MathValue>, MathValue> handler, string? description = null) =>
        SetItem(CustomFunction.CreateVariadic(name, minArgs, handler, description));

    private OrderedDictionary<string, CustomFunction> CopyStorage(int extraCapacity)
    {
        var next = new OrderedDictionary<string, CustomFunction>(
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

    private static void AddNew(
        OrderedDictionary<string, CustomFunction> storage, CustomFunction function, string paramName)
    {
        if (function is null)
        {
            throw new ArgumentException(
                "Collection contains a null function.", paramName);
        }

        if (!storage.TryAdd(function.Name, function))
        {
            throw new ArgumentException(
                $"A function with the name '{function.Name}' already exists in the " + 
                "collection. (Names are case-insensitive.)",
                paramName);
        }
    }

    /// <summary>
    /// Determines whether the collection contains a function with the specified name.
    /// </summary>
    /// <param name="name">The function name to check.</param>
    public bool ContainsName(string name) => _storage?.ContainsKey(name) == true;

    /// <summary>
    /// Attempts to retrieve a custom function by name.
    /// </summary>
    /// <param name="name">The function name to resolve.</param>
    /// <param name="function">When found, receives the custom function instance.</param>
    /// <returns><c>true</c> if found; otherwise <c>false</c>.</returns>
    public bool TryGetFunction(string name, [NotNullWhen(true)] out CustomFunction? function)
    {
        if (_storage is not null && _storage.TryGetValue(name, out function))
        {
            return true;
        }

        function = default;
        return false;
    }

    /// <summary>
    /// Attempts to retrieve a custom function by span name.
    /// </summary>
    public bool TryGetFunction(ReadOnlySpan<char> name, [NotNullWhen(true)] out CustomFunction? function)
    {
        if (_storage is not null)
        {
            return TryGetFunction(name.ToString(), out function);
        }

        function = default;
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

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    public override string ToString()
    {
        if (Count == 0)
        {
            return "(empty)";
        }

        return string.Join(", ", Names);
    }
}
