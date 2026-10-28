using System.Globalization;

namespace Numera.Tests;

[TestClass]
public sealed class ExpressionNormalizerTests
{
    [TestMethod]
    [DataRow("pi : 2", "pi : 2")]
    [DataRow("pi:2", "pi:2")]
    [DataRow("(pi):2", "(pi)/2")]
    [DataRow("tau:4", "tau:4")]
    [DataRow("sin(pi) : 2", "sin(pi) / 2")]
    [DataRow("log10(100):2", "log10(100)/2")]
    [DataRow("(1+2):3", "(1+2)/3")]
    [DataRow("[1, 2]:3", "[1, 2]:3")]
    [DataRow("6 : 2 / 16:9", "6 / 2 / 16/9")]
    [DataRow("100:2", "100/2")]
    [DataRow("1.5:2", "1.5/2")]
    [DataRow("5:-2", "5/-2")]
    [DataRow("log10:2", "log10:2")]
    [DataRow("item1:200", "item1:200")]
    [DataRow("win32:x64", "win32:x64")]
    [DataRow("x2:3", "x2:3")]
    [DataRow("not:200", "not:200")]
    [DataRow("sin:2", "sin:2")]
    [DataRow("status:200", "status:200")]
    [DataRow("x : 2", "x : 2")]
    [DataRow("flag:true", "flag:true")]
    [DataRow("true : 2", "true : 2")]
    [DataRow("4x:200", "4x:200")]
    [DataRow("4x : 200", "4x : 200")]
    [DataRow("(4x) : 200", "(4x) / 200")]
    [DataRow("0xFF:2", "0xFF/2")]
    [DataRow("0b101:2", "0b101/2")]
    [DataRow("0o17:2", "0o17/2")]
    [DataRow("1e5:2", "1e5/2")]
    [DataRow("1.5E-3 : 2", "1.5E-3 / 2")]
    [DataRow("2x:3", "2x:3")]
    [DataRow("1e:2", "1e:2")]
    [DataRow("4not:200", "4not:200")]
    [DataRow("4sin:2", "4sin:2")]
    [DataRow("6:", "6:")]
    [DataRow("6 : x", "6 : x")]
    public void DivisionColonRules(string input, string expected)
    {
        Assert.AreEqual(expected, ExpressionNormalizer.Normalize(input));
    }

    [TestMethod]
    public void DivisionColonCanBeDisabled()
    {
        var options = new NormalizationOptions { ReplaceDivisionColon = false };
        Assert.AreEqual("6 : 2", ExpressionNormalizer.Normalize("6 : 2", options: options));
    }

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

        // 1,5 + 2,5 -> 1.5 + 2.5
        string normalized = ExpressionNormalizer.Normalize("1,5 + 2,5", germanCulture);
        Assert.AreEqual("1.5 + 2.5", normalized);
        Assert.AreEqual(new MathValue(4.0m), Calculator.Evaluate(normalized));

        // Argument semicolons in function calls: max(1,5; 2,5) -> max(1.5, 2.5)
        string fnCall = ExpressionNormalizer.Normalize("max(1,5; 2,5)", germanCulture);
        Assert.AreEqual("max(1.5, 2.5)", fnCall);
        Assert.AreEqual(new MathValue(2.5m), Calculator.Evaluate(fnCall));

        // German thousands period: 1.000.000,50 -> 1000000.50
        string largeNumber = ExpressionNormalizer.Normalize("1.000.000,50", germanCulture);
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
        var german = new CultureInfo("de-DE");
        string input = "= 1.000,50 × (2 + 3)! + 15% of 200 − 50% =";

        Assert.IsTrue(ExpressionNormalizer.TryNormalize(input, german, out var normalized, out _));
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
        Assert.IsFalse(ExpressionNormalizer.TryNormalize(null!, null, out var normalized, out var diagnostic));
        Assert.IsNull(normalized);
        Assert.IsNotNull(diagnostic);
        Assert.IsTrue(diagnostic.Message.Contains("null", StringComparison.OrdinalIgnoreCase));
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

        string normalized = ExpressionNormalizer.Normalize("1,000,000.50", usCulture);
        Assert.AreEqual("1000000.50", normalized);
        Assert.AreEqual(new MathValue(1000000.50m), Calculator.Evaluate(normalized));
    }

    [TestMethod]
    public void NonThreeDigitSeparatorsArePreservedAsDelimiters()
    {
        // 1,2 is not thousands grouping (only 1 digit after comma) -> preserved in US culture
        var us = new CultureInfo("en-US");
        Assert.AreEqual("max(1, 2)", ExpressionNormalizer.Normalize("max(1, 2)", us));
        Assert.AreEqual("1,23", ExpressionNormalizer.Normalize("1,23", us));

        // 1.2 is decimal in US or non-grouping in DE -> preserved appropriately
        var de = new CultureInfo("de-DE");
        Assert.AreEqual("1.2", ExpressionNormalizer.Normalize("1.2", de));
        Assert.AreEqual("max(1.5, 2.5)", ExpressionNormalizer.Normalize("max(1.5, 2.5)", de));
    }

    [TestMethod]
    [DataRow("2:3:4", "2/3/4")]
    [DataRow("(2):(3)", "(2)/(3)")]
    [DataRow("6\t:\t2", "6\t/\t2")]
    [DataRow("6 :\n2", "6 /\n2")]
    [DataRow(": 2", ": 2")]
    [DataRow("6 :", "6 :")]
    [DataRow("1.5e3:2", "1.5e3/2")]
    [DataRow("6:+2", "6/+2")]
    [DataRow("6:(1+1)", "6/(1+1)")]
    [DataRow("6:[1]", "6:[1]")]
    [DataRow("[1:2]", "[1:2]")]
    [DataRow("[1]:2", "[1]:2")]
    [DataRow("(1):[2:3]", "(1):[2:3]")]
    [DataRow("1e-5:2", "1e-5/2")]
    [DataRow("1E+5 : 2", "1E+5 / 2")]
    [DataRow("1.5e-3:2", "1.5e-3/2")]
    [DataRow("6:1e-5", "6/1e-5")]
    [DataRow("6:1E+5", "6/1E+5")]
    [DataRow("6:1e-", "6:1e-")]
    [DataRow("6 : - 2", "6 : - 2")]
    [DataRow("6:0xFF", "6/0xFF")]
    [DataRow("6:1e5", "6/1e5")]
    [DataRow("6 : -0b101", "6 / -0b101")]
    [DataRow("6:2x", "6:2x")]
    [DataRow("6:2pi", "6:2pi")]
    [DataRow("6:1e", "6:1e")]
    [DataRow("6:-x", "6:-x")]
    [DataRow("x:2 + 6:3", "x:2 + 6/3")]
    public void DivisionColonEdgeCases(string input, string expected)
    {
        Assert.AreEqual(expected, ExpressionNormalizer.Normalize(input));
    }

    [TestMethod]
    public void DivisionColonIsIndependentOfUnicodeOperatorOption()
    {
        var noUnicode = new NormalizationOptions { ReplaceUnicodeOperators = false };
        Assert.AreEqual("6 / 2 × 3", ExpressionNormalizer.Normalize("6 : 2 × 3", options: noUnicode));

        var noColon = new NormalizationOptions { ReplaceDivisionColon = false };
        Assert.AreEqual("6 : 2 * 3", ExpressionNormalizer.Normalize("6 : 2 × 3", options: noColon));
    }

    [TestMethod]
    public void DivisionColonCombinesWithCultureNormalization()
    {
        string normalized = ExpressionNormalizer.Normalize("1.000,5 : 2", new CultureInfo("de-DE"));
        Assert.AreEqual("1000.5 / 2", normalized);
        Assert.AreEqual(new MathValue(500.25m), Calculator.Evaluate(normalized));
    }

    [TestMethod]
    [DataRow("6 : 2", 3)]
    [DataRow("0xFF:3", 85)]
    [DataRow("(2 + 4):3", 2)]
    public void DivisionColonOutputEvaluates(string input, int expected)
    {
        // Compare numerically: the result's representation (integer or decimal) is not what this test is about.
        double actual = Calculator.Evaluate(ExpressionNormalizer.Normalize(input)).AsDouble();
        Assert.AreEqual(expected, actual);
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
        Assert.AreEqual("= 5 = ", ExpressionNormalizer.Normalize("= 5 = ", options: options));
        Assert.AreEqual("2 × 3", ExpressionNormalizer.Normalize("2 × 3", options: options));
    }

    [TestMethod]
    public void DigitGroupingEdgeCasesAreCovered()
    {
        // Space grouping with 2-digit groups after space (e.g. "1 23 45") preserves spaces
        Assert.AreEqual("1 23 45", ExpressionNormalizer.Normalize("1 23 45"));

        // Comma in DE culture inside parentheses without semicolon
        Assert.AreEqual("max(1, 2)", ExpressionNormalizer.Normalize("max(1, 2)", new CultureInfo("de-DE")));
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
