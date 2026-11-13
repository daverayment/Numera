using System.Globalization;
using Numera.Ast;

namespace Numera.Tests;

[TestClass]
public sealed class EvaluationResultEqualityTests
{
    private static IEqualityComparer<EvaluationResult> Content => EvaluationResultComparers.Content;

    [TestMethod]
    public void ConstructorRejectsNullContext()
    {
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => new EvaluationResult(new MathValue(7), null!));
        Assert.AreEqual("context", exception.ParamName);
    }

    [TestMethod]
    public void ConstructorPreservesContextAndAllowsNullOptionalMembers()
    {
        var context = new EvaluationContext([new Variable("x", new MathValue(2))], new MathValue(3));
        var result = new EvaluationResult(new MathValue(7), context, assignedVariables: null, options: null);
        Assert.AreEqual(new MathValue(7), result.Value);
        Assert.AreSame(context, result.Context);
        Assert.AreSame(VariableCollection.Empty, result.AssignedVariables);
        Assert.IsNull(result.Options);
        Assert.IsFalse(result.HasAssignments);
        Assert.IsNull(result.SingleAssignedVariable);
        Assert.HasCount(0, result.ReferencedVariables);
        Assert.HasCount(0, result.VariableReferences);
        Assert.AreEqual(new MathValue(3), context.PreviousResult);

        var combined = result + new MathValue(1);
        Assert.AreEqual(new MathValue(8), combined.Value);
        Assert.AreEqual(new MathValue(8), combined.Context.PreviousResult);
        Assert.AreEqual(new MathValue(2), combined.Context.Variables["x"].Value);
        Assert.AreEqual(new MathValue(3), context.PreviousResult);

        var emptyContextResult = new EvaluationResult(new MathValue(7), EvaluationContext.Empty);
        Assert.AreSame(EvaluationContext.Empty, emptyContextResult.Context);
        Assert.IsFalse(emptyContextResult.HasAssignments);
        Assert.HasCount(0, emptyContextResult.ReferencedVariables);
    }

    [TestMethod]
    [DataRow("0", "0.00")]
    [DataRow("1/2", "0.5")]
    [DataRow("2+3", "x=5")]
    [DataRow("0*exp(1000)", "0*exp(1000)")]
    [DataRow("exp(1000)", "exp(1000)")]
    [DataRow("-exp(1000)", "-exp(1000)")]
    public void NumericEqualityAgreesWithOrderingHashingAndCollections(string first, string second)
    {
        var engine = CalculatorEngine.ForCulture(CultureInfo.InvariantCulture);
        var left = engine.Evaluate(first);
        var right = engine.Evaluate(second);

        Assert.IsTrue(left == right);
        Assert.IsFalse(left != right);
        Assert.IsTrue(left.Equals(right));
        Assert.IsTrue(((IEquatable<EvaluationResult>)left).Equals(right));
        Assert.IsTrue(((object)left).Equals(right));
        Assert.IsTrue(((object)right).Equals(left));
        Assert.AreEqual(0, left.CompareTo(right));
        Assert.IsTrue(left <= right);
        Assert.IsTrue(left >= right);
        Assert.AreEqual(left.GetHashCode(), right.GetHashCode());
        Assert.HasCount(1, new HashSet<EvaluationResult> { left, right });
        Assert.HasCount(1, new SortedSet<EvaluationResult> { left, right });

        var dictionary = new Dictionary<EvaluationResult, string> { [left] = "found" };
        Assert.AreEqual("found", dictionary[right]);
    }

    [TestMethod]
    public void NumericEqualityIgnoresOptionsExactnessAndAllResultMetadata()
    {
        var engine = CalculatorEngine.ForCulture(CultureInfo.InvariantCulture);
        var original = engine.Evaluate("x=1/2");
        var different = new EvaluationResult(
            new MathValue(0.5),
            new EvaluationContext([new Variable("other", 100)], 99),
            options: engine.Options with { Culture = CultureInfo.GetCultureInfo("de-DE") });

        Assert.AreEqual(original, different);
        Assert.AreEqual(original.GetHashCode(), different.GetHashCode());
        Assert.IsFalse(Content.Equals(original, different));
        Assert.IsTrue(original == different.Value);
        Assert.IsTrue(different.Value == original);
        Assert.IsFalse(((object)original).Equals(original.Value));
        Assert.IsFalse(((object)original.Value).Equals(original));
        Assert.IsFalse(original.Equals((object?)null));
        Assert.IsFalse(original == Wrap(1));
        Assert.IsTrue(original != Wrap(1));
    }

    [TestMethod]
    public void BoxedComparisonUsesOnlyResultsWhileTypedComparisonAcceptsValues()
    {
        var result = Wrap(42);
        IComparable boxed = result;

        Assert.AreEqual(1, boxed.CompareTo(null));
        Assert.AreEqual(0, boxed.CompareTo(Wrap(42m)));
        Assert.IsTrue(boxed.CompareTo(Wrap(100)) < 0);
        Assert.IsTrue(boxed.CompareTo(Wrap(1)) > 0);
        var exception = Assert.Throws<ArgumentException>(() => boxed.CompareTo(new MathValue(42)));
        Assert.AreEqual("obj", exception.ParamName);
        Assert.Throws<ArgumentException>(() => ((IComparable)new MathValue(42)).CompareTo(result));
        Assert.Throws<ArgumentException>(() => boxed.CompareTo(42));
        Assert.Throws<ArgumentException>(() => boxed.CompareTo("42"));
        Assert.AreEqual(0, ((IComparable<MathValue>)result).CompareTo(new MathValue(42)));
        Assert.AreEqual(0, ((IComparable<EvaluationResult>)result).CompareTo(Wrap(42)));
    }

    [TestMethod]
    public void ContentUsesIndependentCollectionContentsRatherThanIdentity()
    {
        var options = CalculatorOptions.ForCulture(CultureInfo.InvariantCulture);
        var first = CreateSnapshot(options);
        var second = CreateSnapshot(options with { });

        Assert.AreNotSame(first.Context, second.Context);
        Assert.AreNotSame(first.Context.Variables, second.Context.Variables);
        Assert.AreNotSame(first.AssignedVariables, second.AssignedVariables);
        AssertContentEqual(first, second);
        Assert.HasCount(1, new[] { first, second }.Distinct(Content).ToArray());
        var dictionary = new Dictionary<EvaluationResult, string>(Content) { [first] = "found" };
        Assert.AreEqual("found", dictionary[second]);
    }

    [TestMethod]
    public void ContentDistinguishesStoredValueMetadataAtEveryValueLocation()
    {
        (MathValue First, MathValue Second)[] pairs =
        [
            (new MathValue(1), new MathValue(1m)),
            (new MathValue(0.5m), MathValue.FromFraction(1, 2)),
            (new MathValue(0.5m), new MathValue(0.5)),
            (new MathValue(1), new MathValue(1).WithExactness(CalculationExactness.Unverified)),
            (new MathValue(1m), new MathValue(1.00m)),
            (MathValue.FromFraction(1, 3), MathValue.FromFraction(1, 3).WithDecimalDisplayScale(2)),
            (new MathValue(0.0), new MathValue(-0.0)),
            (new MathValue(0m), new MathValue(new decimal(0, 0, 0, true, 0))),
            (new MathValue(double.NaN), new MathValue(BitConverter.Int64BitsToDouble(0x7ff8000000000001L)))
        ];

        foreach (var (first, second) in pairs)
        {
            Assert.AreEqual(first, second);
            AssertContentDifferent(Wrap(first), Wrap(second));
            AssertContentDifferent(
                new EvaluationResult(0, new EvaluationContext(VariableCollection.Empty, first)),
                new EvaluationResult(0, new EvaluationContext(VariableCollection.Empty, second)));
            AssertContentDifferent(
                new EvaluationResult(0, new EvaluationContext([new Variable("x", first)])),
                new EvaluationResult(0, new EvaluationContext([new Variable("x", second)])));
            AssertContentDifferent(
                new EvaluationResult(0, EvaluationContext.Empty, [new Variable("x", first)]),
                new EvaluationResult(0, EvaluationContext.Empty, [new Variable("x", second)]));
            AssertContentEqual(Wrap(first), Wrap(first));
            AssertContentEqual(Wrap(second), Wrap(second));
        }
    }

    [TestMethod]
    public void ContentDistinguishesVariableOrderSpellingDefinitionsAndExpressions()
    {
        VariableCollection original =
        [
            new Variable("x", 1, new NumberNode(1), "1"),
            new Variable("y", 2)
        ];
        VariableCollection[] alternatives =
        [
            [original[1], original[0]],
            [new Variable("X", 1, new NumberNode(1), "1"), original[1]],
            [new Variable("x", 1, new NumberNode(1), "01"), original[1]],
            [new Variable("x", 1, new BinaryNode(BinaryOperator.Add, new NumberNode(0), new NumberNode(1)), "1"), original[1]],
            [new Variable("x", 3, new NumberNode(1), "1"), original[1]],
            [original[0]]
        ];

        foreach (var different in alternatives)
        {
            AssertContentDifferent(
                new EvaluationResult(0, new EvaluationContext(original)),
                new EvaluationResult(0, new EvaluationContext(different)));
            AssertContentDifferent(
                new EvaluationResult(0, EvaluationContext.Empty, original),
                new EvaluationResult(0, EvaluationContext.Empty, different));
        }
    }

    [TestMethod]
    public void ContentUsesAstEqualityAndIgnoresSourceSpans()
    {
        var engine = CalculatorEngine.ForCulture(CultureInfo.InvariantCulture);
        AssertContentEqual(engine.Evaluate("1+2"), engine.Evaluate("  1 + 2  "));
        AssertContentDifferent(engine.Evaluate("1+2"), engine.Evaluate("3"));

        var percent = engine.Evaluate("10%");
        var numeric = engine.Evaluate("0.1");
        Assert.AreEqual(percent, numeric);
        AssertContentDifferent(percent, numeric);

        var first = new EvaluationResult(0, EvaluationContext.Empty,
            [new Variable("x", 1, new NumberNode(1, new TextSpan(0, 1)), "1")]);
        var second = new EvaluationResult(0, EvaluationContext.Empty,
            [new Variable("x", 1, new NumberNode(1, new TextSpan(10, 1)), "1")]);
        AssertContentEqual(first, second);
    }

    [TestMethod]
    public void ContentUsesExistingOptionsEqualityIncludingFunctionCollectionIdentity()
    {
        var options = CalculatorOptions.ForCulture(CultureInfo.InvariantCulture);
        var first = new EvaluationResult(1, EvaluationContext.Empty, options: options);
        AssertContentEqual(first, new EvaluationResult(1, EvaluationContext.Empty, options: options with { }));

        CalculatorOptions[] alternatives =
        [
            options with { Culture = CultureInfo.GetCultureInfo("de-DE") },
            options with { AngleUnit = AngleUnit.Degrees },
            options with { MaxIntegerBits = 8 },
            options with { ResultFormat = options.ResultFormat with { SignificantDigits = 12 } },
            options with { CustomFunctions = [CustomFunction.Create("tick", () => new MathValue(1))] }
        ];
        foreach (var different in alternatives)
        {
            AssertContentDifferent(first, new EvaluationResult(1, EvaluationContext.Empty, options: different));
        }

        var function = CustomFunction.Create("tick", () => new MathValue(1));
        var registered = options with { CustomFunctions = [function] };
        var separate = options with { CustomFunctions = [function] };
        AssertContentDifferent(
            new EvaluationResult(1, EvaluationContext.Empty, options: registered),
            new EvaluationResult(1, EvaluationContext.Empty, options: separate));
        AssertContentEqual(
            new EvaluationResult(1, EvaluationContext.Empty, options: registered),
            new EvaluationResult(1, EvaluationContext.Empty, options: registered with { }));
        AssertContentDifferent(first, Wrap(1));
    }

    [TestMethod]
    public void ContentHandlesMissingStateWithoutChangingDefaultResultBehavior()
    {
        AssertContentEqual(default, default);
        Assert.AreEqual(default(EvaluationResult), Wrap(0));
        AssertContentDifferent(default, Wrap(default));
        AssertContentDifferent(
            new EvaluationResult(0, new EvaluationContext()),
            new EvaluationResult(0, new EvaluationContext(VariableCollection.Empty, 0)));
        AssertContentEqual(Wrap(double.NaN), Wrap(double.NaN));
        AssertContentEqual(Wrap(double.PositiveInfinity), Wrap(double.PositiveInfinity));
        AssertContentEqual(Wrap(double.NegativeInfinity), Wrap(double.NegativeInfinity));
        AssertContentDifferent(Wrap(double.PositiveInfinity), Wrap(double.NegativeInfinity));
    }

    private static EvaluationResult Wrap(MathValue value) => new(value, EvaluationContext.Empty);

    private static EvaluationResult CreateSnapshot(CalculatorOptions options)
    {
        VariableCollection variables =
        [
            new Variable("x", new MathValue(1.00m), new NumberNode(1), "1"),
            new Variable("y", MathValue.FromFraction(1, 3))
        ];
        var context = new EvaluationContext(variables, new MathValue(1.00m), new NumberNode(1));
        return new EvaluationResult(new MathValue(1.00m), context,
            [new Variable("x", new MathValue(1.00m), new NumberNode(1), "1")], options);
    }

    private static void AssertContentEqual(EvaluationResult first, EvaluationResult second)
    {
        Assert.IsTrue(Content.Equals(first, second));
        Assert.IsTrue(Content.Equals(second, first));
        Assert.AreEqual(Content.GetHashCode(first), Content.GetHashCode(second));
    }

    private static void AssertContentDifferent(EvaluationResult first, EvaluationResult second)
    {
        Assert.IsFalse(Content.Equals(first, second));
        Assert.IsFalse(Content.Equals(second, first));
        Assert.HasCount(2, new HashSet<EvaluationResult>(Content) { first, second });
    }
}