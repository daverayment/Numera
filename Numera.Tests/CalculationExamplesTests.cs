using System.Globalization;

namespace Numera.Tests;

[TestClass]
public sealed class CalculationExamplesTests
{
    [TestMethod]
    [DataRow("2 + 3 * 4", "14")]
    [DataRow("(2 + 3) * 4", "20")]
    [DataRow("2^3^2", "512")]
    [DataRow("-2^2", "-4")]
    [DataRow("(-2)^2", "4")]
    [DataRow("0.1 + 0.2", "0.3")]
    [DataRow("0.3 / 0.1", "3")]
    [DataRow("3033554.11 - 3033359.71", "194.4")]
    [DataRow("(1/49)*49", "1")]
    [DataRow("(2^53 + 1) - 2^53", "1")]
    [DataRow("(10^20 + 0.125) - 10^20", "0.125")]
    [DataRow("((2^2000+1)/(2^2000-1)-1)*(2^2000-1)", "2")]
    [DataRow("1/3", "1/3")]
    [DataRow("1/3 + 0.5", "5/6")]
    [DataRow("1/8", "0.125")]
    [DataRow("200 + 15%", "230")]
    [DataRow("200 - 15%", "170")]
    [DataRow("15% of 200", "30")]
    [DataRow("200 * 15%", "30")]
    [DataRow("200 / (1 + 15%)", "4000/23")]
    [DataRow("2(3 + 4)", "14")]
    [DataRow("(2 + 3)(4 + 1)", "25")]
    [DataRow("= 2 × (3 + 4) =", "14")]
    [DataRow("5 − 2", "3")]
    [DataRow("5! × 2", "240")]
    [DataRow("1'000'000 + 1", "1000001")]
    [DataRow("sqrt(144)", "12")]
    [DataRow("cbrt(-125)", "-5")]
    [DataRow("root(81,4)", "3")]
    [DataRow("abs(-12.5)", "12.5")]
    [DataRow("sign(-12.5)", "-1")]
    [DataRow("floor(-7/3)", "-3")]
    [DataRow("ceil(-7/3)", "-2")]
    [DataRow("trunc(-7/3)", "-2")]
    [DataRow("frac(-7/3)", "-1/3")]
    [DataRow("round(2.5)", "3")]
    [DataRow("round(-2.5)", "-3")]
    [DataRow("round(2.55,1)", "2.6")]
    [DataRow("mod(17,5)", "2")]
    [DataRow("clamp(25,0,10)", "10")]
    [DataRow("lerp(10,20,0.25)", "12.5")]
    [DataRow("min(7,2,9)", "2")]
    [DataRow("max(7,2,9)", "9")]
    [DataRow("avg(1/3,2/3)", "0.5")]
    [DataRow("sum(1/3,1/3,1/3)", "1")]
    [DataRow("mul(2,3,4)", "24")]
    [DataRow("median(1,9,3,5)", "4")]
    [DataRow("variance(1,2,3)", "1")]
    [DataRow("gcd(24,36,60)", "12")]
    [DataRow("lcm(4,6)", "12")]
    [DataRow("factorial(6)", "720")]
    [DataRow("comb(52,5)", "2598960")]
    [DataRow("perm(10,3)", "720")]
    [DataRow("isprime(97)", "1")]
    [DataRow("divcount(12)", "6")]
    [DataRow("sigma(12)", "28")]
    [DataRow("sigma(12,2)", "210")]
    [DataRow("totient(12)", "4")]
    [DataRow("0xFF + 1", "256")]
    [DataRow("0b101010", "42")]
    [DataRow("0o755", "493")]
    [DataRow("1 << 8", "256")]
    [DataRow("255 >> 4", "15")]
    [DataRow("band(0b1100,0b1010)", "8")]
    [DataRow("bor(0b1100,0b1010)", "14")]
    [DataRow("bxor(0b1100,0b1010)", "6")]
    [DataRow("bnot(5)", "-6")]
    [DataRow("popcount(0b101101)", "4")]
    [DataRow("0.1 + 0.2 == 0.3", "1")]
    [DataRow("1 < 2 < 3", "1")]
    [DataRow("1 < 3 < 2", "0")]
    [DataRow("0 and (1/0)", "0")]
    [DataRow("1 or (1/0)", "1")]
    [DataRow("1 > 2 < (1/0)", "0")]
    [DataRow("true xor false", "1")]
    [DataRow("not 0", "1")]
    public void GuideExactAnswersRemainExact(string expression, string expected)
    {
        var result = Calculator.Evaluate(expression);
        Assert.AreEqual(Calculator.Evaluate(expected).Value, result.Value, expression);
        Assert.AreEqual(CalculationAccuracy.Exact, result.Accuracy, expression);
        Assert.IsTrue(result.HasExactFraction);
    }

    [TestMethod]
    [DataRow("1/3", "0.333333333333333333333333", true)]
    [DataRow("1/8", "0.125", false)]
    [DataRow("2^100", "1.2676506002282294014967e30", true)]
    [DataRow("1e-400", "1e-400", false)]
    [DataRow("sqrt(2^2000)", "1.07150860718626732094843e301", true)]
    public void GuideNumericDisplaysAreSeparateFromExactAnswers(string expression, string display, bool rounded)
    {
        var result = Calculator.Evaluate(expression);
        var card = result.FormatResult(CultureInfo.InvariantCulture);
        Assert.AreEqual(display, card.PrimaryDisplay);
        Assert.AreEqual(rounded, card.IsDisplayRounded);
        Assert.AreEqual(CalculationAccuracy.Exact, result.Accuracy);
    }

    [TestMethod]
    [DataRow("sin(pi/2)", 1.0)]
    [DataRow("sqrt(2)", 1.4142135623730951)]
    [DataRow("ln(e)", 1.0)]
    [DataRow("log(1000)", 3.0)]
    [DataRow("log2(1024)", 10.0)]
    [DataRow("exp(1)", 2.718281828459045)]
    [DataRow("expm1(1)", 1.718281828459045)]
    [DataRow("sinh(0)", 0.0)]
    [DataRow("cosh(0)", 1.0)]
    [DataRow("tanh(0)", 0.0)]
    [DataRow("arsinh(0)", 0.0)]
    [DataRow("acosh(1)", 0.0)]
    [DataRow("atanh(0)", 0.0)]
    public void GuideFloatingPointSpecialFunctionsMatchDoubleSemantics(string expression, double expected)
    {
        Assert.AreEqual(expected, Calculator.Evaluate(expression).AsDouble(), 1e-15);
    }

    [TestMethod]
    public void GuideSessionsAnglesAndCultureUseExplicitEngineConfiguration()
    {
        var degrees = new CalculatorEngine(new CalculatorOptions { AngleUnit = AngleUnit.Degrees });
        Assert.AreEqual(0.5, degrees.Evaluate("sin(30)").AsDouble(), 1e-15);
        Assert.AreEqual(0.5, degrees.Evaluate("cos(60)").AsDouble(), 1e-15);
        Assert.AreEqual(1.0, degrees.Evaluate("tan(45)").AsDouble(), 1e-15);
        Assert.AreEqual(90.0, degrees.Evaluate("arcsin(1)").AsDouble(), 1e-15);
        var gradians = new CalculatorEngine(new CalculatorOptions { AngleUnit = AngleUnit.Gradians });
        Assert.AreEqual(1.0, gradians.Evaluate("sin(100)").AsDouble(), 1e-15);
        var german = CalculatorEngine.ForCulture(CultureInfo.GetCultureInfo("de-DE"));
        Assert.AreEqual("1.000,75", german.Evaluate("1.000,50 + 0,25").PrimaryDisplay);
    }

    [TestMethod]
    [DataRow("1/0", EvaluationError.DivisionByZero)]
    [DataRow("1/(1/0)", EvaluationError.DivisionByZero)]
    [DataRow("sqrt(-1)", EvaluationError.UndefinedMathResult)]
    [DataRow("log(0)", EvaluationError.UndefinedMathResult)]
    [DataRow("2.5 << 1", EvaluationError.IntegerRequired)]
    [DataRow("10^5000", EvaluationError.LimitExceeded)]
    public void GuideFailuresAreExplicit(string expression, EvaluationError error)
    {
        Assert.IsFalse(Calculator.TryEvaluate(expression, out _, out var diagnostic));
        Assert.AreEqual(error, diagnostic.Code);
    }
}
