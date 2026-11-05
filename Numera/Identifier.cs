using System.Diagnostics.CodeAnalysis;

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
    /// Determines whether the specified character is a valid starting character for an
    /// identifier; it must be a letter or underscore.
    /// </summary>
    public static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

    /// <summary>
    /// Determines whether the specified character is a valid subsequent character for an
    /// identifier.
    /// </summary>
    public static bool IsIdentifierPart(char c) =>
        (char.IsLetter(c) || char.IsAsciiDigit(c) || c == '_') &&
        !ConstantCatalog.IsConstantLetter(c);

    /// <summary>
    /// Determines whether the specified string is a syntactically valid identifier.
    /// </summary>
    public static bool IsValidIdentifier(
        ReadOnlySpan<char> name, [NotNullWhen(false)] out string? errorMessage)
    {
        errorMessage = null;
        if (name.IsEmpty)
        {
            errorMessage = "Identifier cannot be empty.";
            return false;
        }

        if (name.Trim().Length != name.Length)
        {
            errorMessage = "Identifier cannot have leading or trailing whitespace.";
            return false;
        }

        // Allow single-character constant letters (e.g. "π") as valid identifiers, but
        // reject multi-character identifiers that contain constant letters (e.g. "πx").
        if (ConstantCatalog.IsConstantLetter(name[0]))
        {
            if (name.Length != 1)
            {
                errorMessage = "Identifier cannot contain constant letters.";
                return false;
            }

            return true;
        }

        if (!IsIdentifierStart(name[0]))
        {
            errorMessage = "Identifier must start with a letter or underscore.";
            return false;
        }

        for (int i = 1; i < name.Length; i++)
        {
            if (!IsIdentifierPart(name[i]))
            {
                errorMessage =
                    "Identifier can only contain letters, digits, and underscores.";
                return false;
            }
        }

        return true;
    }
}
