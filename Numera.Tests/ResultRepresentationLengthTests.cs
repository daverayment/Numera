using System.Globalization;
using System.Numerics;

namespace Numera.Tests;

[TestClass]
public sealed class ResultRepresentationLengthTests
{
    private static readonly CalculatorEngine InvariantEngine = CalculatorEngine.ForCulture(CultureInfo.InvariantCulture);

    [TestMethod]
    public void DefaultAutomaticDisplayCanReachFiftyCharactersWithTwentyFourSignificantDigits()
    {
        Assert.AreEqual(24, ResultFormatOptions.Default.SignificantDigits);
        Assert.AreEqual(24, ResultFormatOptions.Default.MaxFixedDigits);
        var result = InvariantEngine.Evaluate("-1.23456789012345678901234e-24");
        string expected = "-0." + new string('0', 23) + "123456789012345678901234";

        Assert.AreEqual(expected, result.PrimaryDisplay);
        Assert.AreEqual(50, result.PrimaryDisplay.Length);
        Assert.AreEqual(expected, result.FormatResult().PrimaryDisplay);
        Assert.IsFalse(result.FormatResult().IsDisplayRounded);
    }

    [TestMethod]
    public void DefaultFixedDisplayCanReachThePositiveExponentGroupingBound()
    {
        var result = InvariantEngine.Evaluate("-12345678901234567890123.4");

        Assert.AreEqual("-12,345,678,901,234,567,890,123.4", result.PrimaryDisplay);
        Assert.AreEqual(33, result.PrimaryDisplay.Length);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(22)]
    [DataRow(24)]
    [DataRow(1000)]
    public void CompactScientificOverheadIsBoundedAcrossSupportedPrecisions(int precision)
    {
        var maximumComponent = (BigInteger.One << CalculationLimits.MaxIntegerBits) - 1;
        var options = new ResultFormatOptions
        {
            Notation = NumericNotation.Scientific,
            SignificantDigits = precision
        };
        var formatted = InvariantEngine.FormatResult(MathValue.FromFraction(-1, maximumComponent), options);

        Assert.IsLessThanOrEqualTo(precision + 8, formatted.PrimaryDisplay.Length);
        Assert.EndsWith("e-4933", formatted.PrimaryDisplay);
    }

    [TestMethod]
    [DataRow(ExpressionFormatStyle.NormalizedAscii, ExponentMarker.Lowercase, "e-4933", 32)]
    [DataRow(ExpressionFormatStyle.NormalizedAscii, ExponentMarker.Uppercase, "E-4933", 32)]
    [DataRow(ExpressionFormatStyle.TypographicUnicode, ExponentMarker.Lowercase, " × 10^-4933", 37)]
    [DataRow(ExpressionFormatStyle.MathJax, ExponentMarker.Lowercase, @" \times 10^{-4933}", 44)]
    public void ScientificDisplaysReachTheDocumentedStyleBounds(
        ExpressionFormatStyle style, ExponentMarker marker, string suffix, int length)
    {
        var maximumComponent = (BigInteger.One << CalculationLimits.MaxIntegerBits) - 1;
        var value = MathValue.FromFraction(-BigInteger.One, maximumComponent);
        var options = new ResultFormatOptions
        {
            Notation = NumericNotation.Scientific,
            Style = style,
            ExponentMarker = marker
        };
        var formatted = InvariantEngine.FormatResult(value, options);

        Assert.EndsWith(suffix, formatted.PrimaryDisplay);
        Assert.AreEqual(length, formatted.PrimaryDisplay.Length);
        Assert.IsTrue(formatted.IsDisplayRounded);
    }

    [TestMethod]
    public void MaximumMagnitudeIntegerRetainsAllRadixDigitsBeyondTheFormerCeiling()
    {
        var maximumMagnitude = (BigInteger.One << CalculationLimits.MaxIntegerBits) - 1;
        var value = new MathValue(-maximumMagnitude);
        var formatted = InvariantEngine.FormatResult(value);

        Assert.AreEqual("-0b" + new string('1', 16384), formatted.BinaryRepresentation);
        Assert.AreEqual("-0x" + new string('F', 4096), formatted.HexRepresentation);
        Assert.AreEqual("-0o1" + new string('7', 5461), formatted.OctalRepresentation);
        Assert.AreEqual(16387, formatted.BinaryRepresentation!.Length);
        Assert.AreEqual(4099, formatted.HexRepresentation!.Length);
        Assert.AreEqual(5465, formatted.OctalRepresentation!.Length);
        Assert.AreEqual(4934, formatted.MachineReadable.Length);
        Assert.AreEqual(formatted.MachineReadable, formatted.FractionRepresentation);

        var unprefixed = InvariantEngine.FormatResult(value, prefixRadixOutput: false);
        Assert.AreEqual("-" + new string('1', 16384), unprefixed.BinaryRepresentation);
        Assert.AreEqual("-" + new string('F', 4096), unprefixed.HexRepresentation);
        Assert.AreEqual("-1" + new string('7', 5461), unprefixed.OctalRepresentation);
    }

    [TestMethod]
    public void SuccessfulTryEvaluationKeepsTheFullLongBinaryAlternative()
    {
        Assert.IsTrue(InvariantEngine.TryEvaluate("1 << 16383", out var result, out var diagnostic), diagnostic?.Message);
        var formatted = result.FormatResult();

        Assert.AreEqual(result.PrimaryDisplay, formatted.PrimaryDisplay);
        Assert.AreEqual("0b1" + new string('0', 16383), formatted.BinaryRepresentation);
        Assert.AreEqual(16386, formatted.BinaryRepresentation!.Length);
        Assert.AreEqual(result.MachineReadable, formatted.MachineReadable);
        Assert.AreEqual(formatted.MachineReadable, formatted.FractionRepresentation);
        Assert.IsNotNull(formatted.HexRepresentation);
        Assert.IsNotNull(formatted.OctalRepresentation);
        Assert.IsNull(formatted.AssignmentSummary);
    }

    [TestMethod]
    public void MaximumCoprimeComponentsReachTheExactFractionAndMachineTextBound()
    {
        var maximumComponent = (BigInteger.One << CalculationLimits.MaxIntegerBits) - 1;
        var value = MathValue.FromFraction(-maximumComponent, maximumComponent - 1);
        var formatted = InvariantEngine.FormatResult(value);
        string expected = "-" + maximumComponent.ToString(CultureInfo.InvariantCulture)
            + "/" + (maximumComponent - 1).ToString(CultureInfo.InvariantCulture);

        Assert.AreEqual(expected, formatted.FractionRepresentation);
        Assert.AreEqual(expected, formatted.MachineReadable);
        Assert.AreEqual(9868, formatted.MachineReadable.Length);
        Assert.AreEqual("-1", formatted.PrimaryDisplay);
        Assert.IsTrue(formatted.IsDisplayRounded);
        Assert.IsNull(formatted.BinaryRepresentation);
    }

    [TestMethod]
    [DataRow(24)]
    [DataRow(1000)]
    public void ForcedFixedDisplaysFollowMagnitudeAndGroupingRatherThanACharacterBudget(int precision)
    {
        var maximumComponent = (BigInteger.One << CalculationLimits.MaxIntegerBits) - 1;
        var options = new ResultFormatOptions
        {
            Notation = NumericNotation.Fixed,
            SignificantDigits = precision
        };
        var integer = InvariantEngine.FormatResult(new MathValue(-maximumComponent), options);
        Assert.AreEqual(6578, integer.PrimaryDisplay.Length);
        Assert.AreEqual(1644, integer.PrimaryDisplay.Count(character => character == ','));

        var fraction = InvariantEngine.FormatResult(MathValue.FromFraction(-1, maximumComponent), options);
        Assert.StartsWith("-0." + new string('0', 4932), fraction.PrimaryDisplay);
        Assert.IsLessThanOrEqualTo(precision + 4935, fraction.PrimaryDisplay.Length);
        Assert.IsTrue(fraction.IsDisplayRounded);
    }

    [TestMethod]
    public void CustomSignsAndSeparatorsAddTheirFullLengths()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NegativeSign = "minus";
        culture.NumberFormat.NumberDecimalSeparator = "::";
        var result = InvariantEngine.Evaluate("-1.23456789012345678901234e-24");
        Assert.AreEqual(55, result.FormatResult(culture: culture).PrimaryDisplay.Length);

        var maximumComponent = (BigInteger.One << CalculationLimits.MaxIntegerBits) - 1;
        var scientific = InvariantEngine.FormatResult(MathValue.FromFraction(-1, maximumComponent), culture: culture);
        Assert.AreEqual(37, scientific.PrimaryDisplay.Length);
        Assert.EndsWith("e-4933", scientific.PrimaryDisplay);
    }

    [TestMethod]
    public void NativeRationalFormatsAndStructuredDisplaysDoNotRejectLongCultureText()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NegativeSign = new string('-', 20000);
        var value = MathValue.FromFraction(-24691357, 2);

        Assert.AreEqual(culture.NumberFormat.NegativeSign + "12,345,678.5", value.ToString("N1", culture));
        Assert.AreEqual(culture.NumberFormat.NegativeSign + "12345678.5", value.ToString("F1", culture));
        Assert.AreEqual(value.ToString("N1", culture), InvariantEngine.FormatResult(value, culture: culture).PrimaryDisplay);
    }

    [TestMethod]
    public void NonfiniteCultureSymbolsAreNotLimitedBySignificantDigits()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NaNSymbol = new string('?', 20000);
        var formatted = InvariantEngine.FormatResult(new MathValue(double.NaN), culture: culture);

        Assert.AreEqual(culture.NumberFormat.NaNSymbol, formatted.PrimaryDisplay);
        Assert.AreEqual("NaN", formatted.MachineReadable);
        Assert.IsFalse(formatted.IsDisplayRounded);
        Assert.IsNull(formatted.FractionRepresentation);
    }

    [TestMethod]
    public void AssignmentSummariesArePreparedWithTheSelectedCultureAndRemainStable()
    {
        var engine = CalculatorEngine.ForCulture(CultureInfo.GetCultureInfo("de-DE"));
        var result = engine.Evaluate("x=1234,50");
        var formatted = result.FormatResult();
        string? summary = formatted.AssignmentSummary;
        Assert.IsNotNull(summary);
        Assert.StartsWith("x = 1234,5", summary);
        Assert.StartsWith("x = 1234.5", result.FormatResult(culture: CultureInfo.InvariantCulture).AssignmentSummary!);

        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.AreEqual(summary, formatted.AssignmentSummary);
            Assert.AreEqual("1.234,50", formatted.PrimaryDisplay);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}