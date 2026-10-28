using Numera.Ast;

namespace Numera.Tests;

/// <summary>
/// Spans and diagnostics are produced from normalized text but must point at what the
/// caller typed, including where normalization deleted or contracted characters.
/// </summary>
[TestClass]
public sealed class SourceMapTests
{
    [TestMethod]
    public void Identity_MapsEverythingToItself()
    {
        var map = SourceMap.Identity(5);

        Assert.IsTrue(map.IsIdentity);
        Assert.AreEqual(3, map.MapPosition(3));
        Assert.AreEqual(new TextSpan(1, 2), map.MapSpan(new TextSpan(1, 2)));
    }

    [TestMethod]
    public void Slice_OffsetsPositionsPastTrimmedPrefix()
    {
        // Original length 7: 2 char prefix, slice of 3 chars [2..5), suffix of 2 chars
        // [5..7).
        var map = SourceMap.Slice(2, 3, 7);

        Assert.AreEqual(2, map.MapPosition(0)); // Start of slice
        Assert.AreEqual(
            new TextSpan(3, 2), map.MapSpan(new TextSpan(1, 2)));

        // End of the slice [2..5) is index 5 of original text.
        Assert.AreEqual(5, map.MapPosition(3));

        // Beyond the end of the slice maps to the end of the original text.
        Assert.AreEqual(7, map.MapPosition(4));
    }

    [TestMethod]
    public void MapSpan_ContractedCharacterCoversAllOriginals()
    {
        // "2**3" normalizes to "2^3"; the '^' came from two characters.
        var map = SourceMap.FromRanges([0, 1, 3], [1, 3, 4], 4);

        Assert.AreEqual(new TextSpan(1, 2), map.MapSpan(new TextSpan(1, 1)));
        Assert.AreEqual(new TextSpan(0, 4), map.MapSpan(new TextSpan(0, 3)));
    }

    [TestMethod]
    public void MapSpan_IncludesDeletedCharactersInsideTheSpan()
    {
        // "1,000" normalizes to "1000"; the comma sits in a gap.
        var map = SourceMap.FromRanges([0, 2, 3, 4], [1, 3, 4, 5], 5);

        Assert.AreEqual(new TextSpan(0, 5), map.MapSpan(new TextSpan(0, 4)));
    }

    [TestMethod]
    public void Compose_ChainsMapsBackToTheOriginal()
    {
        var inner = SourceMap.Slice(2, 3, 6);
        var outer = SourceMap.FromRanges([0, 1, 2], [1, 2, 3], 3).Compose(inner);

        Assert.AreEqual(new TextSpan(3, 1), outer.MapSpan(new TextSpan(1, 1)));
    }

    [TestMethod]
    public void NodeSpans_PointAtOriginalTextAfterGroupingSeparators()
    {
        Assert.IsTrue(Calculator.TryParse("1,000 + 2", out var node, out _));

        var binary = (BinaryNode)node;
        Assert.AreEqual(new TextSpan(0, 5), binary.Left.Span);
        Assert.AreEqual(new TextSpan(8, 1), binary.Right.Span);
    }

    [TestMethod]
    public void NodeSpans_CoverBothCharactersOfStarStarPower()
    {
        Assert.IsTrue(Calculator.TryParse("2 ** 3", out var node, out _));

        Assert.AreEqual(new TextSpan(0, 6), node!.Span);
    }

    [TestMethod]
    public void Diagnostics_PointAtOriginalTextAfterLauncherEquals()
    {
        Assert.IsFalse(Calculator.TryParse("=  1 + 2 $", out _, out var diagnostic));

        Assert.AreEqual(9, diagnostic.Position);
    }

    [TestMethod]
    public void Diagnostics_PointAtOriginalTextAfterDeletedSeparators()
    {
        Assert.IsFalse(Calculator.TryParse("1,000 + foo", out _, out var diagnostic));

        Assert.AreEqual(8, diagnostic.Position);
        Assert.AreEqual(3, diagnostic.Length);
    }

    [TestMethod]
    public void Validation_NegativePositionsAndLengthsThrow()
    {
        var map = SourceMap.Slice(2, 5, 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => map.MapPosition(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.MapSpan(new TextSpan(-1, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.MapSpan(new TextSpan(0, -1)));
    }

    [TestMethod]
    public void FromRanges_MismatchedCountsThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => SourceMap.FromRanges([0, 1], [1], 2));
    }

    [TestMethod]
    public void FromRanges_IdentityDetectedReturnsIdentityMap()
    {
        var map = SourceMap.FromRanges([0, 1, 2], [1, 2, 3], 3);

        Assert.IsTrue(map.IsIdentity);
    }

    [TestMethod]
    public void MapPosition_AtOrBeyondEndOfNormalized_MapsToOriginalLength()
    {
        // 5 normalized chars from an original of 10 chars. Slice is [2..7).
        var map = SourceMap.Slice(2, 5, 10);

        // Exactly at end of normalized slice -> index 7 (just after 5th char).
        Assert.AreEqual(7, map.MapPosition(5));

        // Beyond the slice -> the original length of 10.
        Assert.AreEqual(10, map.MapPosition(6));
        Assert.AreEqual(10, map.MapPosition(20));
    }

    [TestMethod]
    public void MapSpan_ZeroLengthSpanAtInteriorOrEof_ProducesZeroLengthOriginalSpan()
    {
        // Slice: original is "= 12345 =", trimmed to "12345" (offset 2, length 5,
        // slice is [2..7).
        var map = SourceMap.Slice(2, 5, 9);

        // Point insertion at normalized index 2 -> original index 4.
        var midPoint = map.MapSpan(new TextSpan(2, 0));
        Assert.AreEqual(new TextSpan(4, 0), midPoint);

        // Point insertion at end of "12345" -> original index 7 (after '5' and before
        // trailing '=').
        var eofPoint = map.MapSpan(new TextSpan(5, 0));
        Assert.AreEqual(new TextSpan(7, 0), eofPoint);
    }

    [TestMethod]
    public void Compose_IdentityCasesOptimizeCleanly()
    {
        var slice = SourceMap.Slice(2, 3, 5);
        var identity = SourceMap.Identity(3);

        // Composing with an identity on either side returns the non-identity map
        // unchanged.
        Assert.AreSame(slice, slice.Compose(identity));
        Assert.AreSame(slice, identity.Compose(slice));
    }

    [TestMethod]
    public void NodeSpans_OriginalStringSlicingMatchesExactOriginalSubstrings()
    {
        // Original has leading and trailing '=', Swiss apostrophe grouping, '×',
        // and '**'.
        string original = "= 1'000 × 2 ** 3 =";
        var engine = new CalculatorEngine(CalculatorOptions.Default);

        Assert.IsTrue(engine.TryParse(original, out var node, out _));

        var root = (BinaryNode)node!;

        // 1. Root span covers the whole arithmetic expression (excluding outer '=').
        Assert.AreEqual("1'000 × 2 ** 3", original.Substring(root.Span.Start, root.Span.Length));

        // 2. Left operand covers "1'000" including the apostrophe.
        Assert.AreEqual("1'000", original.Substring(root.Left.Span.Start, root.Left.Span.Length));

        // 3. Right operand (power node) covers "2 ** 3".
        Assert.AreEqual("2 ** 3", original.Substring(root.Right.Span.Start, root.Right.Span.Length));
    }

    [TestMethod]
    public void RuntimeDiagnostic_DivisionByZero_PointsToOriginalSubexpression()
    {
        // "= 1.000,50 + (5 / 0) ="
        //                ^
        //                | '5 / 0' is at original index 14, length 5
        string original = "= 1.000,50 + (5 / 0) =";
        var engine = new CalculatorEngine(CalculatorOptions.ForCulture(new System.Globalization.CultureInfo("de-DE")));

        Assert.IsFalse(engine.TryEvaluate(original, out _, out var diagnostic));
        Assert.AreEqual(EvaluationError.DivisionByZero, diagnostic!.Code);

        Assert.AreEqual(14, diagnostic.Position);
        Assert.AreEqual(5, diagnostic.Length);
        Assert.AreEqual("5 / 0", original.Substring(diagnostic.Position!.Value, diagnostic.Length));
    }

    [TestMethod]
    public void RuntimeDiagnostic_MathDomainError_PointsToOriginalFunctionCall()
    {
        // "= 1000 + sqrt(-1) ="
        //           ^
        //           | 'sqrt(-1)' is at original index 9, length 8
        string original = "= 1000 + sqrt(-1) =";

        Assert.IsFalse(Calculator.TryEvaluate(original, out _, out var diagnostic));
        Assert.AreEqual(EvaluationError.UndefinedMathResult, diagnostic!.Code);

        Assert.AreEqual(9, diagnostic.Position);
        Assert.AreEqual(8, diagnostic.Length);
        Assert.AreEqual("sqrt(-1)", original.Substring(diagnostic.Position!.Value, diagnostic.Length));
    }

    [TestMethod]
    public void MultipleContractions_SpansRemainAccurateAcrossMultipleStarStarOperators()
    {
        string original = "2 ** 3 ** 4";
        //                 01234567890 (Length 11)

        Assert.IsTrue(Calculator.TryParse(original, out var node, out _));

        // Right-associative power: 2 ** (3 ** 4)
        var root = (BinaryNode)node;
        var right = (BinaryNode)root.Right;

        Assert.AreEqual("2 ** 3 ** 4", original.Substring(root.Span.Start, root.Span.Length));
        Assert.AreEqual("3 ** 4", original.Substring(right.Span.Start, right.Span.Length));
    }

    [TestMethod]
    public void Diagnostics_UnexpectedEofAfterTrailingTrim_PointsToEndOfExpression()
    {
        // "= 1 + " (trimmed trailing spaces before '=')
        string original = "= 1 +   =";
        //                 012345678
        // The arithmetic expression stops after '+', at original index 5.

        Assert.IsFalse(Calculator.TryParse(original, out _, out var diagnostic));
        Assert.AreEqual(EvaluationError.InvalidSyntax, diagnostic!.Code);

        // Must point to index 5 (after '+'), not index 3 (normalized) and not index 8 (end of string).
        Assert.AreEqual(5, diagnostic.Position);
        Assert.AreEqual(0, diagnostic.Length);
    }
}
