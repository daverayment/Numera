using System.Globalization;

namespace Numera.Tests;

[TestClass]
public sealed class EvaluationContextTests
{
    /// <summary>
    /// Builds a deliberately case-sensitive dictionary, so tests can supply keys which
    /// only differ by case and prove that the context provides the case-sensitivity.
    /// </summary>
    private static Dictionary<string, MathValue> Vars(
        params (string Name, decimal Value)[] pairs)
    {
        // Case-sensitive default.
        var dict = new Dictionary<string, MathValue>();
        foreach (var (name, value) in pairs)
        {
            dict[name] = new MathValue(value);
        }
        return dict;
    }

    [TestMethod]
    public void Constructor_PreservesPreviousResult()
    {
        var vars = new Dictionary<string, MathValue> { ["x"] = new(42m) };
        var context = new EvaluationContext(vars, new MathValue(100m));

        Assert.AreEqual(100m, context.PreviousResult?.DecVal);

        Assert.AreEqual(142m, Calculator.Evaluate("ans + x", context).DecVal);

        var otherContext = new EvaluationContext(vars, new MathValue(55m));
        Assert.AreEqual(97m, Calculator.Evaluate("ans + x", otherContext).DecVal);
    }

    [TestMethod]
    public void WithExpression_CanUpdateOrClearPreviousResult()
    {
        var context = new EvaluationContext(
            variables: null,
            previousResult: new MathValue(100m));
        Assert.AreEqual(200m, Calculator.Evaluate("ans * 2", context).DecVal);

        var updated = context with { PreviousResult = new MathValue(50m) };
        Assert.AreEqual(100m, Calculator.Evaluate("ans * 2", updated).DecVal);

        var cleared = context with { PreviousResult = null };
        Assert.IsFalse(Calculator.TryEvaluate("ans * 2", out _, out var diag, cleared));
        Assert.AreEqual(EvaluationError.UnknownConstant, diag!.Code);

        // The original context is unchanged.
        Assert.AreEqual(100m, Calculator.Evaluate("ans", context).DecVal);
    }

    [TestMethod]
    public void Constructor_ExplicitVariablesAns_TakesPrecedenceOverPreviousResult()
    {
        var context = new EvaluationContext(
            variables: Vars(("ans", 42m)),
            previousResult: new MathValue(100m));

        // Explicit variable 'ans' should take precedence over PreviousResult.
        Assert.AreEqual(84m, Calculator.Evaluate("ans * 2", context).DecVal);
    }

    [TestMethod]
    public void Constructor_NullVariablesAreAllowed()
    {
        var context = new EvaluationContext(null, null);

        Assert.IsNull(context.PreviousResult);
        Assert.AreEqual(0, context.Variables.Count);
        Assert.IsFalse(Calculator.TryEvaluate("ans", out _, out var diag, context));
        Assert.AreEqual(EvaluationError.UnknownConstant, diag.Code);
    }

    [TestMethod]
    public void Constructor_CopiesSourceDictionary()
    {
        var source = new Dictionary<string, MathValue> { ["x"] = new(1m) };
        var context = new EvaluationContext(source, null);

        // A later change by the caller must not leak into an evaluation that may be
        // running elsewhere.
        source["x"] = new MathValue(2m);
        source["y"] = new MathValue(3m);

        Assert.AreEqual(1m, Calculator.Evaluate("x", context).DecVal);
        Assert.IsFalse(Calculator.TryEvaluate("y", out _, out _, context));
    }

    [TestMethod]
    public void Variables_CannotBeMutatedThroughDictionaryInterface()
    {
        var context = new EvaluationContext(Vars(("x", 1m)), null);

        // Immutability is enforced at the type level: VariableCollection is sealed and does
        // not implement mutable collection interfaces. Casting via object proves this dynamically.
        object boxed = context.Variables;
        Assert.IsFalse(boxed is IDictionary<string, MathValue>);
        Assert.IsFalse(boxed is ICollection<KeyValuePair<string, MathValue>>);
        Assert.IsFalse(boxed is IList<KeyValuePair<string, MathValue>>);
        Assert.IsFalse(boxed is System.Collections.IDictionary);
        Assert.IsFalse(boxed is System.Collections.IList);

        // Mutation operations like Add produce a new collection instance, preserving the original.
        var updated = context.Variables.Add("y", new MathValue(2m));
        Assert.AreNotSame(context.Variables, updated);
        Assert.IsFalse(context.Variables.ContainsKey("y"));
        Assert.IsTrue(updated.ContainsKey("y"));

        Assert.AreEqual(1m, Calculator.Evaluate("x", context).DecVal);
        Assert.IsFalse(Calculator.TryEvaluate("y", out _, out _, context));
    }

    [TestMethod]
    public void Variables_AreCaseInsensitive()
    {
        var context = new EvaluationContext(Vars(("Rate", 5m)), null);

        Assert.AreEqual(5m, Calculator.Evaluate("rate", context).DecVal);
        Assert.AreEqual(5m, Calculator.Evaluate("RATE", context).DecVal);
        Assert.AreEqual(5m, Calculator.Evaluate("Rate", context).DecVal);
        Assert.AreEqual(5m, Calculator.Evaluate("rAtE", context).DecVal);
    }

    [TestMethod]
    public void Constructor_DuplicateKeysUseLastEnumeratedValue()
    {
        var source = Vars(("x", 10m), ("X", 20m), ("y", 30m));
        var context = new EvaluationContext(source, null);

        string addedKey = context.Variables!.Keys.First(key =>
            EvaluationContext.NameComparer.Equals(key, "x"));

        // Dictionary enumeration order is unspecified, so check that the value retained
        // is one of the two possible values, and that it is consistent.
        var result = Calculator.Evaluate(addedKey, context);
        Assert.Contains(result.DecVal, [10m, 20m]);
        Assert.AreEqual(result.DecVal, Calculator.Evaluate("x", context).DecVal);
        Assert.AreEqual(result.DecVal, Calculator.Evaluate("X", context).DecVal);

        // Unrelated variable should still be unaffected.
        Assert.AreEqual(30m, Calculator.Evaluate("y", context).DecVal);
    }

    [TestMethod]
    public void Constructor_DoesNotChangeSourceDictionary()
    {
        var source = Vars(("x", 10m), ("X", 20m));
        int originalCount = source.Count;

        _ = new EvaluationContext(source, null);

        Assert.HasCount(originalCount, source);
        Assert.AreEqual(10m, source["x"].DecVal);
        Assert.AreEqual(20m, source["X"].DecVal);
    }

    [TestMethod]
    public void NameComparer_IsOrdinalAndCaseInsensitive()
    {
        var comparer = EvaluationContext.NameComparer;

        Assert.IsTrue(comparer.Equals("x", "X"));
        Assert.IsTrue(comparer.Equals("Rate", "RATE"));

        // The comparer is ordinal, so it does not perform culture-specific case folding.
        Assert.IsFalse(comparer.Equals("i", "ı"));
        Assert.IsFalse(comparer.Equals("i", "İ"));
        Assert.IsFalse(comparer.Equals("I", "ı"));
        Assert.IsFalse(comparer.Equals("I", "İ"));
    }

    [TestMethod]
    public void Variables_LookupIsIndependentOfCurrentCulture()
    {
        // The Turkish i's are not equivalent under ordinal comparison, so the context
        // should not find them even if the current culture is Turkish.
        var originalCulture = CultureInfo.CurrentCulture;

        var context = new EvaluationContext(Vars(("i", 1m)), null);

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            Assert.AreEqual(1m, Calculator.Evaluate("i", context).DecVal);
            Assert.AreEqual(1m, Calculator.Evaluate("I", context).DecVal);

            Assert.IsFalse(context.Variables!.ContainsKey("ı"));
            Assert.IsFalse(context.Variables!.ContainsKey("İ"));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
    public void Contexts_WithEquivalentVariablesAreEqual()
    {
        var contextA = new EvaluationContext(
            Vars(("x", 1m), ("Rate", 5m)), new MathValue(10m));
        var contextB = new EvaluationContext(
            Vars(("RATE", 5m), ("X", 1m)), new MathValue(10m));

        Assert.AreEqual(contextA, contextB);
        Assert.AreEqual(contextA.GetHashCode(), contextB.GetHashCode());
    }

    [TestMethod]
    public void Contexts_WithDifferentVariableValuesAreNotEqual()
    {
        var contextA = new EvaluationContext(Vars(("x", 1m)), null);
        var contextB = new EvaluationContext(Vars(("x", 5m)), null);

        Assert.AreNotEqual(contextA, contextB);
    }

    [TestMethod]
    public void Contexts_WithDifferentPreviousResultsAreNotEqual()
    {
        var contextA = new EvaluationContext(Vars(("x", 1m)), new MathValue(10m));
        var contextB = new EvaluationContext(Vars(("x", 1m)), new MathValue(20m));

        Assert.AreNotEqual(contextA, contextB);
    }

    [TestMethod]
    public void Contexts_NullAndEmptyVariablesAreEquivalent()
    {
        var withoutVariables = new EvaluationContext(null, null);
        var withEmptyVariables = new EvaluationContext(
            new Dictionary<string, MathValue>(),
            null);

        Assert.AreEqual(withoutVariables, withEmptyVariables);
        Assert.AreEqual(withoutVariables.GetHashCode(), withEmptyVariables.GetHashCode());
    }

    [TestMethod]
    public void Empty_IsSharedAndHasNoState()
    {
        var empty1 = EvaluationContext.Empty;
        var empty2 = EvaluationContext.Empty;

        Assert.AreSame(empty1, empty2);
        Assert.IsNull(empty1.PreviousResult);
        Assert.AreEqual(0, empty1.Variables.Count);
        Assert.AreSame(VariableCollection.Empty, empty1.Variables);
    }

    [TestMethod]
    public void MultiRound_Advance_ChainsVariablesAndHistoricalCalculations()
    {
        var engine = new CalculatorEngine();
        var context = EvaluationContext.Empty;

        // Round 1: define a = 10
        var res1 = engine.Evaluate("a = 10", context);
        Assert.AreEqual(10, (int)res1.Value.IntVal);
        Assert.IsTrue(res1.HasAssignments);
        Assert.AreEqual("a", res1.SingleAssignedVariable);
        context = res1.Context;

        Assert.AreEqual(1, context.Variables.Count);
        Assert.AreEqual(10, (int)context.Variables["a"].IntVal);
        Assert.AreEqual(10, (int)context.PreviousResult?.IntVal!);

        // Round 2: define b = a * 2 + 5 (using variable from round 1)
        var res2 = engine.Evaluate("b = a * 2 + 5", context);
        Assert.AreEqual(25, (int)res2.Value.IntVal);
        context = res2.Context;

        Assert.AreEqual(2, context.Variables.Count);
        CollectionAssert.AreEqual(new[] { "a", "b" }, context.Variables.Keys.ToArray());
        Assert.AreEqual(25, (int)context.Variables["b"].IntVal);
        Assert.AreEqual(25, (int)context.PreviousResult?.IntVal!);

        // Round 3: use ans and variables together: c = ans + a
        var res3 = engine.Evaluate("c = ans + a", context); // 25 + 10 = 35
        Assert.AreEqual(35, (int)res3.Value.IntVal);
        context = res3.Context;

        Assert.AreEqual(3, context.Variables.Count);
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, context.Variables.Keys.ToArray());

        // Round 4: reassign 'a' to a new value: a = b + c (25 + 35 = 60).
        // Preserves chronological / MRU order: 'a' should move to the end!
        var res4 = engine.Evaluate("a = b + c", context);
        Assert.AreEqual(60, (int)res4.Value.IntVal);
        context = res4.Context;

        Assert.AreEqual(3, context.Variables.Count);
        CollectionAssert.AreEqual(new[] { "b", "c", "a" }, context.Variables.Keys.ToArray());
        Assert.AreEqual(60, (int)context.Variables["a"].IntVal);

        // Round 5: complex expression using updated variables and ans
        // a (60) + b (25) + c (35) + ans (60) = 180
        var res5 = engine.Evaluate("a + b + c + ans", context);
        Assert.AreEqual(180, (int)res5.Value.IntVal);
        context = res5.Context;

        // No new variable assignments in Round 5
        Assert.IsFalse(res5.HasAssignments);
        Assert.AreEqual(0, res5.AssignedVariables.Count);
        Assert.AreEqual(3, context.Variables.Count);
        Assert.AreEqual(180, (int)context.PreviousResult?.IntVal!);
    }

    [TestMethod]
    public void ChainedAssignments_WithinSingleExpression_ArePreservedInOrder()
    {
        var engine = new CalculatorEngine();

        // Right-associative assignment: x = y = 42
        var res = engine.Evaluate("x = y = 42", EvaluationContext.Empty);
        Assert.AreEqual(42, (int)res.Value.IntVal);
        Assert.AreEqual(2, res.AssignedVariables.Count);

        var context = res.Context;
        Assert.AreEqual(2, context.Variables.Count);
        Assert.AreEqual(42, (int)context.Variables["x"].IntVal);
        Assert.AreEqual(42, (int)context.Variables["y"].IntVal);

        // Next evaluation can read both x and y
        var next = engine.Evaluate("x + y", context);
        Assert.AreEqual(84, (int)next.Value.IntVal);
    }

    [TestMethod]
    public void ChainedAssignments_AtOrExceedingNestingDepth_RejectsCleanly()
    {
        // Tight nesting limit to verify chained assignments cannot bypass depth bounds.
        var engine = new CalculatorEngine(new CalculatorOptions { MaxNestingDepth = 4 });

        // Depth 3 chained assignment fits: a = b = 1
        var allowed = engine.Evaluate("a = b = 1", EvaluationContext.Empty);
        Assert.AreEqual(1, (int)allowed.Value.IntVal);

        // Deep chained assignment exceeding MaxNestingDepth throws CalculationLimitException / LimitExceeded
        Assert.IsFalse(engine.TryEvaluate("v = w = x = y = z = 1", out _, out var diag));
        Assert.AreEqual(EvaluationError.LimitExceeded, diag!.Code);
        Assert.Throws<CalculationLimitException>(() => engine.Evaluate("v = w = x = y = z = 1"));
    }

    [TestMethod]
    public void ChainedAssignments_VeryLongChain_IsRejectedDuringParsing()
    {
        // ~1000 chained assignments fit within MaxInputLength; the parser must stop at the
        // nesting limit rather than recursing for the whole chain.
        var engine = new CalculatorEngine();
        string chain = string.Concat(Enumerable.Range(0, 1000).Select(i => $"v{i} = ")) + "1";

        Assert.IsFalse(engine.TryEvaluate(chain, out _, out var diag));
        Assert.AreEqual(EvaluationError.LimitExceeded, diag!.Code);
    }

    [TestMethod]
    [DataRow("not = 5", EvaluationError.InvalidSyntax)]
    [DataRow("sin = 3", EvaluationError.InvalidSyntax)]
    [DataRow("pi = 4", EvaluationError.InvalidSyntax)]
    [DataRow("true = 1", EvaluationError.InvalidSyntax)]
    public void Assignment_InvalidOrReservedIdentifiers_ProduceDiagnostics(string expression, EvaluationError expectedError)
    {
        var engine = new CalculatorEngine();
        Assert.IsFalse(engine.TryEvaluate(expression, out _, out var diagnostic));
        Assert.AreEqual(expectedError, diagnostic!.Code);
        Assert.Throws<ExpressionException>(() => engine.Evaluate(expression));
    }

    [TestMethod]
    public void TrailingEquals_IsTreatedAsLauncherSuffix_NotIncompleteAssignment()
    {
        // The normalizer strips a trailing '=' (e.g. '2 + 2 ='), so 'x =' is just 'x'.
        // It must never be parsed as an assignment with a missing value.
        var engine = new CalculatorEngine();
        Assert.IsFalse(engine.TryEvaluate("x =", out _, out var diagnostic));
        Assert.AreEqual(EvaluationError.UnknownConstant, diagnostic!.Code);

        var context = new EvaluationContext(Vars(("x", 5m)), null);
        var result = engine.Evaluate("x =", context);
        Assert.AreEqual(5m, result.Value.DecVal);
        Assert.AreEqual(5m, context.Variables["x"].DecVal);
    }

    [TestMethod]
    public void Ans_UninitializedInContext_ProducesUnknownConstantError()
    {
        var engine = new CalculatorEngine();
        Assert.IsFalse(engine.TryEvaluate("ans + 1", out _, out var diagnostic, EvaluationContext.Empty));
        Assert.AreEqual(EvaluationError.UnknownConstant, diagnostic!.Code);
        Assert.Throws<ExpressionException>(() => engine.Evaluate("ans + 1", EvaluationContext.Empty));
    }

    [TestMethod]
    public void Ans_ExplicitUserVariable_OverridesPreviousResult()
    {
        var engine = new CalculatorEngine();
        // Provide previous result 100, but explicit user variable "ans" = 5
        var context = new EvaluationContext(
            new Dictionary<string, MathValue> { ["ans"] = new(5m) },
            new MathValue(100m));

        var result = engine.Evaluate("ans * 2", context);
        Assert.AreEqual(10m, result.Value.DecVal);
    }

    [TestMethod]
    public void Context_WithMaliciousOrPoisonedKeys_MaintainsInvariants()
    {
        var engine = new CalculatorEngine();

        // 1. Context containing an empty key or null character
        var weirdVars = new Dictionary<string, MathValue>
        {
            [""] = new(10m),
            ["a\0b"] = new(20m),
            ["valid"] = new(30m)
        };
        var context = new EvaluationContext(weirdVars, null);

        // Valid variable is still readable
        Assert.AreEqual(30m, engine.Evaluate("valid", context).DecVal);

        // Parser will not produce an empty or null-containing identifier token for variables
        Assert.IsFalse(engine.TryEvaluate("", out _, out _, context));

        // 2. Huge collection of 1,000 variables advances safely and maintains lookup
        var manyVars = Enumerable.Range(0, 1000)
            .Select(i => new KeyValuePair<string, MathValue>($"var_{i}", new MathValue((decimal)i)))
            .ToList();
        var largeContext = new EvaluationContext(manyVars, null);
        Assert.AreEqual(1000, largeContext.Variables.Count);

        var evaluateLookup = engine.Evaluate("var_999 + var_0", largeContext);
        Assert.AreEqual(999m, evaluateLookup.Value.DecVal);

        var advanced = largeContext.Advance([new KeyValuePair<string, MathValue>("var_0", new MathValue(42m))], new MathValue(999m));
        Assert.AreEqual(42m, advanced.Variables["var_0"].DecVal);
        Assert.AreEqual(1000, advanced.Variables.Count);
    }
}
