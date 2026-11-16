using System.Globalization;
using System.Numerics;
using System.Text;

namespace Numera.Tests;

[TestClass]
public sealed class DoubleResultFormattingTests
{
    private static readonly CalculatorEngine InvariantEngine = CalculatorEngine.ForCulture(CultureInfo.InvariantCulture);

    [TestMethod]
    [DataRow("sqrt(2)", "1.4142135623731")]
    [DataRow("exp(1)", "2.71828182845905")]
    [DataRow("sin(rad(30))", "0.5")]
    [DataRow("deg(asin(0.5))", "30")]
    [DataRow("sin(rad(45))^2", "0.5")]
    public void DefaultDisplayRoundsApproximateResultsWithoutChangingTheirValues(string expression, string expected)
    {
        var result = InvariantEngine.Evaluate(expression);
        var snapshot = result.FormatResult();
        long bits = BitConverter.DoubleToInt64Bits(result.DoubleValue);
        string serialized = result.Value.Serialize();
        string machine = result.MachineReadable;

        Assert.AreEqual(expected, result.PrimaryDisplay);
        Assert.AreEqual(expected, snapshot.PrimaryDisplay);
        Assert.AreEqual(expected, result.ToString());
        Assert.AreEqual(expected, $"{result}");
        Assert.IsTrue(snapshot.IsDisplayRounded);
        Assert.AreEqual(CalculationExactness.Unverified, result.Exactness);
        Assert.IsFalse(result.HasExactFraction);
        Assert.IsNull(snapshot.FractionRepresentation);
        Assert.AreEqual(machine, snapshot.MachineReadable);
        Assert.AreEqual(result.Value, InvariantEngine.Evaluate(machine).Value);
        Assert.AreEqual(bits, BitConverter.DoubleToInt64Bits(snapshot.Value.DoubleValue));
        Assert.AreEqual(bits, BitConverter.DoubleToInt64Bits(MathValue.Deserialize(serialized).DoubleValue));
        Assert.AreEqual(CalculationExactness.Unverified, MathValue.Deserialize(serialized).Exactness);
        Assert.AreNotEqual(result.Value, InvariantEngine.Evaluate(expected).Value);
        Assert.AreEqual((result.Value * new MathValue(1000000)).DoubleValue,
            InvariantEngine.Evaluate("ans*1000000", result.Context).DoubleValue);

        var characters = new char[64];
        Assert.IsTrue(result.TryFormat(characters, out int charsWritten));
        Assert.AreEqual(expected, new string(characters, 0, charsWritten));
        Assert.IsFalse(result.TryFormat(characters.AsSpan(0, expected.Length - 1), out charsWritten));
        Assert.AreEqual(0, charsWritten);

        var utf8 = new byte[128];
        Assert.IsTrue(result.TryFormat(utf8, out int bytesWritten));
        Assert.AreEqual(expected, Encoding.UTF8.GetString(utf8, 0, bytesWritten));
        Assert.IsFalse(result.TryFormat(utf8.AsSpan(0, expected.Length - 1), out bytesWritten));
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    [DataRow(24, 15, "3.14159265358979")]
    [DataRow(20, 15, "3.14159265358979")]
    [DataRow(8, 15, "3.1415927")]
    [DataRow(24, 17, "3.1415926535897931")]
    [DataRow(16, 17, "3.141592653589793")]
    [DataRow(24, 1, "3")]
    [DataRow(1, 17, "3")]
    [DataRow(1000, 17, "3.1415926535897931")]
    public void DoublePrecisionRespectsBothCeilings(int significantDigits, int doubleSignificantDigits, string expected)
    {
        var options = new ResultFormatOptions
        {
            SignificantDigits = significantDigits,
            DoubleSignificantDigits = doubleSignificantDigits
        };
        Assert.AreEqual(expected, InvariantEngine.FormatResult(new MathValue(Math.PI), options).PrimaryDisplay);
    }

    [TestMethod]
    public void DoublePrecisionDoesNotReduceOtherRepresentationsOrUnverifiedFractions()
    {
        Assert.AreEqual(24, ResultFormatOptions.Default.SignificantDigits);
        Assert.AreEqual(15, ResultFormatOptions.Default.DoubleSignificantDigits);
        var options = new ResultFormatOptions { SignificantDigits = 20, DoubleSignificantDigits = 1, GroupDigits = false };
        (MathValue Value, string Expected)[] cases =
        [
            (new MathValue(BigInteger.Parse("12345678901234567890", CultureInfo.InvariantCulture)), "12345678901234567890"),
            (new MathValue(1.23456789123456789123456789m), "1.2345678912345678912"),
            (MathValue.FromFraction(1, 3), "0.33333333333333333333")
        ];

        foreach (var item in cases)
        {
            Assert.AreEqual(item.Expected, InvariantEngine.FormatResult(item.Value, options).PrimaryDisplay);
            Assert.AreEqual(item.Expected, InvariantEngine.FormatResult(
                item.Value.WithExactness(CalculationExactness.Unverified), options).PrimaryDisplay);
        }
        Assert.AreEqual("0.333333333333333333333333", InvariantEngine.Evaluate("1/3").PrimaryDisplay);
    }

    [TestMethod]
    [DataRow(123456789012344.5, "123456789012344")]
    [DataRow(123456789012345.5, "123456789012346")]
    [DataRow(-123456789012344.5, "-123456789012344")]
    [DataRow(-123456789012345.5, "-123456789012346")]
    public void DefaultDoublePrecisionRoundsExactMidpointsToEven(double value, string expected)
    {
        var snapshot = InvariantEngine.FormatResult(new MathValue(value), new ResultFormatOptions { GroupDigits = false });
        Assert.AreEqual(expected, snapshot.PrimaryDisplay);
        Assert.IsTrue(snapshot.IsDisplayRounded);
    }

    [TestMethod]
    public void ValuesImmediatelyBesideAMidpointRoundInTheCorrectDirection()
    {
        const double midpoint = 123456789012344.5;
        var options = new ResultFormatOptions { GroupDigits = false };
        Assert.AreEqual("123456789012344", InvariantEngine.FormatResult(new MathValue(Math.BitDecrement(midpoint)), options).PrimaryDisplay);
        Assert.AreEqual("123456789012345", InvariantEngine.FormatResult(new MathValue(Math.BitIncrement(midpoint)), options).PrimaryDisplay);
    }

    [TestMethod]
    [DataRow(0.0, "0", false)]
    [DataRow(-0.0, "-0", false)]
    [DataRow(0.5, "0.5", false)]
    [DataRow(30.0, "30", false)]
    [DataRow(0.1, "0.1", true)]
    [DataRow(double.Epsilon, "4.94065645841247e-324", true)]
    [DataRow(double.MaxValue, "1.79769313486232e308", true)]
    [DataRow(double.NaN, "NaN", false)]
    [DataRow(double.PositiveInfinity, "Infinity", false)]
    [DataRow(double.NegativeInfinity, "-Infinity", false)]
    public void DoubleDisplayHandlesExtremesAndReportsRoundingIndependentlyOfExactness(double value, string expected, bool rounded)
    {
        var snapshot = InvariantEngine.FormatResult(new MathValue(value));
        Assert.AreEqual(expected, snapshot.PrimaryDisplay);
        Assert.AreEqual(rounded, snapshot.IsDisplayRounded);
        Assert.AreEqual(CalculationExactness.Unverified, snapshot.Value.Exactness);
        Assert.IsNull(snapshot.FractionRepresentation);
    }

    [TestMethod]
    [DataRow(0.0, "0")]
    [DataRow(-0.0, "-0")]
    [DataRow(0.5, "0.5")]
    [DataRow(30.0, "30")]
    public void DoublesNeverAcquireDecimalScalePadding(double value, string expected)
    {
        var stored = new MathValue(value).WithDecimalDisplayScale(2);
        var snapshot = InvariantEngine.FormatResult(stored);
        Assert.AreEqual(expected, snapshot.PrimaryDisplay);
        Assert.IsFalse(snapshot.IsDisplayRounded);
        Assert.AreEqual(2, snapshot.Value.DecimalDisplayScale);
        Assert.AreEqual(BitConverter.DoubleToInt64Bits(value), BitConverter.DoubleToInt64Bits(snapshot.Value.DoubleValue));
        Assert.AreEqual("30.00", InvariantEngine.FormatResult(new MathValue(30.00m)).PrimaryDisplay);
    }

    [TestMethod]
    public void NotationUsesTheRoundedExponentWithCultureAndGroupingPreserved()
    {
        var value = new MathValue(999.9999999999999);
        var options = new ResultFormatOptions { MaxFixedDigits = 3 };
        Assert.AreEqual("1e3", InvariantEngine.FormatResult(value, options).PrimaryDisplay);
        Assert.AreEqual("1,000", InvariantEngine.FormatResult(value, options with { Notation = NumericNotation.Fixed }).PrimaryDisplay);
        Assert.AreEqual("1E3", InvariantEngine.FormatResult(value, options with { ExponentMarker = ExponentMarker.Uppercase }).PrimaryDisplay);
        Assert.AreEqual("1 × 10^3", InvariantEngine.FormatResult(value, options with { Style = ExpressionFormatStyle.TypographicUnicode }).PrimaryDisplay);
        Assert.AreEqual(@"1 \times 10^{3}", InvariantEngine.FormatResult(value, options with { Style = ExpressionFormatStyle.MathJax }).PrimaryDisplay);
        var german = CultureInfo.GetCultureInfo("de-DE");
        Assert.AreEqual("1.234.567,89012346", InvariantEngine.FormatResult(new MathValue(1234567.8901234567), culture: german).PrimaryDisplay);
        Assert.AreEqual("1,23456789012346e6", InvariantEngine.FormatResult(new MathValue(1234567.8901234567),
            new ResultFormatOptions { Notation = NumericNotation.Scientific }, german).PrimaryDisplay);
    }

    [TestMethod]
    public void DisplayPoliciesAndNativeFormatsRemainSeparateAcrossFacadesAndOverrides()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var engine = InvariantEngine.WithOptions(options => options with
            {
                ResultFormat = new ResultFormatOptions { DoubleSignificantDigits = 17 }
            });
            var result = engine.Evaluate("sqrt(2)");
            const string detailed = "1.4142135623730951";
            const string normal = "1.4142135623731";
            Assert.AreEqual(detailed, result.PrimaryDisplay);
            Assert.AreEqual(detailed, result.ToString());
            Assert.AreEqual(detailed, result.FormatResult().PrimaryDisplay);
            Assert.AreEqual(detailed, engine.FormatResult(result.Value).PrimaryDisplay);
            Assert.AreEqual(normal, Calculator.Evaluate("sqrt(2)").PrimaryDisplay);
            Assert.AreEqual(normal, Calculator.FormatResult(result).PrimaryDisplay);
            Assert.AreEqual(normal, Calculator.FormatResult(result.Value).PrimaryDisplay);
            Assert.AreEqual(normal, result.FormatResult(new ResultFormatOptions()).PrimaryDisplay);
            Assert.AreEqual(detailed, Calculator.FormatResult(result, engine.Options.ResultFormat).PrimaryDisplay);
            Assert.AreEqual(result.Value.ToString("G17", CultureInfo.InvariantCulture), result.ToString("G17", null));
            Assert.AreEqual(detailed, result.Value.ToString(CultureInfo.InvariantCulture));
            Assert.AreEqual("1.41421356237309514547", result.ToString("F20", CultureInfo.InvariantCulture));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
    public void DoublePrecisionDoesNotChangeExactFractionsForCallerAssertedExactValues()
    {
        var value = new MathValue(0.1).WithExactness(CalculationExactness.Exact);
        var options = new ResultFormatOptions { DoubleSignificantDigits = 1 };
        var snapshot = InvariantEngine.FormatResult(value, options);
        Assert.AreEqual("0.1", snapshot.PrimaryDisplay);
        Assert.IsTrue(snapshot.IsDisplayRounded);
        Assert.AreEqual(CalculationExactness.Exact, snapshot.Value.Exactness);
        Assert.AreEqual("3602879701896397/36028797018963968", snapshot.FractionRepresentation);
        Assert.AreEqual(snapshot.FractionRepresentation, snapshot.MachineReadable);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(18)]
    [DataRow(1000)]
    public void InvalidDoublePrecisionIsRejectedEvenWhenFormattingOtherKinds(int digits)
    {
        var options = new ResultFormatOptions { DoubleSignificantDigits = digits };
        Assert.AreEqual(nameof(ResultFormatOptions.DoubleSignificantDigits),
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(options.Validate).ParamName);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CalculatorEngine(new CalculatorOptions { ResultFormat = options }));
        foreach (var value in new[] { new MathValue(0), MathValue.FromFraction(1, 3), new MathValue(0.0), MathValue.NaN })
        {
            var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => InvariantEngine.FormatResult(value, options));
            Assert.AreEqual(nameof(ResultFormatOptions.DoubleSignificantDigits), exception.ParamName);
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                ResultValueFormatter.Format(value, options, CultureInfo.InvariantCulture));
        }
    }
}
