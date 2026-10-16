using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Numera.Tests;

[TestClass]
public class SafetyTests
{
    [TestMethod]
    [DataRow("factorial(1001)")]
    [DataRow("2^10001")]
    [DataRow("2^(-10001)")]
    [DataRow("(2^10000)*(2^10000)")]
    [DataRow("sum(2^16384, 2^16384)")]
    [DataRow("mul(2^10000, 2^10000)")]
    public void ComputationLimitsProduceStructuredFailures(string expression)
    {
        Assert.IsFalse(Calculator.TryEvaluate(expression, out _, out var diagnostic));
        Assert.AreEqual(EvaluationError.LimitExceeded, diagnostic!.Code);
        Assert.Throws<CalculationLimitException>(() => Calculator.Evaluate(expression));
    }

    [TestMethod]
    public void InputLengthCeilingAcceptsBoundaryAndRejectsExcess()
    {
        var accepted = "1" + new string(' ', CalculationLimits.MaxInputLength - 1);
        Assert.AreEqual(BigInteger.One, Calculator.Evaluate(accepted).IntVal);
        Assert.IsFalse(Calculator.TryEvaluate(accepted + " ", out _, out var diagnostic));
        Assert.AreEqual(EvaluationError.LimitExceeded, diagnostic!.Code);
    }

    [TestMethod]
    public void NestingCeilingRejectsDeepInputWithoutStackOverflow()
    {
        var depth = CalculationLimits.MaxNestingDepth;
        // The literal itself occupies a unary-parser frame, leaving depth - 1 frames
        // for enclosing groups at the accepted boundary.
        var accepted = new string('(', depth - 1) + "1" + new string(')', depth - 1);
        var rejected = new string('(', depth) + "1" + new string(')', depth);
        Assert.AreEqual(BigInteger.One, Calculator.Evaluate(accepted).IntVal);
        Assert.IsFalse(Calculator.TryEvaluate(rejected, out _, out var diagnostic));
        Assert.AreEqual(EvaluationError.LimitExceeded, diagnostic!.Code);
    }

    [TestMethod]
    public void OptionsTightenLimitsWithoutChangingOtherEvaluations()
    {
        var options = new EvaluationOptions { MaxInputLength = 3, MaxNestingDepth = 2 };
        Assert.AreEqual(new BigInteger(3), Calculator.Evaluate("1+2", options).IntVal);
        Assert.IsFalse(Calculator.TryEvaluate("12+3", out _, out var diagnostic, options));
        Assert.AreEqual(EvaluationError.LimitExceeded, diagnostic!.Code);
        Assert.AreEqual(new BigInteger(15), Calculator.Evaluate("12+3").IntVal);
        Assert.IsFalse(Calculator.TryEvaluate("--1", out _, out _, options));
        Assert.Throws<ArgumentOutOfRangeException>(() => Calculator.Evaluate("1", options with { MaxInputLength = CalculationLimits.MaxInputLength + 1 }));
    }

    [TestMethod]
    public void ParallelEvaluationsDoNotShareParserState()
    {
        Parallel.For(1, 100, i =>
        {
            Assert.AreEqual(new BigInteger(i * i), Calculator.Evaluate($"{i}^2").IntVal);
        });
    }
}
