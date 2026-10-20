using System.Globalization;

namespace Numera;

/// <summary>
/// Configuration options governing how input expressions are preprocessed and normalized
/// before evaluation.
/// </summary>
public sealed record NormalizationOptions
{
    /// <summary>
    /// The culture used to interpret decimal and thousands separators. When null, invariant
    /// rules apply (period as decimal separator, comma as function argument delimiter).
    /// </summary>
    public CultureInfo? Culture { get; init; }

    /// <summary>
    /// Whether to expand pocket-calculator percentage idioms (e.g. '200 + 15%' to
    /// '200 + (200 * (15 / 100))' and '15% of 200' to '(15 / 100) * 200').
    /// </summary>
    public bool ExpandPercentages { get; init; } = true;

    /// <summary>
    /// Whether to insert explicit multiplication operators for implicit product patterns
    /// such as '2pi', '2(3 + 4)', and '(1 + 2)(3 + 4)'.
    /// </summary>
    public bool ExpandImplicitMultiplication { get; init; } = true;

    /// <summary>
    /// Whether to convert postfix factorials such as '5!' and '(2 + 3)!' into standard
    /// 'factorial(5)' and 'factorial(2 + 3)' function calls.
    /// </summary>
    public bool ConvertPostfixFactorials { get; init; } = true;

    /// <summary>
    /// Whether to replace typographic Unicode mathematical symbols (e.g. ×, •, ·, ÷, ⁄, −, –, —)
    /// and Fortran/Python power operator '**' with canonical ASCII operators (*, /, -, ^).
    /// </summary>
    public bool ReplaceUnicodeOperators { get; init; } = true;

    /// <summary>
    /// Whether to strip leading activation equals ('= 2 + 2') and trailing evaluation equals ('2 + 2 =')
    /// commonly produced by launcher inputs or physical desktop calculator habits.
    /// </summary>
    public bool TrimLauncherEquals { get; init; } = true;

    /// <summary>
    /// Default normalization options with all standard transformations enabled.
    /// </summary>
    public static NormalizationOptions Default { get; } = new();
}
