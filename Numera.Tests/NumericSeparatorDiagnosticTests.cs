using System.Globalization;

namespace Numera.Tests;

[TestClass]
public sealed class NumericSeparatorDiagnosticTests
{
    private static readonly CalculatorEngine Engine = CalculatorEngine.ForCulture(CultureInfo.InvariantCulture);
    private static readonly CalculatorEngine RawEngine = Engine.WithOptions(options => options with { Normalization = null });

    [TestMethod]
    [DataRow("1_000", 1, 1)]
    [DataRow("1__000", 1, 2)]
    [DataRow("1_000_000", 1, 1)]
    [DataRow("1_", 1, 1)]
    [DataRow("1___", 1, 3)]
    [DataRow("2_x", 1, 1)]
    [DataRow("0_0", 1, 1)]
    [DataRow("0.0__0", 3, 2)]
    [DataRow(".5_x", 2, 1)]
    [DataRow("2._x", 2, 1)]
    [DataRow("2e3_4", 3, 1)]
    [DataRow("2E-3__4", 4, 2)]
    [DataRow("0e99999_0", 7, 1)]
    [DataRow("0xFF_000", 4, 1)]
    [DataRow("0b101__0", 5, 2)]
    [DataRow("0o7_x", 3, 1)]
    [DataRow("0x_FF", 2, 1)]
    [DataRow("0b_10", 2, 1)]
    [DataRow("0o_7", 2, 1)]
    [DataRow("-(0xFF_1)", 6, 1)]
    public void AdjacentUnderscoresHaveTheSameDiagnosticAcrossStringApis(string input, int position, int length)
    {
        foreach (var engine in new[] { Engine, RawEngine })
        {
            Assert.IsFalse(engine.TryParse(input, out var expression, out var parsed));
            Assert.IsNull(expression);
            Assert.AreEqual(EvaluationError.InvalidNumberFormat, parsed.Code);
            Assert.AreEqual(position, parsed.Position);
            Assert.AreEqual(length, parsed.Length);
            Assert.IsFalse(parsed.IsIncomplete);
            StringAssert.Contains(parsed.Message, "Underscores can't be used as digit separators.");
            StringAssert.Contains(parsed.Message, "1000");

            Assert.IsFalse(engine.TryEvaluate(input, out _, out var evaluated));
            Assert.AreEqual(parsed, evaluated);
            Assert.AreEqual(parsed, Assert.ThrowsExactly<ExpressionException>(() => engine.Parse(input)).Diagnostic);
            Assert.AreEqual(parsed, Assert.ThrowsExactly<ExpressionException>(() => engine.Evaluate(input)).Diagnostic);
            Assert.AreEqual(parsed, Assert.ThrowsExactly<ExpressionException>(() => engine.FormatExpression(input)).Diagnostic);
            Assert.AreEqual(parsed, Assert.ThrowsExactly<ExpressionException>(() =>
                engine.Parse(input, allowUnboundVariables: true)).Diagnostic);
        }

        Assert.IsFalse(ExpressionNormalizer.TryNormalize(input, CultureInfo.InvariantCulture,
            out _, out var normalized));
        Assert.AreEqual(EvaluationError.InvalidNumberFormat, normalized.Code);
        Assert.AreEqual(position, normalized.Position);
        Assert.AreEqual(length, normalized.Length);
    }

    [TestMethod]
    public void DefinedVariablesAndCustomFunctionsCannotReinterpretAdjacentUnderscores()
    {
        var context = new EvaluationContext([new Variable("_000", 3), new Variable("_x", 7)]);
        foreach (var engine in new[] { Engine, RawEngine })
        {
            var configured = engine.WithOptions(options => options with
            {
                CustomFunctions = [CustomFunction.Create("_f", (MathValue value) => value)]
            });
            foreach (string input in new[] { "1_000", "2_x", "2_f(3)" })
            {
                Assert.IsFalse(configured.TryEvaluate(input, out _, out var diagnostic, context));
                Assert.AreEqual(EvaluationError.InvalidNumberFormat, diagnostic.Code);
                Assert.AreEqual(1, diagnostic.Position);
            }

            var explicitOnly = configured.WithOptions(options => options with { AllowImplicitMultiplication = false });
            Assert.IsFalse(explicitOnly.TryEvaluate("2_x", out _, out var explicitDiagnostic, context));
            Assert.AreEqual(EvaluationError.InvalidNumberFormat, explicitDiagnostic.Code);
        }
    }

    [TestMethod]
    [DataRow("_x", 7)]
    [DataRow("2 _x", 14)]
    [DataRow("2\t_x", 14)]
    [DataRow("2\n_x", 14)]
    [DataRow("2 * _x", 14)]
    [DataRow("2*_x", 14)]
    [DataRow("0x2 _x", 14)]
    [DataRow("2e1 _x", 140)]
    [DataRow("(2)_x", 14)]
    [DataRow("value2_x", 3)]
    [DataRow("2e_x", 6)]
    public void UnderscoreIdentifiersAndSeparatedMultiplicationRemainUsable(string input, int expected)
    {
        var context = new EvaluationContext([
            new Variable("_x", 7), new Variable("value2_x", 3), new Variable("e_x", 3)]);
        foreach (var engine in new[] { Engine, RawEngine })
        {
            var result = engine.Evaluate(input, context);
            Assert.AreEqual(new MathValue(expected), result.Value);
            string formatted = engine.FormatExpression(input, ExpressionFormatOptions.Ascii, context);
            Assert.AreEqual(result.Value, RawEngine.Evaluate(formatted, context).Value);
        }
    }

    [TestMethod]
    [DataRow("en-US", "1,000__000", 5, 2)]
    [DataRow("de-DE", "1.000,5_0", 7, 1)]
    [DataRow("fr-FR", "1\u202f000_0", 5, 1)]
    public void LocalizedLiteralsKeepOriginalUnderscoreSpans(string cultureName, string input, int position, int length)
    {
        var engine = CalculatorEngine.ForCulture(CultureInfo.GetCultureInfo(cultureName));
        Assert.IsFalse(engine.TryEvaluate(input, out _, out var diagnostic));
        Assert.AreEqual(EvaluationError.InvalidNumberFormat, diagnostic.Code);
        Assert.AreEqual(position, diagnostic.Position);
        Assert.AreEqual(length, diagnostic.Length);
    }

    [TestMethod]
    public void CustomizedCultureCannotMakeUnderscoresIntoNumericSeparators()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberGroupSeparator = "_";
        var engine = CalculatorEngine.ForCulture(culture);
        foreach (string input in new[] { "1_000", "1.5_0", "0xFF_0" })
        {
            Assert.IsFalse(engine.TryEvaluate(input, out _, out var diagnostic));
            Assert.AreEqual(EvaluationError.InvalidNumberFormat, diagnostic.Code);
            Assert.AreEqual(input.IndexOf('_'), diagnostic.Position);
            StringAssert.Contains(diagnostic.Message, "Underscores can't be used as digit separators.");
        }

        culture.NumberFormat.NumberDecimalSeparator = "::";
        Assert.IsFalse(engine.TryEvaluate("1::5__0", out _, out var mapped));
        Assert.AreEqual(4, mapped.Position);
        Assert.AreEqual(2, mapped.Length);
    }

    [TestMethod]
    public void EarlierNormalizationPassesDoNotShiftTheReportedUnderscores()
    {
        const string input = "= 2 ** 3 + 1__000 =";
        Assert.IsFalse(Engine.TryEvaluate(input, out _, out var diagnostic));
        Assert.AreEqual(EvaluationError.InvalidNumberFormat, diagnostic.Code);
        Assert.AreEqual(input.IndexOf('_'), diagnostic.Position);
        Assert.AreEqual(2, diagnostic.Length);
        Assert.IsFalse(Calculator.TryEvaluate(input, out _, out var facade));
        Assert.AreEqual(diagnostic, facade);
    }

    [TestMethod]
    public void RejectionDoesNotRunCallbacksOrCommitAssignments()
    {
        int calls = 0;
        var context = new EvaluationContext([new Variable("x", 1)]);
        foreach (var engine in new[] { Engine, RawEngine })
        {
            var configured = engine.WithOptions(options => options with
            {
                CustomFunctions = [CustomFunction.Create("tick", () => new MathValue(++calls))]
            });
            Assert.IsFalse(configured.TryEvaluate("tick() + (x = 2) + 1_000", out _, out var diagnostic, context));
            Assert.AreEqual(EvaluationError.InvalidNumberFormat, diagnostic.Code);
            Assert.AreEqual(new MathValue(1), context.Variables["x"].Value);
        }

        Assert.AreEqual(0, calls);
    }
}
