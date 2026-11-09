using System.Globalization;
using System.Numerics;
using Numera.Ast;

namespace Numera.Tests;

[TestClass]
public sealed class RationalIntegrationTests
{
    [TestMethod]
    public void CapturedValuesRetainExactFractionsWithoutExpressionMetadata()
    {
        var engine = new CalculatorEngine();
        var third = engine.Evaluate("1/3").Value;
        var context = new EvaluationContext(new[] { new Variable("a", third) }, third);
        var bareResult = new EvaluationResult(third, context);

        Assert.AreEqual(new MathValue(1), engine.Evaluate("a * 3", context).Value);
        Assert.AreEqual(new MathValue(1), engine.Evaluate("ans * 3", context).Value);
        Assert.AreEqual(new MathValue(1), (bareResult * 3).Value);
        Assert.AreEqual(CalculationAccuracy.Exact, third.Accuracy);
        Assert.IsTrue(bareResult.HasExactFraction);
    }

    [TestMethod]
    public void ExactDecimalMixingAndDoublePoisoningAreDistinct()
    {
        var engine = new CalculatorEngine();
        var exact = engine.Evaluate("1/3 + 0.5");
        Assert.AreEqual(MathValue.FromFraction(5, 6), exact.Value);
        Assert.AreEqual(CalculationAccuracy.Exact, exact.Accuracy);
        Assert.AreEqual("5/6", exact.FormatResult().FractionRepresentation);

        var approximate = exact + new MathValue(0.5);
        Assert.AreEqual(MathValue.DataType.Double, approximate.Type);
        Assert.AreEqual(CalculationAccuracy.Unverified, approximate.Accuracy);
        Assert.IsFalse(approximate.HasExactFraction);
        Assert.IsNull(approximate.FormatResult().FractionRepresentation);
    }

    [TestMethod]
    public void ReceivingEngineValidatesCapturedValuesAndComposition()
    {
        var engine = new CalculatorEngine(new CalculatorOptions
        {
            MaxIntegerBits = 8
        });
        var context = new EvaluationContext(new[] { new Variable("a", new MathValue(256)) });
        Assert.Throws<CalculationLimitException>(() => engine.Evaluate("a", context));

        var result = engine.Evaluate("200");
        Assert.Throws<CalculationLimitException>(() => engine.Evaluate("ans + 200", result.Context));
    }

    [TestMethod]
    public void FormattingLimitsAreSeparateFromCalculationLimits()
    {
        var engine = new CalculatorEngine(new CalculatorOptions
        {
            ResultFormat = new ResultFormatOptions { MaxOutputCharacters = 8 }
        });
        var result = engine.Evaluate("1/257");
        Assert.AreEqual(MathValue.FromFraction(1, 257), result.Value);
        Assert.Throws<CalculationLimitException>(() => engine.FormatResult(result));
    }

    [TestMethod]
    public void MachineReadableAndSerializationHaveDifferentContracts()
    {
        var value = MathValue.FromFraction(1, 3).WithAccuracy(CalculationAccuracy.Unverified);
        var card = Calculator.FormatResult(value, CultureInfo.InvariantCulture);
        Assert.AreEqual("1/3", card.MachineReadable);
        Assert.IsNull(card.FractionRepresentation);
        Assert.AreEqual(CalculationAccuracy.Exact, Calculator.Evaluate(card.MachineReadable).Accuracy);

        var restored = MathValue.Deserialize(value.Serialize());
        Assert.AreEqual(value, restored);
        Assert.AreEqual(value.Kind, restored.Kind);
        Assert.AreEqual(value.Accuracy, restored.Accuracy);
    }

    [TestMethod]
    public void RationalNumberMetadataCanBeRenderedAsStackedFraction()
    {
        var engine = new CalculatorEngine();
        var node = new NumberNode(MathValue.FromFraction(-5, 6));
        Assert.AreEqual(@"\frac{-5}{6}", engine.FormatExpression(
            node, new ExpressionFormatOptions { Style = ExpressionFormatStyle.MathJax }));
        Assert.AreEqual("(-5/6)", engine.FormatExpression(
            node, new ExpressionFormatOptions { Style = ExpressionFormatStyle.NormalizedAscii, UseUnicodeMinus = false }));
    }

    [TestMethod]
    public void FractionalExplicitIntegerConversionDoesNotReadAnInactiveAccessor()
    {
        var third = Calculator.Evaluate("1/3");
        Assert.Throws<InvalidCastException>(() => _ = (int)third);
        Assert.AreEqual(0.3333333333333333333333333333m, (decimal)third);
    }

    [TestMethod]
    public void LosslessHugeIntegerOutputIsIndependentOfRoundedDisplay()
    {
        var value = new MathValue(BigInteger.One << 100);
        var card = Calculator.FormatResult(value, CultureInfo.InvariantCulture);
        Assert.AreEqual("1267650600228229401496703205376", card.MachineReadable);
        Assert.AreEqual(card.MachineReadable, card.FractionRepresentation);
        Assert.IsTrue(card.IsDisplayRounded);
        Assert.AreEqual(value, Calculator.Evaluate(card.MachineReadable).Value);
    }

    [TestMethod]
    [DataRow(0.1)]
    [DataRow(double.Epsilon)]
    [DataRow(double.MaxValue)]
    public void FiniteDoubleInvariantExpressionPreservesItsStoredBinaryValue(double input)
    {
        var value = new MathValue(input);
        var expression = value.ToInvariantExpression();
        Assert.AreEqual(value, Calculator.Evaluate(expression).Value);
        var restored = MathValue.Deserialize(value.Serialize());
        Assert.AreEqual(MathValue.DataType.Double, restored.Kind);
        Assert.AreEqual(CalculationAccuracy.Unverified, restored.Accuracy);
        Assert.AreEqual(input, restored.DoubleValue);
    }

    [TestMethod]
    public void TrustedExactDoubleCanOfferItsFractionWithoutCertifyingOrdinaryDoubles()
    {
        var ordinary = new MathValue(0.5);
        Assert.IsNull(Calculator.FormatResult(ordinary).FractionRepresentation);
        var trusted = ordinary.WithAccuracy(CalculationAccuracy.Exact);
        Assert.AreEqual("1/2", Calculator.FormatResult(trusted).FractionRepresentation);
        Assert.IsTrue(new EvaluationResult(trusted, EvaluationContext.Empty).HasExactFraction);
        Assert.AreEqual(CalculationAccuracy.Unverified, (trusted + new MathValue(1)).Accuracy);
    }
}
