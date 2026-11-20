using System.Globalization;

namespace Numera;

/// <summary>
/// Formats calculation results and math values into presentation-ready structured
/// outputs, numbers, and strings according to configurable formatting options and culture.
/// </summary>
internal static class ResultFormatter
{
    /// <summary>
    /// Formats an evaluation result into a presentation-ready <see cref="FormattedResult"/>
    /// using the specified options and culture.
    /// </summary>
    /// <param name="result">The evaluation result to format.</param>
    /// <param name="options">The formatting options to use, or null to retain the
    /// result's options.</param>
    /// <param name="culture">The culture to use, or null to retain the result's culture.
    /// </param>
    /// <returns>A <see cref="FormattedResult"/> holding the formatted
    /// representations.</returns>
    public static FormattedResult Format(
        EvaluationResult result,
        ResultFormatOptions? options = null,
        CultureInfo? culture = null)
    {
        return FormatCore(
            result.Value,
            options ?? result.FormatOptions ?? ResultFormatOptions.Default,
            culture ?? result.Culture ?? CultureInfo.CurrentCulture,
            result.HasAssignments ? result.AssignedVariables : null);
    }

    /// <summary>
    /// Formats a calculation value into a presentation-ready <see cref="FormattedResult"/>
    /// using the specified options and culture.
    /// </summary>
    /// <param name="value">The math value to format.</param>
    /// <param name="options">The formatting options to use, or <see langword="null"/> for
    /// defaults.</param>
    /// <param name="culture">The culture to use, or null for current culture.</param>
    /// <returns>A <see cref="FormattedResult"/> holding the formatted
    /// representations.</returns>
    public static FormattedResult Format(
        MathValue value,
        ResultFormatOptions? options = null,
        CultureInfo? culture = null)
    {
        return FormatCore(
            value,
            options ?? ResultFormatOptions.Default,
            culture ?? CultureInfo.CurrentCulture,
            null);
    }

    private static FormattedResult FormatCore(
        MathValue value,
        ResultFormatOptions options,
        CultureInfo culture,
        VariableCollection? assignedVariables)
    {
        options.Validate();
        var (primaryDisplay, isRounded) = ResultValueFormatter.Format(value, options, culture);

        return new FormattedResult(
            value: value,
            primaryDisplay: primaryDisplay,
            machineReadable: value.ToInvariantExpression(),
            hexDisplay: value.ToHexString(options.IncludeRadixPrefixes),
            binaryDisplay: value.ToBinaryString(options.IncludeRadixPrefixes),
            octalDisplay: value.ToOctalString(options.IncludeRadixPrefixes),
            assignedVariables: assignedVariables,
            exactFractionDisplay: ResultValueFormatter.FormatExactFraction(value),
            isDisplayRounded: isRounded);
    }
}
