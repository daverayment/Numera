using System.Globalization;

namespace Numera;

/// <summary>
/// Formats calculation results and math values into presentation-ready structured
/// outputs, numbers, and strings according to configurable formatting options and culture.
/// </summary>
internal static class ResultFormatter
{
    /// <summary>
    /// Formats an evaluation result into a presentation-ready <see cref="FormattedCalculation"/>
    /// using the specified options and culture.
    /// </summary>
    /// <param name="result">The evaluation result to format.</param>
    /// <param name="options">The formatting options to use, or null to retain the
    /// result's options.</param>
    /// <param name="culture">The culture to use, or null to retain the result's culture.
    /// </param>
    /// <param name="prefixRadixOutput">Whether to include radix prefixes ("0x", "0b",
    /// "0o").</param>
    /// <returns>A <see cref="FormattedCalculation"/> holding the formatted
    /// representations.</returns>
    public static FormattedCalculation Format(
        EvaluationResult result,
        ResultFormatOptions? options = null,
        CultureInfo? culture = null,
        bool prefixRadixOutput = true)
    {
        return FormatCore(
            result.Value,
            options ?? result.FormatOptions ?? ResultFormatOptions.Default,
            culture ?? result.Culture ?? CultureInfo.CurrentCulture,
            result.HasAssignments ? result.AssignedVariables : null,
            prefixRadixOutput);
    }

    /// <summary>
    /// Formats a calculation value into a presentation-ready <see cref="FormattedCalculation"/>
    /// using the specified options and culture.
    /// </summary>
    /// <param name="value">The math value to format.</param>
    /// <param name="options">The formatting options to use, or <see langword="null"/> for
    /// defaults.</param>
    /// <param name="culture">The culture to use, or null for current culture.</param>
    /// <param name="prefixRadixOutput">Whether to include radix prefixes ("0x", "0b",
    /// "0o").</param>
    /// <returns>A <see cref="FormattedCalculation"/> holding the formatted
    /// representations.</returns>
    public static FormattedCalculation Format(
        MathValue value,
        ResultFormatOptions? options = null,
        CultureInfo? culture = null,
        bool prefixRadixOutput = true)
    {
        return FormatCore(
            value,
            options ?? ResultFormatOptions.Default,
            culture ?? CultureInfo.CurrentCulture,
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

        return new FormattedCalculation(
            value: value,
            primaryDisplay: primaryDisplay,
            machineReadable: value.ToInvariantExpression(),
            hexRepresentation: value.ToHexString(prefixRadixOutput),
            binaryRepresentation: value.ToBinaryString(prefixRadixOutput),
            octalRepresentation: value.ToOctalString(prefixRadixOutput),
            assignedVariables: assignedVariables,
            fractionRepresentation: ResultValueFormatter.FormatExactFraction(value),
            isDisplayRounded: isRounded);
    }
}
