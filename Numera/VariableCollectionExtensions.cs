namespace Numera;

/// <summary>
/// VariableCollection extension methods.
/// </summary>
public static class VariableCollectionExtensions
{
    /// <summary>
    /// Create a <see cref="VariableCollection"/> from an enumerable Variable source.
    /// </summary>
    /// <param name="source">The enumerable Variable source.</param>
    /// <returns>A new <see cref="VariableCollection"/> consisting of the provided
    /// Variables.</returns>
    /// <remarks>
    /// Duplicate variables in the source collection are permitted, but only the last
    /// appearance of each variable is recorded in the result.
    /// <para>
    /// For a fixed set of values, prefer
    /// <seealso cref="VariableCollection.Create(ReadOnlySpan{Variable})"/> or a
    /// collection expression.
    /// </para>
    /// <para>
    /// If the supplied source is already a <see cref="VariableCollection"/>, it
    /// is simply returned as-is because it is already in an immutable state.
    /// </para>
    /// </remarks>
    public static VariableCollection ToVariableCollection(this IEnumerable<Variable> source) =>
        VariableCollection.CreateRange(source);
}
