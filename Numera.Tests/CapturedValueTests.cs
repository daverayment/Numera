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

    [TestMethod]
    public void RelationalOperatorsBetweenResults_EvaluateCorrectlyWithoutAmbiguity()
    {
        var five = Calculator.Evaluate("5");
        var ten = Calculator.Evaluate("10");
        var anotherFive = Calculator.Evaluate("2 + 3");

        // < and >
        Assert.IsTrue(five < ten);
        Assert.IsFalse(ten < five);
        Assert.IsFalse(five < anotherFive);

        Assert.IsTrue(ten > five);
        Assert.IsFalse(five > ten);
        Assert.IsFalse(five > anotherFive);

        // <= and >=
        Assert.IsTrue(five <= ten);
        Assert.IsTrue(five <= anotherFive);
        Assert.IsFalse(ten <= five);

        Assert.IsTrue(ten >= five);
        Assert.IsTrue(five >= anotherFive);
        Assert.IsFalse(five >= ten);
    }

    [TestMethod]
    public void CompareTo_BetweenResultsAndMathValues_ComparesNumerically()
    {
        var small = Calculator.Evaluate("1/4");
        var large = Calculator.Evaluate("1/2");
        var equalSmall = Calculator.Evaluate("0.25");

        // CompareTo(EvaluationResult)
        Assert.IsTrue(small.CompareTo(large) < 0);
        Assert.IsTrue(large.CompareTo(small) > 0);
        Assert.AreEqual(0, small.CompareTo(equalSmall));

        // CompareTo(MathValue)
        Assert.IsTrue(small.CompareTo(new MathValue(0.5m)) < 0);
        Assert.IsTrue(large.CompareTo(new MathValue(0.25m)) > 0);
        Assert.AreEqual(0, small.CompareTo(new MathValue(0.25m)));

        // Results with distinct context and assignments compare equal numerically
        var assigned = Calculator.Evaluate("x = 10");
        var bare = Calculator.Evaluate("10");
        Assert.AreEqual(assigned, bare);
        Assert.AreEqual(0, assigned.CompareTo(bare));
        Assert.IsTrue(assigned <= bare);
        Assert.IsTrue(assigned >= bare);
        Assert.IsFalse(assigned < bare);
        Assert.IsFalse(assigned > bare);
    }

    [TestMethod]
    public void IComparable_ExplicitImplementation_HandlesObjectsCorrectly()
    {
        var result = Calculator.Evaluate("42");
        IComparable comparable = result;

        // Comparing with another EvaluationResult
        var otherResult = Calculator.Evaluate("100");
        Assert.IsTrue(comparable.CompareTo(otherResult) < 0);
        Assert.IsTrue(comparable.CompareTo(Calculator.Evaluate("42")) == 0);

        Assert.Throws<ArgumentException>(() => comparable.CompareTo(new MathValue(42)));

        // Comparing with null returns 1 by .NET convention
        Assert.AreEqual(1, comparable.CompareTo(null));

        // Incompatible object throws ArgumentException
        Assert.Throws<ArgumentException>(() => comparable.CompareTo("not a number"));
        Assert.Throws<ArgumentException>(() => comparable.CompareTo(42));
    }

    [TestMethod]
    public void Sorting_EvaluationResults_SortsCorrectly()
    {
        EvaluationResult[] results =
        [
            Calculator.Evaluate("10"),
            Calculator.Evaluate("0 * exp(1000)"),  // NaN
            Calculator.Evaluate("-5"),
            Calculator.Evaluate("exp(1000)"),      // Positive Infinity
            Calculator.Evaluate("0"),
            Calculator.Evaluate("-exp(1000)"),     // Negative Infinity
            Calculator.Evaluate("2.5")
        ];

        Array.Sort(results);

        // NaN sorts first, then -Infinity, -5, 0, 2.5, 10, +Infinity
        Assert.IsTrue(results[0].IsNaN);
        Assert.AreEqual(double.NegativeInfinity, results[1].DoubleValue);
        Assert.AreEqual(new MathValue(-5), results[2].Value);
        Assert.AreEqual(new MathValue(0), results[3].Value);
        Assert.AreEqual(new MathValue(2.5m), results[4].Value);
        Assert.AreEqual(new MathValue(10), results[5].Value);
        Assert.AreEqual(double.PositiveInfinity, results[6].DoubleValue);
    }
}
