using System.Globalization;

namespace Numera.Tests;

[TestClass]
public sealed class ExpressionNormalizerTests
{
    [TestMethod]
    [DataRow("= 2 + 2", "2 + 2")]
    [DataRow("2 + 2 =", "2 + 2")]
    [DataRow("= 2 + 2 =", "2 + 2")]
    [DataRow("== 2", "== 2")]
    [DataRow("2 == 2", "2 == 2")]
    [DataRow("2 <= 4", "2 <= 4")]
    [DataRow("2 != 3", "2 != 3")]
    public void LauncherQuirksAreTrimmedCorrectly(string input, string expected)
    {
        string normalized = ExpressionNormalizer.Normalize(input);
        Assert.AreEqual(expected, normalized);
    }

    [TestMethod]
    [DataRow("2 × 3", "2 * 3")]
    [DataRow("2 • 3", "2 * 3")]
    [DataRow("2 · 3", "2 * 3")]
    [DataRow("2 ∗ 3", "2 * 3")]
    [DataRow("6 ÷ 2", "6 / 2")]
    [DataRow("6 ∕ 2", "6 / 2")]
    [DataRow("6 ⁄ 2", "6 / 2")]
    [DataRow("6 : 2", "6 / 2")]
    [DataRow("5 − 3", "5 - 3")]
    [DataRow("5 – 3", "5 - 3")]
    [DataRow("5 — 3", "5 - 3")]
    [DataRow("2 ** 8", "2 ^ 8")]
    public void UnicodeOperatorsAreSanitizedToCanonicalAscii(string input, string expected)
    {
        string normalized = ExpressionNormalizer.Normalize(input);
        Assert.AreEqual(expected, normalized);
    }

    [TestMethod]
    public void EuropeanCultureFormattingIsNormalizedCorrectly()
    {
        var germanCulture = new CultureInfo("de-DE");
        var options = new NormalizationOptions { Culture = germanCulture };

        // 1,5 + 2,5 -> 1.5 + 2.5
        string normalized = ExpressionNormalizer.Normalize("1,5 + 2,5", options);
        Assert.AreEqual("1.5 + 2.5", normalized);
        Assert.AreEqual(new MathValue(4.0m), Calculator.Evaluate(normalized));

        // Argument semicolons in function calls: max(1,5; 2,5) -> max(1.5, 2.5)
        string fnCall = ExpressionNormalizer.Normalize("max(1,5; 2,5)", options);
        Assert.AreEqual("max(1.5, 2.5)", fnCall);
        Assert.AreEqual(new MathValue(2.5m), Calculator.Evaluate(fnCall));

        // German thousands period: 1.000.000,50 -> 1000000.50
        string largeNumber = ExpressionNormalizer.Normalize("1.000.000,50", options);
        Assert.AreEqual("1000000.50", largeNumber);
        Assert.AreEqual(new MathValue(1000000.50m), Calculator.Evaluate(largeNumber));
    }

    [TestMethod]
    public void SwissApostropheAndIsoSpaceGroupingAreNormalized()
    {
        // 1'000'000.50 with straight apostrophe (U+0027)
        string swissStraight = ExpressionNormalizer.Normalize("1'000'000.50");
        Assert.AreEqual("1000000.50", swissStraight);
        Assert.AreEqual(new MathValue(1000000.50m), Calculator.Evaluate(swissStraight));

        // 1’000’000.50 with typographic/curly apostrophe (U+2019)
        string swissCurly = ExpressionNormalizer.Normalize("1\u2019000\u2019000.50");
        Assert.AreEqual("1000000.50", swissCurly);
        Assert.AreEqual(new MathValue(1000000.50m), Calculator.Evaluate(swissCurly));

        // SI / ISO thousands grouping: regular space (U+0020), NBSP (U+00A0), and narrow NBSP (U+202F)
        string siRegularSpace = ExpressionNormalizer.Normalize("1 000 000.50");
        Assert.AreEqual("1000000.50", siRegularSpace);
        Assert.AreEqual(new MathValue(1000000.50m), Calculator.Evaluate(siRegularSpace));

        string siNbsp = ExpressionNormalizer.Normalize("1\u00A0000\u00A0000.50");
        Assert.AreEqual("1000000.50", siNbsp);
        Assert.AreEqual(new MathValue(1000000.50m), Calculator.Evaluate(siNbsp));

        string siNarrowNbsp = ExpressionNormalizer.Normalize("1\u202F000\u202F000.50");
        Assert.AreEqual("1000000.50", siNarrowNbsp);
        Assert.AreEqual(new MathValue(1000000.50m), Calculator.Evaluate(siNarrowNbsp));
    }

    [TestMethod]
    public void TortureTestsComplexPasteCombinations()
    {
        // Complex user paste in German locale. The normalizer only cleans launcher '=', Unicode operators
        // and culture formatting; percentages, factorials and implicit products are the parser's job.
        var options = new NormalizationOptions { Culture = new CultureInfo("de-DE") };
        string input = "= 1.000,50 × (2 + 3)! + 15% of 200 − 50% =";

        Assert.IsTrue(ExpressionNormalizer.TryNormalize(input, out var normalized, options));
        Assert.AreEqual("1000.50 * (2 + 3)! + 15% of 200 - 50%", normalized);

        var result = Calculator.Evaluate(normalized);
        Assert.IsTrue(result.IsFinite);
    }

    [TestMethod]
    [DataRow("10 != 5", "10 != 5")]
    [DataRow("x != y", "x != y")]
    public void InequalityOperatorIsLeftIntact(string input, string expected)
    {
        string normalized = ExpressionNormalizer.Normalize(input);
        Assert.AreEqual(expected, normalized);
    }

    [TestMethod]
    [DataRow("10%3", "10%3")]
    [DataRow("10 % 3", "10 % 3")]
    public void PercentAndModuloTextIsLeftForTheParser(string input, string expected)
    {
        string normalized = ExpressionNormalizer.Normalize(input);
        Assert.AreEqual(expected, normalized);
    }

    [TestMethod]
    public void NullInputThrowsArgumentNullExceptionOrReturnsFalse()
    {
        Assert.Throws<ArgumentNullException>(() => ExpressionNormalizer.Normalize(null!));
        Assert.IsFalse(ExpressionNormalizer.TryNormalize(null!, out var normalized, out var errorMessage));
        Assert.IsNull(normalized);
        Assert.IsNotNull(errorMessage);
        Assert.IsTrue(errorMessage.Contains("null", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void EmptyAndWhitespaceInputsAreHandledGracefully()
    {
        Assert.AreEqual(string.Empty, ExpressionNormalizer.Normalize("   "));
        Assert.AreEqual(string.Empty, ExpressionNormalizer.Normalize("="));
        Assert.AreEqual(string.Empty, ExpressionNormalizer.Normalize("=   ="));
    }

    [TestMethod]
    public void UsCultureThousandsSeparatorsAreNormalized()
    {
        var usCulture = new CultureInfo("en-US");
        var options = new NormalizationOptions { Culture = usCulture };

        string normalized = ExpressionNormalizer.Normalize("1,000,000.50", options);
        Assert.AreEqual("1000000.50", normalized);
        Assert.AreEqual(new MathValue(1000000.50m), Calculator.Evaluate(normalized));
    }

    [TestMethod]
    public void NonThreeDigitSeparatorsArePreservedAsDelimiters()
    {
        // 1,2 is not thousands grouping (only 1 digit after comma) -> preserved in US culture
        var usOptions = new NormalizationOptions { Culture = new CultureInfo("en-US") };
        Assert.AreEqual("max(1, 2)", ExpressionNormalizer.Normalize("max(1, 2)", usOptions));
        Assert.AreEqual("1,23", ExpressionNormalizer.Normalize("1,23", usOptions));

        // 1.2 is decimal in US or non-grouping in DE -> preserved appropriately
        var deOptions = new NormalizationOptions { Culture = new CultureInfo("de-DE") };
        Assert.AreEqual("1.2", ExpressionNormalizer.Normalize("1.2", deOptions));
        Assert.AreEqual("max(1.5, 2.5)", ExpressionNormalizer.Normalize("max(1.5, 2.5)", deOptions));
    }

    [TestMethod]
    public void DisabledNormalizationOptionsBypassRespectivePasses()
    {
        var options = new NormalizationOptions
        {
            TrimLauncherEquals = false,
            ReplaceUnicodeOperators = false,
        };

        // None of these should transform when flags are false
        Assert.AreEqual("= 5 = ", ExpressionNormalizer.Normalize("= 5 = ", options));
        Assert.AreEqual("2 × 3", ExpressionNormalizer.Normalize("2 × 3", options));
    }

    [TestMethod]
    public void DigitGroupingEdgeCasesAreCovered()
    {
        // Space grouping with 2-digit groups after space (e.g. "1 23 45") preserves spaces
        Assert.AreEqual("1 23 45", ExpressionNormalizer.Normalize("1 23 45"));

        // Comma in DE culture inside parentheses without semicolon
        var deOptions = new NormalizationOptions { Culture = new CultureInfo("de-DE") };
        Assert.AreEqual("max(1, 2)", ExpressionNormalizer.Normalize("max(1, 2)", deOptions));
    }

    [TestMethod]
    [DataRow("max(1; 2)", "max(1, 2)")]
    [DataRow("round(2.5; 1)", "round(2.5, 1)")]
    public void SemicolonsInsideParensNormalizeToCommasInDefaultCulture(string input, string expected)
    {
        string normalized = ExpressionNormalizer.Normalize(input);
        Assert.AreEqual(expected, normalized);
    }
}
