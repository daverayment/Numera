using System.Globalization;
using Numera.Ast;

namespace Numera.Tests;

[TestClass]
public sealed class ReservedNameTests
{
    private static readonly CalculatorEngine Engine = CalculatorEngine.ForCulture(CultureInfo.InvariantCulture);
    private static readonly CalculatorEngine RawEngine = Engine.WithOptions(options => options with { Normalization = null });

    [TestMethod]
    public void AllConstantAndFunctionNamesAreReservedAcrossDeclarationSurfaces()
    {
        foreach (string name in Constants.AllNames)
        {
            AssertReservedAcrossDeclarations(name, ReservedNameKind.Constant);
            AssertReservedAcrossDeclarations(name.ToUpperInvariant(), ReservedNameKind.Constant);
        }

        foreach (string name in Functions.AllNames)
        {
            AssertReservedAcrossDeclarations(name, ReservedNameKind.BuiltInFunction);
            AssertReservedAcrossDeclarations(name.ToUpperInvariant(), ReservedNameKind.BuiltInFunction);
        }
    }

    [TestMethod]
    [DataRow("and")]
    [DataRow("or")]
    [DataRow("xor")]
    [DataRow("not")]
    [DataRow("of")]
    public void OperatorKeywordsAreReservedAcrossDeclarationSurfaces(string name)
    {
        AssertReservedAcrossDeclarations(name, ReservedNameKind.Keyword);
        AssertReservedAcrossDeclarations(name.ToUpperInvariant(), ReservedNameKind.Keyword);
    }

    [TestMethod]
    [DataRow("ans")]
    [DataRow("AnS")]
    [DataRow("ANS")]
    public void PreviousResultNameKeepsItsOwnClassificationAndGuidance(string name)
    {
        AssertReservedAcrossDeclarations(name, ReservedNameKind.PreviousResult);
        var functionException = Assert.ThrowsExactly<ArgumentException>(() =>
            CustomFunction.Create(name, () => new MathValue(1)));
        Assert.Contains("cannot shadow the 'ans' variable", functionException.Message);

        var contextException = Assert.ThrowsExactly<ArgumentException>(() =>
            new EvaluationContext([new Variable(name, 1)]));
        Assert.Contains("Use PreviousResult instead", contextException.Message);
    }

    [TestMethod]
    [DataRow("E", "Euler's number")]
    [DataRow("PI", "Archimedes' constant")]
    [DataRow("tau", "the circle constant")]
    [DataRow("phi", "the golden ratio")]
    [DataRow("true", "a boolean literal")]
    [DataRow("false", "a boolean literal")]
    [DataRow("SGN", "built-in function (alias for 'sign')")]
    [DataRow("sin", "built-in function.")]
    [DataRow("AND", "reserved language keyword")]
    public void ParserKeepsDetailedReservedNameMessages(string name, string expectedDetail)
    {
        foreach (var engine in new[] { Engine, RawEngine })
        {
            var exception = Assert.ThrowsExactly<ExpressionException>(() => engine.Parse($"  {name} = 7"));
            Assert.Contains(expectedDetail, exception.Diagnostic.Message);
            Assert.AreEqual(2, exception.Diagnostic.Position);
            Assert.AreEqual(name.Length, exception.Diagnostic.Length);
        }
    }

    [TestMethod]
    [DataRow("AnD", "a language keyword")]
    [DataRow("PI", "a built-in constant")]
    [DataRow("true", "a built-in constant")]
    [DataRow("sin", "a built-in function")]
    [DataRow("SGN", "a built-in function")]
    public void ConstructorsUseTheSameReservationDescription(string name, string description)
    {
        Assert.AreEqual(description, ReservedNames.Classify(name).Description);
        var functionException = Assert.ThrowsExactly<ArgumentException>(() =>
            CustomFunction.Create(name, () => new MathValue(1)));
        Assert.AreEqual("name", functionException.ParamName);
        Assert.Contains($"Custom function name '{name}' cannot shadow {description}.", functionException.Message);

        var contextException = Assert.ThrowsExactly<ArgumentException>(() =>
            new EvaluationContext([new Variable(name, 7)]));
        Assert.AreEqual(nameof(EvaluationContext.Variables), contextException.ParamName);
        Assert.Contains($"Variable name '{name}' cannot shadow {description}.", contextException.Message);
    }

    [TestMethod]
    public void CustomFunctionReservationsAreScopedToTheSuppliedRegistry()
    {
        int calls = 0;
        CustomFunctionCollection functions = [CustomFunction.Create("spark", () => new MathValue(++calls))];
        var engine = Engine.WithOptions(options => options with { CustomFunctions = functions });
        const string name = "SpArK";
        Assert.AreEqual(ReservedNameKind.None, ReservedNames.Classify(name).Kind);
        var reservation = ReservedNames.Classify(name, functions);
        Assert.AreEqual(ReservedNameKind.CustomFunction, reservation.Kind);
        Assert.AreEqual("a registered custom function", reservation.Description);
        Assert.AreEqual(new MathValue(7), Engine.Evaluate($"{name} = 7").Value);
        Assert.AreEqual(name, CustomFunction.Create(name, () => new MathValue(1)).Name);
        var context = new EvaluationContext([new Variable(name, 7)]);
        Assert.AreEqual(new MathValue(7), context.Variables[name].Value);

        var parsedException = Assert.ThrowsExactly<ExpressionException>(() => engine.Evaluate($"  {name} = spark()"));
        Assert.AreEqual(EvaluationError.InvalidSyntax, parsedException.Diagnostic.Code);
        Assert.AreEqual($"Cannot assign to name '{name}' because it is registered as a custom function.", parsedException.Diagnostic.Message);
        Assert.AreEqual(2, parsedException.Diagnostic.Position);
        Assert.AreEqual(name.Length, parsedException.Diagnostic.Length);

        var span = new TextSpan(12, 9);
        var assignment = new AssignmentNode(name, new FunctionCallNode("spark", []), span: span);
        var tree = new BinaryNode(BinaryOperator.Add, new FunctionCallNode("spark", []), assignment);
        var nodeException = Assert.ThrowsExactly<ExpressionException>(() => engine.Evaluate(tree));
        Assert.AreEqual(EvaluationError.InvalidSyntax, nodeException.Diagnostic.Code);
        Assert.AreEqual($"Cannot assign to function name '{name}'.", nodeException.Diagnostic.Message);
        Assert.AreEqual(span.Start, nodeException.Diagnostic.Position);
        Assert.AreEqual(span.Length, nodeException.Diagnostic.Length);
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    public void ContextKeepsPreviousResultGuidanceAheadOfOtherReservedNames()
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(() =>
            new EvaluationContext([new Variable("pi", 1), new Variable("ANS", 2)]));
        Assert.AreEqual(nameof(EvaluationContext.Variables), exception.ParamName);
        Assert.Contains("Use PreviousResult instead", exception.Message);
    }

    [TestMethod]
    [DataRow("_sin")]
    [DataRow("and2")]
    [DataRow("ans_value")]
    [DataRow("pi2")]
    [DataRow("\u03B1")]
    public void NamesContainingReservedTextRemainAvailable(string name)
    {
        var reservation = ReservedNames.Classify(name);
        Assert.AreEqual(ReservedNameKind.None, reservation.Kind);
        Assert.IsNull(reservation.Description);
        Assert.IsNull(reservation.Constant);
        Assert.IsNull(reservation.BuiltInFunction);
        Assert.AreEqual(new MathValue(7), Engine.Evaluate($"{name} = 7").Value);
        Assert.AreEqual(name, CustomFunction.Create(name, () => new MathValue(1)).Name);

        var context = new EvaluationContext([new Variable(name, 7)]);
        Assert.AreEqual(new MathValue(7), Engine.Evaluate(name, context).Value);
    }

    private static void AssertReservedAcrossDeclarations(string name, ReservedNameKind expectedKind)
    {
        var reservation = ReservedNames.Classify(name);
        Assert.AreEqual(expectedKind, reservation.Kind, name);
        foreach (var engine in new[] { Engine, RawEngine })
        {
            var parsedException = Assert.ThrowsExactly<ExpressionException>(() => engine.Parse($"  {name} = 7"));
            Assert.AreEqual(EvaluationError.InvalidSyntax, parsedException.Diagnostic.Code, name);
            Assert.AreEqual(2, parsedException.Diagnostic.Position, name);
            Assert.AreEqual(name.Length, parsedException.Diagnostic.Length, name);
        }

        var span = new TextSpan(12, 9);
        var assignment = new AssignmentNode(name, new NumberNode(new MathValue(7)), span: span);
        var nodeException = Assert.ThrowsExactly<ExpressionException>(() => Engine.Evaluate(assignment));
        Assert.AreEqual(EvaluationError.InvalidSyntax, nodeException.Diagnostic.Code, name);
        Assert.AreEqual(span.Start, nodeException.Diagnostic.Position, name);
        Assert.AreEqual(span.Length, nodeException.Diagnostic.Length, name);

        string expectedMessage;
        switch (expectedKind)
        {
            case ReservedNameKind.Constant:
                Assert.IsTrue(ConstantCatalog.TryGetInfo(name, out var constant));
                Assert.AreSame(constant, reservation.Constant);
                Assert.IsNull(reservation.BuiltInFunction);
                expectedMessage = $"Cannot assign to constant name '{name}'.";
                break;

            case ReservedNameKind.BuiltInFunction:
                Assert.IsTrue(FunctionCatalog.TryGetInfo(name, out var function));
                Assert.AreSame(function, reservation.BuiltInFunction);
                Assert.IsNull(reservation.Constant);
                expectedMessage = $"Cannot assign to function name '{name}'.";
                break;

            default:
                Assert.IsNull(reservation.Constant);
                Assert.IsNull(reservation.BuiltInFunction);
                expectedMessage = $"Cannot assign to reserved name '{name}'.";
                break;
        }

        Assert.AreEqual(expectedMessage, nodeException.Diagnostic.Message);

        var functionException = Assert.ThrowsExactly<ArgumentException>(() =>
            CustomFunction.Create(name, () => new MathValue(1)));
        Assert.AreEqual("name", functionException.ParamName, name);
        var contextException = Assert.ThrowsExactly<ArgumentException>(() =>
            new EvaluationContext([new Variable(name, 7)]));
        Assert.AreEqual(nameof(EvaluationContext.Variables), contextException.ParamName, name);
    }
}
