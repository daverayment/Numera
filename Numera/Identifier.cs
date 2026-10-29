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

    /// <summary>
    /// Determines whether the specified character is a valid starting character for an identifier.
    /// </summary>
    public static bool IsIdentifierStart(char c) =>
        char.IsLetter(c) || c == '_';

    /// <summary>
    /// Determines whether the specified character is a valid subsequent character for an identifier.
    /// </summary>
    public static bool IsIdentifierPart(char c) =>
        (char.IsLetter(c) || char.IsAsciiDigit(c) || c == '_') && !ConstantCatalog.IsConstantLetter(c);

    /// <summary>
    /// Determines whether the specified string is a syntactically valid identifier.
    /// </summary>
    public static bool IsValidIdentifier(ReadOnlySpan<char> name)
    {
        if (name.IsEmpty)
        {
            return false;
        }

        if (ConstantCatalog.IsConstantLetter(name[0]))
        {
            return name.Length == 1;
        }

        if (!IsIdentifierStart(name[0]))
        {
            return false;
        }

        for (int i = 1; i < name.Length; i++)
        {
            if (!IsIdentifierPart(name[i]))
            {
                return false;
            }
        }

        return true;
    }
}
