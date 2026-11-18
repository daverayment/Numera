using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Numera.Ast;

namespace Numera.Tests;

[TestClass]
public sealed class CalculationLimitDiagnosticTests
{
    [TestMethod]
    [DataRow("1001!", CalculationLimitKind.FactorialArgument, 1000, "factorial argument", 0, 5)]
    [DataRow("factorial(1001)", CalculationLimitKind.FactorialArgument, 1000, "factorial argument", 0, 15)]
    [DataRow("9999^9999", CalculationLimitKind.IntegerBits, 16384, "integer or reduced fraction component size", 0, 9)]
    [DataRow("2^10001", CalculationLimitKind.ExponentMagnitude, 10000, "exponent magnitude", 0, 7)]
    [DataRow("sqrt(2^16000)", CalculationLimitKind.ExponentMagnitude, 10000, "exponent magnitude", 5, 7)]
    public void EvaluationReportsTheRejectingLimitAndSourceSpan(
        string expression, CalculationLimitKind kind, int ceiling, string description, int position, int length)
    {
        var engine = CalculatorEngine.ForCulture(CultureInfo.InvariantCulture);

        Assert.IsFalse(engine.TryEvaluate(expression, out var result, out var diagnostic));
        Assert.IsNotNull(diagnostic);
        AssertLimit(diagnostic, kind, ceiling);
        Assert.Contains(description, diagnostic.Message);
        Assert.AreEqual(position, diagnostic.Position);
        Assert.AreEqual(length, diagnostic.Length);
        Assert.IsFalse(result.HasAssignments);

        var exception = Assert.ThrowsExactly<CalculationLimitException>(() => engine.Evaluate(expression));
        Assert.AreEqual(diagnostic, exception.Diagnostic);
    }

    [TestMethod]
    [DataRow("123456", CalculationLimitKind.InputLength, 5)]
    [DataRow("((((1))))", CalculationLimitKind.NestingDepth, 3)]
    [DataRow("256", CalculationLimitKind.IntegerBits, 8)]
    [DataRow("2^4", CalculationLimitKind.ExponentMagnitude, 3)]
    [DataRow("sigma(12,4)", CalculationLimitKind.ExponentMagnitude, 3)]
    [DataRow("1e4", CalculationLimitKind.ExponentMagnitude, 3)]
    public void ConfiguredCeilingsAreReportedInsteadOfHardDefaults(
        string expression, CalculationLimitKind kind, int ceiling)
    {
        var defaults = CalculatorOptions.ForCulture(CultureInfo.InvariantCulture);
        var options = kind switch
        {
            CalculationLimitKind.InputLength => defaults with { MaxInputLength = ceiling },
            CalculationLimitKind.NestingDepth => defaults with { MaxNestingDepth = ceiling },
            CalculationLimitKind.IntegerBits => defaults with { MaxIntegerBits = ceiling },
            CalculationLimitKind.ExponentMagnitude => defaults with { MaxExponentMagnitude = ceiling },
            _ => throw new AssertFailedException("Unexpected test limit kind.")
        };
        var engine = new CalculatorEngine(options);

        Assert.IsFalse(engine.TryEvaluate(expression, out _, out var diagnostic));
        Assert.IsNotNull(diagnostic);
        AssertLimit(diagnostic, kind, ceiling);
    }

    [TestMethod]
    [DataRow("1e5000", CalculationLimitKind.IntegerBits, 16384)]
    [DataRow("1e10000", CalculationLimitKind.IntermediateBits, 32768)]
    [DataRow("1e10001", CalculationLimitKind.ExponentMagnitude, 10000)]
    [DataRow("1e9999999999999999999999", CalculationLimitKind.ExponentMagnitude, 10000)]
    public void DecimalLiteralFailuresPreserveTheirOriginalLimit(
        string expression, CalculationLimitKind kind, int ceiling)
    {
        var engine = CalculatorEngine.ForCulture(CultureInfo.InvariantCulture);

        Assert.IsFalse(engine.TryEvaluate(expression, out _, out var diagnostic));
        Assert.IsNotNull(diagnostic);
        AssertLimit(diagnostic, kind, ceiling);
        Assert.AreEqual(0, diagnostic.Position);
        Assert.AreEqual(expression.Length, diagnostic.Length);
    }

    [TestMethod]
    [DataRow("0x100")]
    [DataRow("0x10000")]
    [DataRow("0b100000000")]
    [DataRow("0o400")]
    public void RadixPreflightAndFinalChecksIdentifyTheConfiguredIntegerLimit(string expression)
    {
        var engine = new CalculatorEngine(CalculatorOptions.ForCulture(CultureInfo.InvariantCulture) with
        {
            MaxIntegerBits = 8
        });

        Assert.AreEqual(new MathValue(255), engine.Evaluate("0xFF").Value);
        Assert.IsFalse(engine.TryEvaluate(expression, out _, out var diagnostic));
        Assert.IsNotNull(diagnostic);
        AssertLimit(diagnostic, CalculationLimitKind.IntegerBits, 8);
        Assert.AreEqual(0, diagnostic.Position);
        Assert.AreEqual(expression.Length, diagnostic.Length);
    }

    [TestMethod]
    [DataRow("= 1 + 1001! =", "1001!", CalculationLimitKind.FactorialArgument, 1000)]
    [DataRow("= 1 + 1e5000 =", "1e5000", CalculationLimitKind.IntegerBits, 16384)]
    public void NormalizedInputKeepsLimitMetadataAndOriginalSourceCoordinates(
        string input, string rejectedPart, CalculationLimitKind kind, int ceiling)
    {
        var engine = CalculatorEngine.ForCulture(CultureInfo.InvariantCulture);

        Assert.IsFalse(engine.TryEvaluate(input, out _, out var diagnostic));
        Assert.IsNotNull(diagnostic);
        AssertLimit(diagnostic, kind, ceiling);
        Assert.AreEqual(input.IndexOf(rejectedPart, StringComparison.Ordinal), diagnostic.Position);
        Assert.AreEqual(rejectedPart.Length, diagnostic.Length);
    }

    [TestMethod]
    public void ManualLiteralValidationAndCapturedValuesKeepLimitMetadata()
    {
        var engine = new CalculatorEngine(new CalculatorOptions { MaxIntegerBits = 8 });
        var node = new NumberNode(new MathValue(256), new TextSpan(8, 3));

        Assert.IsFalse(engine.TryEvaluate(node, out _, out var diagnostic));
        Assert.IsNotNull(diagnostic);
        AssertLimit(diagnostic, CalculationLimitKind.IntegerBits, 8);
        Assert.AreEqual(8, diagnostic.Position);
        Assert.AreEqual(3, diagnostic.Length);

        var context = new EvaluationContext([new Variable("x", MathValue.FromFraction(1, 256))]);
        Assert.IsFalse(engine.TryEvaluate("x", out _, out var capturedDiagnostic, context));
        Assert.IsNotNull(capturedDiagnostic);
        AssertLimit(capturedDiagnostic, CalculationLimitKind.IntegerBits, 8);
        Assert.AreEqual(0, capturedDiagnostic.Position);
        Assert.AreEqual(1, capturedDiagnostic.Length);
    }

    [TestMethod]
    public void AstNodeLimitReportsItsDerivedCeiling()
    {
        var engine = new CalculatorEngine(new CalculatorOptions { MaxInputLength = 2 });
        var allowed = new BinaryNode(BinaryOperator.Add,
            new UnaryNode(UnaryOperator.Positive, new NumberNode(1)), new NumberNode(2));
        var rejected = new BinaryNode(BinaryOperator.Add,
            new BinaryNode(BinaryOperator.Add, new NumberNode(1), new NumberNode(1)), new NumberNode(1));

        Assert.AreEqual(new MathValue(3), engine.Evaluate(allowed).Value);
        Assert.IsFalse(engine.TryEvaluate(rejected, out _, out var diagnostic));
        Assert.IsNotNull(diagnostic);
        AssertLimit(diagnostic, CalculationLimitKind.AstNodeCount, 4);
    }

    [TestMethod]
    public void AstNestingLimitIsDistinctFromNodeCount()
    {
        var engine = new CalculatorEngine(new CalculatorOptions { MaxNestingDepth = 3 });
        var allowed = new UnaryNode(UnaryOperator.Positive,
            new UnaryNode(UnaryOperator.Positive, new NumberNode(1)));
        var rejected = new UnaryNode(UnaryOperator.Positive, allowed);

        Assert.AreEqual(new MathValue(1), engine.Evaluate(allowed).Value);
        Assert.IsFalse(engine.TryEvaluate(rejected, out _, out var diagnostic));
        Assert.IsNotNull(diagnostic);
        AssertLimit(diagnostic, CalculationLimitKind.NestingDepth, 3);
    }

    [TestMethod]
    public void IntegerAndIntermediateBoundariesRemainDistinct()
    {
        var maximumInteger = (BigInteger.One << CalculationLimits.MaxIntegerBits) - 1;
        Assert.AreEqual(maximumInteger, new MathValue(maximumInteger).IntegerValue);
        var stored = Assert.ThrowsExactly<CalculationLimitException>(() =>
            new MathValue(BigInteger.One << CalculationLimits.MaxIntegerBits));
        AssertLimit(stored.Diagnostic, CalculationLimitKind.IntegerBits, CalculationLimits.MaxIntegerBits);

        var maximumComponent = BigInteger.One << (CalculationLimits.MaxIntermediateBits - 1);
        Assert.AreEqual(MathValue.One, MathValue.FromFraction(maximumComponent, maximumComponent));
        var intermediate = Assert.ThrowsExactly<CalculationLimitException>(() =>
            MathValue.FromFraction(BigInteger.One << CalculationLimits.MaxIntermediateBits, BigInteger.One));
        AssertLimit(intermediate.Diagnostic, CalculationLimitKind.IntermediateBits, CalculationLimits.MaxIntermediateBits);

        Assert.AreEqual(BigInteger.One << 63, MathValue.Product(BigInteger.One << 31, BigInteger.One << 32, 64));
        var product = Assert.ThrowsExactly<CalculationLimitException>(() =>
            MathValue.Product(BigInteger.One << 32, BigInteger.One << 32, 64));
        AssertLimit(product.Diagnostic, CalculationLimitKind.IntermediateBits, 64);

        var actualProduct = Assert.ThrowsExactly<CalculationLimitException>(() => MathValue.Product(95, 6, 9));
        AssertLimit(actualProduct.Diagnostic, CalculationLimitKind.IntermediateBits, 9);
    }

    [TestMethod]
    public void FactorialAndExponentCeilingsStillAcceptBoundaryValues()
    {
        var engine = CalculatorEngine.ForCulture(CultureInfo.InvariantCulture);

        Assert.AreEqual(engine.Evaluate("999! * 1000").Value, engine.Evaluate("1000!").Value);
        Assert.AreEqual(BigInteger.One << 10000, engine.Evaluate("2^10000").IntegerValue);
    }

    [TestMethod]
    public void NumberTheoryReportsAlgorithmSpecificLimits()
    {
        var combinations = Assert.ThrowsExactly<CalculationLimitException>(() => NumberTheory.Comb(2002, 1001));
        AssertLimit(combinations.Diagnostic, CalculationLimitKind.CombinatoricsTerms, 1000);
        var permutations = Assert.ThrowsExactly<CalculationLimitException>(() => NumberTheory.Perm(1001, 1001));
        AssertLimit(permutations.Diagnostic, CalculationLimitKind.CombinatoricsTerms, 1000);
        var primality = Assert.ThrowsExactly<CalculationLimitException>(() => NumberTheory.IsPrime((BigInteger.One << 2203) - 1));
        AssertLimit(primality.Diagnostic, CalculationLimitKind.PrimalityBits, CalculationLimits.MaxPrimalityBits);
        var factorization = Assert.ThrowsExactly<CalculationLimitException>(() => NumberTheory.DivCount(BigInteger.One << 128));
        AssertLimit(factorization.Diagnostic, CalculationLimitKind.FactorizationBits, CalculationLimits.MaxFactorizationBits);
    }

    [TestMethod]
    public void ExplicitFormattingPrecisionReportsItsOwnLimit()
    {
        var fraction = MathValue.FromFraction(1, 3);
        Assert.AreEqual(1002, fraction.ToString("F1000", CultureInfo.InvariantCulture).Length);
        var exception = Assert.ThrowsExactly<CalculationLimitException>(() =>
            fraction.ToString("F1001", CultureInfo.InvariantCulture));
        AssertLimit(exception.Diagnostic, CalculationLimitKind.FormattingDigits, 1000);
    }

    [TestMethod]
    public void SerializationBudgetsIdentifyCharactersAndBytesSeparately()
    {
        string json = "{\"Kind\":0,\"Exactness\":0,\"Value\":\"" + new string('1', 25000) + "\"}";
        var characters = Assert.ThrowsExactly<CalculationLimitException>(() => MathValue.Deserialize(json));
        AssertLimit(characters.Diagnostic, CalculationLimitKind.SerializedValueCharacters, 25000);

        string multibyteJson = "{\"Kind\":0,\"Exactness\":0,\"Value\":\"" + new string('\u00E9', 12500) + "\"}";
        Assert.IsLessThan(25000, multibyteJson.Length);
        Assert.IsGreaterThan(25000, Encoding.UTF8.GetByteCount(multibyteJson));
        var bytes = Assert.ThrowsExactly<CalculationLimitException>(() => JsonSerializer.Deserialize<MathValue>(multibyteJson));
        AssertLimit(bytes.Diagnostic, CalculationLimitKind.SerializedValueBytes, 25000);
    }

    [TestMethod]
    [DataRow(CalculationLimitKind.InputLength, "input length", "UTF-16 code units")]
    [DataRow(CalculationLimitKind.NestingDepth, "nesting depth", "levels")]
    [DataRow(CalculationLimitKind.IntegerBits, "integer or reduced fraction component size", "bits")]
    [DataRow(CalculationLimitKind.IntermediateBits, "arithmetic intermediate size", "bits")]
    [DataRow(CalculationLimitKind.FactorialArgument, "factorial argument", "")]
    [DataRow(CalculationLimitKind.ExponentMagnitude, "exponent magnitude", "")]
    [DataRow(CalculationLimitKind.PrimalityBits, "primality input size", "bits")]
    [DataRow(CalculationLimitKind.FactorizationBits, "factorization input size", "bits")]
    public void DefaultMessagesUseReadableLimitDescriptions(CalculationLimitKind kind, string description, string unit)
    {
        var unspecified = new CalculationLimitException(kind);
        Assert.AreEqual($"The {description} limit was exceeded.", unspecified.Message);
        Assert.AreEqual(kind, unspecified.Diagnostic.LimitKind);
        Assert.IsNull(unspecified.Diagnostic.LimitValue);

        string amount = "42";
        if (unit.Length > 0)
        {
            amount += $" {unit}";
        }

        var specified = new CalculationLimitException(kind, 42);
        Assert.AreEqual($"The {description} limit was exceeded ({amount}).", specified.Message);
        AssertLimit(specified.Diagnostic, kind, 42);
    }

    [TestMethod]
    public void EveryLimitKindHasADescriptiveMessageAndSurvivesDiagnosticEnrichment()
    {
        foreach (var kind in Enum.GetValues<CalculationLimitKind>())
        {
            long? ceiling = null;
            if (kind != CalculationLimitKind.ExecutionStack)
            {
                ceiling = 42;
            }

            var exception = new CalculationLimitException(kind, ceiling);
            Assert.AreEqual(kind, exception.Diagnostic.LimitKind);
            Assert.AreEqual(ceiling, exception.Diagnostic.LimitValue);
            Assert.AreNotEqual(new CalculationLimitException().Message, exception.Message);
            Assert.DoesNotContain("Max", exception.Message);

            var positioned = exception.Diagnostic with { Position = 3, Length = 2 };
            Assert.AreEqual(kind, positioned.LimitKind);
            Assert.AreEqual(ceiling, positioned.LimitValue);
            Assert.AreEqual(exception.Message, positioned.Message);
        }
    }

    [TestMethod]
    public void OtherErrorsAndUnspecifiedCallerLimitsHaveNoInventedLimitMetadata()
    {
        Assert.IsFalse(Calculator.TryEvaluate("1/0", out _, out var diagnostic));
        Assert.IsNotNull(diagnostic);
        Assert.AreEqual(EvaluationError.DivisionByZero, diagnostic.Code);
        Assert.IsNull(diagnostic.LimitKind);
        Assert.IsNull(diagnostic.LimitValue);
        Assert.IsNull(new CalculationLimitException().Diagnostic.LimitKind);
        Assert.IsNull(new CalculationLimitException().Diagnostic.LimitValue);
        Assert.ThrowsExactly<ArgumentNullException>(() => Calculator.TryEvaluate((string)null!, out _, out _));
    }

    private static void AssertLimit(EvaluationDiagnostic diagnostic, CalculationLimitKind kind, long ceiling)
    {
        Assert.AreEqual(EvaluationError.LimitExceeded, diagnostic.Code);
        Assert.AreEqual(kind, diagnostic.LimitKind);
        Assert.AreEqual(ceiling, diagnostic.LimitValue);
        Assert.Contains(ceiling.ToString(CultureInfo.InvariantCulture), diagnostic.Message);
    }
}
