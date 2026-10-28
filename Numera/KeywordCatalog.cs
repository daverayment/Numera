using System.Collections.Frozen;

namespace Numera;

/// <summary>
/// Central static registry of reserved language keywords, operator words, and literal
/// keywords.
/// </summary>
internal static class KeywordCatalog
{
    private static readonly StringComparer NameComparer = Identifier.Comparer;

    private static readonly FrozenSet<string> OperatorKeywords = new[]
    {
        "and",
        "or",
        "xor",
        "not",
        "of",
    }.ToFrozenSet(NameComparer);

    private static readonly FrozenSet<string> ReservedForAssignment = new[]
    {
        "and",
        "or",
        "xor",
        "not",
        "of",
        // TODO: "ans"?
    }.ToFrozenSet(NameComparer);

    private static readonly FrozenSet<string> ImplicitMultiplicationBlockers = new[]
    {
        "and",
        "or",
        "xor",
        "not",
        "of",
        "true",
        "false",
    }.ToFrozenSet(NameComparer);

    private static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> OperatorLookup =
        OperatorKeywords.GetAlternateLookup<ReadOnlySpan<char>>();

    private static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> ReservedLookup  =
        ReservedForAssignment.GetAlternateLookup<ReadOnlySpan<char>>();

    private static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> ImplicitLookup =
        ImplicitMultiplicationBlockers.GetAlternateLookup<ReadOnlySpan<char>>();

    /// <summary>
    /// Returns <c>true</c> if the token is an operator keyword (e.g. "and", "or", "not").
    /// Used by percentage rules to determine whether an operand follows '%'.
    /// </summary>
    /// <param name="name">The identifier to check.</param>
    /// <returns><see langword="true"/> if <paramref name="name"/> is an operator keyword;
    /// otherwise, <see langword="false"/>.</returns>
    public static bool IsOperatorKeyword(ReadOnlySpan<char> name) =>
        OperatorLookup.Contains(name);

    /// <summary>
    /// Determines whether a name is in the reserved-name set, including operator keywords
    /// and literal keywords, which cannot be assigned to.
    /// </summary>
    /// <param name="name">Name to test.</param>
    /// <returns><see langword="true"/> if <paramref name="name"/> is reserved; otherwise,
    /// <see langword="false"/>.</returns>
    public static bool IsReserved(ReadOnlySpan<char> name) =>
        ReservedLookup.Contains(name);

    /// <summary>
    /// Determines whether a name is classified as preventing implicit multiplication.
    /// </summary>
    /// <param name="name">The name to test.</param>
    /// <returns><see langword="true"/> if <paramref name="name"/> is in the implicit-
    /// multiplication prevention lookup;
    /// otherwise, <see langword="false"/>.</returns>
    public static bool PreventsImplicitMultiplication(ReadOnlySpan<char> name) =>
        ImplicitLookup.Contains(name);
}
