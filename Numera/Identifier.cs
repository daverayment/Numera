namespace Numera;

/// <summary>
/// Defines casing, comparison, and equality rules for all identifiers in Numera, including
/// variable names, function names, mathematical constants, and reserved keywords.
/// </summary>
/// <remarks>
/// All identifier matching in Numera is ordinal and case-insensitive. Ordinal rules avoid
/// culture-sensitive casing anomalies (such as Turkish dotted and dotless 'i') while
/// allowing users to type identifiers using any casing.
/// </remarks>
public static class Identifier
{
    /// <summary>
    /// The <see cref="StringComparer"/> used across Numera for variable names, function
    /// names, constants, and keywords.
    /// </summary>
    public static StringComparer Comparer => StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// The <see cref="StringComparison"/> used across Numera for variable names, function
    /// names, constants, and keywords.
    /// </summary>
    public static StringComparison Comparison => StringComparison.OrdinalIgnoreCase;
}
