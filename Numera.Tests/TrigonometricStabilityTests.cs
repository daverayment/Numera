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
    public void InfiniteAnglesProduceNaNConsistentlyAcrossTrigonometricFunctions(AngleUnit unit)
    {
        var engine = CreateEngine(unit);
        var context = new EvaluationContext(
        [
            new Variable("positiveInfinity", MathValue.PositiveInfinity),
            new Variable("negativeInfinity", MathValue.NegativeInfinity)
        ]);
        foreach (string function in new[] { "sin", "cos", "tan", "cot", "sec", "csc" })
        {
            foreach (string argument in new[] { "positiveInfinity", "negativeInfinity", "exp(1000)", "-exp(1000)" })
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
            }
        }
    }

    [TestMethod]
    [DataRow("cot")]
    [DataRow("csc")]
    public void FiniteAnglesWithOverflowingReciprocalsStillReportDiagnostics(string function)
    {
        var engine = CreateEngine(AngleUnit.Radians);
        var context = new EvaluationContext([new Variable("x", new MathValue(double.Epsilon))]);

        Assert.IsFalse(engine.TryEvaluate($"{function}(x)", out _, out var diagnostic, context));
        Assert.AreEqual(EvaluationError.UndefinedMathResult, diagnostic!.Code);
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
    [DataRow(AngleUnit.Degrees)]
    [DataRow(AngleUnit.Gradians)]
    public void RationalTwelfthTurnsHaveExactValuesOnlyWhereRational(AngleUnit unit)
    {
        var engine = CreateEngine(unit);
        int?[] halves = [0, 1, null, 2, null, 1, 0, -1, null, -2, null, -1];
        string turn = unit == AngleUnit.Degrees ? "360" : "400";

        for (int step = -12; step < 24; step++)
        {
            int index = (step % 12 + 12) % 12;

            foreach (string function in new[] { "sin", "cos", "sec", "csc" })
            {
                int shiftedIndex = index;

                if (function is "cos" or "sec")
                {
                    shiftedIndex = (index + 3) % 12;
                }

                int? half = halves[shiftedIndex];
                string expression = $"{function}({turn}*10^400+{turn}*({step})/12)";

                if (half == 0 && function is "sec" or "csc")
                {
                    Assert.IsFalse(engine.TryEvaluate(expression, out _, out var diagnostic), expression);
                    Assert.AreEqual(EvaluationError.UndefinedMathResult, diagnostic.Code, expression);
                    continue;
                }

                var result = engine.Evaluate(expression);

                if (half is int numerator)
                {
                    var expected = MathValue.FromFraction(numerator, 2);
                    
                    if (function is "sec" or "csc")
                    {
                        expected = MathValue.FromFraction(2, numerator);
                    }

                    Assert.AreEqual(expected, result.Value, expression);
                    Assert.AreEqual(expected.Kind, result.Kind, expression);
                    Assert.AreEqual(CalculationExactness.Exact, result.Exactness, expression);
                    Assert.AreEqual(expected.ExactFraction, result.ExactFraction, expression);
                }
                else
                {
                    Assert.AreEqual(MathValue.DataType.Double, result.Kind, expression);
                    Assert.AreEqual(CalculationExactness.Unverified, result.Exactness, expression);
                    Assert.IsFalse(result.HasExactFraction, expression);

                    double radians = step * Math.PI / 6;
                    double expected = function switch
                    {
                        "sin" => Math.Sin(radians),
                        "cos" => Math.Cos(radians),
                        "sec" => 1 / Math.Cos(radians),
                        _ => 1 / Math.Sin(radians)
                    };
                    Assert.AreEqual(expected, result.DoubleValue, 4e-15, expression);
                }
            }
        }
    }

    [TestMethod]
    [DataRow(AngleUnit.Degrees)]
    [DataRow(AngleUnit.Gradians)]
    public void RationalEighthTurnsHaveExactTangentAndCotangentOrPoleDiagnostics(AngleUnit unit)
    {
        var engine = CreateEngine(unit);
        string turn = unit == AngleUnit.Degrees ? "360" : "400";

        for (int step = -16; step <= 16; step++)
        {
            int index = (step % 4 + 4) % 4;

            foreach (string function in new[] { "tan", "cot" })
            {
                string expression = $"{function}(-{turn}*10^400+{turn}*({step})/8)";
                bool pole = (function == "tan" && index == 2) || (function == "cot" && index == 0);

                if (pole)
                {
                    Assert.IsFalse(engine.TryEvaluate(expression, out _, out var diagnostic), expression);
                    Assert.AreEqual(EvaluationError.UndefinedMathResult, diagnostic.Code, expression);
                    continue;
                }

                int expected = index switch
                {
                    1 => 1,
                    3 => -1,
                    _ => 0
                };
                var result = engine.Evaluate(expression);
                Assert.AreEqual(MathValue.DataType.Integer, result.Kind, expression);
                Assert.AreEqual(new MathValue(expected), result.Value, expression);
                Assert.AreEqual(CalculationExactness.Exact, result.Exactness, expression);
            }
        }
    }

    [TestMethod]
    [DataRow(AngleUnit.Radians)]
    [DataRow(AngleUnit.Degrees)]
    [DataRow(AngleUnit.Gradians)]
    public void ExactZeroUsesExactResultsInEveryAngleUnit(AngleUnit unit)
    {
        var engine = CreateEngine(unit);

        foreach (string function in new[] { "sin", "cos", "tan", "sec" })
        {
            int expected = function is "cos" or "sec" ? 1 : 0;
            var result = engine.Evaluate($"{function}(0)");
            Assert.AreEqual(MathValue.DataType.Integer, result.Kind);
            Assert.AreEqual(new MathValue(expected), result.Value);
            Assert.AreEqual(CalculationExactness.Exact, result.Exactness);
        }
    }

    [TestMethod]
    [DataRow(AngleUnit.Degrees, 90)]
    [DataRow(AngleUnit.Gradians, 100)]
    public void TinyRationalOffsetsOnBothSidesOfEveryQuadrantSurviveProjection(AngleUnit unit, int quarterTurn)
    {
        var engine = CreateEngine(unit);
        double radiansPerUnit = Math.PI / (quarterTurn * 2);

        for (int quarter = -4; quarter <= 4; quarter++)
        {
            int quadrant = (quarter % 4 + 4) % 4;

            foreach (int side in new[] { -1, 1 })
            {
                double residual = side * 1e-30 * radiansPerUnit;
                double sin = quadrant switch { 0 => residual, 1 => 1, 2 => -residual, _ => -1 };
                double cos = quadrant switch { 0 => 1, 1 => -residual, 2 => -1, _ => residual };
                foreach (string function in new[] { "sin", "cos", "tan", "cot", "sec", "csc" })
                {
                    double expected = function switch
                    {
                        "sin" => sin,
                        "cos" => cos,
                        "tan" => sin / cos,
                        "cot" => cos / sin,
                        "sec" => 1 / cos,
                        _ => 1 / sin
                    };

                    string expression = $"{function}({quarterTurn * 4}*10^400+({quarter}*{quarterTurn})+({side}*1e-30))";
                    var result = engine.Evaluate(expression);
                    Assert.AreEqual(MathValue.DataType.Double, result.Kind, expression);
                    Assert.AreEqual(CalculationExactness.Unverified, result.Exactness, expression);
                    Assert.IsFalse(result.HasExactFraction, expression);
                    Assert.IsLessThanOrEqualTo(2e-15, Math.Abs(result.DoubleValue / expected - 1), expression);
                }
            }
        }
    }

    [TestMethod]
    [DataRow("sin(30+1e-40)", 0.5)]
    [DataRow("cos(60-1e-40)", 0.5)]
    [DataRow("tan(45+1e-40)", 1.0)]
    [DataRow("cot(45-1e-40)", 1.0)]
    [DataRow("sec(60+1e-40)", 2.0)]
    [DataRow("csc(30-1e-40)", 2.0)]
    public void NearSpecialValuesAreStillUnverifiedDoubles(string expression, double expected)
    {
        var result = CreateEngine(AngleUnit.Degrees).Evaluate(expression);
        Assert.AreEqual(expected, result.DoubleValue, 1e-15);
        Assert.AreEqual(CalculationExactness.Unverified, result.Exactness);
        Assert.IsFalse(result.HasExactFraction);
    }

    [TestMethod]
    public void ExactSpecialValuesRemainExactThroughVariablesPreviousAnswerAndArithmetic()
    {
        var engine = CreateEngine(AngleUnit.Degrees);
        var first = engine.Evaluate("x=sin(30.0)");
        Assert.AreEqual(MathValue.DataType.Rational, first.Kind);
        Assert.AreEqual(MathValue.FromFraction(1, 2), first.Value);
        Assert.AreEqual(CalculationExactness.Exact, first.Exactness);
        foreach (string expression in new[] { "x*2", "ans*2", "sin(30)*2", "sec(60)/2", "csc(30)/2" })
        {
            var result = engine.Evaluate(expression, first.Context);
            Assert.AreEqual(MathValue.One, result.Value, expression);
            Assert.AreEqual(CalculationExactness.Exact, result.Exactness, expression);
        }

        Assert.AreEqual(MathValue.FromFraction(1, 2), engine.Evaluate("sin(36030)").Value);
        Assert.AreEqual(CalculationExactness.Unverified, engine.Evaluate("sin(45)^2").Exactness);
    }

    [TestMethod]
    public void UnverifiedAndDoubleInputsNeverAcquireExactnessAtSpecialAngles()
    {
        var engine = CreateEngine(AngleUnit.Degrees);

        foreach (MathValue angle in new[]
        {
            new MathValue(30).WithExactness(CalculationExactness.Unverified),
            new MathValue(30m).WithExactness(CalculationExactness.Unverified),
            MathValue.FromFraction(30, 1).WithExactness(CalculationExactness.Unverified),
            new MathValue(30.0),
            new MathValue(30.0).WithExactness(CalculationExactness.Exact)
        })
        {
            var context = new EvaluationContext([new Variable("x", angle)]);
            var result = engine.Evaluate("sin(x)", context);
            Assert.AreEqual(MathValue.DataType.Double, result.Kind);
            Assert.AreEqual(CalculationExactness.Unverified, result.Exactness);
            Assert.IsFalse(result.HasExactFraction);
        }

        var uncertain = new EvaluationContext([new Variable("x",
            new MathValue(90).WithExactness(CalculationExactness.Unverified))]);
        Assert.AreEqual(CalculationExactness.Unverified, engine.Evaluate("sin(x)", uncertain).Exactness);
        Assert.IsFalse(engine.TryEvaluate("tan(x)", out _, out var diagnostic, uncertain));
        Assert.AreEqual(EvaluationError.UndefinedMathResult, diagnostic.Code);
    }

    [TestMethod]
    [DataRow(AngleUnit.Degrees, 90)]
    [DataRow(AngleUnit.Gradians, 100)]
    public void UnderflowedResidualsAreNotMistakenForPoles(AngleUnit unit, int quarterTurn)
    {
        var engine = CreateEngine(unit);

        foreach (string side in new[] { "+", "-" })
        {
            foreach (string function in new[] { "cos", "cot" })
            {
                var result = engine.Evaluate($"{function}({quarterTurn}{side}1e-400)");
                Assert.AreEqual(0.0, result.DoubleValue);
                Assert.AreEqual(CalculationExactness.Unverified, result.Exactness);
            }

            foreach (string expression in new[]
            {
                $"tan({quarterTurn}{side}1e-400)", $"sec({quarterTurn}{side}1e-400)",
                $"cot(0{side}1e-400)", $"csc(0{side}1e-400)"
            })
            {
                Assert.IsFalse(engine.TryEvaluate(expression, out _, out var diagnostic), expression);
                Assert.AreEqual(EvaluationError.NumericOverflow, diagnostic.Code, expression);
                Assert.IsNotNull(diagnostic.Position);
                var error = Assert.ThrowsExactly<ExpressionException>(() => engine.Evaluate(expression));
                Assert.AreEqual(EvaluationError.NumericOverflow, error.Diagnostic.Code);
            }
        }
    }

    [TestMethod]
    [DataRow(AngleUnit.Degrees)]
    [DataRow(AngleUnit.Gradians)]
    public void TinyResidualConversionPreservesRepresentableSubnormalResults(AngleUnit unit)
    {
        var engine = CreateEngine(unit);

        foreach (string function in new[] { "sin", "tan" })
        {
            var result = engine.Evaluate($"{function}(3e-322)");
            Assert.AreEqual(double.Epsilon, result.DoubleValue);
            Assert.AreEqual(CalculationExactness.Unverified, result.Exactness);
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
