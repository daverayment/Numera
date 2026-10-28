namespace Numera.Tests;

/// <summary>
/// Unparsed trailing text must be reported as the syntax error it is. Validation only
/// makes sense for a tree that consumed the whole input, so it must never get the chance
/// to mask the real problem.
/// </summary>
[TestClass]
public sealed class ParserTrailingInputTests
{
    [TestMethod]
    [DataRow("1 + 2 )", ')')]
    [DataRow("1 + 2 $", '$')]
    [DataRow("1 + 2 ,", ',')]
    public void TrailingCharacter_IsReportedAsUnexpectedWithItsPosition(string input, char trailing)
    {
        Assert.IsFalse(Calculator.TryParse(input, out _, out var diagnostic));

        Assert.AreEqual(EvaluationError.InvalidSyntax, diagnostic!.Code);
        Assert.AreEqual(input.IndexOf(trailing), diagnostic.Position);
        Assert.AreEqual(1, diagnostic.Length);
    }

    [TestMethod]
    [DataRow("1 ; 2", ';')]
    [DataRow("1 [", '[')]
    [DataRow("1 ]", ']')]
    public void ReservedTrailingCharacter_ExplainsItIsReserved(string input, char reserved)
    {
        Assert.IsFalse(Calculator.TryParse(input, out _, out var diagnostic));

        Assert.AreEqual(EvaluationError.InvalidSyntax, diagnostic!.Code);
        Assert.AreEqual(input.IndexOf(reserved), diagnostic.Position);
        Assert.Contains("reserved", diagnostic.Message);
    }
}
