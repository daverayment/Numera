using System.Numerics;

namespace Numera.Tests;

[TestClass]
public sealed class CustomFunctionTests
{
    [TestMethod]
    public void CustomFunction_SingleArgument_EvaluatesCorrectly()
    {
        var options = new CalculatorOptions
        {
            CustomFunctions =
            [
                CustomFunction.Create("double", (MathValue x) => x * new MathValue(2m)),
                CustomFunction.Create("triple", (MathValue x) => x * new MathValue(3m), "Triples the input")
            ]
        };

        var engine = new CalculatorEngine(options);

        var result = engine.Evaluate("double(21)");
        Assert.AreEqual(42m, result.DecVal);

        var compound = engine.Evaluate("triple(double(5))");
        Assert.AreEqual(30m, compound.DecVal);
    }

    [TestMethod]
    public void CustomFunction_TwoAndThreeArguments_EvaluatesCorrectly()
    {
        var options = new CalculatorOptions
        {
            CustomFunctions =
            [
                CustomFunction.Create("myHypot", (MathValue a, MathValue b) =>
                    new MathValue(Math.Sqrt((a * a + b * b).AsDouble()))),
                CustomFunction.Create("weightedAvg", (MathValue a, MathValue b, MathValue weightA) =>
                {
                    var weightB = new MathValue(1m) - weightA;
                    return (a * weightA) + (b * weightB);
                })
            ]
        };

        var engine = new CalculatorEngine(options);

        var resHypot = engine.Evaluate("myHypot(3, 4)");
        Assert.AreEqual(5.0, resHypot.DblVal);

        var resWeighted = engine.Evaluate("weightedAvg(10, 20, 0.25)");
        Assert.AreEqual(17.5m, resWeighted.DecVal);
    }

    [TestMethod]
    public void CustomFunction_ZeroArguments_EvaluatesCorrectly()
    {
        var options = new CalculatorOptions
        {
            CustomFunctions =
            [
                CustomFunction.Create("magicNumber", () => new MathValue(42m))
            ]
        };

        var engine = new CalculatorEngine(options);
        var result = engine.Evaluate("magicNumber()");
        Assert.AreEqual(42m, result.DecVal);
    }

    [TestMethod]
    public void CustomFunction_Variadic_EvaluatesCorrectly()
    {
        var options = new CalculatorOptions
        {
            CustomFunctions =
            [
                CustomFunction.CreateVariadic("sumAll", minArgs: 1, args =>
                {
                    var sum = args[0];
                    for (int i = 1; i < args.Count; i++)
                    {
                        sum += args[i];
                    }
                    return sum;
                })
            ]
        };

        var engine = new CalculatorEngine(options);

        Assert.AreEqual(new MathValue(new BigInteger(10)), engine.Evaluate("sumAll(10)").Value);
        Assert.AreEqual(new MathValue(new BigInteger(60)), engine.Evaluate("sumAll(10, 20, 30)").Value);
    }

    [TestMethod]
    public void CustomFunction_CaseInsensitiveLookup()
    {
        var options = new CalculatorOptions
        {
            CustomFunctions =
            [
                CustomFunction.Create("myFunc", (MathValue x) => x + new MathValue(1m))
            ]
        };

        var engine = new CalculatorEngine(options);

        Assert.AreEqual(6m, engine.Evaluate("MYFUNC(5)").DecVal);
        Assert.AreEqual(7m, engine.Evaluate("myfunc(6)").DecVal);
        Assert.AreEqual(8m, engine.Evaluate("MyFunc(7)").DecVal);
    }

    [TestMethod]
    public void CustomFunction_ArityValidation_RejectsWrongArgumentCount()
    {
        var options = new CalculatorOptions
        {
            CustomFunctions =
            [
                CustomFunction.Create("addTwo", (MathValue a, MathValue b) => a + b),
                CustomFunction.CreateVariadic("atLeastTwo", minArgs: 2, args => args[0] + args[1])
            ]
        };

        var engine = new CalculatorEngine(options);

        // Fixed arity: too few
        var exTooFew = Assert.Throws<ExpressionException>(() => engine.Evaluate("addTwo(1)"));
        Assert.AreEqual(EvaluationError.InvalidArgumentCount, exTooFew.Diagnostic.Code);

        // Fixed arity: too many
        var exTooMany = Assert.Throws<ExpressionException>(() => engine.Evaluate("addTwo(1, 2, 3)"));
        Assert.AreEqual(EvaluationError.InvalidArgumentCount, exTooMany.Diagnostic.Code);

        // Variadic: too few
        var exVariadicFew = Assert.Throws<ExpressionException>(() => engine.Evaluate("atLeastTwo(1)"));
        Assert.AreEqual(EvaluationError.InvalidArgumentCount, exVariadicFew.Diagnostic.Code);
    }

    [TestMethod]
    public void CustomFunction_RequiresParentheses()
    {
        var options = new CalculatorOptions
        {
            CustomFunctions =
            [
                CustomFunction.Create("myFunc", (MathValue x) => x * new MathValue(2m))
            ]
        };

        var engine = new CalculatorEngine(options);

        var ex = Assert.Throws<ExpressionException>(() => engine.Evaluate("myFunc + 2"));
        Assert.AreEqual(EvaluationError.InvalidSyntax, ex.Diagnostic.Code);
    }

    [TestMethod]
    public void CustomFunction_CannotBeAssignedTo()
    {
        var options = new CalculatorOptions
        {
            CustomFunctions =
            [
                CustomFunction.Create("myFunc", (MathValue x) => x * new MathValue(2m))
            ]
        };

        var engine = new CalculatorEngine(options);

        var ex = Assert.Throws<ExpressionException>(() => engine.Evaluate("myFunc = 10"));
        Assert.AreEqual(EvaluationError.InvalidSyntax, ex.Diagnostic.Code);
    }

    [TestMethod]
    public void CustomFunction_OptionsValidation_RejectsShadowingBuiltins()
    {
        // Shadow built-in function
        var optionsShadowFunc = new CalculatorOptions
        {
            CustomFunctions = [CustomFunction.Create("sin", (MathValue x) => x)]
        };
        Assert.Throws<ArgumentException>(() => new CalculatorEngine(optionsShadowFunc));

        // Shadow built-in constant
        var optionsShadowConst = new CalculatorOptions
        {
            CustomFunctions = [CustomFunction.Create("pi", (MathValue x) => x)]
        };
        Assert.Throws<ArgumentException>(() => new CalculatorEngine(optionsShadowConst));

        // Shadow reserved keyword
        var optionsShadowKeyword = new CalculatorOptions
        {
            CustomFunctions = [CustomFunction.Create("and", (MathValue x) => x)]
        };
        Assert.Throws<ArgumentException>(() => new CalculatorEngine(optionsShadowKeyword));
    }

    [TestMethod]
    public void CustomFunction_UnhandledCallbackException_MapsToEvaluationFailed()
    {
        var options = new CalculatorOptions
        {
            CustomFunctions =
            [
                CustomFunction.Create("blowUp", (MathValue _) => throw new InvalidOperationException("Custom failure message"))
            ]
        };

        var engine = new CalculatorEngine(options);

        var ex = Assert.Throws<ExpressionException>(() => engine.Evaluate("blowUp(10)"));
        Assert.AreEqual(EvaluationError.EvaluationFailed, ex.Diagnostic.Code);
        Assert.IsTrue(ex.Diagnostic.Message.Contains("Custom failure message"));
        Assert.IsNotNull(ex.Diagnostic.Position);
    }

    [TestMethod]
    public void CustomFunction_DivideByZero_MapsToDivisionByZero()
    {
        var options = new CalculatorOptions
        {
            CustomFunctions =
            [
                CustomFunction.Create("divByZero", (MathValue _) => throw new DivideByZeroException())
            ]
        };

        var engine = new CalculatorEngine(options);

        var ex = Assert.Throws<ExpressionException>(() => engine.Evaluate("divByZero(10)"));
        Assert.AreEqual(EvaluationError.DivisionByZero, ex.Diagnostic.Code);
    }

    [TestMethod]
    public void CustomFunction_OversizedIntegerResult_ThrowsCalculationLimitException()
    {
        var options = new CalculatorOptions
        {
            CustomFunctions =
            [
                CustomFunction.Create("hugeInt", () => new MathValue(BigInteger.One << (CalculationLimits.MaxIntegerBits + 10)))
            ]
        };

        var engine = new CalculatorEngine(options);

        Assert.Throws<CalculationLimitException>(() => engine.Evaluate("hugeInt()"));
    }

    [TestMethod]
    public void CustomFunctionCollection_BasicPropertiesAndOrdering()
    {
        var f1 = CustomFunction.Create("a", (MathValue x) => x);
        var f2 = CustomFunction.Create("b", (MathValue x) => x, "description b");

        var col = new CustomFunctionCollection([f1, f2]);

        Assert.AreEqual(2, col.Count);
        Assert.AreSequenceEqual(["a", "b"], col.Names.ToArray());
        Assert.AreSame(f1, col[0]);
        Assert.AreSame(f2, col[1]);
        Assert.AreSame(f1, col["A"]);
        Assert.AreSame(f2, col["B"]);
        Assert.IsTrue(col.Contains("a"));
        Assert.IsFalse(col.Contains("c"));

        var added = col.Add(CustomFunction.Create("c", (MathValue x) => x));
        Assert.AreEqual(3, added.Count);
        Assert.AreEqual(2, col.Count); // Immutability

        // Fluent Add overloads
        var fluent = CustomFunctionCollection.Empty
            .Add("f1", (MathValue x) => x)
            .Add("f2", (MathValue a, MathValue b) => a + b)
            .Add("f3", (MathValue a, MathValue b, MathValue c) => a + b + c)
            .Add("fVar", minArgs: 1, args => args[0]);

        Assert.AreEqual(4, fluent.Count);
        Assert.IsTrue(fluent.Contains("f1"));
        Assert.IsTrue(fluent.Contains("f2"));
        Assert.IsTrue(fluent.Contains("f3"));
        Assert.IsTrue(fluent.Contains("fVar"));
    }

    [TestMethod]
    public void CustomFunction_Formatting_TypographicAndAscii()
    {
        var options = new CalculatorOptions
        {
            CustomFunctions =
            [
                CustomFunction.Create("myFunc", (MathValue x) => x * new MathValue(2m)),
                CustomFunction.Create("addTwo", (MathValue a, MathValue b) => a + b)
            ]
        };

        var engine = new CalculatorEngine(options);

        string typographic = engine.FormatExpression("myFunc(21) + addTwo(3, 4)", ExpressionFormatOptions.Typographic);
        Assert.AreEqual("myFunc(21) + addTwo(3, 4)", typographic);

        string ascii = engine.FormatExpression("myFunc(21) * addTwo(3, 4)", ExpressionFormatOptions.Ascii);
        Assert.AreEqual("myFunc(21) * addTwo(3, 4)", ascii);
    }

    [TestMethod]
    public void CustomFunction_Formatting_MathJax()
    {
        var options = new CalculatorOptions
        {
            CustomFunctions =
            [
                CustomFunction.Create("custom_fn", (MathValue x) => x),
                CustomFunction.Create("f", (MathValue x) => x)
            ]
        };

        var engine = new CalculatorEngine(options);

        string mathJax = engine.FormatExpression("custom_fn(10) + f(2)", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"\operatorname{custom\_fn}(10) + \operatorname{f}(2)", mathJax);
    }

    [TestMethod]
    public void CustomFunction_ThrowingExpressionException_PreservesDiagnosticAndAttachesSpan()
    {
        var options = new CalculatorOptions
        {
            CustomFunctions =
            [
                CustomFunction.Create("positiveOnly", (MathValue x) =>
                {
                    if (x.DecVal <= 0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.ArgumentOutOfRange, "Value must be positive."));
                    }
                    return x;
                })
            ]
        };

        var engine = new CalculatorEngine(options);
        var ex = Assert.Throws<ExpressionException>(() =>
            engine.Evaluate("10 + positiveOnly(-5) + 2"));

        Assert.AreEqual(EvaluationError.ArgumentOutOfRange, ex.Diagnostic.Code);
        Assert.Contains("Value must be positive.", ex.Diagnostic.Message);
        Assert.AreEqual(5, ex.Diagnostic.Position);
        Assert.AreEqual("positiveOnly(-5)".Length, ex.Diagnostic.Length);
    }

    [TestMethod]
    public void CustomFunction_SupportsImplicitMultiplication()
    {
        var options = new CalculatorOptions
        {
            CustomFunctions =
            [
                CustomFunction.Create("triple", x => x * new MathValue(3m))
            ]
        };

        var engine = new CalculatorEngine(options);

        Assert.AreEqual(13m, engine.Evaluate("triple(3) + 4").DecVal);
        Assert.AreEqual(36m, engine.Evaluate("triple(3)4").DecVal);
        Assert.AreEqual(36m, engine.Evaluate("4triple(3)").DecVal);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("    ")]
    [DataRow("123bad")]
    [DataRow("my-func")]
    [DataRow("my func")]
    public void CustomFunction_OptionsValidation_RejectsInvalidIdentifierNames(string invalidName)
    {
        var options = new CalculatorOptions
        {
            CustomFunctions =
            [
                CustomFunction.Create(invalidName, (MathValue x) => x)
            ]
        };

        Assert.Throws<ArgumentException>(() => new CalculatorEngine(options));
    }

    [TestMethod]
    public void CustomFunction_TryEvaluate_ReturnsDiagnosticWithoutThrowing()
    {
        var options = new CalculatorOptions
        {
            CustomFunctions =
            [
                CustomFunction.Create("simpleAdd", (MathValue a, MathValue b) => a + b)
            ]
        };

        var engine = new CalculatorEngine(options);

        // Invalid argument count via TryEvaluate.
        bool success = engine.TryEvaluate("simpleAdd(1)", out var result, out var diagnostic);

        Assert.IsFalse(success);
        Assert.IsNotNull(diagnostic);
        Assert.AreEqual(EvaluationError.InvalidArgumentCount, diagnostic.Code);
        Assert.AreEqual(0, diagnostic.Position);
        Assert.AreEqual("simpleAdd".Length, diagnostic.Length);
    }
}
