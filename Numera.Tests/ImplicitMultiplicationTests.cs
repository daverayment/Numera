namespace Numera.Tests;

[TestClass]
public sealed class ImplicitMultiplicationTests
{
    private static CalculatorEngine CreateEngineWithCustomFunctions()
    {
        var options = new CalculatorOptions
        {
            CustomFunctions =
            [
                CustomFunction.Create("double", (MathValue x) => x * new MathValue(2m)),
                CustomFunction.Create("triple", (MathValue x) => x * new MathValue(3m)),
                CustomFunction.Create("addOne", (MathValue x) => x + new MathValue(1m))
            ]
        };

        return new CalculatorEngine(options);
    }

    [TestMethod]
    [DataRow("(2)3", 6)]
    [DataRow("(2) 3", 6)]
    [DataRow("2(3)", 6)]
    [DataRow("(2)(3)", 6)]
    [DataRow("2(3)4", 24)]
    [DataRow("(2)(3)4", 24)]
    [DataRow("(1)(2)(3)4", 24)]
    [DataRow("(1 + 2) 3", 9)]
    [DataRow("(1 + 2)(3 + 4)", 21)]
    [DataRow("(1 + 2)(3 + 4)2", 42)]
    [DataRow("(2).5", 1.0)]
    [DataRow("triple(3) 4", 36)]
    public void ChainedImplicitMultiplication_WithTrailingNumbers_EvaluatesCorrectly(string expression, double expected)
    {
        var engine = CreateEngineWithCustomFunctions();
        var result = engine.Evaluate(expression);
        Assert.AreEqual(expected, result.AsDouble(), 1e-12);
    }

    [TestMethod]
    [DataRow("1 000")]
    [DataRow("2 3")]
    [DataRow("12 34")]
    public void BareNumbers_SeparatedByWhitespace_AreRejected(string expression)
    {
        // Must fail with syntax error (unexpected trailing input), not silently multiply.
        Assert.IsFalse(TestEngines.RawEngine.TryEvaluate(expression, out _, out var diagnostic));
        Assert.AreEqual(EvaluationError.InvalidSyntax, diagnostic.Code);
    }

    [TestMethod]
    [DataRow("2true")]
    [DataRow("2false")]
    [DataRow("2not 3")]
    [DataRow("2of 3")]
    [DataRow("2and")]
    [DataRow("2or")]
    public void BlockedKeywordsAndBooleans_DoNotFormImplicitMultiplication(string expression)
    {
        // Keywords and booleans cannot start an implicit product (e.g. "2true" is not
        // 2 * 1).
        Assert.IsFalse(Calculator.TryEvaluate(expression, out _, out var diagnostic));
        Assert.AreEqual(EvaluationError.InvalidSyntax, diagnostic.Code);
    }

    public void InfixKeywords_EvaluateAsLogicalOperators_NotMultiplication()
    {
        // "2and 3" is not "2 * and * 3" (=6), but rather a logical AND of two non-zero
        // values, which is true (1).
        Assert.AreEqual(1m, Calculator.Evaluate("2and 3").DecVal);
        Assert.AreEqual(1m, Calculator.Evaluate("2or 3").DecVal);
    }

    [TestMethod]
    public void CustomFunctions_ParticipateInImplicitMultiplication()
    {
        var engine = CreateEngineWithCustomFunctions();

        // Number before custom function (with and without space).
        Assert.AreEqual(20m, engine.Evaluate("2double(5)").DecVal);
        Assert.AreEqual(20m, engine.Evaluate("2 double(5)").DecVal);

        // Number after custom function.
        Assert.AreEqual(36m, engine.Evaluate("triple(3)4").DecVal);

        // Multiple custom functions chained together.
        Assert.AreEqual(54m, engine.Evaluate("double(3)triple(3)").DecVal);
        Assert.AreEqual(54m, engine.Evaluate("double(3) triple(3)").DecVal);

        // Custom functions chained with groups and trailing number.
        Assert.AreEqual(48m, engine.Evaluate("2(double(3))4").DecVal);
    }

    [TestMethod]
    [DataRow("2x", 20)]
    [DataRow("2 x", 20)]
    [DataRow("x y", 30)]
    [DataRow("2x y", 60)]
    [DataRow("2(x) y", 60)]
    public void Variables_SupportJuxtapositionWithAndWithoutWhitespace(string expression, int expected)
    {
        var context = new EvaluationContext(
            new VariableCollection(
            [
                new Variable("x", new MathValue(10m)),
                new Variable("y", new MathValue(3m))
            ]), null);

        var result = Calculator.Evaluate(expression, context);
        Assert.AreEqual(expected, (int)result.DecVal);
    }


    [TestMethod]
    public void AllowImplicitMultiplication_Disabled_RejectsAllJuxtaposition()
    {
        var strictEngine = new CalculatorEngine(
            new CalculatorOptions { AllowImplicitMultiplication = false });

        var context = new EvaluationContext(
        [
            new Variable("x", new(5m)),
        ], null);

        Assert.IsFalse(strictEngine.TryEvaluate("2x", out _, out var d1, context));
        Assert.AreEqual(EvaluationError.InvalidSyntax, d1.Code);

        Assert.IsFalse(strictEngine.TryEvaluate("2(3)", out _, out var d2, context));
        Assert.AreEqual(EvaluationError.InvalidSyntax, d2.Code);

        Assert.IsFalse(strictEngine.TryEvaluate("(1 + 2)(3 + 4)", out _, out var d3, context));
        Assert.AreEqual(EvaluationError.InvalidSyntax, d3.Code);
    }
}
