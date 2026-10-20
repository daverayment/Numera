using System.Globalization;
using Numera;

namespace Numera.Tests;

[TestClass]
public sealed class PercentageTortureTests
{
    private static void AssertEquivalentValue(string expected, MathValue actual)
    {
        decimal expectedDecimal = decimal.Parse(expected, CultureInfo.InvariantCulture);
        decimal actualDecimal = actual.Type switch
        {
            MathValue.DataType.Integer => (decimal)actual.IntVal,
            MathValue.DataType.Decimal => actual.DecVal,
            _ => (decimal)actual.DblVal,
        };
        Assert.AreEqual(expectedDecimal, actualDecimal);
    }

    [TestMethod]
    [DataRow("100 + 10% + 20%", "132")]          // Chained markup: (100 + 10%) + 20% = 110 + 22 = 132
    [DataRow("100 + 10% + 5%", "115.5")]         // Chained markup: (100 + 10%) + 5% = 110 + 5.5 = 115.5
    [DataRow("100 - 10% - 10%", "81")]           // Chained discount: (100 - 10%) - 10% = 90 - 9 = 81
    [DataRow("100 + 10% - 10%", "99")]           // Markup then discount: (100 + 10%) - 10% = 110 - 11 = 99
    public void ChainedMarkupAndDiscountsEvaluateCorrectly(string input, string expected)
    {
        string normalized = ExpressionNormalizer.Normalize(input);
        var val = Calculator.Evaluate(normalized);
        AssertEquivalentValue(expected, val);
    }

    [TestMethod]
    [DataRow("10% + 20%", "0.3")]                // Pure standalone percentage addition
    [DataRow("50% - 20%", "0.3")]                // Pure standalone percentage subtraction
    [DataRow("10% * 20%", "0.02")]               // Standalone percentage multiplication
    [DataRow("50% / 10%", "5")]                  // Standalone percentage division
    public void StandalonePercentageArithmeticEvaluatesCorrectly(string input, string expected)
    {
        string normalized = ExpressionNormalizer.Normalize(input);
        var val = Calculator.Evaluate(normalized);
        AssertEquivalentValue(expected, val);
    }

    [TestMethod]
    [DataRow("10% of 20% of 1000", "20")]        // Nested prepositional percentage
    [DataRow("50 + 10% of 200", "70")]           // Prepositional mixed with addition
    [DataRow("10% of (100 + 200)", "30")]        // Prepositional of group
    public void PrepositionalPercentagesEvaluateCorrectly(string input, string expected)
    {
        string normalized = ExpressionNormalizer.Normalize(input);
        var val = Calculator.Evaluate(normalized);
        AssertEquivalentValue(expected, val);
    }

    [TestMethod]
    [DataRow("(100 + 20%) * 2", "240")]          // Parenthesized markup scaled
    [DataRow("2 * (100 + 20%)", "240")]          // Scaled parenthesized markup
    [DataRow("(100 + 20%) + (200 - 10%)", "300")]// Mixed grouped markup and discount
    public void GroupedPercentageExpressionsEvaluateCorrectly(string input, string expected)
    {
        string normalized = ExpressionNormalizer.Normalize(input);
        var val = Calculator.Evaluate(normalized);
        AssertEquivalentValue(expected, val);
    }

    [TestMethod]
    [DataRow("100 % 7", "2")]                    // Explicit modulo with spaces
    [DataRow("(100 + 20%) % 7", "1")]            // Markup result modulo an integer (120 % 7 = 1)
    [DataRow("-100 + 10%", "-90")]               // Negative base markup (-100 + abs(-100) * 0.1 = -90)
    public void ModuloAndUnaryEdgeCasesEvaluateCorrectly(string input, string expected)
    {
        string normalized = ExpressionNormalizer.Normalize(input);
        var val = Calculator.Evaluate(normalized);
        AssertEquivalentValue(expected, val);
    }
}
