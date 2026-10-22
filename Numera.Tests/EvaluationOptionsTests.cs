namespace Numera.Tests;

[TestClass]
public sealed class EvaluationOptionsTests
{
    [TestMethod]
    public void PropertyOrder_PreservesPreviousResultAns()
    {
        var customVars = new Dictionary<string, MathValue> { ["x"] = new(42m) };

        // PreviousResult declared before Variables.
        var options1 = new EvaluationOptions
        {
            PreviousResult = new MathValue(123m),
            Variables = customVars
        };
        Assert.AreEqual(165m, Calculator.Evaluate("ans + x", options1).DecVal);

        // PreviousResult declared after Variables.
        var options2 = new EvaluationOptions
        {
            Variables = customVars,
            PreviousResult = new MathValue(55m)
        };
        Assert.AreEqual(97m, Calculator.Evaluate("ans + x", options2).DecVal);
    }

    [TestMethod]
    public void WithExpression_CanUpdateOrClearPreviousResult()
    {
        var options = new EvaluationOptions
        {
            PreviousResult = new MathValue(100m)
        };
        Assert.AreEqual(200m, Calculator.Evaluate("ans * 2", options).DecVal);

        var updatedOptions = options with { PreviousResult = new MathValue(50m) };
        Assert.AreEqual(100m, Calculator.Evaluate("ans * 2", updatedOptions).DecVal);

        var clearedOptions = options with { PreviousResult = null };
        Assert.IsFalse(Calculator.TryEvaluate("ans * 2", out _, out var diag, clearedOptions));
        Assert.AreEqual(EvaluationError.UnknownConstant, diag!.Code);
    }
    
    [TestMethod]
    public void ExplicitVariablesAns_TakesPrecedenceOverPreviousResult()
    {
        var options = new EvaluationOptions
        {
            PreviousResult = new MathValue(100m),
            Variables = new Dictionary<string, MathValue> { ["ans"] = new(42m) }
        };

        // Explicit variable 'ans' should take precedence over PreviousResult.
        Assert.AreEqual(84m, Calculator.Evaluate("ans * 2", options).DecVal);
    }

    [TestMethod]
    public void Validate_RejectsOutOfRangeLimits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EvaluationOptions { MaxInputLength = 0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EvaluationOptions { MaxInputLength = -1 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EvaluationOptions { MaxInputLength = CalculationLimits.MaxInputLength + 1 }.Validate());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EvaluationOptions { MaxNestingDepth = 0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EvaluationOptions { MaxNestingDepth = -1 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EvaluationOptions { MaxNestingDepth = CalculationLimits.MaxNestingDepth + 1 }.Validate());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EvaluationOptions { AngleUnit = (AngleUnit)(-1) }.Validate());
    }
}
