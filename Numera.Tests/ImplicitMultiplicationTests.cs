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
                CustomFunction.Create("double", x => x * new MathValue(2m)),
                CustomFunction.Create("triple", x => x * new MathValue(3m)),
                CustomFunction.Create("addOne", x => x + new MathValue(1m))
            ]
        };

        return new CalculatorEngine(options);
    }

    [TestMethod]
    // Group and group.
    [DataRow("(1 + 2)(3 + 4)", 21)]
    [DataRow("(1 + 2) (3 + 4)", 21)]
    [DataRow("(2)(3)(4)", 24)]

    // Number and built-in function.
    [DataRow("2sqrt(16)", 8)]
    [DataRow("2 sqrt(16)", 8)]
    [DataRow("2log10(100)", 4)]
    [DataRow("3cbrt(27)", 9)]

    // Built-in function and group / built-in function.
    [DataRow("sqrt(16)(2 + 3)", 20)]
    [DataRow("(2 + 3)sqrt(16)", 20)]
    [DataRow("sqrt(16)cbrt(27)", 12)]
    [DataRow("sqrt(16) cbrt(27)", 12)]

    // Custom functions (mixed with groups and built-ins).
    [DataRow("2double(5)", 20)]
    [DataRow("2 double(5)", 20)]
    [DataRow("double(3)triple(3)", 54)]
    [DataRow("double(3) triple(3)", 54)]
    public void ImplicitMultiplication_WithFunctionsAndGroups_WithoutTrailingNumbers_EvaluatesCorrectly(
        string expression, double expected)
    {
        var engine = CreateEngineWithCustomFunctions();
        Assert.AreEqual(expected, engine.Evaluate(expression).AsDouble(), 1e-12);
    }

    [TestMethod]
    // Group then number.
    [DataRow("(2)3", 6)]
    [DataRow("(2) 3", 6)]
    [DataRow("(1 + 2)3", 9)]
    [DataRow("(1 + 2) 3", 9)]
    [DataRow("(2).5", 1)]

    // Built-in function then number.
    [DataRow("sqrt(16)2", 8)]
    [DataRow("sqrt(16) 2", 8)]
    [DataRow("log10(100)5", 10)]

    // Custom function then number.
    [DataRow("triple(3)4", 36)]
    [DataRow("triple(3) 4", 36)]

    // Chained groups/functions with a final trailing number.
    [DataRow("2(3)4", 24)]
    [DataRow("(2)(3)4", 24)]
    [DataRow("(1)(2)(3)4", 24)]
    [DataRow("(1 + 2)(3 + 4)2", 42)]
    [DataRow("(1 + 2)(3 + 4) 2", 42)]
    [DataRow("2(double(3))4", 48)]
    [DataRow("sqrt(16)(3)2", 24)]
    public void ChainedImplicitMultiplication_WithTrailingNumbers_EvaluatesCorrectly(
        string expression, double expected)
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

    [TestMethod]
    public void InfixKeywords_EvaluateAsLogicalOperators_NotMultiplication()
    {
        // "2and 3" is not "2 * and * 3" (=6), but rather a logical AND of two non-zero
        // values, which is true (1).
        Assert.AreEqual(1m, Calculator.Evaluate("2and 3").DecimalValue);
        Assert.AreEqual(1m, Calculator.Evaluate("2or 3").DecimalValue);
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
        Assert.AreEqual(expected, (int)result.DecimalValue);
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
