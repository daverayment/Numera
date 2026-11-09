using System.Globalization;

namespace Numera;

/// <summary>
/// Formats calculation results and math values into presentation-ready structured
/// outputs, numbers, and strings according to configurable formatting options and culture.
/// </summary>
public static class ResultFormatter
{
    /// <summary>
    /// Formats an evaluation result into a presentation-ready <see cref="FormattedCalculation"/>
    /// using the specified options and culture.
    /// </summary>
    /// <param name="result">The evaluation result to format.</param>
    /// <param name="options">The formatting options to use, or <see langword="null"/> for defaults.</param>
    /// <param name="culture">The culture to use, or <see langword="null"/> for invariant culture.</param>
    /// <param name="prefixRadixOutput">Whether to include radix prefixes ("0x", "0b", "0o").</param>
    /// <returns>A <see cref="FormattedCalculation"/> holding the formatted representations.</returns>
    public static FormattedCalculation Format(
        EvaluationResult result,
        ResultFormatOptions? options = null,
        CultureInfo? culture = null,
        bool prefixRadixOutput = true)
    {
        return FormatCore(
            result.Value,
            options ?? result.FormatOptions ?? ResultFormatOptions.Default,
            culture ?? CultureInfo.InvariantCulture,
            result.HasAssignments ? result.AssignedVariables : null,
            prefixRadixOutput);
    }

    /// <summary>
    /// Formats a calculation value into a presentation-ready <see cref="FormattedCalculation"/>
    /// using the specified options and culture.
    /// </summary>
    /// <param name="value">The math value to format.</param>
    /// <param name="options">The formatting options to use, or <see langword="null"/> for defaults.</param>
    /// <param name="culture">The culture to use, or <see langword="null"/> for invariant culture.</param>
    /// <param name="prefixRadixOutput">Whether to include radix prefixes ("0x", "0b", "0o").</param>
    /// <returns>A <see cref="FormattedCalculation"/> holding the formatted representations.</returns>
    public static FormattedCalculation Format(
        MathValue value,
        ResultFormatOptions? options = null,
        CultureInfo? culture = null,
        bool prefixRadixOutput = true)
    {
        return FormatCore(
            value,
            options ?? ResultFormatOptions.Default,
            culture ?? CultureInfo.InvariantCulture,
            null,
            prefixRadixOutput);
    }

    private static FormattedCalculation FormatCore(
        MathValue value,
        ResultFormatOptions options,
        CultureInfo culture,
        VariableCollection? assignedVariables,
        bool prefixRadixOutput)
    {
        options.Validate();

        var (primaryDisplay, isRounded) = ResultValueFormatter.Format(value, options, culture);
        string machineReadable = value.ToInvariantExpression();
        string? fraction = ResultValueFormatter.FormatExactFraction(value, options);

        var formatted = new FormattedCalculation(
            Value: value,
            PrimaryDisplay: primaryDisplay,
            MachineReadable: machineReadable,
            HexRepresentation: value.ToHexString(prefixRadixOutput),
            BinaryRepresentation: value.ToBinaryString(prefixRadixOutput),
            OctalRepresentation: value.ToOctalString(prefixRadixOutput),
            AssignedVariables: assignedVariables,
            FractionRepresentation: fraction,
            IsDisplayRounded: isRounded);

        // Guard against any representation exceeding the configured maximum character limit.
        foreach (var representation in new[]
        {
            formatted.PrimaryDisplay,
            formatted.MachineReadable,
            formatted.FractionRepresentation,
            formatted.HexRepresentation,
            formatted.BinaryRepresentation,
            formatted.OctalRepresentation,
        })
        {
            if (representation?.Length > options.MaxOutputCharacters)
            {
                throw new CalculationLimitException();
            }
        }

        return formatted;
    }
}
