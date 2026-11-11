using System.Numerics;
using Numera.Ast;

namespace Numera.Tests;

[TestClass]
public sealed class RationalEvaluationTests
{
    [TestMethod]
    [DataRow("(1/3)*3", 1, 1)]
    [DataRow("1/3+0.5", 5, 6)]
    [DataRow("0.1+0.2", 3, 10)]
    [DataRow("1/(2/3)", 3, 2)]
    [DataRow("(1/3)/(1/3)", 1, 1)]
    public void ArithmeticCarriesReducedExactFractions(string expression, int numerator, int denominator)
    {
        AssertFraction(Calculator.Evaluate(expression).Value, numerator, denominator);
    }

    [TestMethod]
    public void FiniteLiteralsBeyondDecimalAreNotRoundedOrUnderflowed()
    {
        const string digits = "123456789012345678901234567890123456789";
        AssertFraction(Calculator.Evaluate("0." + digits).Value,
            BigInteger.Parse(digits), BigInteger.Pow(10, digits.Length));
        AssertFraction(Calculator.Evaluate("1e-400").Value, BigInteger.One, BigInteger.Pow(10, 400));
        AssertFraction(Calculator.Evaluate("2.5e400").Value,
            25 * BigInteger.Pow(10, 399), BigInteger.One);
        AssertFraction(Calculator.Evaluate("0e9999999999999999999999999999999").Value, 0, 1);
        AssertFraction(Calculator.Evaluate("0e-9999999999999999999999999999999").Value, 0, 1);
    }

    [TestMethod]
    public void CapturedRationalVariablesAndAnsRetainValueAndExactness()
    {
        var engine = new CalculatorEngine();
        var assigned = engine.Evaluate("x=1/3");
        AssertFraction(engine.Evaluate("x*3", assigned.Context).Value, 1, 1);
        AssertFraction(engine.Evaluate("ans*3", assigned.Context).Value, 1, 1);
        var approximate = engine.Evaluate("x=pi");
        Assert.AreEqual(CalculationExactness.Unverified,
            engine.Evaluate("floor(x)", approximate.Context).Value.Exactness);
        Assert.AreEqual(CalculationExactness.Unverified,
            engine.Evaluate("ans-ans", approximate.Context).Value.Exactness);
    }

    [TestMethod]
    [DataRow("ceil(-7/3)", -2, 1)]
    [DataRow("floor(-7/3)", -3, 1)]
    [DataRow("trunc(-7/3)", -2, 1)]
    [DataRow("frac(-7/3)", -1, 3)]
    [DataRow("abs(-7/3)", 7, 3)]
    [DataRow("sign(-7/3)", -1, 1)]
    [DataRow("round(-5/2)", -3, 1)]
    [DataRow("round(1/8,2)", 13, 100)]
    [DataRow("round(-1/8,2)", -13, 100)]
    [DataRow("sum(1/3,1/3,1/3)", 1, 1)]
    [DataRow("avg(1/3,2/3)", 1, 2)]
    [DataRow("median(1/3,2/3)", 1, 2)]
    [DataRow("variance(1/3,2/3)", 1, 18)]
    public void BuiltinsUseRationalArithmetic(string expression, int numerator, int denominator)
    {
        AssertFraction(Calculator.Evaluate(expression).Value, numerator, denominator);
    }

    [TestMethod]
    [DataRow("sqrt(2)")]
    [DataRow("floor(sqrt(2))")]
    [DataRow("sign(pi)")]
    [DataRow("pi>3")]
    [DataRow("not pi")]
    [DataRow("round(sin(1))")]
    [DataRow("min(0,pi)")]
    [DataRow("ceil(pi)*0")]
    public void ApproximateInputsNeverCertifyIntegerOrSelectedOutputs(string expression)
    {
        Assert.AreEqual(CalculationExactness.Unverified, Calculator.Evaluate(expression).Value.Exactness);
    }

    [TestMethod]
    public void CallbacksValidateRationalComponentsAndPropagateUnverifiedArguments()
    {
        var engine = new CalculatorEngine(new CalculatorOptions
        {
            MaxIntegerBits = 8,
            CustomFunctions =
            [
                CustomFunction.Create("fraction", () => MathValue.FromFraction(1, 3)),
                CustomFunction.Create("oversized", () => MathValue.FromFraction(1, 257)),
                CustomFunction.Create("claimed", _ => new MathValue(1))
            ]
        });
        AssertFraction(engine.Evaluate("fraction()").Value, 1, 3);
        var exception = Assert.Throws<CalculationLimitException>(() => engine.Evaluate("oversized()"));
        Assert.AreEqual(EvaluationError.LimitExceeded, exception.Diagnostic.Code);
        Assert.AreEqual(0, exception.Diagnostic.Position);
        Assert.AreEqual(CalculationExactness.Unverified, engine.Evaluate("claimed(sqrt(2))").Value.Exactness);
    }

    [TestMethod]
    public void ConfiguredLimitsApplyToLiteralsPowersArithmeticAndCapturedValues()
    {
        var engine = new CalculatorEngine(new CalculatorOptions
        {
            MaxIntegerBits = 8,
            MaxExponentMagnitude = 3
        });
        foreach (var expression in new[] { "256", "1e-4", "2^4", "255*255", "1/257", "1e-3" })
        {
            Assert.Throws<CalculationLimitException>(() => engine.Evaluate(expression), expression);
        }
        AssertFraction(engine.Evaluate("2^3").Value, 8, 1);
        var context = new EvaluationContext(
            new[] { new Variable("x", MathValue.FromFraction(1, 257)) },
            MathValue.FromFraction(1, 257));
        Assert.Throws<CalculationLimitException>(() => engine.Evaluate("x", context));
        Assert.Throws<CalculationLimitException>(() => engine.Evaluate("ans", context));
        var tree = new NumberNode(MathValue.FromFraction(1, 257));
        Assert.Throws<CalculationLimitException>(() => engine.Evaluate(tree));
    }

    [TestMethod]
    [DataRow("(-2)^(1e400+0.5)")]
    [DataRow("(-2)^(-1e400-0.5)")]
    [DataRow("(-1e-400)^(1e400+0.5)")]
    public void HugeFiniteFractionalPowersReportDomainDiagnostics(string expression)
    {
        Assert.IsFalse(Calculator.TryEvaluate(expression, out _, out var diagnostic));
        Assert.AreEqual(EvaluationError.UndefinedMathResult, diagnostic.Code);
        Assert.IsNotNull(diagnostic.Position);
        Assert.IsTrue(diagnostic.Length > 0);
    }

    [TestMethod]
    public void IntermediateLimitIsIndependentOfStoredComponentLimits()
    {
        var engine = new CalculatorEngine(new CalculatorOptions
        {
            MaxIntegerBits = 8
        });
        AssertFraction(engine.Evaluate("255").Value, 255, 1);
        foreach (var expression in new[]
        {
            "255*255", "sum(255,1)", "mul(255,2)", "lcm(17,19)", "6!"
        })
        {
            var exception = Assert.Throws<CalculationLimitException>(() => engine.Evaluate(expression), expression);
            Assert.AreEqual(EvaluationError.LimitExceeded, exception.Diagnostic.Code);
            Assert.IsNotNull(exception.Diagnostic.Position);
        }
        AssertFraction(engine.Evaluate("round(255,28)").Value, 255, 1);
    }

    [TestMethod]
    public void MixedPrecedenceLeftSpinesEvaluateIterativelyWithoutRegrouping()
    {
        ExpressionNode tree = new NumberNode(new MathValue(1));
        for (int i = 0; i < 2000; i++)
        {
            tree = new BinaryNode(
                i % 2 == 0 ? BinaryOperator.Add : BinaryOperator.Multiply,
                tree, new NumberNode(new MathValue(1)));
        }
        AssertFraction(new CalculatorEngine().Evaluate(tree).Value, 1001, 1);
    }

    [TestMethod]
    public void ExactLiteralParsingStillHonorsConfiguredInputLength()
    {
        var engine = new CalculatorEngine(new CalculatorOptions { MaxInputLength = 3 });
        AssertFraction(engine.Evaluate(".25").Value, 1, 4);
        Assert.Throws<CalculationLimitException>(() => engine.Evaluate(".250"));
        Assert.Throws<CalculationLimitException>(() => engine.Evaluate("0e99"));
    }

    [TestMethod]
    [DataRow("isprime(2^127-1)")]
    [DataRow("divcount(2^127-1)")]
    [DataRow("divisorcount(2^127-1)")]
    [DataRow("sigma(2^127-1)")]
    [DataRow("divsum(2^127-1,0)")]
    [DataRow("totient(2^127-1)")]
    [DataRow("phi(2^127-1)")]
    [DataRow("eulerphi(2^127-1)")]
    public void ProbablePrimeBasedResultsDoNotAcquireAnExactGuarantee(string expression)
    {
        var engine = new CalculatorEngine();
        var result = engine.Evaluate(expression);
        Assert.AreEqual(CalculationExactness.Unverified, result.Exactness);
        Assert.IsFalse(result.HasExactFraction);
        Assert.IsNull(result.FormatResult().FractionRepresentation);
        Assert.AreEqual(CalculationExactness.Unverified, (result + 0).Exactness);
        Assert.AreEqual(CalculationExactness.Unverified, engine.Evaluate("ans", result.Context).Exactness);
    }

    [TestMethod]
    [DataRow("sqrt(1e-400)", "1e-200", MathValue.DataType.Rational)]
    [DataRow("sqrt(1e-400)*1e200", "1", MathValue.DataType.Integer)]
    [DataRow("cbrt(1e-600)", "1e-200", MathValue.DataType.Rational)]
    [DataRow("cbrt(-1e-600)", "-1e-200", MathValue.DataType.Rational)]
    [DataRow("root(1e-800,4)", "1e-200", MathValue.DataType.Rational)]
    [DataRow("nthroot(-1e-600,3)", "-1e-200", MathValue.DataType.Rational)]
    [DataRow("sqrt(4/9)", "2/3", MathValue.DataType.Rational)]
    [DataRow("sqrt(0.25)", "0.5", MathValue.DataType.Decimal)]
    [DataRow("cbrt(-0.125)", "-0.5", MathValue.DataType.Decimal)]
    [DataRow("root(16/81,4)", "2/3", MathValue.DataType.Rational)]
    [DataRow("root(1e-400,1)", "1e-400", MathValue.DataType.Rational)]
    [DataRow("sqrt(1e-4000)", "1e-2000", MathValue.DataType.Rational)]
    public void PerfectFractionRootsPreserveExactValuesAndDomains(
        string expression, string expectedExpression, MathValue.DataType expectedType)
    {
        var expected = Calculator.Evaluate(expectedExpression).GetExactFraction();
        var result = Calculator.Evaluate(expression);

        Assert.AreEqual(expectedType, result.Type);
        AssertFraction(result.Value, expected.Numerator, expected.Denominator);
    }

    [TestMethod]
    [DataRow("sqrt(-1e-400)")]
    [DataRow("root(-1e-400,2)")]
    [DataRow("root(-1e-400,-2)")]
    [DataRow("nthroot(-1e-400,4)")]
    [DataRow("root(-1e-400,0.5)")]
    [DataRow("root(1e-400,0)")]
    public void RootDomainsAreCheckedBeforeFloatingPointProjection(string expression)
    {
        Assert.IsFalse(Calculator.TryEvaluate(expression, out _, out var diagnostic));
        Assert.AreEqual(EvaluationError.UndefinedMathResult, diagnostic.Code);
        Assert.AreEqual(0, diagnostic.Position);
        Assert.AreEqual(expression.Length, diagnostic.Length);
    }

    [TestMethod]
    public void NonPerfectRootsPreserveMagnitudeOutsideTheDoubleInputRange()
    {
        (string Expression, double Expected)[] cases =
        [
            ("sqrt(2e-400)", Math.Sqrt(2) * 1e-200),
            ("cbrt(2e-600)", Math.Cbrt(2) * 1e-200),
            ("cbrt(-2e-600)", -Math.Cbrt(2) * 1e-200),
            ("root(2e-800,4)", Math.Pow(2, 0.25) * 1e-200),
            ("sqrt(2^2000+1)", Math.ScaleB(1, 1000)),
            ("sqrt((2^2000+1)/3)", Math.ScaleB(1 / Math.Sqrt(3), 1000)),
            ("cbrt(2e600)", Math.Cbrt(2) * 1e200),
            ("root(2e-400,-2)", 1 / Math.Sqrt(2) * 1e200),
            ("nthroot(-2e-600,-3)", -1 / Math.Cbrt(2) * 1e200),
            ("sqrt(1/(2^1075))", Math.ScaleB(Math.Sqrt(0.5), -537)),
            ("sqrt(3/(2^1076))", Math.ScaleB(Math.Sqrt(3), -538)),
            ("cbrt(3/(2^1076))", Math.ScaleB(Math.Cbrt(0.75), -358))
        ];

        foreach (var (expression, expected) in cases)
        {
            var result = Calculator.Evaluate(expression);
            Assert.AreEqual(MathValue.DataType.Double, result.Type, expression);
            Assert.AreEqual(CalculationExactness.Unverified, result.Exactness, expression);
            Assert.IsFalse(result.HasExactFraction, expression);
            Assert.AreEqual(expected, result.AsDouble(), Math.Abs(expected) * 3e-15, expression);
        }
    }

    [TestMethod]
    [DataRow("root(1/(2^1500),2.5)", -600)]
    [DataRow("root(2^1500,2.5)", 600)]
    public void FractionalDegreeRootsUseTheOriginalMagnitude(string expression, int expectedExponent)
    {
        var result = Calculator.Evaluate(expression);

        Assert.AreEqual(Math.ScaleB(1, expectedExponent), result.AsDouble());
        Assert.AreEqual(MathValue.DataType.Double, result.Type);
        Assert.AreEqual(CalculationExactness.Unverified, result.Exactness);
        Assert.IsFalse(result.HasExactFraction);
    }

    [TestMethod]
    [DataRow("stdev(0,2e-200)", 2)]
    [DataRow("stdev(-1e-200,0,1e-200)", 1)]
    public void StandardDeviationRetainsTinyVarianceMagnitudeWithoutCertifyingItsResult(
        string expression, int varianceCoefficient)
    {
        double expected = Math.Sqrt(varianceCoefficient) * 1e-200;
        var result = Calculator.Evaluate(expression);

        Assert.AreEqual(expected, result.AsDouble(), expected * 3e-15);
        Assert.AreEqual(MathValue.DataType.Double, result.Type);
        Assert.AreEqual(CalculationExactness.Unverified, result.Exactness);
        Assert.IsFalse(result.HasExactFraction);
    }

    [TestMethod]
    [DataRow("sqrt(2e-4000)", 0.0)]
    [DataRow("root(2e400,0.5)", double.PositiveInfinity)]
    public void ApproximateRootsStillRespectTheDoubleOutputRange(string expression, double expected)
    {
        var result = Calculator.Evaluate(expression);

        Assert.AreEqual(expected, result.AsDouble());
        Assert.AreEqual(MathValue.DataType.Double, result.Type);
        Assert.AreEqual(CalculationExactness.Unverified, result.Exactness);
        Assert.IsFalse(result.HasExactFraction);
    }

    [TestMethod]
    public void PerfectRootsOfUnverifiedInputsRemainUnverified()
    {
        var context = new EvaluationContext
        {
            Variables = [new Variable("x", MathValue.FromFraction(4, 9).WithExactness(CalculationExactness.Unverified))]
        };
        var result = Calculator.Evaluate("sqrt(x)", context);

        Assert.AreEqual(MathValue.FromFraction(2, 3), result.Value);
        Assert.AreEqual(CalculationExactness.Unverified, result.Exactness);
        Assert.IsFalse(result.HasExactFraction);
    }

    [TestMethod]
    public void RootDoubleInputsAndNonfiniteResultsKeepTheirApproximateSemantics()
    {
        var context = new EvaluationContext
        {
            Variables =
            [
                new Variable("x", new MathValue(0.25)),
                new Variable("negativeZero", new MathValue(-0.0)),
                new Variable("infinity", new MathValue(double.PositiveInfinity)),
                new Variable("nan", new MathValue(double.NaN))
            ]
        };

        var result = Calculator.Evaluate("sqrt(x)", context);
        Assert.AreEqual(MathValue.DataType.Double, result.Type);
        Assert.AreEqual(0.5, result.AsDouble());
        Assert.AreEqual(CalculationExactness.Unverified, result.Exactness);
        Assert.AreEqual(long.MinValue, BitConverter.DoubleToInt64Bits(Calculator.Evaluate("sqrt(negativeZero)", context).AsDouble()));
        Assert.AreEqual(double.PositiveInfinity, Calculator.Evaluate("sqrt(infinity)", context).AsDouble());
        Assert.IsTrue(Calculator.Evaluate("cbrt(nan)", context).IsNaN);
        Assert.AreEqual(double.PositiveInfinity, Calculator.Evaluate("root(0,-2)").AsDouble());
    }

    [TestMethod]
    public void DeterministicNumberTheoryAndCompositeWitnessesRemainExact()
    {
        foreach (var expression in new[] { "isprime(97)", "isprime(2^64)", "divcount(12)", "sigma(12)", "totient(12)" })
        {
            Assert.AreEqual(CalculationExactness.Exact, Calculator.Evaluate(expression).Exactness);
        }
    }

    private static void AssertFraction(MathValue value, BigInteger numerator, BigInteger denominator)
    {
        var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
        Assert.AreEqual(CalculationExactness.Exact, value.Exactness);
        Assert.IsTrue(value.TryGetExactFraction(out var actualNumerator, out var actualDenominator));
        Assert.AreEqual(numerator / gcd, actualNumerator);
        Assert.AreEqual(denominator / gcd, actualDenominator);
    }
}
