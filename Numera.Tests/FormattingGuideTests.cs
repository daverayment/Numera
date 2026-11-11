using System.Globalization;
using System.Numerics;
using System.Text;

namespace Numera.Tests;

/// <summary>
/// Pins every behaviour that docs/architecture.md demonstrates in its formatting and culture
/// examples, so the documentation cannot drift from what the library actually does.
/// </summary>
[TestClass]
public sealed class FormattingGuideTests
{
    private static readonly CultureInfo German = new("de-DE");

    private static CalculatorEngine GermanEngine() =>
        new(CalculatorOptions.ForCulture(German));

    [TestMethod]
    public void RadixExamples_MatchTheDocumentedOutput()
    {
        var value = Calculator.Evaluate("1100");

        Assert.AreEqual("0x44C", value.ToHexString());
        Assert.AreEqual("0b10001001100", value.ToBinaryString());
        Assert.AreEqual("0o2114", value.ToOctalString());
        Assert.AreEqual("44C", value.ToHexString(prefix: false));

        Assert.AreEqual("-0xFF", Calculator.Evaluate("-255").ToHexString());
        Assert.IsNull(Calculator.Evaluate("1.5").ToHexString());
    }

    [TestMethod]
    [DataRow(255, "0xFF")]
    [DataRow(-255, "-0xFF")]
    [DataRow(128, "0x80")]
    [DataRow(4095, "0xFFF")]
    [DataRow(16, "0x10")]
    public void HexString_HasNoLeadingZeroPaddingWhenTheTopBitIsSet(int input, string expected)
    {
        Assert.AreEqual(expected, new MathValue(new BigInteger(input)).ToHexString());
    }

    [TestMethod]
    public void TryFormat_UsingDefaultFormat_WritesIntoTheBuffer()
    {
        var value = Calculator.Evaluate("1100");
        Span<char> buffer = stackalloc char[64];

        Assert.IsTrue(value.TryFormat(buffer, out var written, default, CultureInfo.InvariantCulture));
        Assert.AreEqual("1,100", new string(buffer[..written]));
    }

    [TestMethod]
    public void TryFormat_ReturnsFalseWhenTheBufferIsTooSmall()
    {
        var value = new MathValue(new BigInteger(123456));

        Span<char> chars = stackalloc char[3];
        Assert.IsFalse(value.TryFormat(chars, out _, default, CultureInfo.InvariantCulture));

        Span<byte> utf8 = stackalloc byte[3];
        Assert.IsFalse(value.TryFormat(utf8, out _, default, CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void EngineWithCulture_ReadsAndDisplaysInThatCulture()
    {
        var engine = GermanEngine();

        var value = engine.Evaluate("1000 + 2,5");
        var shown = engine.FormatResult(value);

        Assert.AreEqual("1.002,5", shown.PrimaryDisplay);
        Assert.AreEqual("1002.5", shown.MachineReadable);
        Assert.IsNull(shown.HexRepresentation);
    }

    [TestMethod]
    public void EngineDisplayText_CanBeTypedBackIntoTheSameEngine()
    {
        var engine = GermanEngine();
        var shown = engine.FormatResult(engine.Evaluate("1000 + 2,5"));

        Assert.IsNotNull(shown.PrimaryDisplay);
        Assert.AreEqual(1002.5m, engine.Evaluate(shown.PrimaryDisplay).DecimalValue);
    }

    [TestMethod]
    public void DisplayText_IsGroupedByDefault()
    {
        var engine = GermanEngine();

        Assert.AreEqual("1.234.567", engine.FormatResult(engine.Evaluate("1234567")).PrimaryDisplay);
        Assert.AreEqual("1.234.567,5", engine.FormatResult(engine.Evaluate("1234567,5")).PrimaryDisplay);

        var unGrouped = engine.FormatResult(
            engine.Evaluate("1234567"),
            engine.Options.ResultFormat with { GroupDigits = false });
        Assert.AreEqual("1234567", unGrouped.PrimaryDisplay);
    }

    [TestMethod]
    public void RadixAndMachineReadableText_NeverDependOnCulture()
    {
        var engine = GermanEngine();
        var shown = engine.FormatResult(engine.Evaluate("1100"));

        Assert.AreEqual("1100", shown.MachineReadable);
        Assert.AreEqual("0x44C", shown.HexRepresentation);
        Assert.AreEqual("0b10001001100", shown.BinaryRepresentation);
        Assert.AreEqual("0o2114", shown.OctalRepresentation);
    }

    [TestMethod]
    [DataRow("de-DE", "1000 + 2,5", "1.002,5")]
    [DataRow("fr-FR", "1000 + 2,5", "1\u202f002,5")]
    [DataRow("en-US", "1000 + 2.5", "1,002.5")]
    public void StaticFacade_ReadsAndDisplaysUsingCurrentCulture(string culture, string input, string expected)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var result = Calculator.Evaluate(input);

            Assert.AreEqual(1002.5m, result.DecimalValue);
            Assert.AreEqual(culture, result.Culture?.Name);
            Assert.AreEqual(expected, result.PrimaryDisplay);
            Assert.AreEqual(expected, result.FormatResult().PrimaryDisplay);
            Assert.AreEqual("1002.5", result.MachineReadable);
            Assert.AreEqual(result.Value, Calculator.Evaluate(expected).Value);
            Assert.IsTrue(Calculator.TryEvaluate(input, out var attempted, out var diagnostic));
            Assert.IsNull(diagnostic);
            Assert.AreEqual(result.Value, attempted.Value);
            Assert.AreEqual(expected, attempted.PrimaryDisplay);
            Assert.AreEqual(result.Value, Calculator.Evaluate(Calculator.Parse(input)).Value);
            Assert.IsTrue(Calculator.TryParse(input, out var node, out diagnostic));
            Assert.IsNull(diagnostic);
            Assert.AreEqual(result.Value, Calculator.Evaluate(node).Value);
            Assert.AreEqual("1000 + 2.5", Calculator.FormatExpression(input, ExpressionFormatOptions.Ascii));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void ResultsAndExistingEnginesRetainCultureWhileFacadeFollowsCurrentCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = German;
            var engine = new CalculatorEngine();
            var result = Calculator.Evaluate("1000 + 2,5");
            Assert.AreEqual(2.5m, Calculator.Evaluate("max(1,5; 2,5)").DecimalValue);

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

            Assert.AreEqual("1.002,5", result.PrimaryDisplay);
            Assert.AreEqual("1.002,5", result.FormatResult().PrimaryDisplay);
            Assert.AreEqual("1.002,5", result.FormatResult(null, null).PrimaryDisplay);
            Assert.AreEqual("1.002,5", result.ToString());
            Assert.AreEqual("1.002,5", $"{result}");
            Assert.AreEqual("1.002,5", engine.Evaluate("1000 + 2,5").PrimaryDisplay);
            Assert.AreEqual(result.Value, engine.Evaluate(result.PrimaryDisplay).Value);
            Assert.AreEqual("1,002.5", Calculator.Evaluate("1000 + 2.5").PrimaryDisplay);
            Assert.AreEqual("1,002.5", result.FormatResult(culture: CultureInfo.InvariantCulture).PrimaryDisplay);
            Assert.AreEqual("1002.5", result.FormatResult().MachineReadable);
            Assert.AreEqual(result.Value, CalculatorEngine.ForCulture(CultureInfo.InvariantCulture)
                .Evaluate(result.MachineReadable).Value);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void ExplicitNumericFormatsAndSpansRetainResultCultureUnlessOverridden()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            var result = GermanEngine().Evaluate("1234,5");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

            Assert.AreEqual("1234,50", result.ToString("F2", null));
            Assert.AreEqual("1234,50", $"{result:F2}");
            Assert.AreEqual("1234.50", result.ToString("F2", CultureInfo.InvariantCulture));
            Span<char> chars = stackalloc char[32];
            Span<byte> bytes = stackalloc byte[32];
            foreach (string format in new[] { "", "F2" })
            {
                string expected = format.Length == 0 ? "1.234,5" : "1234,50";
                Assert.IsTrue(result.TryFormat(chars, out int charsWritten, format));
                Assert.AreEqual(expected, chars[..charsWritten].ToString());
                Assert.IsTrue(result.TryFormat(bytes, out int bytesWritten, format));
                Assert.AreEqual(expected, Encoding.UTF8.GetString(bytes[..bytesWritten]));

                string invariant = format.Length == 0 ? "1,234.5" : "1234.50";
                Assert.IsTrue(result.TryFormat(chars, out charsWritten, format, CultureInfo.InvariantCulture));
                Assert.AreEqual(invariant, chars[..charsWritten].ToString());
                Assert.IsTrue(result.TryFormat(bytes, out bytesWritten, format, CultureInfo.InvariantCulture));
                Assert.AreEqual(invariant, Encoding.UTF8.GetString(bytes[..bytesWritten]));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void ResultOverridesAndReceivingEnginePolicyAreExplicitAndIndependent()
    {
        var options = new ResultFormatOptions { SignificantDigits = 3, GroupDigits = false };
        var origin = new CalculatorEngine(new CalculatorOptions { Culture = German, ResultFormat = options });
        var result = origin.Evaluate("amount=12,345");
        var moreDigits = options with { SignificantDigits = 5 };

        Assert.AreEqual("12,3", result.FormatResult().PrimaryDisplay);
        Assert.AreEqual("12,3", result.FormatResult(culture: null).PrimaryDisplay);
        Assert.AreEqual("12,345", result.FormatResult(moreDigits).PrimaryDisplay);
        Assert.AreEqual("12.3", result.FormatResult(culture: CultureInfo.InvariantCulture).PrimaryDisplay);
        var overridden = result.FormatResult(moreDigits, CultureInfo.InvariantCulture);
        Assert.AreEqual("12.345", overridden.PrimaryDisplay);
        Assert.AreEqual("12.345", overridden.MachineReadable);
        Assert.AreEqual("2469/200", overridden.FractionRepresentation);
        Assert.AreSame(result.AssignedVariables, overridden.AssignedVariables);

        var receiving = new CalculatorEngine(new CalculatorOptions
        {
            Culture = CultureInfo.InvariantCulture,
            ResultFormat = options with { SignificantDigits = 4 }
        });
        Assert.AreEqual("12.34", receiving.FormatResult(result).PrimaryDisplay);
        Assert.AreEqual("12.34", receiving.FormatResult(result.Value).PrimaryDisplay);
        Assert.AreEqual("12.34", receiving.FormatResult(result, null, null).PrimaryDisplay);
        Assert.AreEqual("12.34", receiving.FormatResult(result.Value, null, null).PrimaryDisplay);
        Assert.AreEqual("12,345", receiving.FormatResult(result, moreDigits, German).PrimaryDisplay);
        Assert.AreEqual("12,345", receiving.FormatResult(result.Value, moreDigits, German).PrimaryDisplay);
        Assert.AreSame(result.AssignedVariables, receiving.FormatResult(result).AssignedVariables);
        Assert.AreEqual("12,3", result.PrimaryDisplay);
        Assert.AreSame(options, result.FormatOptions);

        var integer = origin.Evaluate("n=42");
        Assert.AreEqual("2A", integer.FormatResult(prefixRadixOutput: false).HexRepresentation);
        Assert.AreEqual("101010", receiving.FormatResult(integer, prefixRadixOutput: false).BinaryRepresentation);
        Assert.AreEqual("52", receiving.FormatResult(integer.Value, prefixRadixOutput: false).OctalRepresentation);
    }

    [TestMethod]
    public void ResultsWithoutCapturedOptionsUseHumanFacingDefaults()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = German;
            var result = new EvaluationResult(new MathValue(12.5m), EvaluationContext.Empty);

            Assert.IsNull(result.Options);
            Assert.AreEqual("12,5", result.PrimaryDisplay);
            Assert.AreEqual("12,5", result.FormatResult().PrimaryDisplay);
            Assert.AreEqual("12,50", result.ToString("F2", null));
            Assert.AreEqual("12.5", result.FormatResult(culture: CultureInfo.InvariantCulture).PrimaryDisplay);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void DoubleResults_UseRoundTripPrecision()
    {
        var value = new MathValue(0.1 + 0.2);

        Assert.AreEqual(MathValue.DataType.Double, value.Kind);
        Assert.AreEqual("0.30000000000000004", value.ToString(CultureInfo.InvariantCulture));
    }
}
