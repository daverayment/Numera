using System.Diagnostics.CodeAnalysis;

namespace Numera;

/// <summary>
/// Describes a built-in constant and the invocation name represented by this entry.
/// </summary>
/// <param name="Name">The recognized name or symbol represented by this entry.</param>
/// <param name="CanonicalName">The primary textual identifier for the constant.</param>
/// <param name="Kind">The mathematical or Boolean constant's identity.</param>
/// <param name="Value">The stored value, including its calculation exactness.</param>
/// <param name="Description">A human-readable summary of the constant.</param>
/// <param name="Aliases">Alternative supported spellings, excluding casing variants.
/// </param>
/// <param name="IsAlias">Whether this entry represents an alias rather than the canonical
/// name.</param>
public sealed record ConstantInfo(
    string Name,
    string CanonicalName,
    ConstantKind Kind,
    MathValue Value,
    string Description,
    IReadOnlyList<string> Aliases,
    bool IsAlias = false);

/// <summary>
/// Read-only discovery and resolution of built-in mathematical and Boolean constants.
/// </summary>
/// <remarks>
/// Uses the parser's culture-independent identifier rules and supported Greek symbols.
/// Irrational constants are finite Decimal approximations with Unverified exactness;
/// true and false have Exact values of one and zero. These names do not include
/// arbitrary variables or every predefined MathValue property, such as NaN or Infinity.
/// </remarks>
public static class Constants
{
    /// <summary>
    /// Gets all recognized names, including aliases, sorted ordinally ignoring case.
    /// Casing variants are not enumerated separately.
    /// </summary>
    public static IReadOnlyCollection<string> AllNames => ConstantCatalog.AllNames;

    /// <summary>
    /// Gets canonical names, sorted ordinally ignoring case.
    /// </summary>
    public static IReadOnlyCollection<string> CanonicalNames => ConstantCatalog.CanonicalNames;

    /// <summary>
    /// Gets metadata for all recognized names, including aliases, in name order.
    /// </summary>
    public static IReadOnlyList<ConstantInfo> All => ConstantCatalog.All;

    /// <summary>
    /// Gets metadata for canonical constants in name order.
    /// </summary>
    public static IReadOnlyList<ConstantInfo> Canonical => ConstantCatalog.Canonical;

    /// <summary>
    /// Looks up metadata for a name or supported symbol without evaluating an expression.
    /// Matching is ordinal and case-insensitive within the parser's supported spellings.
    /// </summary>
    public static bool TryGetInfo(string name, [NotNullWhen(true)] out ConstantInfo? info)
    {
        ArgumentNullException.ThrowIfNull(name);
        return ConstantCatalog.TryGetInfo(name, out info);
    }

    /// <summary>
    /// Resolves a name or supported symbol to the same stored value and exactness used
    /// by evaluation. Unknown names return false and set value to default; null throws.
    /// Leading and trailing whitespace is not ignored.
    /// </summary>
    public static bool TryResolve(string name, out MathValue value)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (ConstantCatalog.TryGetInfo(name, out var info))
        {
            value = info.Value;
            return true;
        }

        value = default;
        return false;
    }
}
