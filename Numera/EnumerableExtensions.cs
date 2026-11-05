namespace Numera;

internal static class EnumerableExtensions
{
    /// <summary>
    /// Attempts to get the number of elements in a sequence without enumerating it. If
    /// the source implements <see cref="ICollection{T}"/> or
    /// <see cref="IReadOnlyCollection{T}"/>, the count is returned directly. Otherwise,
    /// the TryGetNonEnumeratedCount extension method is called on the source.
    /// </summary>
    internal static bool TryGetCount<T>(
        this IEnumerable<T> source, out int count)
    {
        if (source is IReadOnlyCollection<T> readOnlyCollection)
        {
            count = readOnlyCollection.Count;
            return true;
        }

        return source.TryGetNonEnumeratedCount(out count);
    }
}
