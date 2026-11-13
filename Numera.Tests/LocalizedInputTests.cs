using System.Globalization;
using Numera.Ast;

namespace Numera.Tests;

[TestClass]
public sealed class LocalizedInputTests
{
    [TestMethod]
    [DataRow("en-US", "pow(2, 100)", "pow(2, 100)")]
    [DataRow("en-US", "pow(2;100)", "pow(2,100)")]
    [DataRow("en-US", "max(1,000+2,000;3,000+9,000)", "max(1000+2000,3000+9000)")]
    [DataRow("en-US", "max(1000+2000,3000+9000)", "max(1000+2000,3000+9000)")]
    [DataRow("en-US", "1,234,567.890", "1234567.890")]
    [DataRow("en-US", "max((1,234), 567)", "max((1234), 567)")]
    [DataRow("en-US", "max(1 000,123)", "max(1000,123)")]
    [DataRow("en-US", "max(1'000,123)", "max(1000,123)")]
    [DataRow("en-US", "max(1.234,567)", "max(1.234,567)")]
    [DataRow("en-US", "max(1e2,100)", "max(1e2,100)")]
    [DataRow("en-US", "max(0,100)", "max(0,100)")]
    [DataRow("en-US", "lerp(0,100,0.5)", "lerp(0,100,0.5)")]
    [DataRow("en-US", "2max(1;234)", "2max(1,234)")]
    [DataRow("en-US", "log10(100)", "log10(100)")]
    [DataRow("en-US", "item1 + 1'000", "item1 + 1000")]
    [DataRow("en-US", "0x10(1,234)", "0x10(1234)")]
    [DataRow("en-US", "max(0x10,100)", "max(0x10,100)")]
    [DataRow("en-US", "1e3(1,234)", "1e3(1234)")]
    [DataRow("en-US", "1. + 1.e2", "1. + 1.e2")]
    [DataRow("de-DE", "max(1,234)", "max(1.234)")]
    [DataRow("de-DE", "max(1;234;567)", "max(1,234,567)")]
    [DataRow("de-DE", "max(1,234;567)", "max(1.234,567)")]
    [DataRow("de-DE", "max(1.234.567,890)", "max(1234567.890)")]
    [DataRow("de-DE", ",5 + ,5", ".5 + .5")]
    [DataRow("de-DE", "1 + ,5", "1 + .5")]
    [DataRow("de-DE", "1,25e-3", "1.25e-3")]
    [DataRow("fr-FR", "1\u202f234,50", "1234.50")]
    [DataRow("hi-IN", "12,34,567.89", "1234567.89")]
    [DataRow("hi-IN", "max(12,34,567;2)", "max(1234567,2)")]
    [DataRow("de-CH", "1\u2019234.50", "1234.50")]
    public void AcceptedInputHasOneExplicitLocalizedInterpretation(string culture, string input, string expected)
    {
        Assert.AreEqual(expected, ExpressionNormalizer.Normalize(input, CultureInfo.GetCultureInfo(culture)));
    }

    [TestMethod]
    [DataRow("not (1,000 + 2)", "not (1000 + 2)", 0)]
    [DataRow("not(1,000 == 1000)", "not(1000 == 1000)", 0)]
    [DataRow("NoT (1,000 == 1000)", "NoT (1000 == 1000)", 0)]
    [DataRow("1 and (1,000)", "1 and (1000)", 1)]
    [DataRow("0 or(1,000)", "0 or(1000)", 1)]
    [DataRow("1 xor (1,000)", "1 xor (1000)", 0)]
    [DataRow("10% of (1,000)", "10% of (1000)", 100)]
    [DataRow("2and(1,000)", "2and(1000)", 1)]
    [DataRow("1 and(not(1,000 == 1000))", "1 and(not(1000 == 1000))", 0)]
    [DataRow("max(not(1,000 == 1000);2)", "max(not(1000 == 1000),2)", 2)]
    public void OperatorKeywordsIntroduceGroupingRatherThanFunctionArguments(
        string input, string normalized, int expectedValue)
    {
        var engine = CalculatorEngine.ForCulture(CultureInfo.GetCultureInfo("en-US"),
            options => options with { RequireExplicitLogicalNegation = true });

        Assert.AreEqual(normalized, ExpressionNormalizer.Normalize(input, engine.Options.Culture));
        var tree = engine.Parse(input);
        Assert.AreEqual(new MathValue(expectedValue), engine.Evaluate(tree).Value);
        Assert.AreEqual(new MathValue(expectedValue), engine.Evaluate(input).Value);
        var formatted = engine.FormatExpression(input, ExpressionFormatOptions.Ascii);
        Assert.AreEqual(new MathValue(expectedValue), engine.Evaluate(formatted).Value);
    }

    [TestMethod]
    [DataRow("en-US", "pow(2,100)")]
    [DataRow("en-US", "max(1,234)")]
    [DataRow("en-US", "max(1,234,567)")]
    [DataRow("en-US", "max(1,234,567.890)")]
    [DataRow("en-US", "max(1,000+2,000,3,000+9,000)")]
    [DataRow("en-US", "max(1,234,56)")]
    [DataRow("hi-IN", "max(1,23,456)")]
    [DataRow("en-US", "max(1;min(2,100))")]
    [DataRow("en-US", "2max(1,234)")]
    [DataRow("en-US", "not (max(1,234))")]
    public void AmbiguousCommasFailWithoutArityOrWholeExpressionGuessing(string culture, string input)
    {
        Assert.IsFalse(ExpressionNormalizer.TryNormalize(input, CultureInfo.GetCultureInfo(culture), out var text, out var diagnostic));
        Assert.IsNull(text);
        Assert.IsNotNull(diagnostic);
        Assert.AreEqual(EvaluationError.InvalidNumberFormat, diagnostic.Code);
        Assert.Contains("ambiguous", diagnostic.Message.ToLowerInvariant());
        Assert.Contains(";", diagnostic.Message);
        Assert.AreEqual(",", input.Substring(diagnostic.Position!.Value, diagnostic.Length));
    }

    [TestMethod]
    [DataRow("en-US", "1,23")]
    [DataRow("en-US", "not (1,23)")]
    [DataRow("en-US", "1234,567")]
    [DataRow("en-US", "1,234,56")]
    [DataRow("en-US", "1'23")]
    [DataRow("en-US", "1 23 456")]
    [DataRow("en-US", "1,234'567")]
    [DataRow("en-US", "1.2.3")]
    [DataRow("en-US", "0x10 000")]
    [DataRow("en-US", "0b1'000")]
    [DataRow("en-US", "0x10,000")]
    [DataRow("en-US", "1e2,000")]
    [DataRow("en-US", "1e2 000")]
    [DataRow("en-US", "1.2'345")]
    [DataRow("de-DE", "0x10.000")]
    [DataRow("en-US", "\u0661\u0662\u0663")]
    [DataRow("de-DE", "0.123")]
    [DataRow("de-DE", "1.23")]
    [DataRow("de-DE", "max(1,234,567)")]
    [DataRow("de-DE", "max(1,234,567.890)")]
    [DataRow("de-DE", "max(1.5;2.5)")]
    [DataRow("fr-FR", "0.123")]
    [DataRow("hi-IN", "1,234,567")]
    public void MalformedNumbersAndForeignDecimalFormsAreRejected(string culture, string input)
    {
        Assert.IsFalse(ExpressionNormalizer.TryNormalize(input, CultureInfo.GetCultureInfo(culture), out var text, out var diagnostic));
        Assert.IsNull(text);
        Assert.IsNotNull(diagnostic);
        Assert.AreEqual(EvaluationError.InvalidNumberFormat, diagnostic.Code);
        Assert.IsTrue(diagnostic.Position >= 0);
        Assert.IsGreaterThan(0, diagnostic.Length);
    }

    [TestMethod]
    [DataRow("en-US", "max(1, 2;3)")]
    [DataRow("de-DE", "max(1, 2)")]
    [DataRow("de-DE", "max(x,2)")]
    public void InvalidArgumentSeparatorsHaveActionableSyntaxDiagnostics(string culture, string input)
    {
        Assert.IsFalse(ExpressionNormalizer.TryNormalize(input, CultureInfo.GetCultureInfo(culture), out _, out var diagnostic));
        Assert.IsNotNull(diagnostic);
        Assert.AreEqual(EvaluationError.InvalidSyntax, diagnostic.Code);
        Assert.Contains(";", diagnostic.Message);
    }

    [TestMethod]
    public void NestedContainersKeepIndependentSeparatorStylesAndPreserveMatrixRows()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        Assert.AreEqual("max(1234,min(2, 100))", ExpressionNormalizer.Normalize("max(1,234;min(2, 100))", culture));
        Assert.AreEqual("max(1, min(2000,3))", ExpressionNormalizer.Normalize("max(1, min(2,000;3))", culture));
        Assert.AreEqual("max([1,000;2,000],3)", ExpressionNormalizer.Normalize("max([1,000;2,000];3)", culture));
        Assert.AreEqual("max([1,000;2,000],3)", ExpressionNormalizer.Normalize("max([1,000;2,000],3)", culture));
        Assert.IsFalse(ExpressionNormalizer.TryNormalize("max([1;2],1,234)", culture, out _, out var diagnostic));
        Assert.AreEqual(EvaluationError.InvalidNumberFormat, diagnostic!.Code);
    }

    [TestMethod]
    public void IdenticalCultureDecimalAndGroupSeparatorsDoNotSilentlyChooseDecimals()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberDecimalSeparator = ",";
        culture.NumberFormat.NumberGroupSeparator = ",";
        culture.TextInfo.ListSeparator = ";";
        Assert.AreEqual("1.25", ExpressionNormalizer.Normalize("1,25", culture));
        Assert.AreEqual("0.123", ExpressionNormalizer.Normalize("0,123", culture));
        Assert.AreEqual(".123", ExpressionNormalizer.Normalize(",123", culture));
        foreach (string input in new[] { "1,234", "123,456,789,25", "max(1,234;2)" })
        {
            Assert.IsFalse(ExpressionNormalizer.TryNormalize(input, culture, out _, out var diagnostic));
            Assert.AreEqual(EvaluationError.InvalidNumberFormat, diagnostic!.Code);
            Assert.Contains("Ambiguous", diagnostic.Message);
        }
    }

    [TestMethod]
    public void ExplicitGroupSizesIncludingTerminalZeroAreRespected()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberGroupSizes = [3, 0];
        Assert.AreEqual("1234567", ExpressionNormalizer.Normalize("1234,567", culture));
        Assert.IsFalse(ExpressionNormalizer.TryNormalize("1,234,567", culture, out _, out _));
        Assert.IsFalse(ExpressionNormalizer.TryNormalize("max(1234,567)", culture, out _, out var diagnostic));
        Assert.AreEqual(EvaluationError.InvalidNumberFormat, diagnostic!.Code);
    }

    [TestMethod]
    public void MultiCharacterNumberSeparatorsMapToTheirOriginalRanges()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberDecimalSeparator = "::";
        culture.NumberFormat.NumberGroupSeparator = "_";
        var options = new NormalizationOptions { ReplaceDivisionColon = false };
        Assert.IsTrue(ExpressionNormalizer.TryNormalize("1_234::50", culture, out var text, out _, out var map, options));
        Assert.AreEqual("1234.50", text);
        Assert.AreEqual(new TextSpan(5, 2), map.MapSpan(new TextSpan(4, 1)));
        Assert.IsFalse(ExpressionNormalizer.TryNormalize("0x10_000", culture, out _, out var diagnostic, options));
        Assert.AreEqual(EvaluationError.InvalidNumberFormat, diagnostic!.Code);
    }

    [TestMethod]
    public void EngineSurfacesFailBeforeEvaluationAndUseOriginalSourcePositions()
    {
        var engine = CalculatorEngine.ForCulture(CultureInfo.GetCultureInfo("en-US"));
        const string input = "= 2 ** 3 + max(1,234) =";
        int comma = input.IndexOf(',');
        Assert.IsFalse(engine.TryEvaluate(input, out _, out var evaluationDiagnostic));
        Assert.IsFalse(engine.TryParse(input, out _, out var parseDiagnostic));

        var parseException = Assert.ThrowsExactly<ExpressionException>(() => engine.Parse(input));
        var evaluationException = Assert.ThrowsExactly<ExpressionException>(() => engine.Evaluate(input));
        var formatException = Assert.ThrowsExactly<ExpressionException>(() => engine.FormatExpression(input));
        foreach (var diagnostic in new[]
        {
            evaluationDiagnostic,
            parseDiagnostic,
            parseException.Diagnostic,
            evaluationException.Diagnostic,
            formatException.Diagnostic
        })
        {
            Assert.IsNotNull(diagnostic);
            Assert.AreEqual(EvaluationError.InvalidNumberFormat, diagnostic.Code);
            Assert.AreEqual(comma, diagnostic.Position);
            Assert.AreEqual(1, diagnostic.Length);
        }

        Assert.IsFalse(ExpressionNormalizer.TryNormalize(input, engine.Options.Culture, out _, out var standalone));
        Assert.AreEqual(evaluationDiagnostic, standalone);
    }

    [TestMethod]
    public void ConfiguredMultiCharacterArgumentSeparatorsHaveIndependentCallStyles()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.TextInfo.ListSeparator = "||";
        Assert.IsTrue(ExpressionNormalizer.TryNormalize("max(1,234||2)", culture, out var text, out _, out var map));
        Assert.AreEqual("max(1234,2)", text);
        Assert.AreEqual(new TextSpan(9, 2), map.MapSpan(new TextSpan(8, 1)));
        Assert.AreEqual("max(1,max(2,3))", ExpressionNormalizer.Normalize("max(1;max(2||3))", culture));

        Assert.IsFalse(ExpressionNormalizer.TryNormalize("max(1||2;3)", culture, out _, out var diagnostic));
        Assert.AreEqual(EvaluationError.InvalidSyntax, diagnostic!.Code);
        Assert.AreEqual(5, diagnostic.Position);
        Assert.AreEqual(2, diagnostic.Length);
    }

    [TestMethod]
    public void ExplicitInputEvaluatesCorrectlyAndRawModeRemainsInvariant()
    {
        var us = CalculatorEngine.ForCulture(CultureInfo.GetCultureInfo("en-US"));
        Assert.AreEqual(Calculator.Evaluate("2^100").Value, us.Evaluate("pow(2;100)").Value);
        Assert.AreEqual(new MathValue(12000), us.Evaluate("max(1,000+2,000;3,000+9,000)").Value);
        var german = CalculatorEngine.ForCulture(CultureInfo.GetCultureInfo("de-DE"));
        Assert.AreEqual(new MathValue(1), german.Evaluate(",5 + ,5").Value);
        Assert.AreEqual(new MathValue(1.234m), german.Evaluate("max(1,234)").Value);
        var raw = german.WithOptions(german.Options with { Normalization = null });
        Assert.AreEqual(new MathValue(234), raw.Evaluate("max(1,234)").Value);
        Assert.AreEqual(new MathValue(0.123m), raw.Evaluate("0.123").Value);
        Assert.IsFalse(raw.TryEvaluate("max(1;234)", out _, out _));
    }

    [TestMethod]
    public void NormalizationFailureDoesNotInvokeCustomFunctions()
    {
        int calls = 0;
        var options = CalculatorOptions.ForCulture(CultureInfo.GetCultureInfo("en-US")) with
        {
            CustomFunctions =
            [
                CustomFunction.Create("tick", () =>
                {
                    calls++;
                    return new MathValue(1);
                })
            ]
        };
        var engine = new CalculatorEngine(options);
        Assert.IsFalse(engine.TryEvaluate("tick() + max(1,234)", out _, out var diagnostic));
        Assert.AreEqual(EvaluationError.InvalidNumberFormat, diagnostic!.Code);
        Assert.AreEqual(0, calls);
        Assert.AreEqual(new MathValue(235), engine.Evaluate("tick() + max(1;234)").Value);
        Assert.AreEqual(1, calls);
    }
}
