using System.Numerics;
using Numera.Ast;

namespace Numera.Tests;

[TestClass]
public sealed class PrematureDoubleConversionTests
{
    [TestMethod]
    [DataRow("ln(1e400)", 921.0340371976183)]
    [DataRow("ln(1e-400)", -921.0340371976183)]
    [DataRow("log(1e400)", 400.0)]
    [DataRow("log10(1e-400)", -400.0)]
    [DataRow("log2(2^2000)", 2000.0)]
    [DataRow("log2(1/(2^2000))", -2000.0)]
    [DataRow("logn(1e400,1e-400)", -1.0)]
    [DataRow("logn(2^2000,2)", 2000.0)]
    [DataRow("log1p(1e400)", 921.0340371976183)]
    [DataRow("log1p(-1+1e-400)", -921.0340371976183)]
    public void LogarithmsRetainMagnitudesOutsideDoubleRange(string expression, double expected)
    {
        AssertApproximation(Calculator.Evaluate(expression), expected, Math.Abs(expected) * 3e-15);
    }

    [TestMethod]
    [DataRow("ln(1+1e-30)", 1e-30)]
    [DataRow("ln(1-1e-30)", -1e-30)]
    [DataRow("log2(1+1e-30)", 1.4426950408889634e-30)]
    [DataRow("log10(1-1e-30)", -4.342944819032518e-31)]
    [DataRow("logn(1+2e-30,1+1e-30)", 2.0)]
    [DataRow("logn(1-2e-30,1-1e-30)", 2.0)]
    [DataRow("logn(1+2e-400,1+1e-400)", 2.0)]
    [DataRow("logn(1-2e-400,1-1e-400)", 2.0)]
    [DataRow("logn(1+1e-400,1-1e-300)", -1e-100)]
    [DataRow("ln(1+1e-320)", 1e-320)]
    [DataRow("logn(1+2/(10^4800),1+1/(10^4800))", 2.0)]
    [DataRow("log1p(-1e-30)", -1e-30)]
    [DataRow("log1p(-0.5)", -0.6931471805599453)]
    [DataRow("ln(1.5)", 0.4054651081081644)]
    [DataRow("log1p(1e-30)", 1e-30)]
    public void LogarithmsPreserveSmallDifferencesBeforeProjection(string expression, double expected)
    {
        AssertApproximation(Calculator.Evaluate(expression), expected, Math.Abs(expected) * 3e-15);
    }

    [TestMethod]
    [DataRow("ln(0)")]
    [DataRow("ln(-1e-400)")]
    [DataRow("log2(-1e400)")]
    [DataRow("log10(0)")]
    [DataRow("logn(1e-400,1)")]
    [DataRow("logn(2,-1e-400)")]
    [DataRow("log1p(-1)")]
    [DataRow("log1p(-1-1e-400)")]
    public void LogarithmDomainsUseOriginalInputs(string expression)
    {
        AssertDomainError(CalculatorEngine.Default, expression);
    }

    [TestMethod]
    [DataRow("hypot(3e200,4e200)", 5e200)]
    [DataRow("hypot(-3e-200,4e-200)", 5e-200)]
    [DataRow("hypot(1e308,1e308)", 1.4142135623730951e308)]
    [DataRow("hypot(0,-1e-300)", 1e-300)]
    [DataRow("hypot(1e300,1e-300)", 1e300)]
    [DataRow("hypot(0,0)", 0.0)]
    [DataRow("hypot(1e400,1)", double.PositiveInfinity)]
    public void HypotenuseAvoidsIntermediateOverflowAndUnderflow(string expression, double expected)
    {
        AssertApproximation(Calculator.Evaluate(expression), expected,
            double.IsFinite(expected) ? Math.Abs(expected) * 3e-15 : 0);
    }

    [TestMethod]
    public void HypotenuseRoundsOnlyAfterCombiningSubnormalInputs()
    {
        // Each input alone rounds to zero, but their hypotenuse rounds to the
        // smallest positive Double. Converting the inputs first would lose it.
        AssertApproximation(Calculator.Evaluate("hypot(2e-324,2e-324)"), double.Epsilon, 0);
    }

    [TestMethod]
    [DataRow("sin")]
    [DataRow("cos")]
    [DataRow("tan")]
    [DataRow("cot")]
    [DataRow("sec")]
    [DataRow("csc")]
    public void ExactAnglesAreReducedBeforeConversion(string function)
    {
        foreach (var unit in new[] { AngleUnit.Degrees, AngleUnit.Gradians })
        {
            var engine = new CalculatorEngine(new CalculatorOptions { AngleUnit = unit });
            int revolution = unit == AngleUnit.Degrees ? 360 : 400;
            foreach (var offset in new[] { "37/3", "-37/3", "45", "-45" })
            {
                double expected = engine.Evaluate($"{function}({offset})").DoubleValue;
                foreach (var sign in new[] { "+", "-" })
                {
                    string expression = $"{function}({offset}{sign}{revolution}*10^400)";
                    AssertApproximation(engine.Evaluate(expression), expected, 0);
                    AssertApproximation(engine.Evaluate(engine.Parse(expression)), expected, 0);
                }
            }
        }
    }

    [TestMethod]
    [DataRow(AngleUnit.Degrees, "tan(360*10^400+90)")]
    [DataRow(AngleUnit.Degrees, "sec(-360*10^400-90)")]
    [DataRow(AngleUnit.Degrees, "cot(360*10^400)")]
    [DataRow(AngleUnit.Gradians, "tan(400*10^400+100)")]
    [DataRow(AngleUnit.Gradians, "csc(-400*10^400)")]
    public void LargeExactAngleSingularitiesStillReportDiagnostics(AngleUnit unit, string expression)
    {
        AssertDomainError(new CalculatorEngine(new CalculatorOptions { AngleUnit = unit }), expression);
    }

    [TestMethod]
    public void CapturedInputsKeepTheirValueAndOutputsRemainUnverified()
    {
        var engine = CalculatorEngine.Default;
        var captured = engine.Evaluate("x=1+1e-30");
        var result = engine.Evaluate("ln(x)", captured.Context);
        AssertApproximation(result, 1e-30, 3e-45);
        AssertApproximation(engine.Evaluate("ans*1000000", result.Context), 1e-24, 3e-39);
        AssertApproximation(engine.Evaluate(new FunctionCallNode("ln",
            [new NumberNode(MathValue.FromFraction(BigInteger.One, BigInteger.Pow(10, 400)))])),
            -921.0340371976183, 3e-12);
    }

    [TestMethod]
    public void SuppliedDoublesRetainFloatingPointBehavior()
    {
        var context = new EvaluationContext
        {
            Variables =
            [
                new Variable("x", new MathValue(3e200)),
                new Variable("y", new MathValue(4e200)),
                new Variable("infinity", new MathValue(double.PositiveInfinity)),
                new Variable("nanvalue", new MathValue(double.NaN))
            ]
        };
        AssertApproximation(Calculator.Evaluate("hypot(x,y)", context), 5e200, 1.5e186);
        AssertApproximation(Calculator.Evaluate("ln(infinity)", context), double.PositiveInfinity, 0);
        Assert.IsTrue(Calculator.Evaluate("ln(nanvalue)", context).IsNaN);
        Assert.IsTrue(Calculator.Evaluate("hypot(nanvalue,1)", context).IsNaN);
        Assert.IsTrue(Calculator.Evaluate("logn(2,infinity)", context).IsNaN);
        AssertApproximation(Calculator.Evaluate("logn(1,infinity)", context), 0, 0);
    }

    [TestMethod]
    public void RadianAndSuppliedDoubleAnglePathsAreUnchanged()
    {
        var context = new EvaluationContext { Variables = [new Variable("x", new MathValue(1e100))] };
        AssertApproximation(Calculator.Evaluate("sin(x)", context), Math.Sin(1e100), 0);
        AssertApproximation(TestEngines.DegreesEngine.Evaluate("sin(x)", context),
            double.SinPi(Math.IEEERemainder(1e100, 360) / 180), 0);
    }

    [TestMethod]
    public void SuppliedDoubleLog1pPreservesSmallArguments()
    {
        var context = new EvaluationContext { Variables = [new Variable("x", new MathValue(1e-5))] };
        AssertApproximation(Calculator.Evaluate("log1p(x)", context), 0.000009999950000333330833353333166668095, 2e-21);
        context = new EvaluationContext { Variables = [new Variable("x", new MathValue(1e-30))] };
        AssertApproximation(Calculator.Evaluate("log1p(x)", context), 1e-30, 0);
    }

    [TestMethod]
    public void ImprovementsStillHonorConfiguredInputLimits()
    {
        var engine = new CalculatorEngine(new CalculatorOptions { MaxIntegerBits = 8 });
        foreach (var expression in new[] { "ln(1/257)", "hypot(256,1)", "sin(360*1000)" })
        {
            Assert.Throws<CalculationLimitException>(() => engine.Evaluate(expression), expression);
        }
    }

    private static void AssertApproximation(EvaluationResult result, double expected, double tolerance)
    {
        Assert.AreEqual(MathValue.DataType.Double, result.Type);
        Assert.AreEqual(expected, result.DoubleValue, tolerance);
        Assert.AreEqual(CalculationExactness.Unverified, result.Exactness);
        Assert.IsFalse(result.HasExactFraction);
        Assert.IsNull(result.FormatResult().FractionRepresentation);
    }

    private static void AssertDomainError(CalculatorEngine engine, string expression)
    {
        Assert.IsFalse(engine.TryEvaluate(expression, out _, out var diagnostic));
        Assert.AreEqual(EvaluationError.UndefinedMathResult, diagnostic.Code);
        Assert.AreEqual(0, diagnostic.Position);
        Assert.AreEqual(expression.Length, diagnostic.Length);
    }
}
