using System.Globalization;
using System.Numerics;
using System.Text;

namespace Numera.Tests;

[TestClass]
public sealed class DecimalDisplayScaleTests
{
    [TestMethod]
    [DataRow("194.40", "194.40")]
    [DataRow("194.40 - 100", "94.40")]
    [DataRow("194.40 + 100", "294.40")]
    [DataRow("194.400 - 100", "94.400")]
    [DataRow("194.4000 - 100", "94.4000")]
    [DataRow("194.40000 - 100", "94.4")]
    [DataRow("100.0", "100.0")]
    [DataRow("100.00", "100.00")]
    [DataRow("100.000", "100")]
    [DataRow("100.00 - 100", "0.00")]
    [DataRow("100.000 - 100", "0")]
    [DataRow("100.00 - 200", "-100.00")]
    [DataRow("194.40 * 2", "388.80")]
    [DataRow("194.40 / 2", "97.20")]
    [DataRow("194.40 % 100", "94.40")]
    [DataRow("1.20 * 2.0", "2.400")]
    [DataRow("1.20^2", "1.4400")]
    [DataRow("1.00/3*3", "1.00")]
    [DataRow("194.40/7*7", "194.40")]
    [DataRow("(194.40/10^30)*10^30", "194.40")]
    [DataRow("abs(-194.40)", "194.40")]
    [DataRow("frac(194.40)", "0.40")]
    [DataRow("round(194.400,2)", "194.40")]
    [DataRow("sum(194.40, 100)", "294.40")]
    [DataRow("avg(194.40, 100)", "147.20")]
    [DataRow("1.2340e2", "123.40")]
    [DataRow("0.00", "0.00")]
    [DataRow("0.00e1", "0.0")]
    [DataRow("0.00e2", "0")]
    [DataRow("(1/2)*2", "1")]
    [DataRow("((24*(1/2))/((1/3)*(2/1)))", "18")]
    [DataRow("1/3*3", "1")]
    [DataRow("0.1-0.1", "0.0")]
    [DataRow("0e-99999999999999999999", "0")]
    [DataRow("0e99999999999999999999", "0")]
    public void ExpressionsPreserveOnlyBoundedDecimalScale(string expression, string expected)
    {
        var result = Calculator.Evaluate(expression);
        var card = result.FormatResult(CultureInfo.InvariantCulture);
        Assert.AreEqual(expected, card.PrimaryDisplay);
        AssertDefaultDisplay(result, expected);
        Assert.IsFalse(card.IsDisplayRounded);
        Assert.AreEqual(CalculationAccuracy.Exact, result.Accuracy);
        Assert.IsTrue(result.HasExactFraction);
    }

    [TestMethod]
    [DataRow("0", 0, MathValue.DataType.Integer)]
    [DataRow("0.000", 3, MathValue.DataType.Decimal)]
    [DataRow("0.000e1", 2, MathValue.DataType.Decimal)]
    [DataRow("0.000e3", 0, MathValue.DataType.Decimal)]
    [DataRow("0.000e4", 0, MathValue.DataType.Decimal)]
    [DataRow("0.000e-2", 5, MathValue.DataType.Decimal)]
    [DataRow("0e-28", 28, MathValue.DataType.Decimal)]
    [DataRow("0.00e-28", 29, MathValue.DataType.Decimal)]
    [DataRow("0e-29", 29, MathValue.DataType.Decimal)]
    [DataRow("0e-30", 29, MathValue.DataType.Decimal)]
    [DataRow("0e-0", 0, MathValue.DataType.Decimal)]
    [DataRow("0e9223372036854775807", 0, MathValue.DataType.Integer)]
    [DataRow("0e-9223372036854775808", 29, MathValue.DataType.Decimal)]
    [DataRow("0.00e9223372036854775808", 0, MathValue.DataType.Decimal)]
    [DataRow("0e+9223372036854775808", 0, MathValue.DataType.Integer)]
    [DataRow("0e-9223372036854775809", 29, MathValue.DataType.Decimal)]
    public void ZeroLiteralScalesRemainBoundedAcrossExponentBranches(
        string expression, int expectedScale, MathValue.DataType expectedType)
    {
        var engine = new CalculatorEngine(new CalculatorOptions
        {
            MaxIntegerBits = 1,
            MaxExponentMagnitude = 1
        });
        var result = engine.Evaluate(expression);

        Assert.IsTrue(result.IsZero);
        Assert.AreEqual(expectedScale, result.Value.DecimalDisplayScale);
        Assert.AreEqual(expectedType, result.Type);
        Assert.AreEqual(CalculationAccuracy.Exact, result.Accuracy);
        Assert.AreEqual((BigInteger.Zero, BigInteger.One), result.GetExactFraction());
    }

    [TestMethod]
    public void ScaleSurvivesCapturedValuesOperatorsAndSerialization()
    {
        var engine = new CalculatorEngine();
        var captured = engine.Evaluate("amount=194.40");
        Assert.AreEqual("94.40", engine.Evaluate("amount-100", captured.Context).FormatResult().PrimaryDisplay);
        Assert.AreEqual("94.40", engine.Evaluate("ans-100", captured.Context).FormatResult().PrimaryDisplay);
        Assert.AreEqual("94.40", (captured - 100).FormatResult().PrimaryDisplay);
        Assert.AreEqual("-194.40", (-captured).FormatResult().PrimaryDisplay);
        var value = captured.Value - new MathValue(100);
        Assert.AreEqual("94.40", engine.FormatResult(value).PrimaryDisplay);
        Assert.AreEqual("94.40", value.ToInvariantExpression());
        Assert.AreEqual("472/5", engine.FormatResult(value).FractionRepresentation);
        Assert.AreEqual(new MathValue(94.4m), value);
        Assert.AreEqual(new MathValue(94.4m).GetHashCode(), value.GetHashCode());
        foreach (var stored in new[] { value, engine.Evaluate("1.00").Value, engine.Evaluate("1.00/3").Value })
        {
            var restored = MathValue.Deserialize(stored.Serialize());
            Assert.AreEqual(stored.Kind, restored.Kind);
            Assert.AreEqual(stored.Accuracy, restored.Accuracy);
            Assert.AreEqual(stored.GetExactFraction(), restored.GetExactFraction());
            Assert.AreEqual(stored.DecimalDisplayScale, restored.DecimalDisplayScale);
            Assert.AreEqual(engine.FormatResult(stored).PrimaryDisplay, engine.FormatResult(restored).PrimaryDisplay);
        }
    }

    [TestMethod]
    public void DirectDecimalValuesRespectTheSameDisplayThresholds()
    {
        Assert.AreEqual("1", Calculator.FormatResult(MathValue.FromFraction(1, 2) * new MathValue(2)).PrimaryDisplay);
        Assert.AreEqual("94.40", Calculator.FormatResult(new MathValue(94.40m)).PrimaryDisplay);
        Assert.AreEqual("94.4000", Calculator.FormatResult(new MathValue(94.4000m)).PrimaryDisplay);
        Assert.AreEqual("94.4", Calculator.FormatResult(new MathValue(94.40000m)).PrimaryDisplay);
        Assert.AreEqual("100.00", Calculator.FormatResult(new MathValue(100.00m)).PrimaryDisplay);
        Assert.AreEqual("100", Calculator.FormatResult(new MathValue(100.000m)).PrimaryDisplay);
        var unverified = new MathValue(94.40m).WithAccuracy(CalculationAccuracy.Unverified);
        Assert.AreEqual("94.40", Calculator.FormatResult(unverified).PrimaryDisplay);
        Assert.IsNull(Calculator.FormatResult(unverified).FractionRepresentation);
        Assert.AreEqual("0.5", Calculator.FormatResult(new MathValue(0.5)).PrimaryDisplay);
    }

    [TestMethod]
    public void PrecisionNotationCultureAndOutputLimitsStillApply()
    {
        var shortDisplay = new CalculatorEngine(new CalculatorOptions
        {
            ResultFormat = new ResultFormatOptions { SignificantDigits = 3 }
        });
        Assert.AreEqual("94.4", shortDisplay.Evaluate("194.40-100").FormatResult().PrimaryDisplay);
        AssertDefaultDisplay(shortDisplay.Evaluate("194.40-100"), "94.4");
        var rounded = shortDisplay.Evaluate("194.40").FormatResult();
        Assert.AreEqual("194", rounded.PrimaryDisplay);
        Assert.IsTrue(rounded.IsDisplayRounded);
        var scientific = new CalculatorEngine(new CalculatorOptions
        {
            ResultFormat = new ResultFormatOptions { Notation = NumericNotation.Scientific }
        });
        Assert.AreEqual("9.44e1", scientific.Evaluate("194.40-100").FormatResult().PrimaryDisplay);
        AssertDefaultDisplay(scientific.Evaluate("194.40-100"), "9.44e1");
        var german = CalculatorEngine.ForCulture(CultureInfo.GetCultureInfo("de-DE"));
        Assert.AreEqual("94,40", german.Evaluate("194,40-100").FormatResult().PrimaryDisplay);
        AssertDefaultDisplay(german.Evaluate("194,40-100"), "94,40");
        Assert.AreEqual("94.40", german.Evaluate("194,40-100").ToString(CultureInfo.InvariantCulture));
        var limited = new CalculatorEngine(new CalculatorOptions
        {
            ResultFormat = new ResultFormatOptions { MaxOutputCharacters = 4 }
        });
        Assert.ThrowsExactly<CalculationLimitException>(() => limited.Evaluate("94.40").FormatResult());
        Assert.ThrowsExactly<CalculationLimitException>(() => ResultValueFormatter.Format(
            new MathValue(94.40m), new ResultFormatOptions { MaxOutputCharacters = 4 }, CultureInfo.InvariantCulture));
        Assert.ThrowsExactly<CalculationLimitException>(() => ResultValueFormatter.Format(
            new MathValue(0.00m), new ResultFormatOptions { MaxOutputCharacters = 3 }, CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void DisplayMetadataIsBoundedAndValidatedWithoutChangingRationalValues()
    {
        var value = Calculator.Evaluate("1.20000000000000000000000000000^100").Value;
        Assert.IsTrue(value.DecimalDisplayScale <= MathValue.MaxDecimalDisplayScale);
        Assert.AreEqual("1", Calculator.Evaluate(
            "1.000000000000000000000000000000/1.0000000000000000000000000000").FormatResult().PrimaryDisplay);
        var third = Calculator.Evaluate("1.00/3").Value;
        Assert.AreEqual(MathValue.DataType.Rational, third.Kind);
        Assert.AreEqual(BigInteger.One, third.Numerator);
        Assert.AreEqual(new BigInteger(3), third.Denominator);
        Assert.ThrowsExactly<FormatException>(() => MathValue.Deserialize(
            """{"Kind":0,"Accuracy":1,"Value":"1","DecimalDisplayScale":30}"""));
        Assert.ThrowsExactly<FormatException>(() => MathValue.Deserialize(
            """{"Kind":0,"Accuracy":1,"Value":"1","DecimalDisplayScale":-1}"""));
        Assert.ThrowsExactly<FormatException>(() => MathValue.Deserialize(
            """{"Kind":2,"Accuracy":0,"Value":"1","DecimalDisplayScale":2}"""));
    }

    [TestMethod]
    public void DirectEvaluationOutputPreservesScaleWithoutChangingExplicitFormats()
    {
        var result = Calculator.Evaluate("194.40-100");
        Assert.AreEqual("94.40", result.ToString());
        Assert.AreEqual("94.40", result.ToString(CultureInfo.InvariantCulture));
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        output.Write(result);
        Assert.AreEqual("94.40", output.ToString());
        Assert.AreEqual("94.4", result.ToString("F1", CultureInfo.InvariantCulture));
        Assert.AreEqual("94.4", result.ToString("G", CultureInfo.InvariantCulture));
        Assert.AreEqual("94.40", result.Value.ToInvariantExpression());
        Span<char> chars = stackalloc char[4];
        Assert.IsFalse(result.TryFormat(chars, out int charsWritten));
        Assert.AreEqual(0, charsWritten);
        Span<byte> bytes = stackalloc byte[4];
        Assert.IsFalse(result.TryFormat(bytes, out int bytesWritten));
        Assert.AreEqual(0, bytesWritten);
        Assert.IsTrue(result.TryFormat(chars, out charsWritten, "F1", CultureInfo.InvariantCulture));
        Assert.AreEqual("94.4", chars[..charsWritten].ToString());
        Assert.IsTrue(result.TryFormat(bytes, out bytesWritten, "F1", CultureInfo.InvariantCulture));
        Assert.AreEqual("94.4", Encoding.UTF8.GetString(bytes[..bytesWritten]));
    }

    private static void AssertDefaultDisplay(EvaluationResult result, string expected)
    {
        Assert.AreEqual(expected, result.ToString());
        Assert.AreEqual(expected, result.ToString(null, null));
        Assert.AreEqual(expected, result.ToString(string.Empty, null));
        Assert.AreEqual(expected, $"{result}");
        Assert.AreEqual(expected, string.Format("{0}", result));
        IFormattable formattable = result;
        Assert.AreEqual(expected, formattable.ToString(null, null));
        Span<char> chars = stackalloc char[64];
        ISpanFormattable spanFormattable = result;
        Assert.IsTrue(spanFormattable.TryFormat(chars, out int charsWritten, default, null));
        Assert.AreEqual(expected, chars[..charsWritten].ToString());
        Span<byte> bytes = stackalloc byte[64];
        IUtf8SpanFormattable utf8Formattable = result;
        Assert.IsTrue(utf8Formattable.TryFormat(bytes, out int bytesWritten, default, null));
        Assert.AreEqual(expected, Encoding.UTF8.GetString(bytes[..bytesWritten]));
    }

    [TestMethod]
    public void DefaultResultStringsUseNumericPresentationRatherThanNativeValueText()
    {
        var result = Calculator.Evaluate("2^100");
        AssertDefaultDisplay(result, "1.2676506002282294014967e30");
        Assert.AreEqual("1267650600228229401496703205376", result.Value.ToString(CultureInfo.InvariantCulture));
        AssertDefaultDisplay(Calculator.Evaluate("1e-400"), "1e-400");
        AssertDefaultDisplay(Calculator.Evaluate("1/3"), "0.333333333333333333333333");
    }

    [TestMethod]
    [DataRow("1e-400", ExponentMarker.Lowercase, "1e-400")]
    [DataRow("1e-400", ExponentMarker.Uppercase, "1E-400")]
    [DataRow("-1e400", ExponentMarker.Lowercase, "-1e400")]
    [DataRow("-1e400", ExponentMarker.Uppercase, "-1E400")]
    [DataRow("2^100", ExponentMarker.Lowercase, "1.2676506002282294014967e30")]
    [DataRow("2^100", ExponentMarker.Uppercase, "1.2676506002282294014967E30")]
    public void ExponentCasingIsConsistentAcrossResultDisplaySurfaces(
        string expression, ExponentMarker marker, string expected)
    {
        var options = new ResultFormatOptions { ExponentMarker = marker };
        var engine = CalculatorEngine.ForCulture(CultureInfo.InvariantCulture,
            settings => settings with { ResultFormat = options });
        var result = engine.Evaluate(expression);

        AssertDefaultDisplay(result, expected);
        Assert.AreEqual(expected, result.PrimaryDisplay);
        Assert.AreEqual(expected, result.FormatResult().PrimaryDisplay);
        Assert.AreEqual(expected, engine.FormatResult(result).PrimaryDisplay);
        Assert.AreEqual(expected, ResultFormatter.Format(result, options).PrimaryDisplay);
        Assert.AreEqual(expected, ResultFormatter.Format(result.Value, options).PrimaryDisplay);
        Assert.AreEqual(result.Value.ToInvariantExpression(), result.MachineReadable);
        Assert.AreEqual(Calculator.Evaluate(expression).MachineReadable, result.MachineReadable);
    }

    [TestMethod]
    public void ResultDefaultsAndOverridesAreSeparateFromExpressionFormatting()
    {
        Assert.AreEqual(ExpressionFormatStyle.NormalizedAscii, ResultFormatOptions.Default.Style);
        Assert.AreEqual(ExponentMarker.Lowercase, ResultFormatOptions.Default.ExponentMarker);
        var result = Calculator.Evaluate("1e-400");
        Assert.AreEqual("1e-400", result.Value.ToString(CultureInfo.InvariantCulture));
        Assert.AreEqual("1E-400", ResultFormatter.Format(result,
            new ResultFormatOptions { ExponentMarker = ExponentMarker.Uppercase }).PrimaryDisplay);
        AssertDefaultDisplay(result, "1e-400");
        Assert.AreEqual("exp(1E-400) + ResultE",
            Calculator.FormatExpression("exp(1e-400)+ResultE", ExpressionFormatOptions.Ascii));
        Assert.AreEqual("1 × 10^-400", Calculator.FormatExpression("1e-400"));
    }

    [TestMethod]
    public void DefaultResultStringsDoNotFormatUnusedAlternateRepresentations()
    {
        var engine = new CalculatorEngine(new CalculatorOptions
        {
            ResultFormat = new ResultFormatOptions { MaxOutputCharacters = 10 }
        });
        var result = engine.Evaluate("1e-50");
        AssertDefaultDisplay(result, "1e-50");
        Assert.ThrowsExactly<CalculationLimitException>(() => result.FormatResult());
        var limited = new CalculatorEngine(new CalculatorOptions
        {
            ResultFormat = new ResultFormatOptions { MaxOutputCharacters = 4 }
        });
        var tooLong = limited.Evaluate("94.40");
        Assert.ThrowsExactly<CalculationLimitException>(() => tooLong.ToString());
    }
}
