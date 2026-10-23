using System.Globalization;

namespace Numera;

/// <summary>
/// Configuration options governing how input expressions are preprocessed and normalized
/// before evaluation.
/// </summary>
public sealed record NormalizationOptions
{
    /// <summary>
    /// The culture used to interpret decimal and thousands separators. When null,
    /// invariant rules apply (period as decimal separator, comma as function argument    
    /// delimiter).
    /// </summary>
    public CultureInfo? Culture { get; init; }

    /// <summary>
    /// Whether to replace typographic Unicode mathematical symbols (e.g. ×, •, ·, ÷, ⁄, −, –, —)
    /// and Fortran/Python power operator '**' with canonical ASCII operators (*, /, -, ^).
    /// </summary>
    public bool ReplaceUnicodeOperators { get; init; } = true;

    /// <summary>
    /// Whether to replace European ratio/division colons flanked by numeric operands or
    /// closing parentheses (e.g. '6 : 2' or '16:9') with standard division slashes ('/').
    /// Identifiers must be parenthesised to take part, e.g. '(pi):2'. Defaults to
    /// <c>true</c>.
    /// </summary>
    public bool ReplaceDivisionColon { get; init; } = true;

    /// <summary>
    /// Whether to strip leading activation equals ('= 2 + 2') and trailing evaluation
    /// equals ('2 + 2 =') commonly produced by launcher inputs or physical desktop
    /// calculator habits.
    /// </summary>
    public bool TrimLauncherEquals { get; init; } = true;

    /// <summary>
    /// Default normalization options with all standard transformations enabled.
    /// </summary>
    public static NormalizationOptions Default { get; } = new();
}
