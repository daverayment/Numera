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
    public void MathJaxSignedNumberNodesKeepTheirGroupingInPowersAndUnaryOperators()
    {
        var cases = new (MathValue Value, string GroupedText)[]
        {
            (new MathValue(-5), "(-5)"),
            (new MathValue(-2m), "(-2)"),
            (new MathValue(-0.1), "(-0.1)"),
            (new MathValue(-0.0), "(-0)"),
            (MathValue.FromFraction(-1, 3), @"\left(\frac{-1}{3}\right)"),
            (new MathValue(-BigInteger.Pow(10, 30)), @"(-1 \times 10^{30})"),
            (new MathValue(-0.00001), @"(-1 \times 10^{-5})"),
        };

        foreach (var (value, groupedText) in cases)
        {
            var literal = new NumberNode(value);
            var two = new NumberNode(new MathValue(2));
            Assert.AreEqual($"{groupedText}^{{2}}",
                Engine.FormatExpression(new BinaryNode(BinaryOperator.Power, literal, two), ExpressionFormatOptions.LaTeX));
            Assert.AreEqual($"-{groupedText}",
                Engine.FormatExpression(new UnaryNode(UnaryOperator.Negate, literal), ExpressionFormatOptions.LaTeX));
            Assert.AreEqual($"+{groupedText}",
                Engine.FormatExpression(new UnaryNode(UnaryOperator.Positive, literal), ExpressionFormatOptions.LaTeX));
            Assert.AreEqual($"{groupedText}!",
                Engine.FormatExpression(new UnaryNode(UnaryOperator.Factorial, literal), ExpressionFormatOptions.LaTeX));
            Assert.AreEqual($@"{groupedText}\%",
                Engine.FormatExpression(new UnaryNode(UnaryOperator.Percent, literal), ExpressionFormatOptions.LaTeX));
        }

        var negativeExponent = new BinaryNode(BinaryOperator.Power,
            new NumberNode(new MathValue(2)), new NumberNode(new MathValue(-5)));
        Assert.AreEqual("2^{-5}", Engine.FormatExpression(negativeExponent, ExpressionFormatOptions.LaTeX));
        Assert.AreEqual("2^{2}", Engine.FormatExpression(
            new BinaryNode(BinaryOperator.Power, new NumberNode(new MathValue(2)), new NumberNode(new MathValue(2))),
            ExpressionFormatOptions.LaTeX));
    }

    [TestMethod]
    [DataRow(1, 2)]
    [DataRow(1, 3)]
    [DataRow(-1, 2)]
    public void MathJaxRationalPowerBasesMatchDivisionTreeGrouping(int numerator, int denominator)
    {
        var literal = new NumberNode(MathValue.FromFraction(numerator, denominator));
        var division = new BinaryNode(BinaryOperator.Divide,
            new NumberNode(new MathValue(numerator)), new NumberNode(new MathValue(denominator)));
        var two = new NumberNode(new MathValue(2));
        string fraction = $@"\frac{{{numerator}}}{{{denominator}}}";
        string expected = $@"\left({fraction}\right)^{{2}}";

        Assert.AreEqual(expected, Engine.FormatExpression(
            new BinaryNode(BinaryOperator.Power, literal, two), ExpressionFormatOptions.LaTeX));
        Assert.AreEqual(expected, Engine.FormatExpression(
            new BinaryNode(BinaryOperator.Power, division, two), ExpressionFormatOptions.LaTeX));
        Assert.AreEqual($"2^{{{fraction}}}", Engine.FormatExpression(
            new BinaryNode(BinaryOperator.Power, two, literal), ExpressionFormatOptions.LaTeX));
        if (numerator > 0)
        {
            Assert.AreEqual($@"{fraction}\%", Engine.FormatExpression(
                new UnaryNode(UnaryOperator.Percent, literal), ExpressionFormatOptions.LaTeX));
        }
    }

    [TestMethod]
    [DataRow("asin", "arcsin", @"\arcsin(0.5)")]
    [DataRow("acos", "arccos", @"\arccos(0.5)")]
    [DataRow("atan", "arctan", @"\arctan(0.5)")]
    public void MathJaxInverseTrigonometricAliasesUseTheSameStandardCommand(
        string shortName, string longName, string expected)
    {
        foreach (string name in new[] { shortName, longName, longName.ToUpperInvariant() })
        {
            string expression = $"{name}(0.5)";
            Assert.AreEqual(expected, Engine.FormatExpression(expression, ExpressionFormatOptions.LaTeX));
            Assert.AreEqual(expected, Engine.FormatExpression(Engine.Parse(expression), ExpressionFormatOptions.LaTeX));
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

    [TestMethod]
    [DataRow("a + (b + c)", "a + (b + c)", "a + (b + c)", "a + (b + c)")]
    [DataRow("a * (b * c)", "a * (b * c)", "a \u00D7 (b \u00D7 c)", @"a \cdot (b \cdot c)")]
    [DataRow("2(3x)", "2 (3 x)", "2 (3 x)", "2 (3 x)")]
    [DataRow("2(3*x)", "2 (3 * x)", "2 (3 \u00D7 x)", @"2 (3 \cdot x)")]
    [DataRow("a + b + c", "a + b + c", "a + b + c", "a + b + c")]
    [DataRow("a * b * c", "a * b * c", "a \u00D7 b \u00D7 c", @"a \cdot b \cdot c")]
    [DataRow("a && (b && c)", "a && b && c", "a && b && c", @"a \land b \land c")]
    public void NumericRightGroupingAndUnclutteredChainsAreConsistentAcrossStyles(
        string expression, string ascii, string typographic, string latex)
    {
        var context = new EvaluationContext([
            new Variable("a", new MathValue(12)),
            new Variable("b", new MathValue(4)),
            new Variable("c", new MathValue(2)),
            new Variable("x", new MathValue(5)),
        ]);
        Assert.AreEqual(ascii, Engine.FormatExpression(expression, ExpressionFormatOptions.Ascii, context));
        Assert.AreEqual(typographic, Engine.FormatExpression(expression, ExpressionFormatOptions.Typographic, context));
        Assert.AreEqual(latex, Engine.FormatExpression(expression, ExpressionFormatOptions.LaTeX, context));
        Assert.AreEqual(Engine.Evaluate(expression, context).Value, Engine.Evaluate(ascii, context).Value);
    }

    [TestMethod]
    [DataRow("200 + (10 + 5%)", "210.5")]
    [DataRow("100 + (10% + 20%)", "100.3")]
    [DataRow("10% + (20% + 30%)", "0.6")]
    [DataRow("-10% + (20% + 30%)", "0.4")]
    [DataRow("200 + (10 + -5%)", "209.5")]
    [DataRow("200 + (10 + 5% + 5%)", "211.025")]
    [DataRow("100 + (20 + (10 + 5%))", "130.5")]
    [DataRow("200 * (10% + 20%)", "60")]
    [DataRow("100 + 10% + 20%", "132")]
    [DataRow("x = 200 + (10 + 5%)", "210.5")]
    public void PercentageGroupingPreservesValuesAndAssignments(string expression, string expected)
    {
        var tree = Engine.Parse(expression);
        var original = Engine.Evaluate(tree);
        Assert.AreEqual(new MathValue(decimal.Parse(expected, CultureInfo.InvariantCulture)), original.Value);
        string text = Engine.FormatExpression(tree, ExpressionFormatOptions.Ascii);
        var restoredTree = Engine.Parse(text);
        Assert.AreEqual(tree, restoredTree);
        var restored = Engine.Evaluate(restoredTree);
        Assert.AreEqual(original.Value, restored.Value);
        Assert.AreEqual(CalculationExactness.Exact, restored.Exactness);
        Assert.AreEqual(original.AssignedVariables.Count, restored.AssignedVariables.Count);
        foreach (var assignment in original.AssignedVariables)
        {
            Assert.AreEqual(assignment.Value, restored.Context.Variables[assignment.Name].Value);
        }
    }

    [TestMethod]
    [DataRow("a + (b + c)", "a + b + c", 1e16, -1e16, 1.0, 1.0)]
    [DataRow("a * (b * c)", "a * b * c", 1e308, 1e308, 1e-308, double.PositiveInfinity)]
    [DataRow("a * (b * c)", "a * b * c", 1e-308, 1e-308, 1e308, 0.0)]
    public void SuppliedDoubleBindingsRetainRoundingOverflowAndUnderflowBehavior(
        string expression, string regroupedExpression, double a, double b, double c, double regroupedValue)
    {
        var context = new EvaluationContext([
            new Variable("a", new MathValue(a)),
            new Variable("b", new MathValue(b)),
            new Variable("c", new MathValue(c)),
        ]);
        var original = Engine.Evaluate(expression, context);
        Assert.IsTrue(double.IsFinite(original.DoubleValue));

        var regrouped = Engine.Evaluate(regroupedExpression, context);
        Assert.AreEqual(regroupedValue, regrouped.DoubleValue);
        Assert.AreNotEqual(original.Value, regrouped.Value);

        string text = Engine.FormatExpression(expression, ExpressionFormatOptions.Ascii, context);
        Assert.AreEqual(expression, text);

        var restored = Engine.Evaluate(text, context);
        Assert.AreEqual(original.Value, restored.Value);
        Assert.AreEqual(CalculationExactness.Unverified, restored.Exactness);
    }

    [TestMethod]
    [DataRow(BinaryOperator.Add, true)]
    [DataRow(BinaryOperator.Add, false)]
    [DataRow(BinaryOperator.Multiply, true)]
    [DataRow(BinaryOperator.Multiply, false)]
    public void RightGroupingPreservesIntermediateLimitSuccessAndFailure(BinaryOperator operation, bool groupedSucceeds)
    {
        var limited = new CalculatorEngine(Engine.Options with { MaxIntegerBits = 7 });
        MathValue[] values;
        string symbol;
        if (operation == BinaryOperator.Add)
        {
            symbol = "+";
            if (groupedSucceeds)
            {
                values = [new MathValue(127), new MathValue(1), new MathValue(-1)];
            }
            else
            {
                values = [new MathValue(-127), new MathValue(127), new MathValue(1)];
            }
        }
        else if (operation == BinaryOperator.Multiply)
        {
            symbol = "*";
            if (groupedSucceeds)
            {
                values = [new MathValue(127), new MathValue(127), MathValue.FromFraction(1, 127)];
            }
            else
            {
                values = [MathValue.FromFraction(1, 127), new MathValue(127), new MathValue(127)];
            }
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        var context = new EvaluationContext([
            new Variable("a", values[0]),
            new Variable("b", values[1]),
            new Variable("c", values[2]),
        ]);
        string expression = $"a {symbol} (b {symbol} c)";
        string text = limited.FormatExpression(expression, ExpressionFormatOptions.Ascii, context);
        Assert.AreEqual(expression, text);
        Assert.AreEqual(groupedSucceeds, limited.TryEvaluate(expression, out var original, out var originalDiagnostic, context));
        Assert.AreEqual(groupedSucceeds, limited.TryEvaluate(text, out var restored, out var restoredDiagnostic, context));
        Assert.AreEqual(!groupedSucceeds, limited.TryEvaluate($"a {symbol} b {symbol} c", out _, out var regroupedDiagnostic, context));

        if (groupedSucceeds)
        {
            Assert.AreEqual(new MathValue(127), original.Value);
            Assert.AreEqual(original.Value, restored.Value);
            Assert.IsNotNull(regroupedDiagnostic);
            Assert.AreEqual(EvaluationError.LimitExceeded, regroupedDiagnostic.Code);
        }
        else
        {
            Assert.IsNotNull(originalDiagnostic);
            Assert.IsNotNull(restoredDiagnostic);
            Assert.AreEqual(EvaluationError.LimitExceeded, originalDiagnostic.Code);
            Assert.AreEqual(originalDiagnostic.Code, restoredDiagnostic.Code);
        }
    }
}
