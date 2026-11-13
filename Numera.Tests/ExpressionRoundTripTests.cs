using System.Globalization;
using System.Numerics;
using Numera.Ast;

namespace Numera.Tests;

[TestClass]
public sealed class ExpressionRoundTripTests
{
    private static readonly CalculatorEngine Engine = new(
        CalculatorOptions.ForCulture(CultureInfo.InvariantCulture) with { Normalization = null });

    [TestMethod]
    [DataRow("12345678901234567890123456789")]
    [DataRow("1267650600228229401496703205376")]
    [DataRow("10000000000000000000000001")]
    [DataRow("-12345678901234567890123456789")]
    [DataRow("0.0000000000000000000000000001")]
    [DataRow("0.0000000000000000000000000012")]
    [DataRow("-0.0000000000000000000000000012")]
    [DataRow("123.4500")]
    [DataRow("0")]
    public void ExactLiteralValuesSurviveAsciiFormattingAtEveryThreshold(string expression)
    {
        var original = Engine.Parse(expression);
        var expected = Engine.Evaluate(original);
        foreach (int threshold in new[] { 1, 2, 24, CalculationLimits.MaxSignificantDigits })
        {
            var options = ExpressionFormatOptions.Ascii with { MaxFixedDigits = threshold };
            string text = Engine.FormatExpression(original, options);
            var restored = Engine.Evaluate(text);
            Assert.AreEqual(expected.Value, restored.Value, $"{expression}, threshold {threshold}: {text}");
            Assert.AreEqual(CalculationExactness.Exact, restored.Exactness);
        }
    }

    [TestMethod]
    public void LosslessScientificNotationRetainsAllSignificantDigitsAndCompressesOnlyZeros()
    {
        var beyondPowerOfTen = new NumberNode(new MathValue(BigInteger.Pow(10, 25) + 1));
        Assert.AreEqual("1." + new string('0', 24) + "1E25", Engine.FormatExpression(beyondPowerOfTen, ExpressionFormatOptions.Ascii));

        var powerOfTwo = new NumberNode(new MathValue(BigInteger.One << 100));
        Assert.AreEqual("1.267650600228229401496703205376E30", Engine.FormatExpression(powerOfTwo, ExpressionFormatOptions.Ascii));

        var powerOfTen = new NumberNode(new MathValue(BigInteger.Pow(10, 1000)));
        Assert.AreEqual("1E1000", Engine.FormatExpression(powerOfTen, ExpressionFormatOptions.Ascii));

        var largeValue = new NumberNode(new MathValue((BigInteger.One << 2000) + 1));
        string text = Engine.FormatExpression(largeValue, ExpressionFormatOptions.Ascii);
        Assert.AreEqual(largeValue.Value, Engine.Evaluate(text).Value);
    }

    [TestMethod]
    public void RationalNumberNodesAlwaysUseReducedParenthesizedFractions()
    {
        var large = BigInteger.Pow(10, 100);
        foreach (var value in new[]
        {
            MathValue.FromFraction(1, 2),
            MathValue.FromFraction(1, 3),
            MathValue.FromFraction(1, BigInteger.Pow(10, 25)),
            MathValue.FromFraction(-1, BigInteger.Pow(10, 25)),
            MathValue.FromFraction(large + 1, large - 1),
        })
        {
            Assert.AreEqual(MathValue.DataType.Rational, value.Kind);
            string expected = $"({value.Numerator.ToString(CultureInfo.InvariantCulture)}/{value.Denominator.ToString(CultureInfo.InvariantCulture)})";
            foreach (int threshold in new[] { 1, 24, 1000 })
            {
                string text = Engine.FormatExpression(new NumberNode(value), ExpressionFormatOptions.Ascii with { MaxFixedDigits = threshold });
                Assert.AreEqual(expected, text);
                Assert.AreEqual(value, Engine.Evaluate(text).Value);
            }
        }
    }

    [TestMethod]
    [DataRow(0.0)]
    [DataRow(-2.0)]
    [DataRow(0.1)]
    [DataRow(-0.1)]
    [DataRow(0.00001)]
    [DataRow(1e100)]
    [DataRow(double.MaxValue)]
    [DataRow(double.Epsilon)]
    public void FiniteDoubleLiteralsPreserveTheirStoredValueNotTheirExactness(double number)
    {
        var value = new MathValue(number);
        var literal = new NumberNode(value);
        string text = Engine.FormatExpression(literal, ExpressionFormatOptions.Ascii);
        var restored = Engine.Evaluate(text);
        Assert.AreEqual(value, restored.Value);
        Assert.AreEqual(CalculationExactness.Unverified, value.Exactness);
        Assert.AreEqual(CalculationExactness.Exact, restored.Exactness);
    }

    [TestMethod]
    [DataRow(0.00001, "1E-05")]
    [DataRow(-0.00001, "-1E-05")]
    [DataRow(1e100, "1E+100")]
    [DataRow(0.1, "0.1")]
    public void TypographicDoubleLiteralsRetainNativeGeneralNotation(double number, string expected)
    {
        var options = ExpressionFormatOptions.Typographic with { UseUnicodeMinus = false };
        var literal = new NumberNode(new MathValue(number));
        Assert.AreEqual(expected, Engine.FormatExpression(literal, options));
        Assert.AreEqual(expected, Engine.FormatExpression(literal, options with { MaxFixedDigits = 1 }));
        Assert.AreEqual(expected, Engine.FormatExpression(literal, options with { MaxFixedDigits = 1000 }));
    }

    [TestMethod]
    public void DoubleLiteralExportDoesNotPromiseDoubleArithmeticSemantics()
    {
        var tree = new BinaryNode(BinaryOperator.Add, new NumberNode(new MathValue(0.1)), new NumberNode(new MathValue(0.2)));
        var original = Engine.Evaluate(tree);
        string text = Engine.FormatExpression(tree, ExpressionFormatOptions.Ascii);
        var restored = Engine.Evaluate(text);
        Assert.AreEqual(CalculationExactness.Unverified, original.Exactness);
        Assert.AreEqual(CalculationExactness.Exact, restored.Exactness);
        Assert.AreNotEqual(original.Value, restored.Value);
    }

    [TestMethod]
    public void PresentationStylesStillRoundLargeExactLiteralsToTwentyFourDigits()
    {
        var literal = new NumberNode(new MathValue(BigInteger.One << 100));
        Assert.AreEqual("1.2676506002282294014967 \u00D7 10^30", Engine.FormatExpression(literal, ExpressionFormatOptions.Typographic));
        Assert.AreEqual(@"1.2676506002282294014967 \times 10^{30}", Engine.FormatExpression(literal, ExpressionFormatOptions.LaTeX));
        Assert.AreEqual(24, ResultFormatOptions.Default.SignificantDigits);
        Assert.AreEqual(24, ResultFormatOptions.Default.MaxFixedDigits);
    }

    [TestMethod]
    public void FractionAndScientificNumberNodesRemainAtomicInPowersAndPercentages()
    {
        foreach (var value in new[]
        {
            MathValue.FromFraction(1, 3),
            MathValue.FromFraction(-1, 3),
            new MathValue(0.1),
            new MathValue(-2.0),
            new MathValue(-2m),
            new MathValue(-(BigInteger.One << 100)),
            new MathValue(BigInteger.Pow(10, 25) + 1),
        })
        {
            var literal = new NumberNode(value);
            var power = new BinaryNode(BinaryOperator.Power, literal, new NumberNode(new MathValue(2)));
            string powerText = Engine.FormatExpression(power, ExpressionFormatOptions.Ascii);
            var restoredLiteral = Engine.Evaluate(Engine.FormatExpression(literal, ExpressionFormatOptions.Ascii)).Value;
            var exactPower = new BinaryNode(BinaryOperator.Power, new NumberNode(restoredLiteral), new NumberNode(new MathValue(2)));
            Assert.AreEqual(Engine.Evaluate(exactPower).Value, Engine.Evaluate(powerText).Value, powerText);

            var percent = new UnaryNode(UnaryOperator.Percent, literal);
            string percentText = Engine.FormatExpression(percent, ExpressionFormatOptions.Ascii);
            Assert.AreEqual(restoredLiteral / new MathValue(100), Engine.Evaluate(percentText).Value, percentText);
        }
    }

    [TestMethod]
    public void OfOnTheLeftOfJuxtapositionPreservesGroupingInEveryStyle()
    {
        var context = new EvaluationContext([new Variable("x", new MathValue(2))]);
        const string expression = "(10% of 200)x";
        string ascii = Engine.FormatExpression(expression, ExpressionFormatOptions.Ascii, context);
        Assert.AreEqual("(10% of 200) x", ascii);
        Assert.AreEqual(Engine.Parse(expression, context), Engine.Parse(ascii, context));
        Assert.AreEqual("(10% of 200) x", Engine.FormatExpression(expression, ExpressionFormatOptions.Typographic, context));
        Assert.AreEqual(@"(10\% \mathbin{\text{of}} 200) x", Engine.FormatExpression(expression, ExpressionFormatOptions.LaTeX, context));
    }

    [TestMethod]
    public void OfGroupingPreventsPrematureOverflowDuringReevaluation()
    {
        var context = new EvaluationContext([new Variable("x", new MathValue(2.0))]);
        const string expression = "(50% of 1e308)x";
        var original = Engine.Evaluate(expression, context);
        string text = Engine.FormatExpression(expression, ExpressionFormatOptions.Ascii, context);
        var restored = Engine.Evaluate(text, context);
        Assert.AreEqual("(50% of 1E308) x", text);
        Assert.IsTrue(double.IsFinite(original.AsDouble()));
        Assert.IsTrue(double.IsFinite(restored.AsDouble()));
        Assert.AreEqual(original.Value, restored.Value);
    }

    [TestMethod]
    [DataRow("!(1 == 1)")]
    [DataRow("!(1 < 2 < 3)")]
    [DataRow("!!(1 == 0) && 1")]
    public void LogicalComparisonNegationRoundTripsWithStrictParsing(string expression)
    {
        var strict = new CalculatorEngine(Engine.Options with { RequireExplicitLogicalNegation = true });
        var original = strict.Parse(expression);
        string text = strict.FormatExpression(original, ExpressionFormatOptions.Ascii);
        Assert.AreEqual(original, strict.Parse(text));
        Assert.AreEqual(strict.Evaluate(original).Value, strict.Evaluate(text).Value);
    }

    [TestMethod]
    public void FormattingDoesNotExecuteCallbacksOrAssignments()
    {
        int calls = 0;
        var engine = new CalculatorEngine(Engine.Options with
        {
            CustomFunctions = [CustomFunction.Create("tick", () =>
            {
                calls++;
                return new MathValue(1);
            })]
        });
        const string expression = "x = 12345678901234567890123456789 + tick()";
        var context = new EvaluationContext([new Variable("x", new MathValue(7))]);
        var tree = engine.Parse(expression, context);
        string text = engine.FormatExpression(tree, ExpressionFormatOptions.Ascii);
        Assert.AreEqual(0, calls);
        Assert.AreEqual(new MathValue(7), context.Variables["x"].Value);

        var original = engine.Evaluate(tree, context);
        var restored = engine.Evaluate(text, context);
        Assert.AreEqual(2, calls);
        Assert.AreEqual(original.Value, restored.Value);
        Assert.AreEqual(original.Context.Variables["x"].Value, restored.Context.Variables["x"].Value);
    }
}
