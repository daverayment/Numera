using Numera.Ast;

namespace Numera.Tests;

[TestClass]
public sealed class CapturedValueTests
{
    [TestMethod]
    public void VariableDefinitionDoesNotResolveChangedBindings()
    {
        var engine = new CalculatorEngine();
        var initial = engine.Evaluate("x = 3");
        var assigned = engine.Evaluate("a = x / 3", initial.Context);
        var changed = engine.Evaluate("x = 6", assigned.Context);

        Assert.AreEqual(new MathValue(3), engine.Evaluate("a * 3", changed.Context).Value);
        Assert.AreEqual(new MathValue(1), engine.Evaluate("a + 0", changed.Context).Value);
    }

    [TestMethod]
    public void ResultCompositionUsesValuesRatherThanMergedBindings()
    {
        var engine = new CalculatorEngine();
        var first = engine.Evaluate("x", engine.Evaluate("x = 3").Context);
        var second = engine.Evaluate("6", engine.Evaluate("x = 6").Context);

        var combined = first + second;
        Assert.AreEqual(new MathValue(9), combined.Value);
        Assert.AreEqual(new MathValue(6), engine.Evaluate("x", combined.Context).Value);
        Assert.AreEqual(new MathValue(9), combined.Context.PreviousResult);
    }

    [TestMethod]
    public void UnaryAndMixedCompositionDoNotReplayAns()
    {
        var engine = new CalculatorEngine();
        var result = engine.Evaluate("ans + 1", engine.Evaluate("5").Context);

        Assert.AreEqual(new MathValue(6), (+result).Value);
        Assert.AreEqual(new MathValue(-6), (-result).Value);
        Assert.AreEqual(new MathValue(7), (result + new MathValue(1)).Value);
        Assert.AreEqual(new MathValue(7), (new MathValue(1) + result).Value);
    }

    [TestMethod]
    public void EachReachedCallbackOccurrenceRunsOnceInSourceOrder()
    {
        var calls = new List<int>();
        var engine = new CalculatorEngine(new CalculatorOptions
        {
            CustomFunctions =
            [
                CustomFunction.Create("tick", () =>
                {
                    calls.Add(calls.Count + 1);
                    return new MathValue((double)calls.Count);
                })
            ]
        });

        var result = engine.Evaluate("tick() * 1 / 1");
        Assert.AreEqual(new MathValue(1.0), result.Value);
        CollectionAssert.AreEqual(new[] { 1 }, calls);

        var combined = result + result;
        Assert.AreEqual(new MathValue(2.0), combined.Value);
        Assert.AreEqual(new MathValue(1.0), (+result).Value);
        Assert.HasCount(1, calls);

        Assert.AreEqual(new MathValue(5.0), engine.Evaluate("tick() + tick()").Value);
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, calls);
    }

    [TestMethod]
    public void GroupedDivisionPreservesInnerZeroErrorAndSpan()
    {
        var exception = Assert.Throws<ExpressionException>(
            () => Calculator.Evaluate("1 / (1 / 0)"));
        Assert.AreEqual(EvaluationError.DivisionByZero, exception.Diagnostic.Code);
        Assert.AreEqual(5, exception.Diagnostic.Position);
    }

    [TestMethod]
    public void ShortCircuitSkipsUnreachedCallbacksAndComparisonOperands()
    {
        int calls = 0;
        var engine = new CalculatorEngine(new CalculatorOptions
        {
            CustomFunctions =
            [
                CustomFunction.Create("tick", () => new MathValue(++calls))
            ]
        });

        Assert.AreEqual(new MathValue(0), engine.Evaluate("0 and tick()").Value);
        Assert.AreEqual(new MathValue(1), engine.Evaluate("1 or tick()").Value);
        Assert.AreEqual(new MathValue(0), engine.Evaluate("2 < 1 < tick()").Value);
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    public void AssignmentOperandsRunInSourceOrderAndCompositionDoesNotReassign()
    {
        var engine = new CalculatorEngine();
        var result = engine.Evaluate("(x = 2) + (x = x + 1)");
        Assert.AreEqual(new MathValue(5), result.Value);
        Assert.AreEqual(new MathValue(3), result.Context.Variables["x"].Value);

        var combined = result + new MathValue(0);
        Assert.AreEqual(new MathValue(5), combined.Value);
        Assert.AreEqual(new MathValue(3), combined.Context.Variables["x"].Value);
        Assert.IsFalse(combined.HasAssignments);
    }

    [TestMethod]
    public void ComposedPercentUsesOriginalShapeButCapturedNumericValue()
    {
        var engine = new CalculatorEngine();
        var percent = engine.Evaluate("10%");
        var total = engine.Evaluate("200");
        var markedUp = total + percent;

        Assert.AreEqual(new MathValue(220), markedUp.Value);
        Assert.IsInstanceOfType<BinaryNode>(markedUp.Context.PreviousExpression);
        Assert.AreEqual(new MathValue(200.1m), (total + new MathValue(0.1m)).Value);
        Assert.AreEqual(new MathValue(20), (total * percent).Value);
    }

    [TestMethod]
    public void SharedAstNodeIsEvaluatedForEachWrittenOccurrenceNotCached()
    {
        int calls = 0;
        var engine = new CalculatorEngine(new CalculatorOptions
        {
            CustomFunctions = [CustomFunction.Create("tick", () => new MathValue(++calls))]
        });
        var shared = new FunctionCallNode("tick", []);
        var result = engine.Evaluate(new BinaryNode(BinaryOperator.Add, shared, shared));

        Assert.AreEqual(new MathValue(3), result.Value);
        Assert.AreEqual(2, calls);
        Assert.AreEqual(new MathValue(6), (result + result).Value);
        Assert.AreEqual(2, calls);
    }

    [TestMethod]
    public void ResultMetadataRemainsBoundedWithoutReplayingIt()
    {
        var engine = new CalculatorEngine(new CalculatorOptions { MaxInputLength = 3 });
        var first = engine.Evaluate("0");
        var second = first + first;
        Assert.AreEqual(new MathValue(0), second.Value);
        Assert.Throws<CalculationLimitException>(() => _ = second + second);
    }

    [TestMethod]
    [DataRow("255", "1", "+")]
    [DataRow("-255", "1", "-")]
    [DataRow("128", "2", "*")]
    [DataRow("1/128", "2", "/")]
    [DataRow("128/251", "1/241", "%")]
    public void BinaryCompositionEnforcesConfiguredResultLimits(string leftExpression, string rightExpression, string operation)
    {
        var engine = new CalculatorEngine(new CalculatorOptions { MaxIntegerBits = 8 });
        var left = engine.Evaluate(leftExpression);
        var right = engine.Evaluate(rightExpression);

        foreach (var compose in BinaryCompositions(left, right, operation))
        {
            var exception = Assert.Throws<CalculationLimitException>(() => compose());
            Assert.AreEqual(EvaluationError.LimitExceeded, exception.Diagnostic.Code);
        }
    }

    [TestMethod]
    [DataRow("254", "1", "+", "255")]
    [DataRow("-254", "1", "-", "-255")]
    [DataRow("64", "2", "*", "128")]
    [DataRow("1/64", "2", "/", "1/128")]
    [DataRow("2/3", "1/3", "%", "0")]
    public void BinaryCompositionAllowsValuesWithinConfiguredLimits(
        string leftExpression, string rightExpression, string operation, string expectedExpression)
    {
        var engine = new CalculatorEngine(new CalculatorOptions { MaxIntegerBits = 8 });
        var left = engine.Evaluate(leftExpression);
        var right = engine.Evaluate(rightExpression);
        var expected = Calculator.Evaluate(expectedExpression).Value;

        foreach (var compose in BinaryCompositions(left, right, operation))
        {
            var result = compose();
            Assert.AreEqual(expected, result.Value);
            Assert.AreSame(engine.Options, result.Options);
            Assert.AreEqual(expected, result.Context.PreviousResult);
        }
    }

    [TestMethod]
    public void CompositionValidatesCapturedOperandsBeforeCancellation()
    {
        var engine = new CalculatorEngine(new CalculatorOptions { MaxIntegerBits = 8 });
        var zero = engine.Evaluate("0");
        var oversized = Calculator.Evaluate("256");
        Action[] operations =
        [
            () => _ = zero * oversized,
            () => _ = zero * oversized.Value,
            () => _ = oversized.Value * zero,
            () => _ = zero.Value / new EvaluationResult(
                oversized.Value, oversized.Context, options: engine.Options)
        ];

        foreach (var operation in operations)
        {
            Assert.Throws<CalculationLimitException>(operation);
        }

        Assert.AreEqual(new MathValue(0), (oversized * zero).Value);
        Assert.AreSame(oversized.Options, (oversized * zero).Options);
    }

    [TestMethod]
    public void UnaryCompositionEnforcesConfiguredOperandLimits()
    {
        var engine = new CalculatorEngine(new CalculatorOptions { MaxIntegerBits = 8 });
        var oversized = Calculator.Evaluate("1/256");
        var captured = new EvaluationResult(oversized.Value, oversized.Context, options: engine.Options);

        Assert.Throws<CalculationLimitException>(() => _ = +captured);
        Assert.Throws<CalculationLimitException>(() => _ = -captured);
        Assert.AreEqual(new MathValue(-255), (-engine.Evaluate("255")).Value);
        Assert.AreEqual(new MathValue(255), (+engine.Evaluate("255")).Value);
    }

    [TestMethod]
    public void PercentageCompositionEnforcesConfiguredResultLimits()
    {
        var engine = new CalculatorEngine(new CalculatorOptions { MaxIntegerBits = 8 });
        var total = engine.Evaluate("200");
        var percent = engine.Evaluate("50%");

        Assert.Throws<CalculationLimitException>(() => _ = total + percent);
        Assert.Throws<CalculationLimitException>(() => _ = total.Value + percent);
        Assert.Throws<CalculationLimitException>(() => _ = total - -percent);
        Assert.AreEqual(new MathValue(250), (total + engine.Evaluate("25%")).Value);
    }

    private static Func<EvaluationResult>[] BinaryCompositions(EvaluationResult left, EvaluationResult right, string operation) =>
        operation switch
        {
            "+" => [() => left + right, () => left + right.Value, () => left.Value + right],
            "-" => [() => left - right, () => left - right.Value, () => left.Value - right],
            "*" => [() => left * right, () => left * right.Value, () => left.Value * right],
            "/" => [() => left / right, () => left / right.Value, () => left.Value / right],
            "%" => [() => left % right, () => left % right.Value, () => left.Value % right],
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

    [TestMethod]
    public void ResultMetadataDoesNotRequireOriginalFunctionsInTheReceivingEngine()
    {
        int calls = 0;
        var source = new CalculatorEngine(new CalculatorOptions
        {
            CustomFunctions = [CustomFunction.Create("tick", () => new MathValue(++calls))]
        });
        var combined = Calculator.Evaluate("1") + source.Evaluate("tick()");
        Assert.AreEqual(new MathValue(2), combined.Value);
        Assert.AreEqual(1, calls);
        Assert.IsTrue((combined + new MathValue(double.PositiveInfinity)).Value.DoubleValue
            == double.PositiveInfinity);
    }
}
