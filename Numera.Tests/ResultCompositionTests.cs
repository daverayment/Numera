using System.Globalization;
using Numera.Ast;

namespace Numera.Tests;

[TestClass]
public sealed class ResultCompositionTests
{
    [TestMethod]
    public void CompatibleVariablesFormAUnionKeepingLeftEntriesAndOrder()
    {
        var leftExpression = new BinaryNode(BinaryOperator.Add,
            new NumberNode(new MathValue(1), new TextSpan(0, 1)),
            new NumberNode(new MathValue(1), new TextSpan(4, 1)), new TextSpan(0, 5));
        var rightExpression = new BinaryNode(BinaryOperator.Add,
            new NumberNode(new MathValue(1), new TextSpan(10, 1)),
            new NumberNode(new MathValue(1), new TextSpan(14, 1)), new TextSpan(10, 5));
        var rate = new Variable("Rate", new MathValue(2), leftExpression, "1 + 1");
        var otherRate = new Variable("RATE", new MathValue(2), rightExpression, "1 + 1");
        var left = Result(new MathValue(8), [rate, new Variable("fee", new MathValue(3))]);
        var right = Result(new MathValue(2), [new Variable("tax", new MathValue(4)), otherRate]);

        foreach (var compose in BinaryOperations(left, right))
        {
            var combined = compose();
            CollectionAssert.AreEqual(new[] { "Rate", "fee", "tax" }, combined.Context.Variables.Keys.ToArray());
            Assert.AreEqual(rate, combined.Context.Variables["rate"]);
            Assert.AreSame(leftExpression, combined.Context.Variables["rate"].Expression);
            Assert.AreEqual(new MathValue(3), combined.Context.Variables["fee"].Value);
            Assert.AreEqual(new MathValue(4), combined.Context.Variables["tax"].Value);
            Assert.AreEqual(combined.Value, combined.Context.PreviousResult);
            Assert.IsFalse(combined.HasAssignments);
            Assert.IsInstanceOfType<BinaryNode>(combined.Context.PreviousExpression);
        }

        Assert.HasCount(2, left.Context.Variables);
        Assert.HasCount(2, right.Context.Variables);
        Assert.AreEqual("RATE", right.Context.Variables["rate"].Name);
        Assert.AreSame(rightExpression, right.Context.Variables["rate"].Expression);
        Assert.AreEqual(new MathValue(8), left.Context.PreviousResult);
        Assert.AreEqual(new MathValue(2), right.Context.PreviousResult);
    }

    [TestMethod]
    public void SharedVariablesRequireIdenticalStoredValues()
    {
        (MathValue Left, MathValue Right)[] cases =
        [
            (new MathValue(2), new MathValue(3)),
            (new MathValue(2), new MathValue(2.0m)),
            (new MathValue(2), new MathValue(2.0)),
            (new MathValue(2), new MathValue(2).WithExactness(CalculationExactness.Unverified)),
            (MathValue.FromFraction(1, 3), MathValue.FromFraction(1, 3).WithDecimalDisplayScale(2)),
            (new MathValue(2.0m), new MathValue(2.00m)),
            (new MathValue(0m), new MathValue(new decimal(0, 0, 0, true, 0))),
            (new MathValue(0.0), new MathValue(-0.0)),
            (new MathValue(BitConverter.Int64BitsToDouble(0x7ff8_0000_0000_0001)),
                new MathValue(BitConverter.Int64BitsToDouble(0x7ff8_0000_0000_0002)))
        ];

        foreach (var (first, second) in cases)
        {
            var left = Result(new MathValue(1), [new Variable("x", first)]);
            var right = Result(new MathValue(0), [new Variable("X", second)]);
            AssertConflictsInBothOrders(left, right, "variable");
            AssertConflictsInBothOrders(left, right, "x", StringComparison.OrdinalIgnoreCase);
        }
    }

    [TestMethod]
    public void SharedVariablesRequireMatchingDefinitionsAndExpressions()
    {
        var original = new Variable("x", new MathValue(2), new NumberNode(new MathValue(2)), "2");
        Variable[] alternatives =
        [
            new Variable("X", original.Value, original.Expression, null),
            new Variable("X", original.Value, original.Expression, " 2"),
            new Variable("X", original.Value, original.Expression, "TWO"),
            new Variable("X", original.Value, null, original.Definition),
            new Variable("X", original.Value, new BinaryNode(BinaryOperator.Add,
                new NumberNode(new MathValue(1)), new NumberNode(new MathValue(1))), original.Definition)
        ];

        foreach (var alternative in alternatives)
        {
            AssertConflictsInBothOrders(Result(new MathValue(1), [original]),
                Result(new MathValue(0), [alternative]), "variable");
        }
    }

    [TestMethod]
    public void IndependentlyStoredIdenticalValuesAreCompatible()
    {
        MathValue[] values =
        [
            new MathValue(2),
            new MathValue(2.00m),
            MathValue.FromFraction(1, 3).WithDecimalDisplayScale(2),
            new MathValue(-0.0),
            new MathValue(new decimal(0, 0, 0, true, 0)),
            new MathValue(BitConverter.Int64BitsToDouble(0x7ff8_0000_0000_0001)),
            new MathValue(2).WithExactness(CalculationExactness.Unverified)
        ];

        foreach (var value in values)
        {
            var left = Result(new MathValue(8), [new Variable("x", value)]);
            var right = Result(new MathValue(2), [new Variable("X", value)]);
            foreach (var compose in BinaryOperations(left, right).Concat(BinaryOperations(right, left)))
            {
                var combined = compose();
                Assert.HasCount(1, combined.Context.Variables);
            }
        }
    }

    [TestMethod]
    [DataRow(nameof(CalculatorOptions.Culture))]
    [DataRow(nameof(CalculatorOptions.AngleUnit))]
    [DataRow(nameof(CalculatorOptions.Normalization))]
    [DataRow(nameof(CalculatorOptions.AllowImplicitMultiplication))]
    [DataRow(nameof(CalculatorOptions.AllowPostfixFactorial))]
    [DataRow(nameof(CalculatorOptions.RequireExplicitLogicalNegation))]
    [DataRow(nameof(CalculatorOptions.CustomFunctions))]
    [DataRow(nameof(CalculatorOptions.MaxInputLength))]
    [DataRow(nameof(CalculatorOptions.MaxNestingDepth))]
    [DataRow(nameof(CalculatorOptions.MaxIntegerBits))]
    [DataRow(nameof(CalculatorOptions.MaxExponentMagnitude))]
    [DataRow(nameof(CalculatorOptions.ResultFormat))]
    public void IncompatibleOptionsFailBeforeArithmeticInEitherOrder(string setting)
    {
        var options = new CalculatorOptions { Culture = CultureInfo.InvariantCulture };
        var different = setting switch
        {
            nameof(CalculatorOptions.Culture) => options with { Culture = CultureInfo.GetCultureInfo("de-DE") },
            nameof(CalculatorOptions.AngleUnit) => options with { AngleUnit = AngleUnit.Degrees },
            nameof(CalculatorOptions.Normalization) => options with { Normalization = null },
            nameof(CalculatorOptions.AllowImplicitMultiplication) => options with { AllowImplicitMultiplication = false },
            nameof(CalculatorOptions.AllowPostfixFactorial) => options with { AllowPostfixFactorial = false },
            nameof(CalculatorOptions.RequireExplicitLogicalNegation) => options with { RequireExplicitLogicalNegation = true },
            nameof(CalculatorOptions.CustomFunctions) => options with
            {
                CustomFunctions = [CustomFunction.Create("tick", () => new MathValue(1))]
            },
            nameof(CalculatorOptions.MaxInputLength) => options with { MaxInputLength = options.MaxInputLength - 1 },
            nameof(CalculatorOptions.MaxNestingDepth) => options with { MaxNestingDepth = options.MaxNestingDepth - 1 },
            nameof(CalculatorOptions.MaxIntegerBits) => options with { MaxIntegerBits = options.MaxIntegerBits - 1 },
            nameof(CalculatorOptions.MaxExponentMagnitude) => options with { MaxExponentMagnitude = options.MaxExponentMagnitude - 1 },
            nameof(CalculatorOptions.ResultFormat) => options with
            {
                ResultFormat = options.ResultFormat with { GroupDigits = !options.ResultFormat.GroupDigits }
            },
            _ => throw new ArgumentOutOfRangeException(nameof(setting))
        };

        AssertConflictsInBothOrders(Result(new MathValue(1), options: options),
            Result(new MathValue(0), options: different), setting);
    }

    [TestMethod]
    public void EqualOptionsNeedNotBeTheSameInstance()
    {
        var options = new CalculatorOptions { Culture = CultureInfo.InvariantCulture };
        var equalOptions = options with { ResultFormat = options.ResultFormat with { } };
        Assert.AreNotSame(options, equalOptions);
        var left = Result(new MathValue(8), options: options);
        var right = Result(new MathValue(2), options: equalOptions);

        foreach (var compose in BinaryOperations(left, right))
        {
            Assert.AreSame(options, compose().Options);
        }

        foreach (var compose in BinaryOperations(right, left))
        {
            Assert.AreSame(equalOptions, compose().Options);
        }
    }

    [TestMethod]
    public void CustomFunctionsRequireSharedCollectionIdentityAndAreNeverReplayed()
    {
        int calls = 0;
        var function = CustomFunction.Create("tick", () => new MathValue(++calls));
        var options = new CalculatorOptions { CustomFunctions = [function] };
        var differentCollection = options with { CustomFunctions = [function] };
        var otherImplementation = options with
        {
            CustomFunctions = [CustomFunction.Create("tick", () => new MathValue(100))]
        };
        var source = new CalculatorEngine(options);
        var captured = source.Evaluate("tick()");

        AssertConflictsInBothOrders(captured, Result(new MathValue(2), options: differentCollection), "CustomFunctions");
        AssertConflictsInBothOrders(captured, Result(new MathValue(2), options: otherImplementation), "CustomFunctions");
        Assert.AreEqual(new MathValue(2), (captured + captured).Value);
        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public void MissingOptionsAdoptTheOtherOperandsOptionsOrDefaults()
    {
        var options = new CalculatorOptions { Culture = CultureInfo.GetCultureInfo("de-DE"), MaxIntegerBits = 8 };
        var withOptions = Result(new MathValue(8), options: options);
        var withoutOptions = Result(new MathValue(2));

        foreach (var compose in BinaryOperations(withOptions, withoutOptions)
            .Concat(BinaryOperations(withoutOptions, withOptions)))
        {
            Assert.AreSame(options, compose().Options);
        }

        var combined = withoutOptions + Result(new MathValue(3));
        Assert.AreEqual(CalculatorOptions.ForCurrentCulture(), combined.Options);
        Assert.AreEqual(new MathValue(5), combined.Value);
        Assert.ThrowsExactly<CalculationLimitException>(() => _ = withOptions + Result(new MathValue(256)));
        Assert.ThrowsExactly<CalculationLimitException>(() => _ = Result(new MathValue(256)) + withOptions);
    }

    [TestMethod]
    public void DefaultInitializedResultsAreRejectedForEveryArithmeticOperator()
    {
        EvaluationResult missing = default;
        var valid = Result(new MathValue(2));
        foreach (var compose in BinaryOperations(missing, valid)
            .Concat(BinaryOperations(valid, missing)))
        {
            var exception = Assert.ThrowsExactly<InvalidOperationException>(() => compose());
            StringAssert.Contains(exception.Message, "default-initialized");
        }

        Func<EvaluationResult>[] operations =
        [
            () => +missing,
            () => -missing,
            () => missing + new MathValue(2),
            () => new MathValue(2) + missing,
            () => missing - new MathValue(2),
            () => new MathValue(2) - missing,
            () => missing * new MathValue(2),
            () => new MathValue(2) * missing,
            () => missing / new MathValue(2),
            () => new MathValue(2) / missing,
            () => missing % new MathValue(2),
            () => new MathValue(2) % missing
        ];
        foreach (var operation in operations)
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => operation());
        }
    }

    [TestMethod]
    public void EarlierAnsReferencesRemainHistoricalAndUseCapturedValues()
    {
        var engine = new CalculatorEngine();
        var first = engine.Evaluate("ans + 1", engine.Evaluate("5").Context);
        var second = engine.Evaluate("ans + 1", engine.Evaluate("10").Context);
        var combined = first + second;

        Assert.AreEqual(new MathValue(17), combined.Value);
        Assert.AreEqual(new MathValue(17), combined.Context.PreviousResult);
        Assert.Contains("ans", combined.ReferencedVariables);
        Assert.HasCount(2, combined.VariableReferences);
        Assert.IsFalse(combined.HasAssignments);
        Assert.AreEqual(new MathValue(34), engine.Evaluate("ans * 2", combined.Context).Value);
    }

    [TestMethod]
    public void SequentialAssignmentsStillReplaceEarlierBindings()
    {
        var engine = new CalculatorEngine();
        var first = engine.Evaluate("x = 2");
        var second = engine.Evaluate("X = 3", first.Context);

        Assert.AreEqual(new MathValue(2), first.Context.Variables["x"].Value);
        Assert.AreEqual(new MathValue(3), second.Context.Variables["x"].Value);
        Assert.AreEqual("X", second.Context.Variables["x"].Name);
        Assert.AreEqual(new MathValue(3), second.Context.PreviousResult);
        Assert.IsTrue(second.HasAssignments);
        AssertConflictsInBothOrders(first, second, "variable");
    }

    private static EvaluationResult Result(MathValue value, VariableCollection? variables = null,
        CalculatorOptions? options = null) =>
        new(value, new EvaluationContext(variables, value), options: options);

    private static Func<EvaluationResult>[] BinaryOperations(EvaluationResult left, EvaluationResult right) =>
    [
        () => left + right,
        () => left - right,
        () => left * right,
        () => left / right,
        () => left % right
    ];

    private static void AssertConflictsInBothOrders(EvaluationResult left, EvaluationResult right,
        string expectedMessage, StringComparison comparison = StringComparison.Ordinal)
    {
        foreach (var compose in BinaryOperations(left, right).Concat(BinaryOperations(right, left)))
        {
            var exception = Assert.ThrowsExactly<InvalidOperationException>(() => compose());
            Assert.IsTrue(exception.Message.Contains(expectedMessage, comparison), exception.Message);
        }
    }
}
