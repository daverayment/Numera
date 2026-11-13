using System.Globalization;
using Numera.Ast;

namespace Numera.Tests;

[TestClass]
public sealed class LauncherAssignmentTests
{
    private static readonly CalculatorEngine Engine = CalculatorEngine.ForCulture(CultureInfo.InvariantCulture);
    private static readonly EvaluationContext Context = new(
        [new Variable("x", new MathValue(42)), new Variable("y", new MathValue(7))],
        new MathValue(100));

    [TestMethod]
    [DataRow("x =")]
    [DataRow("x=   ")]
    [DataRow("  x =\t")]
    [DataRow("= x = ")]
    [DataRow("= X =\u00a0")]
    [DataRow("(x =)")]
    [DataRow("= (x = ) =")]
    [DataRow("= 2 ** 3 + (x = ) =")]
    [DataRow("false && (x = ) =")]
    [DataRow("(a = (x = )) =")]
    public void MissingAssignmentValueIsSyntaxErrorOnEverySurface(string expression)
    {
        int target = expression.IndexOf("x", StringComparison.OrdinalIgnoreCase);
        int assignment = expression.IndexOf('=', target);
        foreach (var context in new[] { EvaluationContext.Empty, Context })
        {
            Assert.IsFalse(Engine.TryEvaluate(expression, out _, out var evaluationDiagnostic, context));
            Assert.IsFalse(Engine.TryParse(expression, out var node, out var parseDiagnostic, context));
            Assert.IsNull(node);
            var evaluationException = Assert.ThrowsExactly<ExpressionException>(() => Engine.Evaluate(expression, context));
            var parseException = Assert.ThrowsExactly<ExpressionException>(() => Engine.Parse(expression, context));
            var formatException = Assert.ThrowsExactly<ExpressionException>(() => Engine.FormatExpression(expression, context: context));
            foreach (var diagnostic in new[] { evaluationDiagnostic, parseDiagnostic, evaluationException.Diagnostic, parseException.Diagnostic, formatException.Diagnostic })
            {
                Assert.IsNotNull(diagnostic);
                Assert.AreEqual(EvaluationError.InvalidSyntax, diagnostic.Code);
                Assert.AreEqual(assignment, diagnostic.Position);
                Assert.AreEqual(1, diagnostic.Length);
                Assert.Contains("value after '='", diagnostic.Message);
            }
        }
        Assert.AreEqual(new MathValue(42), Context.Variables["x"].Value);
        Assert.AreEqual(new MathValue(100), Context.PreviousResult);
    }

    [TestMethod]
    public void StandaloneNormalizationPreservesTheAssignmentOperatorAndItsSourceMap()
    {
        const string expression = "= \t x = \u00a0";
        Assert.IsTrue(ExpressionNormalizer.TryNormalize(expression, CultureInfo.InvariantCulture,
            out var text, out var diagnostic, out var map));
        Assert.IsNotNull(text);
        Assert.AreEqual("x =", text);
        Assert.IsNull(diagnostic);
        int assignment = text.IndexOf('=');
        Assert.AreEqual(new TextSpan(expression.LastIndexOf('='), 1), map.MapSpan(new TextSpan(assignment, 1)));
    }

    [TestMethod]
    [DataRow("x = 5 =")]
    [DataRow("x=5=")]
    [DataRow("= x = 5 =")]
    [DataRow("\t= x = 5 = ")]
    public void CompletedAssignmentKeepsItsValueDefinitionAndOriginalSpan(string expression)
    {
        Assert.IsTrue(Engine.TryEvaluate(expression, out var result, out _, Context));
        Assert.AreEqual(new MathValue(5), result.Value);
        Assert.IsTrue(result.HasAssignments);
        Assert.HasCount(1, result.AssignedVariables);
        Assert.AreEqual("5", result.AssignedVariables["x"].Definition);
        Assert.AreEqual(new MathValue(5), result.Context.Variables["x"].Value);
        Assert.AreEqual(new MathValue(42), Context.Variables["x"].Value);

        var assignment = (AssignmentNode)Engine.Parse(expression, Context);
        int start = expression.IndexOf('x');
        int end = expression.LastIndexOf('5') + 1;
        Assert.AreEqual(new TextSpan(start, end - start), assignment.Span);
        Assert.AreEqual("5", assignment.Definition);
    }

    [TestMethod]
    public void CompletedChainedAndVariableAssignmentsAcceptLauncherDecoration()
    {
        var chain = Engine.Evaluate("a = b = 5 =", Context);
        Assert.AreEqual(new MathValue(5), chain.Value);
        Assert.HasCount(2, chain.AssignedVariables);
        Assert.AreEqual("5", chain.AssignedVariables["a"].Definition);
        Assert.AreEqual("5", chain.AssignedVariables["b"].Definition);

        var variable = Engine.Evaluate("x = y =", Context);
        Assert.AreEqual(new MathValue(7), variable.Value);
        Assert.HasCount(1, variable.AssignedVariables);
        Assert.AreEqual("y", variable.AssignedVariables["x"].Definition);
        Assert.AreEqual(new MathValue(7), variable.Context.Variables["y"].Value);
    }

    [TestMethod]
    [DataRow("x = 5 + =")]
    [DataRow("x = 5 - =")]
    [DataRow("x = =")]
    [DataRow("x = 5 ==")]
    [DataRow("x = * =")]
    [DataRow("x = ( ) =")]
    public void DecorationCannotMakeMalformedAssignmentValuesSucceed(string expression)
    {
        Assert.IsFalse(Engine.TryEvaluate(expression, out _, out var diagnostic, Context));
        Assert.AreEqual(EvaluationError.InvalidSyntax, diagnostic!.Code);
        Assert.AreEqual(new MathValue(42), Context.Variables["x"].Value);
    }

    [TestMethod]
    public void RawAndDisabledLauncherModesStillReportMissingValues()
    {
        var disabled = Engine.WithOptions(Engine.Options with
        {
            Normalization = new NormalizationOptions { TrimLauncherEquals = false }
        });
        var raw = Engine.WithOptions(Engine.Options with { Normalization = null });
        foreach (var engine in new[] { disabled, raw })
        {
            Assert.IsFalse(engine.TryEvaluate(" x = ", out _, out var diagnostic, Context));
            Assert.AreEqual(EvaluationError.InvalidSyntax, diagnostic!.Code);
            Assert.AreEqual(3, diagnostic.Position);
            Assert.AreEqual(1, diagnostic.Length);
            Assert.IsFalse(engine.TryEvaluate("x = 5 =", out _, out diagnostic, Context));
            Assert.AreEqual(EvaluationError.InvalidSyntax, diagnostic!.Code);
            Assert.AreEqual(new MathValue(5), engine.Evaluate("x = 5", Context).Value);
        }
    }

    [TestMethod]
    public void CompletedAssignmentsRetainLocalizedDecimalNormalization()
    {
        var german = Engine.WithCulture(CultureInfo.GetCultureInfo("de-DE"));
        var result = german.Evaluate("= x = 1,5 =", Context);
        Assert.AreEqual(new MathValue(1.5m), result.Value);
        Assert.AreEqual("1.5", result.AssignedVariables["x"].Definition);
    }

    [TestMethod]
    public void InvalidAssignmentsDoNotExecuteCustomFunctionsOrAdvanceContext()
    {
        int calls = 0;
        var engine = Engine.WithOptions(Engine.Options with
        {
            CustomFunctions =
            [
                CustomFunction.Create("tick", () =>
                {
                    calls++;
                    return new MathValue(1);
                })
            ]
        });
        Assert.IsFalse(engine.TryEvaluate("= tick() + (x = ) =", out _, out var diagnostic, Context));
        Assert.AreEqual(EvaluationError.InvalidSyntax, diagnostic!.Code);
        Assert.AreEqual(0, calls);
        Assert.AreEqual(new MathValue(42), Context.Variables["x"].Value);
        Assert.AreEqual(new MathValue(100), Context.PreviousResult);
        Assert.IsTrue(engine.TryEvaluate("x = tick() =", out var result, out _, Context));
        Assert.AreEqual(1, calls);
        Assert.AreEqual(new MathValue(1), result.Context.Variables["x"].Value);
    }
}
