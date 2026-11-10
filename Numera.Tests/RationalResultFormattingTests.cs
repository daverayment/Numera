using System.Globalization;
using System.Numerics;

namespace Numera.Tests;

[TestClass]
public sealed class RationalResultFormattingTests
{
    private static (string Text, bool IsRounded) Format(
        MathValue value,
        int digits = 24,
        NumericNotation notation = NumericNotation.Auto,
        IFormatProvider? provider = null,
        ExpressionFormatStyle style = ExpressionFormatStyle.TypographicUnicode,
        int maxOutputCharacters = CalculationLimits.MaxOutputCharacters,
        int maxFixedDigits = 24,
        bool groupDigits = false)
        => ResultValueFormatter.Format(
            value,
            new ResultFormatOptions
            {
                SignificantDigits = digits,
                Notation = notation,
                Style = style,
                MaxOutputCharacters = maxOutputCharacters,
                MaxFixedDigits = maxFixedDigits,
                GroupDigits = groupDigits,
            },
            provider ?? CultureInfo.InvariantCulture);

    [TestMethod]
    public void RecurringFractionsRoundDirectlyAndRetainAnExactAlternate()
    {
        var value = MathValue.FromFraction(new BigInteger(1), new BigInteger(3));
        Assert.AreEqual(("0.333333333333333333333333", true), Format(value));
        Assert.AreEqual("1/3", ResultValueFormatter.FormatExactFraction(value, new()));
        Assert.AreEqual(("0.000000333333333333333333333333", true),
            Format(MathValue.FromFraction(BigInteger.One, new BigInteger(3000000))));
        Assert.AreEqual(("3.33333333333333333333333 × 10^-7", true),
            Format(MathValue.FromFraction(BigInteger.One, new BigInteger(3000000)), maxFixedDigits: 6));
    }

    [TestMethod]
    public void SignificantDigitTiesRoundToEvenWithCorrectSigns()
    {
        Assert.AreEqual(("1.2", true), Format(MathValue.FromFraction(new BigInteger(125), new BigInteger(100)), 2));
        Assert.AreEqual(("1.4", true), Format(MathValue.FromFraction(new BigInteger(135), new BigInteger(100)), 2));
        Assert.AreEqual(("-1.2", true), Format(MathValue.FromFraction(new BigInteger(-125), new BigInteger(100)), 2));
        Assert.AreEqual(("-1.4", true), Format(MathValue.FromFraction(new BigInteger(-135), new BigInteger(100)), 2));
    }

    [TestMethod]
    public void TerminatingFractionsBeyondDecimalCapacityStayExact()
    {
        var denominator = BigInteger.Pow(10, 40);
        var value = MathValue.FromFraction(BigInteger.One, denominator);
        Assert.AreEqual(("1 × 10^-40", false), Format(value));
        Assert.AreEqual(("0." + new string('0', 39) + "1", false),
            Format(value, notation: NumericNotation.Fixed));
        Assert.AreEqual("1/" + denominator.ToString(CultureInfo.InvariantCulture),
            ResultValueFormatter.FormatExactFraction(value, new()));
    }

    [TestMethod]
    public void ExtremeMagnitudesRemainFiniteWithoutDoubleProjection()
    {
        var power = BigInteger.Pow(10, 4000);
        Assert.AreEqual(("1 × 10^-4000", false), Format(MathValue.FromFraction(BigInteger.One, power)));
        Assert.AreEqual(("1 × 10^4000", false), Format(new MathValue(power)));
        Assert.ThrowsExactly<CalculationLimitException>(() =>
            Format(MathValue.FromFraction(BigInteger.One, power), notation: NumericNotation.Fixed,
                maxOutputCharacters: 32));
    }

    [TestMethod]
    public void AutomaticNotationUsesTheRoundedExponentAtBothBoundaries()
    {
        Assert.AreEqual(("0." + new string('0', 23) + "1", false),
            Format(MathValue.FromFraction(BigInteger.One, BigInteger.Pow(10, 24))));
        Assert.AreEqual(("1 × 10^-25", false),
            Format(MathValue.FromFraction(BigInteger.One, BigInteger.Pow(10, 25))));
        var carry = BigInteger.Parse(new string('9', 24) + "5", CultureInfo.InvariantCulture);
        Assert.AreEqual(("0." + new string('0', 23) + "1", true),
            Format(MathValue.FromFraction(carry, BigInteger.Pow(10, 49))));
        Assert.AreEqual(("1 × 10^24", true),
            Format(MathValue.FromFraction(carry, new BigInteger(10))));
        Assert.AreEqual(("1" + new string('0', 23), false),
            Format(new MathValue(BigInteger.Pow(10, 23))));
        Assert.AreEqual(("1 × 10^24", false),
            Format(new MathValue(BigInteger.Pow(10, 24))));
    }

    [TestMethod]
    public void ExplicitNotationCultureAndTrailingZeroesAreRespected()
    {
        Assert.AreEqual(("1,2500", false), Format(new MathValue(1.2500m), provider: CultureInfo.GetCultureInfo("de-DE")));
        Assert.AreEqual(("1,25 × 10^0", false),
            Format(new MathValue(1.2500m), notation: NumericNotation.Scientific,
                provider: CultureInfo.GetCultureInfo("de-DE")));
        Assert.AreEqual(("1.25e0", false),
            Format(new MathValue(1.2500m), notation: NumericNotation.Scientific,
                style: ExpressionFormatStyle.NormalizedAscii));
        Assert.AreEqual((@"1.25 \times 10^{0}", false),
            Format(new MathValue(1.2500m), notation: NumericNotation.Scientific,
                style: ExpressionFormatStyle.MathJax));
        Assert.AreEqual(("0", false), Format(new MathValue(0), notation: NumericNotation.Scientific));
    }

    [TestMethod]
    [DataRow(ExpressionFormatStyle.TypographicUnicode, "1.25 × 10^0")]
    [DataRow(ExpressionFormatStyle.MathJax, @"1.25 \times 10^{0}")]
    public void NonCompactStylesIgnoreExponentMarkerCasing(ExpressionFormatStyle style, string expected)
    {
        foreach (var marker in Enum.GetValues<ExponentMarker>())
        {
            var options = new ResultFormatOptions
            {
                Style = style,
                Notation = NumericNotation.Scientific,
                ExponentMarker = marker,
            };
            Assert.AreEqual((expected, false),
                ResultValueFormatter.Format(new MathValue(1.25m), options, CultureInfo.InvariantCulture));
        }
    }

    [TestMethod]
    public void UndefinedExponentMarkersAreRejectedBeforeFormattingAnyValue()
    {
        var options = new ResultFormatOptions { ExponentMarker = (ExponentMarker)99 };
        foreach (var value in new[] { new MathValue(0), new MathValue(1), new MathValue(double.NaN) })
        {
            var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                ResultValueFormatter.Format(value, options, CultureInfo.InvariantCulture));
            Assert.AreEqual(nameof(ResultFormatOptions.ExponentMarker), exception.ParamName);
        }
    }

    [TestMethod]
    public void CustomAutomaticThresholdsApplyAfterRounding()
    {
        Assert.AreEqual(("0.01", false),
            Format(new MathValue(0.01m), maxFixedDigits: 2));
        Assert.AreEqual(("1 × 10^-3", false),
            Format(new MathValue(0.001m), maxFixedDigits: 2));
        Assert.AreEqual(("99", false),
            Format(new MathValue(99), maxFixedDigits: 2));
        Assert.AreEqual(("1 × 10^2", false),
            Format(new MathValue(100), maxFixedDigits: 2));
        Assert.AreEqual(("1 × 10^3", false),
            Format(new MathValue(1000), maxFixedDigits: 2));
        Assert.AreEqual(("0.01", true),
            Format(new MathValue(0.009999m), digits: 3, maxFixedDigits: 2));
        Assert.AreEqual(("1 × 10^3", true),
            Format(new MathValue(999.9m), digits: 3, maxFixedDigits: 2));
        Assert.AreEqual(("1000", false),
            Format(new MathValue(1000), notation: NumericNotation.Fixed, maxFixedDigits: 2));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Format(new MathValue(1), maxFixedDigits: 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Format(new MathValue(1), maxFixedDigits: 1001));
    }

    [TestMethod]
    public void DoubleRoundingDescribesTheStoredBinaryValueNotItsAccuracy()
    {
        Assert.AreEqual(("0.100000000000000005551115", true), Format(new MathValue(0.1)));
        Assert.AreEqual(("0.1", true), Format(new MathValue(0.1), digits: 16));
        Assert.AreEqual(("0.5", false), Format(new MathValue(0.5)));
        Assert.AreEqual(("5 × 10^-324", true), Format(new MathValue(double.Epsilon), 1));
        Assert.IsNull(ResultValueFormatter.FormatExactFraction(new MathValue(0.5), new()));
        Assert.AreEqual(("NaN", false), Format(new MathValue(double.NaN)));
        Assert.AreEqual(("Infinity", false), Format(new MathValue(double.PositiveInfinity)));
        Assert.AreEqual(("-0", false), Format(new MathValue(-0.0)));
        Assert.IsNull(ResultValueFormatter.FormatExactFraction(new MathValue(-0.0), new()));
        Assert.ThrowsExactly<CalculationLimitException>(() =>
            Format(new MathValue(-0.0), maxOutputCharacters: 1));
    }

    [TestMethod]
    public void FractionAlternateIsReducedInvariantAndDoesNotAppendOne()
    {
        Assert.AreEqual("-1/3", ResultValueFormatter.FormatExactFraction(
            MathValue.FromFraction(new BigInteger(-2), new BigInteger(6)), new()));
        Assert.AreEqual("5", ResultValueFormatter.FormatExactFraction(new MathValue(5), new()));
        Assert.AreEqual("5/4", ResultValueFormatter.FormatExactFraction(new MathValue(1.25m), new()));
    }

    [TestMethod]
    public void FormatLimitsRejectRatherThanTruncate()
    {
        Assert.ThrowsExactly<CalculationLimitException>(() =>
            Format(MathValue.FromFraction(BigInteger.One, BigInteger.Pow(10, 40)), notation: NumericNotation.Fixed,
                maxOutputCharacters: 20));
        Assert.ThrowsExactly<CalculationLimitException>(() =>
            ResultValueFormatter.FormatExactFraction(
                MathValue.FromFraction(BigInteger.One, BigInteger.Pow(10, 40)),
                new ResultFormatOptions { MaxOutputCharacters = 20 }));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Format(new MathValue(1), digits: 1001));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            Format(new MathValue(1), digits: 1001));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            Format(new MathValue(1), maxOutputCharacters: 16385));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            Format(new MathValue(1), notation: (NumericNotation)99));
        Assert.AreEqual(("1", false),
            Format(new MathValue(1), maxOutputCharacters: 1));
    }

    [TestMethod]
    [DataRow("123", "123", new int[] { 3 }, ",")]
    [DataRow("123456", "123,456", new int[] { 3 }, ",")]
    [DataRow("1.234567", "1.234567", new int[] { 3 }, ",")]
    [DataRow("123", "1,23", new int[] { 2 }, ",")]
    [DataRow("1234", "1,234", new int[] { 3, 0 }, ",")]
    [DataRow("1234567", "1234,567", new int[] { 3, 0 }, ",")]
    [DataRow("1234567", "12,34,567", new int[] { 3, 2, 0 }, ",")]
    [DataRow("12345678", "1,23,45,678", new int[] { 3, 2 }, ",")]
    [DataRow("123456", "1,2,3,456", new int[] { 3, 1 }, ",")]
    [DataRow("-1234.50", "-1,234.50", new int[] { 3, 0 }, ",")]
    [DataRow("123456", "123456", new int[] { 0 }, ",")]
    [DataRow("123456", "123456", new int[] { 3 }, "")]
    public void CultureGroupingAndLengthChecksAgree(
        string number, string expected, int[] groupSizes, string groupSeparator)
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberGroupSizes = groupSizes;
        culture.NumberFormat.NumberGroupSeparator = groupSeparator;
        var value = new MathValue(decimal.Parse(number, CultureInfo.InvariantCulture));

        Assert.AreEqual((expected, false), Format(value, provider: culture, groupDigits: true,
            maxOutputCharacters: expected.Length));
        Assert.ThrowsExactly<CalculationLimitException>(() => Format(value, provider: culture,
            groupDigits: true, maxOutputCharacters: expected.Length - 1));
    }

    [TestMethod]
    [DataRow(new int[] { 3 }, "-12,345,678.5")]
    [DataRow(new int[] { 2 }, "-12,34,56,78.5")]
    [DataRow(new int[] { 3, 0 }, "-12345,678.5")]
    [DataRow(new int[] { 3, 2 }, "-1,23,45,678.5")]
    [DataRow(new int[] { 3, 2, 0 }, "-123,45,678.5")]
    [DataRow(new int[] { 3, 1 }, "-1,2,3,4,5,678.5")]
    [DataRow(new int[] { 0 }, "-12345678.5")]
    [DataRow(new int[] { }, "-12345678.5")]
    public void ExplicitRationalGroupingUsesTheSharedCultureRules(int[] groupSizes, string expected)
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberGroupSizes = groupSizes;
        var value = MathValue.FromFraction(-24691357, 2);

        Assert.AreEqual(MathValue.DataType.Rational, value.Kind);
        Assert.AreEqual(expected, value.ToString("N1", culture));
        Assert.AreEqual((expected, false), Format(value, provider: culture, groupDigits: true,
            maxOutputCharacters: expected.Length));
        Assert.ThrowsExactly<CalculationLimitException>(() => Format(value, provider: culture,
            groupDigits: true, maxOutputCharacters: expected.Length - 1));
    }

    [TestMethod]
    [DataRow("0.00", "0::00")]
    [DataRow("1.2300", "1::2300")]
    [DataRow("-1234567.50", "minus1__234__567::50")]
    [DataRow("-0.00125", "minus0::00125")]
    public void FixedLengthIncludesSignsSeparatorsAndPreservedZeros(string number, string expected)
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NegativeSign = "minus";
        culture.NumberFormat.NumberDecimalSeparator = "::";
        culture.NumberFormat.NumberGroupSeparator = "__";
        var value = new MathValue(decimal.Parse(number, CultureInfo.InvariantCulture));

        Assert.AreEqual((expected, false), Format(value, notation: NumericNotation.Fixed,
            provider: culture, groupDigits: true, maxOutputCharacters: expected.Length));
        Assert.ThrowsExactly<CalculationLimitException>(() => Format(value, notation: NumericNotation.Fixed,
            provider: culture, groupDigits: true, maxOutputCharacters: expected.Length - 1));
    }

    [TestMethod]
    public void RoundedCarryUsesTheResultingFixedLayoutAndGroupCount()
    {
        Assert.AreEqual(("1,000", true), Format(new MathValue(999.9m), digits: 3,
            notation: NumericNotation.Fixed, groupDigits: true, maxOutputCharacters: 5));
        Assert.ThrowsExactly<CalculationLimitException>(() => Format(new MathValue(999.9m), digits: 3,
            notation: NumericNotation.Fixed, groupDigits: true, maxOutputCharacters: 4));
    }

    [TestMethod]
    [DataRow(ExpressionFormatStyle.NormalizedAscii, ExponentMarker.Lowercase, "minus1::25e0")]
    [DataRow(ExpressionFormatStyle.NormalizedAscii, ExponentMarker.Uppercase, "minus1::25E0")]
    [DataRow(ExpressionFormatStyle.TypographicUnicode, ExponentMarker.Lowercase, "minus1::25 × 10^0")]
    [DataRow(ExpressionFormatStyle.MathJax, ExponentMarker.Lowercase, @"minus1::25 \times 10^{0}")]
    public void ScientificLengthChecksAcceptTheExactBoundary(
        ExpressionFormatStyle style, ExponentMarker marker, string expected)
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NegativeSign = "minus";
        culture.NumberFormat.NumberDecimalSeparator = "::";
        var value = new MathValue(-1.25m);
        var options = new ResultFormatOptions
        {
            Notation = NumericNotation.Scientific,
            Style = style,
            ExponentMarker = marker,
            MaxOutputCharacters = expected.Length,
        };

        Assert.AreEqual((expected, false), ResultValueFormatter.Format(value, options, culture));
        Assert.ThrowsExactly<CalculationLimitException>(() => ResultValueFormatter.Format(
            value, options with { MaxOutputCharacters = expected.Length - 1 }, culture));
    }

    [TestMethod]
    public void DefaultResultStringsEnforceGroupedLengthForIndianCulture()
    {
        var culture = CultureInfo.GetCultureInfo("hi-IN");
        var limited = CalculatorEngine.ForCulture(culture, options => options with
        {
            ResultFormat = new ResultFormatOptions { MaxOutputCharacters = 10 }
        });
        Assert.ThrowsExactly<CalculationLimitException>(() => limited.Evaluate("12345678").ToString());

        var exactBoundary = CalculatorEngine.ForCulture(culture, options => options with
        {
            ResultFormat = new ResultFormatOptions { MaxOutputCharacters = 11 }
        });
        Assert.AreEqual("1,23,45,678", exactBoundary.Evaluate("12345678").ToString());
    }

    [TestMethod]
    public void DecimalExponentRequiresPositiveComponents()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ResultValueFormatter.DecimalExponent(0, 1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ResultValueFormatter.DecimalExponent(-1, 1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ResultValueFormatter.DecimalExponent(1, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ResultValueFormatter.DecimalExponent(1, -1));
        Assert.AreEqual(-3, ResultValueFormatter.DecimalExponent(1, 1000));
        Assert.AreEqual(2, ResultValueFormatter.DecimalExponent(999, 1));
        Assert.AreEqual(3, ResultValueFormatter.DecimalExponent(1000, 1));
    }

    [TestMethod]
    public void MaximumPrecisionAndComponentSizesRemainWithinFormattingWorkBounds()
    {
        var denominator = BigInteger.One << 16383;
        var (Text, IsRounded) =
            Format(MathValue.FromFraction(BigInteger.One, denominator), digits: 1000);
        Assert.IsLessThanOrEqualTo(1020, Text.Length);
        Assert.IsTrue(IsRounded);
        Assert.IsTrue(Text.Contains(" × 10^-", StringComparison.Ordinal));
        Assert.AreEqual(("1", true), Format(
            MathValue.FromFraction(denominator + 1, denominator), digits: 1000));
    }

    [TestMethod]
    public void StructuredResultExposesNumericAndExactFractionViews()
    {
        var formatted = Calculator.FormatResult(Calculator.Evaluate("2 / 6"), CultureInfo.InvariantCulture);
        Assert.AreEqual("0.333333333333333333333333", formatted.PrimaryDisplay);
        Assert.IsTrue(formatted.IsDisplayRounded);
        Assert.AreEqual("1/3", formatted.FractionRepresentation);
        Assert.AreEqual("1/3", formatted.MachineReadable);

        var unverified = Calculator.FormatResult(new MathValue(0.5), CultureInfo.InvariantCulture);
        Assert.IsFalse(unverified.IsDisplayRounded);
        Assert.IsNull(unverified.FractionRepresentation);
    }

    [TestMethod]
    public void ExistingSevenFieldDeconstructionRemainsAvailable()
    {
        var result = new FormattedCalculation(new MathValue(5), "5", "5");
        var (value, display, machine, hex, binary, octal, assignments) = result;
        Assert.AreEqual(new MathValue(5), value);
        Assert.AreEqual("5", display);
        Assert.AreEqual("5", machine);
        Assert.IsNull(hex);
        Assert.IsNull(binary);
        Assert.IsNull(octal);
        Assert.IsNull(assignments);
        Assert.IsFalse(result.IsDisplayRounded);
        Assert.IsNull(result.FractionRepresentation);
    }
}
