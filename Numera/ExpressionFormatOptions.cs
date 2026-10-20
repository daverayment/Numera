using System.Globalization;
using Numera.Ast;

namespace Numera;

/// <summary>
/// Specifies the rendering style for mathematical expression formatting.
/// </summary>
public enum ExpressionFormatStyle
{
    /// <summary>
    /// Linear typographic Unicode text suitable for plain text labels, subtitles, search results, and clipboards.
    /// E.g. "2 × π × √(16) ÷ 2"
    /// </summary>
    TypographicUnicode,

    /// <summary>
    /// MathJax / KaTeX / LaTeX formula representation.
    /// E.g. "2 \cdot \pi \cdot \frac{\sqrt{16}}{2}"
    /// </summary>
    MathJax,

    /// <summary>
    /// Standard normalized ASCII invariant expression with clean, standardized operator spacing.
    /// E.g. "2 * pi * sqrt(16) / 2"
    /// </summary>
    NormalizedAscii,
}

/// <summary>
/// Specifies the multiplication symbol used when rendering expressions.
/// </summary>
public enum MultiplicationSymbol
{
    /// <summary>
    /// Cross symbol ('×' in Unicode / '\times' in LaTeX).
    /// </summary>
    Cross,

    /// <summary>
    /// Centered dot symbol ('·' in Unicode / '\cdot' in LaTeX).
    /// </summary>
    Dot,

    /// <summary>
    /// Standard ASCII asterisk ('*').
    /// </summary>
    Asterisk,
}

/// <summary>
/// Specifies the division symbol used when rendering expressions.
/// </summary>
public enum DivisionSymbol
{
    /// <summary>
    /// Slash ('/').
    /// </summary>
    Slash,

    /// <summary>
    /// Obelus ('÷' in Unicode / '\div' in LaTeX).
    /// </summary>
    Obelus,

    /// <summary>
    /// 2D fraction markup ('\frac{numerator}{denominator}' in MathJax/LaTeX).
    /// In linear styles, falls back to Slash or Obelus.
    /// </summary>
    Fraction,
}

/// <summary>
/// Configures formatting options when transforming an abstract syntax tree or expression string.
/// </summary>
public sealed record ExpressionFormatOptions
{
    /// <summary>
    /// Gets the target rendering style. Defaults to <see cref="ExpressionFormatStyle.TypographicUnicode"/>.
    /// </summary>
    public ExpressionFormatStyle Style { get; init; } = ExpressionFormatStyle.TypographicUnicode;

    /// <summary>
    /// Gets the multiplication symbol to render. Defaults to <see cref="MultiplicationSymbol.Cross"/>.
    /// </summary>
    public MultiplicationSymbol Multiplication { get; init; } = MultiplicationSymbol.Cross;

    /// <summary>
    /// Gets the division symbol to render. Defaults to <see cref="DivisionSymbol.Obelus"/>.
    /// </summary>
    public DivisionSymbol Division { get; init; } = DivisionSymbol.Obelus;

    /// <summary>
    /// Whether to render mathematical minus '−' (\u2212) instead of hyphen-minus '-' in typographic modes.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool UseUnicodeMinus { get; init; } = true;

    /// <summary>
    /// Whether to render Greek letters (e.g. 'π', 'τ') for named constants.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool UseGreekSymbols { get; init; } = true;

    /// <summary>
    /// Whether to render integer powers as Unicode superscripts (e.g. 'x²', '2¹⁰') in typographic mode.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool UseSuperscriptPowers { get; init; } = true;

    /// <summary>
    /// Pre-configured defaults for Typographic Unicode rendering.
    /// </summary>
    public static ExpressionFormatOptions Typographic { get; } = new();

    /// <summary>
    /// Pre-configured defaults for MathJax / LaTeX rendering.
    /// </summary>
    public static ExpressionFormatOptions LaTeX { get; } = new()
    {
        Style = ExpressionFormatStyle.MathJax,
        Multiplication = MultiplicationSymbol.Dot,
        Division = DivisionSymbol.Fraction,
    };

    /// <summary>
    /// Pre-configured defaults for normalized ASCII invariant rendering.
    /// </summary>
    public static ExpressionFormatOptions Ascii { get; } = new()
    {
        Style = ExpressionFormatStyle.NormalizedAscii,
        Multiplication = MultiplicationSymbol.Asterisk,
        Division = DivisionSymbol.Slash,
        UseUnicodeMinus = false,
        UseGreekSymbols = false,
        UseSuperscriptPowers = false,
    };
}
