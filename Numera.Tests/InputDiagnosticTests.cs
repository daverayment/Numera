using System.Globalization;
using Numera.Ast;

namespace Numera.Tests;

[TestClass]
public sealed class InputDiagnosticTests
{
    private static readonly CalculatorEngine Engine = CalculatorEngine.ForCulture(CultureInfo.InvariantCulture);

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("2 +")]
    [DataRow("2 +   ")]
    [DataRow("2^")]
    [DataRow("-")]
    [DataRow("not")]
    [DataRow("sin(")]
    [DataRow("sin")]
    [DataRow("SIN   ")]
    [DataRow("(2 + 3")]
    [DataRow("((2)")]
    [DataRow("x =")]
    [DataRow("x =   ")]
    [DataRow("0x")]
    [DataRow("0b")]
    [DataRow("0o")]
    [DataRow("max(1,")]
    [DataRow("atan2(1")]
    [DataRow("2 <")]
    [DataRow("1 and")]
    public void RecognizedUnfinishedSyntaxIsFlaggedAcrossStringApis(string input)
    {
        foreach (var engine in new[] { Engine, Engine.WithOptions(options => options with { Normalization = null }) })
        {
            Assert.IsFalse(engine.TryParse(input, out var tree, out var parsed));
            Assert.IsNull(tree);
            Assert.AreEqual(EvaluationError.InvalidSyntax, parsed.Code, input);
            Assert.IsTrue(parsed.IsIncomplete, input);
            Assert.IsFalse(engine.TryEvaluate(input, out _, out var evaluated));
            Assert.AreEqual(parsed, evaluated, input);
            Assert.AreEqual(parsed, Assert.ThrowsExactly<ExpressionException>(() => engine.Parse(input)).Diagnostic);
            Assert.AreEqual(parsed, Assert.ThrowsExactly<ExpressionException>(() => engine.Evaluate(input)).Diagnostic);
            Assert.AreEqual(parsed, Assert.ThrowsExactly<ExpressionException>(() => engine.FormatExpression(input)).Diagnostic);
        }

        Assert.IsFalse(Calculator.TryParse(input, out _, out var facadeParsed));
        Assert.IsTrue(facadeParsed.IsIncomplete, input);
        Assert.IsFalse(Calculator.TryEvaluate(input, out _, out var facadeEvaluated));
        Assert.IsTrue(facadeEvaluated.IsIncomplete, input);
    }

    [TestMethod]
    [DataRow("2 + )", EvaluationError.InvalidSyntax)]
    [DataRow("2 3", EvaluationError.InvalidNumberFormat)]
    [DataRow("2 $ 3", EvaluationError.InvalidSyntax)]
    [DataRow("()", EvaluationError.InvalidSyntax)]
    [DataRow("max()", EvaluationError.InvalidArgumentCount)]
    [DataRow("atan2(1)", EvaluationError.InvalidArgumentCount)]
    [DataRow("max(1,)", EvaluationError.InvalidSyntax)]
    [DataRow("(x = )", EvaluationError.InvalidSyntax)]
    [DataRow("sin + 2", EvaluationError.InvalidSyntax)]
    [DataRow("0xG", EvaluationError.InvalidSyntax)]
    [DataRow("0b2", EvaluationError.InvalidSyntax)]
    [DataRow("sqr", EvaluationError.UnknownVariable)]
    [DataRow("missing", EvaluationError.UnknownVariable)]
    [DataRow("missing(2)", EvaluationError.UnknownFunction)]
    [DataRow("ans", EvaluationError.NoPreviousResult)]
    [DataRow("ANS + 1", EvaluationError.NoPreviousResult)]
    [DataRow("1/0", EvaluationError.DivisionByZero)]
    [DataRow("sqrt(-1)", EvaluationError.UndefinedMathResult)]
    public void OtherFailuresAreNotIncomplete(string input, EvaluationError code)
    {
        Assert.IsFalse(Engine.TryEvaluate(input, out _, out var diagnostic));
        Assert.AreEqual(code, diagnostic.Code, input);
        Assert.IsFalse(diagnostic.IsIncomplete, input);
    }

    [TestMethod]
    [DataRow("a = b =")]
    [DataRow("(x =")]
    [DataRow(".")]
    public void RawUnfinishedSyntaxIsFlaggedWithoutLauncherInterpretation(string input)
    {
        var engine = Engine.WithOptions(options => options with { Normalization = null });
        Assert.IsFalse(engine.TryEvaluate(input, out _, out var diagnostic));
        Assert.AreEqual(EvaluationError.InvalidSyntax, diagnostic.Code);
        Assert.IsTrue(diagnostic.IsIncomplete);
    }

    [TestMethod]
    public void LauncherDecorationKeepsItsExistingInterpretation()
    {
        var context = new EvaluationContext([new Variable("b", 5)]);
        Assert.AreEqual(new MathValue(5), Engine.Evaluate("a = b =", context).Value);
        Assert.IsFalse(Engine.TryEvaluate("a = b =", out _, out var diagnostic));
        Assert.AreEqual(EvaluationError.UnknownVariable, diagnostic.Code);
        Assert.IsFalse(diagnostic.IsIncomplete);
    }

    [TestMethod]
    [DataRow("missing", EvaluationError.UnknownVariable)]
    [DataRow("ans", EvaluationError.NoPreviousResult)]
    [DataRow("AnS", EvaluationError.NoPreviousResult)]
    public void DeferredAndSuppliedTreesUseTheSameIdentifierCategories(string name, EvaluationError code)
    {
        Assert.IsFalse(Engine.TryParse(name, out _, out var parsed));
        Assert.AreEqual(code, parsed.Code);

        var deferred = Engine.Parse(name, allowUnboundVariables: true);
        foreach (var tree in new[] { deferred, new IdentifierNode(name, Span: new TextSpan(0, name.Length)) })
        {
            Assert.IsFalse(Engine.TryEvaluate(tree, out _, out var evaluated));
            Assert.AreEqual(parsed, evaluated);
            Assert.AreEqual(parsed, Assert.ThrowsExactly<ExpressionException>(() => Engine.Evaluate(tree)).Diagnostic);
            Assert.IsFalse(Calculator.TryEvaluate(tree, out _, out var facade));
            Assert.AreEqual(code, facade.Code);
            Assert.IsFalse(facade.IsIncomplete);
        }
    }

    [TestMethod]
    public void NamesResolveNormallyWhenValuesAreSupplied()
    {
        var context = new EvaluationContext([new Variable("missing", 7)], new MathValue(9));
        Assert.AreEqual(new MathValue(16), Engine.Evaluate("missing + ans", context).Value);
        Assert.AreEqual(new MathValue(16), Engine.Evaluate("missing + AnS", context).Value);
        Assert.AreEqual(new MathValue(7), Engine.Evaluate("missing = 7").Value);
    }

    [TestMethod]
    public void KnownCustomNamesRequireCallsWithoutGuessingPrefixes()
    {
        int calls = 0;
        var engine = Engine.WithOptions(options => options with
        {
            CustomFunctions = [CustomFunction.Create("triple", (MathValue value) =>
            {
                calls++;
                return value * new MathValue(3);
            })]
        });

        foreach (string input in new[] { "triple", "triple(", "triple(2) +", "(x = 2) +" })
        {
            Assert.IsFalse(engine.TryEvaluate(input, out _, out var diagnostic));
            Assert.IsTrue(diagnostic.IsIncomplete, input);
        }

        Assert.IsFalse(engine.TryEvaluate("trip", out _, out var prefix));
        Assert.AreEqual(EvaluationError.UnknownVariable, prefix.Code);
        Assert.IsFalse(prefix.IsIncomplete);
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    [DataRow("= 2 ×", 5, 0)]
    [DataRow("  x =   ", 4, 1)]
    [DataRow("  sin   ", 2, 3)]
    public void IncompleteFlagsDoNotDependOnDiagnosticSpanShape(string input, int position, int length)
    {
        Assert.IsFalse(Engine.TryEvaluate(input, out _, out var diagnostic));
        Assert.IsTrue(diagnostic.IsIncomplete);
        Assert.AreEqual(position, diagnostic.Position);
        Assert.AreEqual(length, diagnostic.Length);
    }

    [TestMethod]
    public void NormalizedUnknownNamesKeepOriginalSourceCoordinates()
    {
        Assert.IsFalse(Engine.TryEvaluate("= 2 × missing", out _, out var diagnostic));
        Assert.AreEqual(EvaluationError.UnknownVariable, diagnostic.Code);
        Assert.AreEqual(6, diagnostic.Position);
        Assert.AreEqual(7, diagnostic.Length);
        Assert.IsFalse(diagnostic.IsIncomplete);
    }

    [TestMethod]
    public void NumberNormalizationAndLimitFailuresAreOutsideIncompleteClassification()
    {
        foreach (var (culture, input) in new[] { ("en-US", "1,00"), ("de-DE", "1.00") })
        {
            var engine = CalculatorEngine.ForCulture(CultureInfo.GetCultureInfo(culture));
            Assert.IsFalse(engine.TryEvaluate(input, out _, out var diagnostic));
            Assert.AreEqual(EvaluationError.InvalidNumberFormat, diagnostic.Code);
            Assert.IsFalse(diagnostic.IsIncomplete);
        }

        var limited = Engine.WithOptions(options => options with { MaxInputLength = 3 });
        Assert.IsFalse(limited.TryEvaluate("1234 +", out _, out var limit));
        Assert.AreEqual(EvaluationError.LimitExceeded, limit.Code);
        Assert.IsFalse(limit.IsIncomplete);
        Assert.AreEqual(CalculationLimitKind.InputLength, limit.LimitKind);
    }

    [TestMethod]
    public void DiagnosticConstructorAndDeconstructionRemainFourPart()
    {
        var diagnostic = new EvaluationDiagnostic(EvaluationError.InvalidSyntax, "More input needed", 2, 0);
        Assert.IsFalse(diagnostic.IsIncomplete);

        var incomplete = diagnostic with { IsIncomplete = true };
        var (code, message, position, length) = incomplete;
        Assert.AreEqual(diagnostic.Code, code);
        Assert.AreEqual(diagnostic.Message, message);
        Assert.AreEqual(diagnostic.Position, position);
        Assert.AreEqual(diagnostic.Length, length);
        Assert.IsTrue((incomplete with { Position = 3 }).IsIncomplete);
    }
}
