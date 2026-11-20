namespace Numera;

/// <summary>
/// Specifies the letter used for the exponent in compact scientific result displays.
/// </summary>
public enum ExponentMarker
{
    /// <summary>
    /// Lowercase e, as in "1e-400".
    /// </summary>
    Lowercase,

    /// <summary>
    /// Uppercase E, as in "1E-400".
    /// </summary>
    Uppercase
}

/// <summary>
/// Controls the numeric presentation of calculation results, including notation,
/// precision and formatting style. These settings do not impose a character limit.
/// </summary>
public sealed record ResultFormatOptions
{
    /// <summary>
    /// Gets default result formatting options.
    /// </summary>
    public static ResultFormatOptions Default { get; } = new();

    /// <summary>
    /// Gets the overall significant-digit ceiling, defaulting to 24. Double displays
    /// also respect <see cref="DoubleSignificantDigits"/>; the smaller ceiling applies.
    /// Must be between 1 and <see cref="CalculationLimits.MaxSignificantDigits"/> (1,000).
    /// </summary>
    public int SignificantDigits { get; init; } = 24;

    /// <summary>
    /// Gets the additional significant-digit ceiling for Double displays, defaulting to
    /// 15. Must be between 1 and 17. Does not affect other representations, stored values,
    /// machine-readable expressions, serialization, or explicit numeric format strings.
    /// </summary>
    /// <remarks>
    /// For example, with <see cref="SignificantDigits"/> set to 20, Doubles still use at
    /// most 15 digits by default. Set this property to 17 to inspect more of the stored
    /// approximation. Seventeen decimal digits suffice to distinguish finite Double
    /// values when parsed as Double; neither ceiling guarantees mathematical accuracy.
    /// </remarks>
    public int DoubleSignificantDigits { get; init; } = 15;

    internal int ResolveSignificantDigits(MathValue.DataType kind)
    {
        if (kind == MathValue.DataType.Double)
        {
            return Math.Min(SignificantDigits, DoubleSignificantDigits);
        }
        return SignificantDigits;
    }

    /// <summary>
    /// Gets the magnitude threshold for automatic notation selection, defaulting to 24.
    /// Fixed notation is selected when the rounded base-10 exponent is at least
    /// -MaxFixedDigits and less than MaxFixedDigits. This does not limit the total
    /// number of fractional digits or apply when notation is explicitly selected.
    /// Must be between 1 and <see cref="CalculationLimits.MaxSignificantDigits"/> (1,000).
    /// </summary>
    public int MaxFixedDigits { get; init; } = 24;

    /// <summary>
    /// Gets the notation, defaulting to automatic selection after rounding.
    /// </summary>
    public NumericNotation Notation { get; init; } = NumericNotation.Auto;

    /// <summary>
    /// Gets the rendering style used only for scientific notation, defaulting to compact
    /// e-notation.
    /// </summary>
    public ExpressionFormatStyle ScientificStyle { get; init; } = ExpressionFormatStyle.NormalizedAscii;

    /// <summary>
    /// Gets the exponent marker for compact scientific displays, defaulting to lowercase
    /// e. Applies only to <see cref="ExpressionFormatStyle.NormalizedAscii"/> result
    /// displays; expression formatting and machine-readable representations are
    /// unaffected.
    /// </summary>
    public ExponentMarker ExponentMarker { get; init; } = ExponentMarker.Lowercase;

    /// <summary>
    /// Gets whether to group digits in the integer part of a number, defaulting to true.
    /// Group sizes and separators follow the supplied culture, including variable group
    /// sizes.
    /// </summary>
    public bool GroupDigits { get; init; } = true;

    /// <summary>
    /// Gets whether hexadecimal, binary, and octal display alternatives include
    /// their radix prefixes (0x, 0b, and 0o). Defaults to true.
    /// Does not affect the primary display, exact fractions, or machine-readable text.
    /// </summary>
    public bool IncludeRadixPrefixes { get; init; } = true;

    /// <summary>
    /// Validates the format options against supported ranges and safety limits.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if any setting is outside its
    /// valid range.</exception>
    public void Validate()
    {
        if (SignificantDigits < 1 || SignificantDigits > CalculationLimits.MaxSignificantDigits)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SignificantDigits),
                $"Significant digits must be between 1 and {CalculationLimits.MaxSignificantDigits}.");
        }

        if (DoubleSignificantDigits < 1 || DoubleSignificantDigits > 17)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DoubleSignificantDigits),
                "Double significant digits must be between 1 and 17.");
        }

        if (MaxFixedDigits < 1 || MaxFixedDigits > CalculationLimits.MaxSignificantDigits)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxFixedDigits),
                $"Maximum fixed digits must be between 1 and {CalculationLimits.MaxSignificantDigits}.");
        }

        if (!Enum.IsDefined(Notation))
        {
            throw new ArgumentOutOfRangeException(nameof(Notation));
        }

        if (!Enum.IsDefined(ScientificStyle))
        {
            throw new ArgumentOutOfRangeException(nameof(ScientificStyle));
        }

        if (!Enum.IsDefined(ExponentMarker))
        {
            throw new ArgumentOutOfRangeException(nameof(ExponentMarker));
        }
    }
}
