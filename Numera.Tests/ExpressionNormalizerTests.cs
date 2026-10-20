using System.Globalization;
using System.Numerics;

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
    [DataRow("200 + 15%", "(200 + (abs(200) * (15 / 100)))")]
    [DataRow("200 - 15%", "(200 - (abs(200) * (15 / 100)))")]
    [DataRow("15% of 200", "(15 / 100) * 200")]
    [DataRow("200 * 15%", "200 * (15 / 100)")]
    [DataRow("50%", "(50 / 100)")]
    public void PercentageIdiomsAreNormalizedAccurately(string input, string expected)
    {
        string normalized = ExpressionNormalizer.Normalize(input);
        Assert.AreEqual(expected, normalized);
    }

    [TestMethod]
    public void PercentageMarkupEvaluatesCorrectlyThroughCalculator()
    {
        var resultPlus = Calculator.Evaluate(ExpressionNormalizer.Normalize("200 + 15%"));
        Assert.AreEqual(new MathValue(230m), resultPlus);

        var resultMinus = Calculator.Evaluate(ExpressionNormalizer.Normalize("200 - 15%"));
        Assert.AreEqual(new MathValue(170m), resultMinus);

        var resultOf = Calculator.Evaluate(ExpressionNormalizer.Normalize("15% of 200"));
        Assert.AreEqual(new MathValue(30m), resultOf);
    }

    [TestMethod]
    public void BinaryModuloIsNotConfusedWithPercentage()
    {
        // "10 % 3" with spaces is modulo
        string input = "10 % 3";
        string normalized = ExpressionNormalizer.Normalize(input);
        Assert.AreEqual("10 % 3", normalized);

        var result = Calculator.Evaluate(normalized);
        Assert.AreEqual(new MathValue(BigInteger.One), result);
    }

    [TestMethod]
    [DataRow("5!", "factorial(5)")]
    [DataRow("(2 + 3)!", "factorial((2 + 3))")]
    public void PostfixFactorialsAreConverted(string input, string expected)
    {
        string normalized = ExpressionNormalizer.Normalize(input);
        Assert.AreEqual(expected, normalized);

        var result = Calculator.Evaluate(normalized);
        Assert.AreEqual(new MathValue(new BigInteger(120)), result);
    }

    [TestMethod]
    [DataRow("2pi", "2 * pi")]
    [DataRow("2(3 + 4)", "2 * (3 + 4)")]
    [DataRow("(1 + 2)(3 + 4)", "(1 + 2) * (3 + 4)")]
    [DataRow("3sqrt(16)", "3 * sqrt(16)")]
    public void ImplicitMultiplicationIsExpanded(string input, string expected)
    {
        string normalized = ExpressionNormalizer.Normalize(input);
        Assert.AreEqual(expected, normalized);

        var eval = Calculator.Evaluate(normalized);
        Assert.IsTrue(eval.IsFinite);
    }

    [TestMethod]
    public void ScientificNotationAndHexBinaryLiteralsAreNotBrokenByImplicitMultiplication()
    {
        // Scientific notation '1e5', '2.5e-3' must remain intact
        Assert.AreEqual("1e5", ExpressionNormalizer.Normalize("1e5"));
        Assert.AreEqual("2.5e-3", ExpressionNormalizer.Normalize("2.5e-3"));
        Assert.AreEqual("1E+6", ExpressionNormalizer.Normalize("1E+6"));

        // Hex and binary literals must not be split
        Assert.AreEqual("0x100", ExpressionNormalizer.Normalize("0x100"));
        Assert.AreEqual("0b101", ExpressionNormalizer.Normalize("0b101"));
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
        // Complex user paste:
        // "= 1.000,50 × (2 + 3)! + 15% of 200 − 50% =" in German locale
        var options = new NormalizationOptions { Culture = new CultureInfo("de-DE") };
        string input = "= 1.000,50 × (2 + 3)! + 15% of 200 − 50% =";

        Assert.IsTrue(ExpressionNormalizer.TryNormalize(input, out var normalized, options));
        Assert.IsNotNull(normalized);

        // Verify that the resulting normalized string evaluates without errors
        var result = Calculator.Evaluate(normalized);
        Assert.IsTrue(result.IsFinite);
    }

    [TestMethod]
    [DataRow("10 != 5", "10 != 5")]
    [DataRow("x != y", "x != y")]
    public void InequalityOperatorIsNotMistakenForPostfixFactorial(string input, string expected)
    {
        string normalized = ExpressionNormalizer.Normalize(input);
        Assert.AreEqual(expected, normalized);
    }

    [TestMethod]
    [DataRow("10%3", "10%3")] // binary modulo without spaces
    [DataRow("10 % 3", "10 % 3")] // binary modulo with spaces
    public void BinaryModuloWithoutSpacesRetained(string input, string expected)
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
            ExpandPercentages = false,
            ConvertPostfixFactorials = false,
            ExpandImplicitMultiplication = false
        };

        // None of these should transform when flags are false
        Assert.AreEqual("= 5 = ", ExpressionNormalizer.Normalize("= 5 = ", options));
        Assert.AreEqual("2 × 3", ExpressionNormalizer.Normalize("2 × 3", options));
        Assert.AreEqual("5!", ExpressionNormalizer.Normalize("5!", options));
        Assert.AreEqual("2(3)", ExpressionNormalizer.Normalize("2(3)", options));
        Assert.AreEqual("20%", ExpressionNormalizer.Normalize("20%", options));
    }

    [TestMethod]
    public void ImplicitMultiplicationHandlesClosingParenthesisBeforeIdentifierOrNumber()
    {
        // Closing parenthesis followed by digit or identifier
        Assert.AreEqual("(2 + 3) * 4", ExpressionNormalizer.Normalize("(2 + 3)4"));
        Assert.AreEqual("(2 + 3) * pi", ExpressionNormalizer.Normalize("(2 + 3)pi"));

        // Non-prefix non-scientific digit followed by letter
        Assert.AreEqual("3 * a", ExpressionNormalizer.Normalize("3a"));
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
    public void PostfixFactorialWithPrecedingOperatorIgnored()
    {
        // '!' preceded by an operator like '+!' shouldn't convert to factorial
        Assert.AreEqual("+!", ExpressionNormalizer.Normalize("+!"));
    }

    [TestMethod]
    public void MultiplePercentagesDoNotDesync()
    {
        // Standalone multiple percentages: "10% + 20%" has '+' preceding 20%, but because 10% is itself a percentage,
        // it is treated as standalone percentage rate addition rather than markup, yielding "(10 / 100) + (20 / 100)".
        string normalized = ExpressionNormalizer.Normalize("10% + 20%");
        Assert.AreEqual("(10 / 100) + (20 / 100)", normalized);
        Assert.AreEqual(new MathValue(0.30m), Calculator.Evaluate(normalized));

        // Multiplicative multiple percentages
        string mul = ExpressionNormalizer.Normalize("50% * 20%");
        Assert.AreEqual("(50 / 100) * (20 / 100)", mul);
        Assert.AreEqual(new MathValue(0.1m), Calculator.Evaluate(mul));

        // Additive markup followed by percentage compounds cumulatively: (100 + 10%) + 20% = 110 + 22 = 132
        string markup = ExpressionNormalizer.Normalize("100 + 10% + 20%");
        Assert.AreEqual("((100 + (abs(100) * (10 / 100))) + (abs((100 + (abs(100) * (10 / 100)))) * (20 / 100)))", markup);
        Assert.AreEqual(new MathValue(132m), Calculator.Evaluate(markup));
    }

    [TestMethod]
    public void MultiplePostfixFactorialsDoNotDesync()
    {
        string normalized = ExpressionNormalizer.Normalize("3! + 4!");
        Assert.AreEqual("factorial(3) + factorial(4)", normalized);
        Assert.AreEqual(new MathValue(new BigInteger(30)), Calculator.Evaluate(normalized));
    }

    [TestMethod]
    public void OctalLiteralsAreNotCorruptedByImplicitMultiplication()
    {
        string octLower = ExpressionNormalizer.Normalize("0o77");
        Assert.AreEqual("0o77", octLower);
        Assert.AreEqual(new MathValue(new BigInteger(63)), Calculator.Evaluate(octLower));

        string octUpper = ExpressionNormalizer.Normalize("0O12");
        Assert.AreEqual("0O12", octUpper);
        Assert.AreEqual(new MathValue(new BigInteger(10)), Calculator.Evaluate(octUpper));
    }

    [TestMethod]
    [DataRow("100 + 15 %", "(100 + (abs(100) * (15 / 100)))")]
    [DataRow("15 % + 20", "(15 / 100) + 20")]
    [DataRow("(15 %) * 2", "((15 / 100)) * 2")]
    [DataRow("50 %", "(50 / 100)")]
    public void PercentWithPrecedingWhitespaceNormalizesAsPostfixWhenNotModulo(string input, string expected)
    {
        string normalized = ExpressionNormalizer.Normalize(input);
        Assert.AreEqual(expected, normalized);
    }

    [TestMethod]
    [DataRow("10 % 3", "10 % 3")]
    [DataRow("10%3", "10%3")]
    [DataRow("20 % (2 + 3)", "20 % (2 + 3)")]
    public void BinaryModuloIsNotTreatedAsPostfixPercent(string input, string expected)
    {
        string normalized = ExpressionNormalizer.Normalize(input);
        Assert.AreEqual(expected, normalized);
    }

    [TestMethod]
    [DataRow("50 * 2 + 10%", "(50 * 2 + (abs(50 * 2) * (10 / 100)))", "110.0")]
    [DataRow("100 / 2 - 20%", "(100 / 2 - (abs(100 / 2) * (20 / 100)))", "40.0")]
    [DataRow("10 + 50 * 2 + 10%", "10 + (50 * 2 + (abs(50 * 2) * (10 / 100)))", "120.0")]
    [DataRow("(10 + 50) * 2 + 10%", "((10 + 50) * 2 + (abs((10 + 50) * 2) * (10 / 100)))", "132.0")]
    [DataRow("-50 * 2 + 10%", "(-50 * 2 + (abs(-50 * 2) * (10 / 100)))", "-90.0")]
    [DataRow("min(10, 50 * 2 + 10%)", "min(10, (50 * 2 + (abs(50 * 2) * (10 / 100))))", "10")]
    public void CompoundMultiplicativeTermsAreCapturedAccuratelyForMarkup(string input, string expected, string expectedValue)
    {
        string normalized = ExpressionNormalizer.Normalize(input);
        Assert.AreEqual(expected, normalized);

        var val = Calculator.Evaluate(normalized);
        Assert.AreEqual(expectedValue, val.ToString(CultureInfo.InvariantCulture));
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
