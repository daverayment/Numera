using System.Diagnostics.CodeAnalysis;

namespace Numera;

/// <summary>
/// Describes a parameter accepted by a calculator function.
/// </summary>
/// <param name="Name">The conventional identifier for the parameter (e.g. "x", "min",
/// "max").</param>
/// <param name="IsOptional">Whether this parameter can be omitted by callers.</param>
/// <param name="IsVariadic">Whether this parameter accepts one or more repeated
/// arguments.</param>
public sealed record ParameterInfo(string Name, bool IsOptional = false, bool IsVariadic = false);

/// <summary>
/// Categorizes functions supported by Numera to assist in documentation, discovery, and
/// UI grouping.
/// </summary>
public enum FunctionCategory
{
    /// <summary>
    /// Trigonometric functions and polar helpers (sin, cos, tan, atan2, etc.).
    /// </summary>
    Trigonometry,

    /// <summary>
    /// Hyperbolic functions and their area/inverse counterparts (sinh, cosh, atanh, etc.).
    /// </summary>
    Hyperbolic,

    /// <summary>
    /// Conversions between radians, degrees, and gradians (deg2rad, rad2deg, etc.).
    /// </summary>
    AngleConversion,

    /// <summary>
    /// Exponential, power, root, and logarithmic functions (exp, log10, sqrt, pow, etc.).
    /// </summary>
    ExponentialsAndLogarithms,

    /// <summary>
    /// Basic operations, rounding, clamping, and interpolation (round, abs, clamp, lerp, etc.).
    /// </summary>
    ArithmeticAndRounding,

    /// <summary>
    /// Variadic summary and statistical operations (min, max, avg, sum, mul).
    /// </summary>
    StatisticalAndAggregation,

    /// <summary>
    /// Combinatorics, permutations, factorials, and discrete integer operations (factorial).
    /// </summary>
    CombinatoricsAndNumberTheory,

    /// <summary>
    /// Bitwise manipulation and integer bit shifts (shl, shr, band, bor, bxor, bnot).
    /// </summary>
    Bitwise,

    /// <summary>
    /// Probability, error, and cardinal functions (erf, erfc, ncdf, sinc).
    /// </summary>
    SpecialAndProbability,

    /// <summary>
    /// Floating-point and numerical property classification (e.g. isnan, isfinite, isinf).
    /// </summary>
    Inspection,

    /// <summary>
    /// Pseudo-random number generation (rand, randi).
    /// </summary>
    Random,
}

/// <summary>
/// Inclusive argument-count bounds and parameter specifications for a supported function.
/// A null maximum denotes a variadic function.
/// </summary>
public sealed record FunctionSignature(
    int MinimumArguments,
    int? MaximumArguments,
    IReadOnlyList<ParameterInfo> Parameters)
{
    /// <summary>
    /// Initializes a new instance of <see cref="FunctionSignature"/> with argument
    /// bounds and an empty parameter list.
    /// </summary>
    public FunctionSignature(int minimumArguments, int? maximumArguments)
        : this(minimumArguments, maximumArguments, [])
    {
    }

    /// <summary>
    /// Whether the signature permits more than one argument, not whether it requires them.
    /// </summary>
    public bool AllowsMultipleArguments => MaximumArguments is null or > 1;
}

/// <summary>
/// Complete metadata for a function recognized by Numera, including its canonical name,
/// signature, description, aliases, and discovery keywords.
/// </summary>
/// <param name="Name">The specific invocation name represented by this metadata instance.
/// </param>
/// <param name="CanonicalName">The primary, standard identifier for the function (e.g.
/// ISO 80000-2:2019 / NIST DLMF).</param>
/// <param name="Category">The functional classification domain.</param>
/// <param name="Signature">Technical argument bounds and parameter descriptions.</param>
/// <param name="Description">Human-readable summary of the function's mathematical
/// behavior.</param>
/// <param name="Aliases">Alternative invocation names supported for this function.
/// </param>
/// <param name="Keywords">Curated keywords, synonyms, and domain terms for search
/// discovery.</param>
/// <param name="IsAlias">Whether this instance represents an alias rather than the
/// canonical name.</param>
public sealed record FunctionInfo(
    string Name,
    string CanonicalName,
    FunctionCategory Category,
    FunctionSignature Signature,
    string Description,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<string> Keywords,
    bool IsAlias = false);

/// <summary>
/// Read-only function discovery and metadata API.
/// </summary>
public static class Functions
{
    /// <summary>
    /// Gets all recognized function names, including canonical names and aliases, sorted
    /// alphabetically.
    /// </summary>
    public static IReadOnlyCollection<string> AllNames => FunctionCatalog.AllNames;

    /// <summary>
    /// Gets all canonical function names, sorted alphabetically.
    /// </summary>
    public static IReadOnlyCollection<string> CanonicalNames => FunctionCatalog.CanonicalNames;

    /// <summary>
    /// Gets metadata for all recognized function names, including aliases.
    /// </summary>
    public static IReadOnlyList<FunctionInfo> All => FunctionCatalog.All;

    /// <summary>
    /// Gets metadata for all canonical functions.
    /// </summary>
    public static IReadOnlyList<FunctionInfo> Canonical => FunctionCatalog.Canonical;

    /// <summary>
    /// Looks up function metadata by name using ordinal, case-insensitive matching.
    /// </summary>
    public static bool TryGetInfo(string name, [NotNullWhen(true)] out FunctionInfo? info)
    {
        ArgumentNullException.ThrowIfNull(name);
        return FunctionCatalog.TryGetInfo(name, out info);
    }

    /// <summary>
    /// Looks up a supported function signature using ordinal, case-insensitive matching.
    /// </summary>
    public static bool TryGetSignature(string name, [NotNullWhen(true)] out FunctionSignature? signature)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (FunctionCatalog.TryGetInfo(name, out var info))
        {
            signature = info.Signature;
            return true;
        }

        signature = null;
        return false;
    }

    /// <summary>
    /// Searches for functions matching the given query using tiered relevance matching
    /// (exact name/alias, prefix, substring, category/abbreviation, and curated keywords).
    /// </summary>
    /// <param name="query">The search term to match.</param>
    /// <param name="includeAliases">Whether to include alias entries alongside canonical
    /// results.</param>
    /// <returns>Matching functions ordered by relevance tier and then alphabetically.
    /// </returns>
    public static IEnumerable<FunctionInfo> Search(string query, bool includeAliases = false)
    {
        ArgumentNullException.ThrowIfNull(query);
        return FunctionCatalog.Search(query, includeAliases);
    }
}
