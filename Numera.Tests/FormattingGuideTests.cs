using System.Globalization;
using System.Numerics;

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
        Assert.AreEqual("1100", new string(buffer[..written]));
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

        Assert.AreEqual("1002,5", shown.PrimaryDisplay);
        Assert.AreEqual("1002.5", shown.MachineReadable);
        Assert.IsNull(shown.HexRepresentation);
    }

    [TestMethod]
    public void EngineDisplayText_CanBeTypedBackIntoTheSameEngine()
    {
        var engine = GermanEngine();
        var shown = engine.FormatResult(engine.Evaluate("1000 + 2,5"));

        Assert.AreEqual(1002.5m, engine.Evaluate(shown.PrimaryDisplay).DecimalValue);
    }

    [TestMethod]
    public void DisplayText_IsNotGrouped()
    {
        var engine = GermanEngine();

        Assert.AreEqual("1234567", engine.FormatResult(engine.Evaluate("1234567")).PrimaryDisplay);
        Assert.AreEqual("1234567,5", engine.FormatResult(engine.Evaluate("1234567,5")).PrimaryDisplay);
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
    public void StaticFacade_ReadsInvariantInputWhateverTheCurrentCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = German;

            Assert.AreEqual(1002.5m, Calculator.Evaluate("1000 + 2.5").DecimalValue);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void StaticFormatResult_WithoutACulture_UsesTheCurrentCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = German;
            var value = Calculator.Evaluate("1000 + 2.5");

            var shown = Calculator.FormatResult(value);

            // Display follows the current culture, so the facade, which reads invariant text,
            // cannot re-parse it. That asymmetry is why the docs recommend an engine.
            Assert.AreEqual("1002,5", shown.PrimaryDisplay);
            Assert.AreEqual("1002.5", shown.MachineReadable);
            Assert.AreEqual("1002.5", Calculator.FormatResult(value, CultureInfo.InvariantCulture).PrimaryDisplay);
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
