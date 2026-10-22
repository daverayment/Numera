using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Numera.Tests;

[TestClass]
public class ImplicitMultiplicationFormattingTests
{
    [TestMethod]
    [DataRow("2(1/2)")]
    [DataRow("(pi/e)tau")]
    [DataRow("(pi%e)tau")]
    [DataRow("(pi*e)tau")]
    [DataRow("2(3)")]
    [DataRow("2(-pi)")]
    [DataRow("2pi")]
    [DataRow("(2+3)(4+5)")]
    public void AsciiFormattingRoundTripsToTheSameTree(string input)
    {
        var tree = Calculator.Parse(input);
        var formatted = Calculator.FormatExpression(tree, ExpressionFormatOptions.Ascii);
        Assert.AreEqual(tree, Calculator.Parse(formatted), $"'{input}' formatted as '{formatted}'.");
    }

    [TestMethod]
    [DataRow("2(1/2)", @"2 \cdot \frac{1}{2}")]
    [DataRow("(pi/e)tau", @"\frac{\pi}{e} \tau")]
    [DataRow("2(3)", @"2 \cdot 3")]
    [DataRow("2pi", @"2 \pi")]
    public void LaTeXImplicitMultiplicationIsUnambiguous(string input, string expected)
    {
        Assert.AreEqual(expected, Calculator.FormatExpression(input, ExpressionFormatOptions.LaTeX));
    }

    [TestMethod]
    public void LaTeXGroupsModuloLeftOperandOfImplicitMultiplication()
    {
        var formatted = Calculator.FormatExpression("(pi%e)tau", ExpressionFormatOptions.LaTeX);
        Assert.StartsWith("(", formatted);
    }
}

