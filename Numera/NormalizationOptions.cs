namespace Numera;

/// <summary>
/// Configuration options governing how input expressions are preprocessed and normalized
/// before evaluation. These flags only switch individual passes on or off; the culture used to read
/// numbers comes from <see cref="CalculatorOptions.Culture"/> (or is passed directly to
/// <see cref="ExpressionNormalizer"/>), and culture handling always runs.
/// </summary>
public sealed record NormalizationOptions
{
    /// <summary>
    /// Whether to replace typographic operators (such as <c>×</c>, <c>÷</c> and the Unicode minus) and
    /// the Fortran/Python power operator <c>**</c> with the canonical ASCII operators the parser
    /// understands (<c>*</c>, <c>/</c>, <c>-</c>, <c>^</c>), so pasted text evaluates. Defaults to
    /// <c>true</c>.
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
    /// Default normalization options with all standard transformations enabled. The instance is
    /// immutable and shared.
    /// </summary>
    public static NormalizationOptions Default { get; } = new();
}
