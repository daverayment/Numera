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
/// precision, formatting style, and output safety limits.
/// </summary>
public sealed record ResultFormatOptions
{
    /// <summary>
    /// Gets default result formatting options.
    /// </summary>
    public static ResultFormatOptions Default { get; } = new();

    /// <summary>
    /// Gets the number of significant digits, defaulting to 24.
    /// Must be between 1 and <see cref="CalculationLimits.MaxSignificantDigits"/> (1,000).
    /// </summary>
    public int SignificantDigits { get; init; } = 24;

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
    /// Gets the scientific rendering style, defaulting to compact e-notation.
    /// </summary>
    public ExpressionFormatStyle Style { get; init; } = ExpressionFormatStyle.NormalizedAscii;

    /// <summary>
    /// Gets the exponent marker for compact scientific displays, defaulting to lowercase e.
    /// Applies only to <see cref="ExpressionFormatStyle.NormalizedAscii"/> result displays;
    /// expression formatting and machine-readable representations are unaffected.
    /// </summary>
    public ExponentMarker ExponentMarker { get; init; } = ExponentMarker.Lowercase;

    /// <summary>
    /// Gets whether to group digits in the integer part of a number, defaulting to true.
    /// Group sizes and separators follow the supplied culture, including variable group sizes.
    /// </summary>
    public bool GroupDigits { get; init; } = true;

    /// <summary>
    /// Gets the maximum characters permitted in a single formatted numeric representation.
    /// Defaults to <see cref="CalculationLimits.MaxOutputCharacters"/> (16,384).
    /// Includes signs, decimal and group separators, scientific suffixes, and preserved trailing zeros.
    /// </summary>
    public int MaxOutputCharacters { get; init; } = CalculationLimits.MaxOutputCharacters;

    /// <summary>
    /// Validates the format options against supported ranges and safety limits.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if any setting is outside its valid range.</exception>
    public void Validate()
    {
        if (SignificantDigits < 1 || SignificantDigits > CalculationLimits.MaxSignificantDigits)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SignificantDigits),
                $"Significant digits must be between 1 and {CalculationLimits.MaxSignificantDigits}.");
        }

        if (MaxFixedDigits < 1 || MaxFixedDigits > CalculationLimits.MaxSignificantDigits)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxFixedDigits),
                $"Maximum fixed digits must be between 1 and {CalculationLimits.MaxSignificantDigits}.");
        }

        if (MaxOutputCharacters < 1 || MaxOutputCharacters > CalculationLimits.MaxOutputCharacters)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxOutputCharacters),
                $"Maximum output characters must be between 1 and {CalculationLimits.MaxOutputCharacters}.");
        }

        if (!Enum.IsDefined(Notation))
        {
            throw new ArgumentOutOfRangeException(nameof(Notation));
        }

        if (!Enum.IsDefined(Style))
        {
            throw new ArgumentOutOfRangeException(nameof(Style));
        }

        if (!Enum.IsDefined(ExponentMarker))
        {
            throw new ArgumentOutOfRangeException(nameof(ExponentMarker));
        }
    }
}
