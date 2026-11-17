using System.Globalization;
using System.Numerics;

namespace Numera.Tests;

[TestClass]
public sealed class RandomFunctionTests
{
    [TestMethod]
    [DataRow("randi(1)", "0")]
    [DataRow("randi(3, 4)", "3")]
    [DataRow("randi(-4, -3)", "-4")]
    [DataRow("randi(9223372036854775806, 9223372036854775807)", "9223372036854775806")]
    [DataRow("randi(-9223372036854775808, -9223372036854775807)", "-9223372036854775808")]
    [DataRow("randi(2/2, 4/2)", "1")]
    [DataRow("randi(1.0, 2.0)", "1")]
    [DataRow("randbetween(6, 6)", "6")]
    [DataRow("randbetween(0, 0)", "0")]
    [DataRow("randbetween(-6, -6)", "-6")]
    [DataRow("randbetween(9223372036854775807, 9223372036854775807)", "9223372036854775807")]
    [DataRow("randbetween(-9223372036854775808, -9223372036854775808)", "-9223372036854775808")]
    [DataRow("randbetween(2/2, 1.0)", "1")]
    [DataRow("RANDBETWEEN(6, 6)", "6")]
    public void SingleOutcomeRangesAreExactIncludingSignedEndpoints(string expression, string expected)
    {
        var engine = CalculatorEngine.ForCulture(CultureInfo.InvariantCulture);
        var result = engine.Evaluate(expression);

        Assert.AreEqual(BigInteger.Parse(expected, CultureInfo.InvariantCulture), result.IntegerValue);
        Assert.AreEqual(MathValue.DataType.Integer, result.Kind);
        Assert.AreEqual(CalculationExactness.Exact, result.Exactness);
    }

    [TestMethod]
    [DataRow("randi(6)", 0L, 6L, false)]
    [DataRow("randi(1, 7)", 1L, 7L, false)]
    [DataRow("randi(-5, -1)", -5L, -1L, false)]
    [DataRow("randi(-3, 3)", -3L, 3L, false)]
    [DataRow("randi(-9223372036854775808, 9223372036854775807)", long.MinValue, long.MaxValue, false)]
    [DataRow("randbetween(1, 6)", 1L, 6L, true)]
    [DataRow("randbetween(-5, -1)", -5L, -1L, true)]
    [DataRow("randbetween(-3, 3)", -3L, 3L, true)]
    [DataRow("randbetween(0, 9223372036854775807)", 0L, long.MaxValue, true)]
    [DataRow("randbetween(9223372036854775806, 9223372036854775807)", long.MaxValue - 1, long.MaxValue, true)]
    [DataRow("randbetween(-9223372036854775808, 9223372036854775807)", long.MinValue, long.MaxValue, true)]
    public void DrawsStayWithinTheirDocumentedRange(string expression, long minimum, long maximum, bool inclusive)
    {
        var engine = CalculatorEngine.ForCulture(CultureInfo.InvariantCulture);
        for (int i = 0; i < 128; i++)
        {
            var result = engine.Evaluate(expression);
            Assert.AreEqual(MathValue.DataType.Integer, result.Kind);
            Assert.AreEqual(CalculationExactness.Exact, result.Exactness);
            Assert.IsTrue(result.IntegerValue >= minimum);
 
            if (inclusive)
            {
                Assert.IsTrue(result.IntegerValue <= maximum);
            }
            else
            {
                Assert.IsTrue(result.IntegerValue < maximum);
            }
        }
    }

    [TestMethod]
    [DataRow("randi(0)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("randi(-1)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("randi(1, 1)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("randi(2, 1)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("randbetween(2, 1)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("randi(9223372036854775808)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("randi(-9223372036854775809, 0)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("randi(0, 9223372036854775808)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("randbetween(-9223372036854775809, 0)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("randbetween(0, 9223372036854775808)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("randbetween(9223372036854775808, 9223372036854775808)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("randi(0.5)", EvaluationError.IntegerRequired)]
    [DataRow("randi(0.5, 2)", EvaluationError.IntegerRequired)]
    [DataRow("randi(0, 1/3)", EvaluationError.IntegerRequired)]
    [DataRow("randbetween(0.5, 2)", EvaluationError.IntegerRequired)]
    [DataRow("randbetween(0, 1/3)", EvaluationError.IntegerRequired)]
    [DataRow("randi()", EvaluationError.InvalidArgumentCount)]
    [DataRow("randi(1, 2, 3)", EvaluationError.InvalidArgumentCount)]
    [DataRow("randbetween()", EvaluationError.InvalidArgumentCount)]
    [DataRow("randbetween(1)", EvaluationError.InvalidArgumentCount)]
    [DataRow("randbetween(1, 2, 3)", EvaluationError.InvalidArgumentCount)]
    public void InvalidBoundsAndArityHaveStructuredDiagnostics(string expression, EvaluationError expected)
    {
        var engine = CalculatorEngine.ForCulture(CultureInfo.InvariantCulture);
        Assert.IsFalse(engine.TryEvaluate(expression, out _, out var diagnostic));
        Assert.IsNotNull(diagnostic);
        Assert.AreEqual(expected, diagnostic.Code);
        Assert.IsFalse(diagnostic.IsIncomplete);
    }

    [TestMethod]
    public void NonfiniteBoundsRequireIntegers()
    {
        double[] invalidBounds = [double.NaN, double.PositiveInfinity, double.NegativeInfinity];
        string[] expressions = ["randi(bound)", "randi(bound, 2)", "randi(0, bound)",
            "randbetween(bound, 2)", "randbetween(0, bound)"];
        var engine = CalculatorEngine.ForCulture(CultureInfo.InvariantCulture);

        foreach (double bound in invalidBounds)
        {
            var context = new EvaluationContext([new Variable("bound", new MathValue(bound))]);
            foreach (string expression in expressions)
            {
                Assert.IsFalse(engine.TryEvaluate(expression, out _, out var diagnostic, context));
                Assert.AreEqual(EvaluationError.IntegerRequired, diagnostic!.Code);
            }
        }
    }

    [TestMethod]
    [DataRow("randi(3, 1)")]
    [DataRow("randbetween(3, 1)")]
    public void BoundErrorsKeepTheFunctionSourceSpan(string expression)
    {
        var engine = CalculatorEngine.ForCulture(CultureInfo.InvariantCulture);
        string input = "2 + " + expression;
        var exception = Assert.ThrowsExactly<ExpressionException>(() => engine.Evaluate(input));

        Assert.AreEqual(EvaluationError.ArgumentOutOfRange, exception.Diagnostic.Code);
        Assert.AreEqual(4, exception.Diagnostic.Position);
        Assert.AreEqual(expression.Length, exception.Diagnostic.Length);
    }

    [TestMethod]
    public void RandomRangesRespectEngineLimitsAndLocalizedArgumentDelimiters()
    {
        var engine = CalculatorEngine.ForCurrentCulture(options => options with { MaxIntegerBits = 3 });
        Assert.AreEqual(new BigInteger(7), engine.Evaluate("randbetween(7, 7)").IntegerValue);
        Assert.ThrowsExactly<CalculationLimitException>(() => engine.Evaluate("randbetween(8, 8)"));
        Assert.ThrowsExactly<CalculationLimitException>(() => engine.Evaluate("randi(7, 8)"));

        var german = CalculatorEngine.ForCulture(CultureInfo.GetCultureInfo("de-DE"));
        Assert.AreEqual(new BigInteger(6), german.Evaluate("randbetween(6; 6)").IntegerValue);
        Assert.AreEqual(new BigInteger(6), german.Evaluate("randi(6; 7)").IntegerValue);
    }

    [TestMethod]
    public void DiscoveryExplainsBothRangeConventions()
    {
        Assert.IsTrue(Functions.TryGetInfo("randi", out var randi));
        Assert.AreEqual(1, randi.Signature.MinimumArguments);
        Assert.AreEqual(2, randi.Signature.MaximumArguments);
        Assert.AreEqual("minInclusive", randi.Signature.Parameters[0].Name);
        Assert.IsTrue(randi.Signature.Parameters[0].IsOptional);
        Assert.AreEqual("maxExclusive", randi.Signature.Parameters[1].Name);
        Assert.IsFalse(randi.Signature.Parameters[1].IsOptional);

        Assert.IsTrue(Functions.TryGetInfo("randbetween", out var between));
        Assert.AreEqual(FunctionCategory.Random, between.Category);
        Assert.AreEqual(2, between.Signature.MinimumArguments);
        Assert.AreEqual(2, between.Signature.MaximumArguments);
        Assert.AreSequenceEqual(
            ["minInclusive", "maxInclusive"],
            between.Signature.Parameters.Select(parameter => parameter.Name).ToArray());
        Assert.DoesNotContain(parameter => parameter.IsOptional, between.Signature.Parameters);
        Assert.Contains(info => info.Name == "randbetween", Functions.Search("dice").ToList());
        Assert.Contains(info => info.Name == "randbetween", new CalculatorEngine().GetFunctions().ToList());
        Assert.ThrowsExactly<ArgumentException>(() =>
            new EvaluationContext([new Variable("randbetween", new MathValue(1))]));
    }
}
