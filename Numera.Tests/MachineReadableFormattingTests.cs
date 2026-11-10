using System.Globalization;
using System.Numerics;

namespace Numera.Tests;

[TestClass]
public sealed class MachineReadableFormattingTests
{
    [TestMethod]
    [DataRow("194.40", "194.40")]
    [DataRow("194.40-100", "94.40")]
    [DataRow("194.40000-100", "94.40000")]
    [DataRow("100.00", "100.00")]
    [DataRow("100.000", "100.000")]
    [DataRow("100.00-100", "0.00")]
    [DataRow("100.00-200", "-100.00")]
    [DataRow("194.40/7*7", "194.40")]
    [DataRow("1.2340e2", "123.40")]
    [DataRow("10000000000000000000000000.0001", "10000000000000000000000000.0001")]
    [DataRow("1.00/3", "1/3")]
    [DataRow("1.00/3*3", "1.00")]
    [DataRow("1/2", "1/2")]
    [DataRow("7/8", "7/8")]
    [DataRow("0.5", "0.5")]
    [DataRow("(1/2)*2", "1")]
    [DataRow("1e-40", "1/10000000000000000000000000000000000000000")]
    public void MachineTextPreservesNumericValueAndRepresentableDecimalScale(string expression, string expected)
    {
        var result = Calculator.Evaluate(expression);
        var card = result.FormatResult(CultureInfo.InvariantCulture);
        Assert.AreEqual(expected, card.MachineReadable);
        Assert.AreEqual(expected, result.MachineReadable);
        Assert.AreEqual(expected, result.Value.ToInvariantExpression());
        var restored = Calculator.Evaluate(card.MachineReadable);
        Assert.AreEqual(result.Value, restored.Value);
        Assert.AreEqual(result.GetExactFraction(), restored.GetExactFraction());
        if (!expected.Contains('/'))
        {
            Assert.AreEqual(result.Value.DecimalDisplayScale, restored.Value.DecimalDisplayScale);
            Assert.AreEqual(card.PrimaryDisplay, restored.PrimaryDisplay);
        }
    }

    [TestMethod]
    public void MachineTextNeverUsesTheDisplayPrecisionOrNotation()
    {
        var engine = new CalculatorEngine(new CalculatorOptions
        {
            ResultFormat = new ResultFormatOptions
            {
                SignificantDigits = 2,
                Notation = NumericNotation.Scientific
            }
        });
        var result = engine.Evaluate("194.40-100");
        Assert.AreEqual("9.4e1", result.PrimaryDisplay);
        Assert.AreEqual("94.40", result.MachineReadable);
        Assert.AreEqual(result.Value, Calculator.Evaluate(result.MachineReadable).Value);
        const string precise = "1.2345678901234567890123456789";
        Assert.AreEqual(precise, engine.Evaluate(precise).MachineReadable);
        var large = engine.Evaluate("2^100");
        Assert.AreEqual("1267650600228229401496703205376", large.MachineReadable);
        Assert.AreEqual(BigInteger.One << 100, Calculator.Evaluate(large.MachineReadable).IntegerValue);
    }

    [TestMethod]
    public void MachineTextIsInvariantForCulturedAndCapturedResults()
    {
        var engine = CalculatorEngine.ForCulture(CultureInfo.GetCultureInfo("de-DE"));
        var captured = engine.Evaluate("amount=194,40");
        var result = engine.Evaluate("amount-100", captured.Context);
        Assert.AreEqual("94,40", result.PrimaryDisplay);
        Assert.AreEqual("94.40", result.MachineReadable);
        Assert.AreEqual("94.40", (captured - 100).MachineReadable);
        Assert.AreEqual("94.40", engine.FormatResult(new MathValue(94.40m)).MachineReadable);
        Assert.AreEqual(result.Value, Calculator.Evaluate(result.MachineReadable).Value);
    }

    [TestMethod]
    public void MachineTextRetainsFullNativeDecimalScaleWithoutDisplayPaddingLimits()
    {
        var result = Calculator.Evaluate("94.40000");
        Assert.AreEqual("94.4", result.PrimaryDisplay);
        Assert.AreEqual("94.40000", result.MachineReadable);
        Assert.AreEqual("94.40000", Calculator.FormatResult(new MathValue(94.40000m)).MachineReadable);
        var whole = Calculator.Evaluate("100.000");
        Assert.AreEqual("100", whole.PrimaryDisplay);
        Assert.AreEqual("100.000", whole.MachineReadable);
        var value = new MathValue(1.0000000000000000000000000000m);
        Assert.AreEqual("1.0000000000000000000000000000", value.ToInvariantExpression());
        Assert.AreEqual(value, Calculator.Evaluate(value.ToInvariantExpression()).Value);
    }

    [TestMethod]
    public void MachineTextLimitsThrowInsteadOfTrimmingScaleOrRoundingValue()
    {
        var engine = new CalculatorEngine(new CalculatorOptions
        {
            ResultFormat = new ResultFormatOptions { SignificantDigits = 1, MaxOutputCharacters = 5 }
        });
        var result = engine.Evaluate("194.40");
        Assert.AreEqual("200", result.ToString());
        Assert.AreEqual("194.40", result.Value.ToInvariantExpression());
        Assert.ThrowsExactly<CalculationLimitException>(() => result.FormatResult());
    }
}
