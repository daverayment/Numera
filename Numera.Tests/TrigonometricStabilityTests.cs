using System.Globalization;

namespace Numera.Tests;

[TestClass]
public sealed class TrigonometricStabilityTests
{
    [TestMethod]
    [DataRow(AngleUnit.Radians)]
    [DataRow(AngleUnit.Degrees)]
    [DataRow(AngleUnit.Gradians)]
    public void NaNAnglesPropagateWithoutPoleDiagnostics(AngleUnit unit)
    {
        var engine = CreateEngine(unit);
        var context = new EvaluationContext([new Variable("x", MathValue.NaN)]);

        foreach (string function in new[] { "tan", "cot", "sec", "csc" })
        {
            foreach (string argument in new[] { "x", "exp(1000)-exp(1000)" })
            {
                string expression = $"{function}({argument})";
                var result = engine.Evaluate(expression, context);
                Assert.IsTrue(result.IsNaN, expression);
                Assert.AreEqual(MathValue.DataType.Double, result.Kind);
                Assert.AreEqual(CalculationExactness.Unverified, result.Exactness);
                Assert.IsFalse(result.HasExactFraction);
                Assert.IsTrue(engine.TryEvaluate(expression, out var triedResult, out var diagnostic, context), expression);
                Assert.IsNull(diagnostic);
                Assert.IsTrue(triedResult.IsNaN);
                Assert.IsTrue(engine.Evaluate($"{function}(ans)", result.Context).IsNaN);
            }
        }
    }

    [TestMethod]
    [DataRow(AngleUnit.Radians)]
    [DataRow(AngleUnit.Degrees)]
    [DataRow(AngleUnit.Gradians)]
    public void InfiniteAnglesRetainExistingDomainDiagnostics(AngleUnit unit)
    {
        var engine = CreateEngine(unit);
        foreach (var angle in new[] { MathValue.PositiveInfinity, MathValue.NegativeInfinity })
        {
            var context = new EvaluationContext([new Variable("x", angle)]);
            foreach (string function in new[] { "tan", "cot", "sec", "csc" })
            {
                string expression = $"{function}(x)";
                Assert.IsFalse(engine.TryEvaluate(expression, out _, out var diagnostic, context), expression);
                Assert.AreEqual(EvaluationError.UndefinedMathResult, diagnostic!.Code);
                Assert.AreEqual(0, diagnostic.Position);
                Assert.AreEqual(expression.Length, diagnostic.Length);
            }
        }
    }

    [TestMethod]
    [DataRow(AngleUnit.Radians)]
    [DataRow(AngleUnit.Degrees)]
    [DataRow(AngleUnit.Gradians)]
    public void TinyNonzeroAnglesRemainNonzeroWithFiniteReciprocals(AngleUnit unit)
    {
        var engine = CreateEngine(unit);
        double radiansPerUnit = unit switch
        {
            AngleUnit.Degrees => Math.PI / 180.0,
            AngleUnit.Gradians => Math.PI / 200.0,
            _ => 1.0,
        };

        foreach (double angle in new[] { 1e-14, -1e-14, 1e-300, -1e-300 })
        {
            var context = new EvaluationContext([new Variable("x", new MathValue(angle))]);
            double expectedSine = angle * radiansPerUnit;
            foreach (string function in new[] { "sin", "tan", "cot", "csc" })
            {
                double expected = expectedSine;
                if (function is "cot" or "csc")
                {
                    expected = 1.0 / expectedSine;
                }

                var result = engine.Evaluate($"{function}(x)", context);
                Assert.AreEqual(MathValue.DataType.Double, result.Kind);
                Assert.AreEqual(CalculationExactness.Unverified, result.Exactness);
                Assert.IsTrue(double.IsFinite(result.DoubleValue));
                Assert.AreNotEqual(0.0, result.DoubleValue);
                Assert.IsLessThanOrEqualTo(4e-15, Math.Abs(result.DoubleValue / expected - 1.0),
                    $"{unit}: {function}({angle:R}) must preserve the small-angle result.");
            }
        }
    }

    [TestMethod]
    [DataRow(AngleUnit.Degrees, 90.0, "cos", "sec", "tan")]
    [DataRow(AngleUnit.Degrees, -90.0, "cos", "sec", "tan")]
    [DataRow(AngleUnit.Degrees, 180.0, "sin", "csc", "cot")]
    [DataRow(AngleUnit.Degrees, -180.0, "sin", "csc", "cot")]
    [DataRow(AngleUnit.Gradians, 100.0, "cos", "sec", "tan")]
    [DataRow(AngleUnit.Gradians, -100.0, "cos", "sec", "tan")]
    [DataRow(AngleUnit.Gradians, 200.0, "sin", "csc", "cot")]
    [DataRow(AngleUnit.Gradians, -200.0, "sin", "csc", "cot")]
    public void AdjacentRepresentableAnglesAreNotZerosOrPoles(
        AngleUnit unit, double pole, string zeroFunction, string reciprocalFunction, string ratioFunction)
    {
        var engine = CreateEngine(unit);
        double previousZero = 0.0;
        foreach (double angle in new[] { Math.BitDecrement(pole), Math.BitIncrement(pole) })
        {
            var context = new EvaluationContext([new Variable("x", new MathValue(angle))]);
            double zeroResult = engine.Evaluate($"{zeroFunction}(x)", context).DoubleValue;
            double reciprocalResult = engine.Evaluate($"{reciprocalFunction}(x)", context).DoubleValue;
            double ratioResult = engine.Evaluate($"{ratioFunction}(x)", context).DoubleValue;

            Assert.AreNotEqual(0.0, zeroResult);
            Assert.IsLessThan(1e-12, Math.Abs(zeroResult));
            Assert.IsTrue(double.IsFinite(reciprocalResult));
            Assert.IsTrue(double.IsFinite(ratioResult));
            Assert.IsGreaterThan(1e12, Math.Abs(reciprocalResult));
            Assert.IsGreaterThan(1e12, Math.Abs(ratioResult));
            if (previousZero != 0.0)
            {
                Assert.AreNotEqual(Math.Sign(previousZero), Math.Sign(zeroResult));
            }
            previousZero = zeroResult;
        }
    }

    [TestMethod]
    [DataRow(AngleUnit.Degrees, 45.0)]
    [DataRow(AngleUnit.Gradians, 50.0)]
    public void AdjacentEighthTurnsDoNotBecomeUnitTangent(AngleUnit unit, double eighthTurn)
    {
        var engine = CreateEngine(unit);
        foreach (double angle in new[] { Math.BitDecrement(eighthTurn), Math.BitIncrement(eighthTurn) })
        {
            var context = new EvaluationContext([new Variable("x", new MathValue(angle))]);
            double tangent = engine.Evaluate("tan(x)", context).DoubleValue;
            Assert.AreNotEqual(1.0, tangent);
            Assert.AreEqual(Math.Sign(angle - eighthTurn), Math.Sign(tangent - 1.0));
        }
    }

    [TestMethod]
    [DataRow("tan", 1.5707963267948966)]
    [DataRow("sec", 1.5707963267948966)]
    [DataRow("cot", 3.141592653589793)]
    [DataRow("csc", 3.141592653589793)]
    public void RadianValuesBesideRoundedPiMultiplesHaveFiniteReciprocals(string function, double pole)
    {
        var engine = CreateEngine(AngleUnit.Radians);
        foreach (double angle in new[] { Math.BitDecrement(pole), Math.BitIncrement(pole) })
        {
            var context = new EvaluationContext([new Variable("x", new MathValue(angle))]);
            double expected = function switch
            {
                "tan" => Math.Tan(angle),
                "sec" => 1.0 / Math.Cos(angle),
                "cot" => Math.Cos(angle) / Math.Sin(angle),
                "csc" => 1.0 / Math.Sin(angle),
                _ => throw new ArgumentException("Unsupported reciprocal function.", nameof(function)),
            };
            double result = engine.Evaluate($"{function}(x)", context).DoubleValue;
            Assert.IsTrue(double.IsFinite(result));
            Assert.IsGreaterThan(1e12, Math.Abs(result));
            Assert.AreEqual(expected, result);
        }
    }

    [TestMethod]
    [DataRow(AngleUnit.Degrees, "360", "90", "cos")]
    [DataRow(AngleUnit.Degrees, "360", "90", "tan")]
    [DataRow(AngleUnit.Degrees, "360", "90", "sec")]
    [DataRow(AngleUnit.Degrees, "360", "180", "sin")]
    [DataRow(AngleUnit.Degrees, "360", "180", "cot")]
    [DataRow(AngleUnit.Degrees, "360", "180", "csc")]
    [DataRow(AngleUnit.Gradians, "400", "100", "cos")]
    [DataRow(AngleUnit.Gradians, "400", "100", "tan")]
    [DataRow(AngleUnit.Gradians, "400", "100", "sec")]
    [DataRow(AngleUnit.Gradians, "400", "200", "sin")]
    [DataRow(AngleUnit.Gradians, "400", "200", "cot")]
    [DataRow(AngleUnit.Gradians, "400", "200", "csc")]
    public void NearPoleOffsetsSurviveExactLargeRevolutionReduction(
        AngleUnit unit, string revolution, string pole, string function)
    {
        var engine = CreateEngine(unit);
        foreach (string offset in new[] { "+1e-12", "-1e-12" })
        {
            double expected = engine.Evaluate($"{function}({pole}{offset})").DoubleValue;
            double actual = engine.Evaluate($"{function}({revolution}*10^400+{pole}{offset})").DoubleValue;
            Assert.IsTrue(double.IsFinite(actual));
            Assert.AreNotEqual(0.0, actual);
            Assert.AreEqual(expected, actual);
        }
    }

    [TestMethod]
    public void FloorDoesNotCorrectAnApproximateTangentToAnExactIdentity()
    {
        var engine = CreateEngine(AngleUnit.Radians);
        double expected = Math.Floor(Math.Tan(engine.Evaluate("pi/4").AsDouble()));
        Assert.AreEqual(expected, engine.Evaluate("floor(tan(pi/4))").AsDouble());
    }

    private static CalculatorEngine CreateEngine(AngleUnit unit) => new(
        CalculatorOptions.ForCulture(CultureInfo.InvariantCulture) with { AngleUnit = unit, Normalization = null });
}
