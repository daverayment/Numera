using System.Globalization;
using System.Numerics;

namespace Numera.Tests;

[TestClass]
public sealed class RationalPowerTests
{
    private static readonly CalculatorEngine Engine = CalculatorEngine.ForCulture(CultureInfo.InvariantCulture);

    [TestMethod]
    [DataRow("8^(1/3)", "2", MathValue.DataType.Integer)]
    [DataRow("(-8)^(1/3)", "-2", MathValue.DataType.Integer)]
    [DataRow("(-8)^(2/3)", "4", MathValue.DataType.Integer)]
    [DataRow("(-8)^(-1/3)", "-1/2", MathValue.DataType.Rational)]
    [DataRow("(-8)^(-2/3)", "1/4", MathValue.DataType.Rational)]
    [DataRow("(16/81)^(3/4)", "8/27", MathValue.DataType.Rational)]
    [DataRow("(16/81)^(-3/4)", "27/8", MathValue.DataType.Rational)]
    [DataRow("(-8/27)^(2/3)", "4/9", MathValue.DataType.Rational)]
    [DataRow("(-8)^(2/6)", "-2", MathValue.DataType.Integer)]
    [DataRow("(-8)^(-2/-6)", "-2", MathValue.DataType.Integer)]
    [DataRow("(-32)^0.2", "-2", MathValue.DataType.Decimal)]
    [DataRow("4^0.5", "2", MathValue.DataType.Decimal)]
    [DataRow("8.0^(1/3)", "2", MathValue.DataType.Integer)]
    [DataRow("0.0625^0.5", "0.25", MathValue.DataType.Decimal)]
    [DataRow("(1/16)^0.5", "1/4", MathValue.DataType.Rational)]
    [DataRow("0^(2/3)", "0", MathValue.DataType.Integer)]
    [DataRow("0.0^0.2", "0", MathValue.DataType.Decimal)]
    [DataRow("(1e400)^(1/2)", "1e200", MathValue.DataType.Integer)]
    [DataRow("(-1e-600)^(1/3)", "-1e-200", MathValue.DataType.Rational)]
    [DataRow("(1e-800)^(1/4)", "1e-200", MathValue.DataType.Rational)]
    public void RecognizedPowersAreExactAcrossEntryPoints(string expression, string expected, MathValue.DataType kind)
    {
        var tree = Engine.Parse(expression);
        var result = Engine.Evaluate(tree);

        Assert.AreEqual(Engine.Evaluate(expected).Value.GetExactFraction(), result.GetExactFraction(), expression);
        Assert.AreEqual(kind, result.Kind, expression);
        Assert.AreEqual(CalculationExactness.Exact, result.Exactness);
        Assert.IsTrue(result.HasExactFraction);
        Assert.AreEqual(result.Value, Engine.Evaluate(expression).Value);
    }

    [TestMethod]
    [DataRow("(-8)^(1/2)")]
    [DataRow("(-8)^(2/4)")]
    [DataRow("(-8)^0.3333333333333333333333333333")]
    [DataRow("pow(-8; 1/2)")]
    public void NonrealPowersReportTheOriginalPowerSpan(string expression)
    {
        Assert.IsFalse(Engine.TryEvaluate(expression, out _, out var diagnostic));
        Assert.AreEqual(EvaluationError.UndefinedMathResult, diagnostic.Code);
        Assert.IsTrue(diagnostic.Message.Contains("odd denominator", StringComparison.Ordinal));
        Assert.IsNotNull(diagnostic.Position);
        Assert.IsGreaterThan(0, diagnostic.Length);
        Assert.ThrowsExactly<ExpressionException>(() => Engine.Evaluate(expression));
    }

    [TestMethod]
    [DataRow("0^(-1/3)")]
    [DataRow("0^(-2/3)")]
    [DataRow("pow(0; -1/3)")]
    public void ZeroToANegativeRationalPowerReportsDivisionByZero(string expression)
    {
        Assert.IsFalse(Engine.TryEvaluate(expression, out _, out var diagnostic));
        Assert.AreEqual(EvaluationError.DivisionByZero, diagnostic.Code);
    }

    [TestMethod]
    [DataRow("(-2)^(1/3)", -1.2599210498948732)]
    [DataRow("(-2)^(2/3)", 1.5874010519681995)]
    [DataRow("(-2)^(-1/3)", -0.7937005259840997)]
    [DataRow("(-2)^(-2/3)", 0.6299605249474366)]
    [DataRow("2^(1/2)", 1.4142135623730951)]
    [DataRow("2^(2/5)", 1.3195079107728942)]
    [DataRow("(2e400)^(1/2)", 1.414213562373095e200)]
    [DataRow("(2e-400)^(-1/2)", 7.071067811865475e199)]
    [DataRow("(-2e600)^(-1/3)", -7.937005259840997e-201)]
    [DataRow("(2e400)^(2/3)", 7.368062997280773e266)]
    [DataRow("(2e-400)^(2/3)", 3.419951893353394e-267)]
    [DataRow("(1+1e-400)^(1e400/3)", 1.3956124250860895)]
    [DataRow("(1-1e-400)^(1e400/3)", 0.7165313105737893)]
    [DataRow("(1+1e-400)^(-1e400/3)", 0.7165313105737893)]
    [DataRow("(1+1e-4800)^(1e4800/3)", 1.3956124250860895)]
    public void IrrationalPowersRetainRangeAndSign(string expression, double expected)
    {
        var result = Engine.Evaluate(expression);

        Assert.AreEqual(MathValue.DataType.Double, result.Kind);
        Assert.AreEqual(CalculationExactness.Unverified, result.Exactness);
        Assert.IsFalse(result.HasExactFraction);
        double tolerance = Math.Abs(expected) * 2e-13;
        Assert.AreEqual(expected, result.DoubleValue, tolerance, expression);
    }

    [TestMethod]
    public void NearOnePowersKeepAnEffectThatProjectingTheBaseWouldErase()
    {
        var magnitude = BigInteger.Pow(10, 400);
        var exponent = MathValue.FromFraction(magnitude, 3);
        var basis = MathValue.FromFraction(magnitude + 1, magnitude);

        Assert.AreEqual(1.0, basis.ToDouble());
        Assert.AreEqual(double.PositiveInfinity, exponent.ToDouble());
        double actual = MathValue.Pow(basis, exponent).DoubleValue;
        Assert.AreEqual(1.3956124250860895, actual, 5e-16);
    }

    [TestMethod]
    public void PerfectFractionPowersRoundTripAcrossSignsAndDegrees()
    {
        for (int degree = 2; degree <= 9; degree++)
        {
            for (int numerator = -3; numerator <= 3; numerator++)
            {
                if (numerator == 0)
                {
                    continue;
                }

                for (int denominator = 1; denominator <= 3; denominator++)
                {
                    var root = MathValue.FromFraction(numerator, denominator);
                    var basis = MathValue.Pow(root, new MathValue(degree));
                    if (degree % 2 == 0 && numerator < 0)
                    {
                        root = -root;
                    }

                    foreach (int power in new[] { -3, -1, 1, 2, 3 })
                    {
                        var expected = MathValue.Pow(root, new MathValue(power));
                        var actual = MathValue.Pow(basis, MathValue.FromFraction(power, degree));

                        Assert.AreEqual(
                            expected.GetExactFraction(),
                            actual.GetExactFraction(),
                            $"({numerator}/{denominator})^{degree}, power {power}/{degree}");

                        Assert.AreEqual(CalculationExactness.Exact, actual.Exactness);
                    }
                }
            }
        }
    }

    [TestMethod]
    public void CapturedExponentsAndPowUseTheSameNumericOperation()
    {
        var assignment = Engine.Evaluate("third = 1/3");
        foreach (string expression in new[] { "(-8)^third", "(-8)^ans", "pow(-8; third)", "pow(-8; ans)" })
        {
            var result = Engine.Evaluate(expression, assignment.Context);

            Assert.AreEqual(new MathValue(-2), result.Value, expression);
            Assert.AreEqual(CalculationExactness.Exact, result.Exactness);
        }

        Assert.AreEqual(
            new MathValue(-2),
            MathValue.Pow(new MathValue(-8), MathValue.FromFraction(1, 3)));
    }

    [TestMethod]
    public void ExactResultsInheritBothOperandsExactness()
    {
        foreach (bool unverifiedBase in new[] { false, true })
        {
            var basis = new MathValue(-8);
            var exponent = MathValue.FromFraction(1, 3);
            if (unverifiedBase)
            {
                basis = basis.WithExactness(CalculationExactness.Unverified);
            }
            else
            {
                exponent = exponent.WithExactness(CalculationExactness.Unverified);
            }

            var direct = MathValue.Pow(basis, exponent);
            var context = new EvaluationContext([new Variable("b", basis), new Variable("p", exponent)]);

            foreach (var result in new[]
            {
                direct,
                Engine.Evaluate("b^p", context).Value,
                Engine.Evaluate("pow(b; p)", context).Value
            })
            {
                Assert.AreEqual(new MathValue(-2), result);
                Assert.AreEqual(CalculationExactness.Unverified, result.Exactness);
                Assert.IsFalse(result.TryGetExactFraction(out _, out _));
            }
        }
    }

    [TestMethod]
    public void DoubleOperandsDoNotEnterTheExactDomain()
    {
        var result = MathValue.Pow(new MathValue(-8.0), MathValue.FromFraction(1, 3));
        Assert.AreEqual(-2.0, result.DoubleValue, 1e-15);
        Assert.AreEqual(CalculationExactness.Unverified, result.Exactness);
        Assert.IsFalse(result.TryGetExactFraction(out _, out _));

        var asserted = MathValue.Pow(
            new MathValue(8.0).WithExactness(CalculationExactness.Exact),
            MathValue.FromFraction(1, 3));
        Assert.AreEqual(MathValue.DataType.Double, asserted.Kind);
        Assert.AreEqual(CalculationExactness.Unverified, asserted.Exactness);

        var doubleExponent = new MathValue(1.0 / 3);
        Assert.ThrowsExactly<ExpressionException>(
            () => MathValue.Pow(new MathValue(-8), doubleExponent));

        var approximate = MathValue.Pow(new MathValue(8), doubleExponent);
        Assert.AreEqual(MathValue.DataType.Double, approximate.Kind);
        Assert.AreEqual(CalculationExactness.Unverified, approximate.Exactness);
    }

    [TestMethod]
    public void HugeDegreesAndNumeratorsDoNotRequireNarrowingOrUnboundedPowers()
    {
        var huge = BigInteger.Pow(10, 400);
        var tinyPower = MathValue.FromFraction(1, huge + 1);
        Assert.AreEqual(new MathValue(-1), MathValue.Pow(new MathValue(-1), tinyPower));
        Assert.AreEqual(new MathValue(0), MathValue.Pow(new MathValue(0), tinyPower));
        Assert.AreEqual(1.0, MathValue.Pow(new MathValue(2), tinyPower).DoubleValue);
        Assert.AreEqual(-1.0, MathValue.Pow(new MathValue(-2), tinyPower).DoubleValue);

        var hugePower = MathValue.FromFraction(huge + 1, 3);
        Assert.AreEqual(new MathValue(-1), MathValue.Pow(new MathValue(-1), hugePower));
        var exception = Assert.ThrowsExactly<CalculationLimitException>(() => MathValue.Pow(new MathValue(8), hugePower));
        Assert.AreEqual(CalculationLimitKind.IntegerBits, exception.Diagnostic.LimitKind);
        Assert.AreEqual((long)CalculationLimits.MaxIntegerBits, exception.Diagnostic.LimitValue);
        Assert.AreEqual(double.PositiveInfinity, MathValue.Pow(new MathValue(2), hugePower).DoubleValue);
        Assert.AreEqual(double.NegativeInfinity, MathValue.Pow(new MathValue(-2), hugePower).DoubleValue);
        Assert.AreEqual(0.0, MathValue.Pow(new MathValue(2), -hugePower).DoubleValue);

        var nearOnePower = MathValue.FromFraction(huge + 1, huge + 3);
        var nearOneResult = MathValue.Pow(new MathValue(-2), nearOnePower);
        Assert.AreEqual(-2.0, nearOneResult.DoubleValue);
        Assert.AreEqual(CalculationExactness.Unverified, nearOneResult.Exactness);

        var evenDenominator = MathValue.FromFraction(huge + 1, huge + 2);
        Assert.ThrowsExactly<ExpressionException>(() => MathValue.Pow(new MathValue(-2), evenDenominator));
    }

    [TestMethod]
    [DataRow(2147483647L)]
    [DataRow(2147483648L)]
    [DataRow(2147483649L)]
    public void RootDegreesAcrossTheIntegerBoundaryRemainBounded(long degree)
    {
        var result = MathValue.Pow(new MathValue(2), MathValue.FromFraction(1, degree));
        Assert.AreEqual(Math.Pow(2, 1.0 / degree), result.DoubleValue, 5e-16);
        Assert.AreEqual(CalculationExactness.Unverified, result.Exactness);
    }

    [TestMethod]
    public void ApproximateOverflowAndUnderflowKeepTheRealSign()
    {
        var overflow = Engine.Evaluate("(-2e2000)^(5/3)");
        Assert.AreEqual(double.NegativeInfinity, overflow.DoubleValue);

        var underflow = Engine.Evaluate("(-2e-2000)^(5/3)");
        Assert.AreEqual(BitConverter.DoubleToInt64Bits(-0.0), BitConverter.DoubleToInt64Bits(underflow.DoubleValue));
        Assert.AreEqual(CalculationExactness.Unverified, underflow.Exactness);
        Assert.IsFalse(underflow.HasExactFraction);

        var fraction = Engine.Evaluate("(-8)^(-1/3)");
        Assert.AreEqual("-0.5", fraction.PrimaryDisplay);
        Assert.AreEqual("-1/2", fraction.FormatResult().FractionRepresentation);
    }

    [TestMethod]
    public void RationalPowerWorkUsesBitLimitsNotTheIntegerExponentCeiling()
    {
        var engine = Engine.WithOptions(options => options with { MaxExponentMagnitude = 1 });
        Assert.AreEqual(new MathValue(512), engine.Evaluate("64^(3/2)").Value);
        Assert.AreEqual(new MathValue(512), MathValue.Pow(new MathValue(64), MathValue.FromFraction(3, 2), 1));
        Assert.IsFalse(engine.TryEvaluate("2^2", out _, out var diagnostic));
        Assert.AreEqual(CalculationLimitKind.ExponentMagnitude, diagnostic.LimitKind);
        Assert.AreEqual(new MathValue(BigInteger.One << 10001), Engine.Evaluate("4^(10001/2)").Value);
        Assert.AreEqual(new MathValue(BigInteger.One << 16383), Engine.Evaluate("4^(16383/2)").Value);
        Assert.IsFalse(Engine.TryEvaluate("4^(16385/2)", out _, out diagnostic));
        Assert.AreEqual(CalculationLimitKind.IntegerBits, diagnostic.LimitKind);
        Assert.AreEqual((long)CalculationLimits.MaxIntegerBits, diagnostic.LimitValue);
        Assert.IsFalse(Engine.TryEvaluate("9^(12001/2)", out _, out diagnostic));
        Assert.AreEqual(CalculationLimitKind.IntegerBits, diagnostic.LimitKind);

        var smallEngine = Engine.WithOptions(options => options with { MaxIntegerBits = 8 });
        Assert.AreEqual(new MathValue(128), smallEngine.Evaluate("4^(7/2)").Value);
        Assert.IsFalse(smallEngine.TryEvaluate("4^(9/2)", out _, out diagnostic));
        Assert.AreEqual(CalculationLimitKind.IntegerBits, diagnostic.LimitKind);
        Assert.AreEqual(8L, diagnostic.LimitValue);
    }

    [TestMethod]
    public void RootFunctionsRetainTheirDomainRepresentationAndExactness()
    {
        foreach (string expression in new[] { "sqrt(16.0)", "cbrt(64.0)", "root(256.0; 4)", "nthroot(256.0; 4)" })
        {
            var result = Engine.Evaluate(expression);
            Assert.AreEqual(MathValue.DataType.Decimal, result.Kind);
            Assert.AreEqual(new MathValue(4), result.Value);
            Assert.AreEqual(CalculationExactness.Exact, result.Exactness);
        }

        var context = new EvaluationContext([new Variable("degree", new MathValue(3.0))]);
        var approximateDegree = Engine.Evaluate("root(8; degree)", context);
        Assert.AreEqual(new MathValue(2), approximateDegree.Value);
        Assert.AreEqual(CalculationExactness.Unverified, approximateDegree.Exactness);
        Assert.IsFalse(Engine.TryEvaluate("sqrt(-8)", out _, out var diagnostic));
        Assert.AreEqual(EvaluationError.UndefinedMathResult, diagnostic.Code);
    }

    [TestMethod]
    public void NonfiniteOperandsAndIntegerSpecialCasesKeepTheirExistingSemantics()
    {
        foreach (double basis in new[] { double.PositiveInfinity, double.NegativeInfinity, double.NaN })
        {
            foreach (var exponent in new[] { MathValue.FromFraction(1, 3), MathValue.FromFraction(-1, 3), new MathValue(double.NaN) })
            {
                Assert.AreEqual(Math.Pow(basis, exponent.ToDouble()), MathValue.Pow(new MathValue(basis), exponent).DoubleValue);
            }
        }

        Assert.AreEqual(new MathValue(1), Engine.Evaluate("0^0").Value);
        foreach (double exponent in new[] { double.PositiveInfinity, double.NegativeInfinity, double.NaN })
        {
            Assert.AreEqual(Math.Pow(-8, exponent), MathValue.Pow(new MathValue(-8), new MathValue(exponent)).DoubleValue);
        }
    }
}
