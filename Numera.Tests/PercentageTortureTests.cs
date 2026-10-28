using System.Globalization;

namespace Numera.Tests;

[TestClass]
public sealed class PercentageTortureTests
{
    private static void AssertEvaluatesTo(string expected, string input)
    {
        decimal expectedDecimal = decimal.Parse(expected, CultureInfo.InvariantCulture);
        MathValue actual = Calculator.Evaluate(input);
        decimal actualDecimal = actual.Type switch
        {
            MathValue.DataType.Integer => (decimal)actual.IntVal,
            MathValue.DataType.Decimal => actual.DecVal,
            _ => (decimal)actual.DblVal,
        };
        Assert.AreEqual(expectedDecimal, actualDecimal);
    }

    [TestMethod]
    [DataRow("100 + 10% + 20%", "132")]
    [DataRow("100 + 10% + 5%", "115.5")]
    [DataRow("100 - 10% - 10%", "81")]
    [DataRow("100 + 10% - 10%", "99")]
    public void ChainedMarkupAndDiscountsEvaluateCorrectly(string input, string expected) =>
        AssertEvaluatesTo(expected, input);

    [TestMethod]
    [DataRow("10% + 20%", "0.3")]
    [DataRow("50% - 20%", "0.3")]
    [DataRow("10% * 20%", "0.02")]
    [DataRow("50% / 10%", "5")]
    [DataRow("-10% + 20%", "0.1")]
    [DataRow("10% * 2 + 5%", "0.21")]
    public void StandalonePercentageArithmeticEvaluatesCorrectly(string input, string expected) =>
        AssertEvaluatesTo(expected, input);

    [TestMethod]
    [DataRow("10% of 20% of 1000", "20")]
    [DataRow("50 + 10% of 200", "70")]
    [DataRow("10% of (100 + 200)", "30")]
    public void PrepositionalPercentagesEvaluateCorrectly(string input, string expected) =>
        AssertEvaluatesTo(expected, input);

    [TestMethod]
    [DataRow("(100 + 20%) * 2", "240")]
    [DataRow("2 * (100 + 20%)", "240")]
    [DataRow("(100 + 20%) + (200 - 10%)", "300")]
    public void GroupedPercentageExpressionsEvaluateCorrectly(string input, string expected) =>
        AssertEvaluatesTo(expected, input);

    [TestMethod]
    [DataRow("100 % 7", "2")]
    [DataRow("(100 + 20%) % 7", "1")]
    public void ModuloEvaluatesCorrectly(string input, string expected) =>
        AssertEvaluatesTo(expected, input);

    [TestMethod]
    [DataRow("-100 + 10%", "-110")]
    [DataRow("-100 - 10%", "-90")]
    [DataRow("-50 * 2 + 10%", "-110")]
    [DataRow("10 + 50 * 2 + 10%", "121")]
    [DataRow("100 + -10%", "90")]
    [DataRow("100 - -10%", "110")]
    [DataRow("-100 + -10%", "-90")]
    [DataRow("200 * 15% + 10%", "33")]
    public void SignedAndStructuralMarkupScalesTheAccumulatedBase(string input, string expected) =>
        AssertEvaluatesTo(expected, input);

    [TestMethod]
    [DataRow("200 of 10%")]
    [DataRow("(10% + 5) of 200")]
    public void OfRequiresPercentageOnLeft(string input) =>
        Assert.ThrowsExactly<ExpressionException>(() => Calculator.Evaluate(input));

    [TestMethod]
    [DataRow("200 + 15%", "230")]
    [DataRow("200 - 15%", "170")]
    [DataRow("15% of 200", "30")]
    [DataRow("200 * 15%", "30")]
    [DataRow("50%", "0.5")]
    [DataRow("100 + 15 %", "115")]
    [DataRow("15 % + 20", "20.15")]
    [DataRow("(15 %) * 2", "0.3")]
    [DataRow("50 %", "0.5")]
    public void PercentWithOrWithoutWhitespaceIsPostfixWhenNoOperandFollows(string input, string expected) =>
        AssertEvaluatesTo(expected, input);

    [TestMethod]
    [DataRow("10 % 3", "1")]
    [DataRow("10%3", "1")]
    [DataRow("20 % (2 + 3)", "0")]
    public void ModuloIsChosenWhenAnOperandFollowsPercent(string input, string expected) =>
        AssertEvaluatesTo(expected, input);

    [TestMethod]
    [DataRow("50 * 2 + 10%", "110")]
    [DataRow("100 / 2 - 20%", "40")]
    [DataRow("(10 + 50) * 2 + 10%", "132")]
    [DataRow("min(10, 50 * 2 + 10%)", "10")]
    public void MarkupAppliesToTheWholeLeftExpression(string input, string expected) =>
        AssertEvaluatesTo(expected, input);

    [TestMethod]
    [DataRow("3! + 4!", "30")]
    [DataRow("(2 + 3)!", "120")]
    [DataRow("3sqrt(16)", "12")]
    [DataRow("0o77", "63")]
    [DataRow("0O12", "10")]
    [DataRow("0b101", "5")]
    [DataRow("1E+6", "1000000")]
    [DataRow("2.5e-3", "0.0025")]
    public void FactorialsImplicitProductsAndLiteralsParseWithoutNormalization(string input, string expected) =>
        AssertEvaluatesTo(expected, input);

    [TestMethod]
    public void ComplexPasteEvaluatesAfterCultureNormalization()
    {
        var german = new CultureInfo("de-DE");
        Assert.IsTrue(ExpressionNormalizer.TryNormalize("= 1.000,50 × (2 + 3)! + 15% of 200 − 50% =", german, out var normalized, out _));

        // (1000.50 * 120 + 30) is halved by the trailing "- 50%": 120090 * 0.5.
        AssertEvaluatesTo("60045", normalized);
    }
}
