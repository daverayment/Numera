using System.Globalization;
using System.Numerics;
using System.Text;

namespace Numera.Tests;

[TestClass]
public sealed class MathValueFormattingTests
{
    [TestMethod]
    public void SpanFormattableWorksForIntegerDecimalAndDouble()
    {
        var intVal = new MathValue(new BigInteger(12345));
        var decVal = new MathValue(123.45m);
        var dblVal = new MathValue(3.14159);

        Span<char> buffer = stackalloc char[64];

        // Integer formatting
        Assert.IsTrue(intVal.TryFormat(buffer, out int written));
        Assert.AreEqual("12345", new string(buffer[..written]));

        // Decimal formatting
        Assert.IsTrue(decVal.TryFormat(buffer, out written, "N2", CultureInfo.InvariantCulture));
        Assert.AreEqual("123.45", new string(buffer[..written]));

        // Double formatting
        Assert.IsTrue(dblVal.TryFormat(buffer, out written, "F2", CultureInfo.InvariantCulture));
        Assert.AreEqual("3.14", new string(buffer[..written]));
    }

    [TestMethod]
    public void Utf8SpanFormattableWorksForIntegerDecimalAndDouble()
    {
        var intVal = new MathValue(new BigInteger(12345));
        var decVal = new MathValue(123.45m);
        var dblVal = new MathValue(3.14159);

        Span<byte> utf8Buffer = stackalloc byte[64];

        // Integer UTF-8
        Assert.IsTrue(intVal.TryFormat(utf8Buffer, out int written));
        Assert.AreEqual("12345", Encoding.UTF8.GetString(utf8Buffer[..written]));

        // Decimal UTF-8
        Assert.IsTrue(decVal.TryFormat(utf8Buffer, out written, "F2", CultureInfo.InvariantCulture));
        Assert.AreEqual("123.45", Encoding.UTF8.GetString(utf8Buffer[..written]));

        // Double UTF-8
        Assert.IsTrue(dblVal.TryFormat(utf8Buffer, out written, "F2", CultureInfo.InvariantCulture));
        Assert.AreEqual("3.14", Encoding.UTF8.GetString(utf8Buffer[..written]));
    }

    [TestMethod]
    public void MultiRadixHelpersFormatCorrectly()
    {
        var value = new MathValue(new BigInteger(1092)); // 0x444, 0b10001000100, 0o2104

        Assert.AreEqual("0x444", value.ToHexString(prefix: true));
        Assert.AreEqual("444", value.ToHexString(prefix: false));

        Assert.AreEqual("0b10001000100", value.ToBinaryString(prefix: true));
        Assert.AreEqual("10001000100", value.ToBinaryString(prefix: false));

        Assert.AreEqual("0o2104", value.ToOctalString(prefix: true));
        Assert.AreEqual("2104", value.ToOctalString(prefix: false));

        // Negative values
        var neg = new MathValue(new BigInteger(-1092));
        Assert.AreEqual("-0x444", neg.ToHexString(prefix: true));
        Assert.AreEqual("-444", neg.ToHexString(prefix: false));
        Assert.AreEqual("-0b10001000100", neg.ToBinaryString(prefix: true));
        Assert.AreEqual("-10001000100", neg.ToBinaryString(prefix: false));
        Assert.AreEqual("-0o2104", neg.ToOctalString(prefix: true));
        Assert.AreEqual("-2104", neg.ToOctalString(prefix: false));

        // Zero
        var zero = new MathValue(BigInteger.Zero);
        Assert.AreEqual("0x0", zero.ToHexString(prefix: true));
        Assert.AreEqual("0", zero.ToHexString(prefix: false));
        Assert.AreEqual("0b0", zero.ToBinaryString(prefix: true));
        Assert.AreEqual("0", zero.ToBinaryString(prefix: false));
        Assert.AreEqual("0o0", zero.ToOctalString(prefix: true));
        Assert.AreEqual("0", zero.ToOctalString(prefix: false));
    }

    [TestMethod]
    public void MultiRadixHelpersReturnNullForNonIntegers()
    {
        var decVal = new MathValue(12.34m);
        var dblVal = new MathValue(5.67);

        Assert.IsNull(decVal.ToHexString());
        Assert.IsNull(decVal.ToBinaryString());
        Assert.IsNull(decVal.ToOctalString());

        Assert.IsNull(dblVal.ToHexString());
        Assert.IsNull(dblVal.ToBinaryString());
        Assert.IsNull(dblVal.ToOctalString());
    }

    [TestMethod]
    public void DecimalTrailingZerosArePreserved()
    {
        // 194.40m has scale 2 and must preserve the trailing zero
        var val = new MathValue(194.40m);
        Assert.AreEqual("194.40", val.ToString(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void LargeIntegerFormattingPreservesExactDigits()
    {
        // 2^100 exact digits
        var large = new MathValue(BigInteger.One << 100);
        string formatted = large.ToString(CultureInfo.InvariantCulture);
        Assert.IsFalse(formatted.Contains('e') || formatted.Contains('E'));
        Assert.AreEqual("1267650600228229401496703205376", formatted);
    }

    [TestMethod]
    public void CalculatorFormatResultProducesStructuredCard()
    {
        var intResult = Calculator.Evaluate("1092");
        var formattedInt = Calculator.FormatResult(intResult, CultureInfo.InvariantCulture);

        Assert.AreEqual(intResult, formattedInt.Value);
        Assert.AreEqual("1092", formattedInt.PrimaryDisplay);
        Assert.AreEqual("1092", formattedInt.MachineReadable);
        Assert.AreEqual("0x444", formattedInt.HexRepresentation);
        Assert.AreEqual("0b10001000100", formattedInt.BinaryRepresentation);
        Assert.AreEqual("0o2104", formattedInt.OctalRepresentation);

        var decResult = Calculator.Evaluate("194.40");
        var formattedDec = Calculator.FormatResult(decResult, CultureInfo.InvariantCulture);

        Assert.AreEqual(decResult, formattedDec.Value);
        Assert.AreEqual("194.40", formattedDec.PrimaryDisplay);
        Assert.AreEqual("194.40", formattedDec.MachineReadable);
        Assert.IsNull(formattedDec.HexRepresentation);
        Assert.IsNull(formattedDec.BinaryRepresentation);
        Assert.IsNull(formattedDec.OctalRepresentation);
    }

    [TestMethod]
    public void EvaluationResult_ForwardedRadixMethods_WorkCorrectly()
    {
        var intResult = Calculator.Evaluate("1092");

        // Forwarded multi-radix methods on EvaluationResult
        Assert.AreEqual("0x444", intResult.ToHexString(prefix: true));
        Assert.AreEqual("444", intResult.ToHexString(prefix: false));
        Assert.AreEqual("0x444", intResult.ToHexString());

        Assert.AreEqual("0b10001000100", intResult.ToBinaryString(prefix: true));
        Assert.AreEqual("10001000100", intResult.ToBinaryString(prefix: false));
        Assert.AreEqual("0b10001000100", intResult.ToBinaryString());

        Assert.AreEqual("0o2104", intResult.ToOctalString(prefix: true));
        Assert.AreEqual("2104", intResult.ToOctalString(prefix: false));
        Assert.AreEqual("0o2104", intResult.ToOctalString());

        // Negative integer
        var negResult = Calculator.Evaluate("-1092");
        Assert.AreEqual("-0x444", negResult.ToHexString());
        Assert.AreEqual("-444", negResult.ToHexString(prefix: false));
        Assert.AreEqual("-0b10001000100", negResult.ToBinaryString());
        Assert.AreEqual("-10001000100", negResult.ToBinaryString(prefix: false));
        Assert.AreEqual("-0o2104", negResult.ToOctalString());
        Assert.AreEqual("-2104", negResult.ToOctalString(prefix: false));

        // Non-integer returns null
        var decResult = Calculator.Evaluate("1.5");
        Assert.IsNull(decResult.ToHexString());
        Assert.IsNull(decResult.ToBinaryString());
        Assert.IsNull(decResult.ToOctalString());
    }

    [TestMethod]
    public void EvaluationResult_ForwardedFormattingMethods_WorkCorrectly()
    {
        var intResult = Calculator.Evaluate("12345");
        var decResult = Calculator.Evaluate("123.45");
        var dblResult = Calculator.Evaluate("1 / 3");

        // ToString() and ToString(IFormatProvider)
        Assert.AreEqual("12345", intResult.ToString());
        Assert.AreEqual("12345", intResult.ToString(CultureInfo.InvariantCulture));
        Assert.AreEqual("123.45", decResult.ToString(CultureInfo.InvariantCulture));

        // ToString(format, formatProvider)
        Assert.AreEqual("123.45", decResult.ToString("F2", CultureInfo.InvariantCulture));
        Assert.AreEqual("0.33", dblResult.ToString("F2", CultureInfo.InvariantCulture));

        // TryFormat (Span<char>)
        Span<char> charBuf = stackalloc char[64];
        Assert.IsTrue(intResult.TryFormat(charBuf, out int charsWritten));
        Assert.AreEqual("12345", new string(charBuf[..charsWritten]));

        Assert.IsTrue(decResult.TryFormat(charBuf, out charsWritten, "N2", CultureInfo.InvariantCulture));
        Assert.AreEqual("123.45", new string(charBuf[..charsWritten]));

        // TryFormat (Span<byte> UTF-8)
        Span<byte> utf8Buf = stackalloc byte[64];
        Assert.IsTrue(intResult.TryFormat(utf8Buf, out int bytesWritten));
        Assert.AreEqual("12345", Encoding.UTF8.GetString(utf8Buf[..bytesWritten]));

        Assert.IsTrue(decResult.TryFormat(utf8Buf, out bytesWritten, "N2", CultureInfo.InvariantCulture));
        Assert.AreEqual("123.45", Encoding.UTF8.GetString(utf8Buf[..bytesWritten]));

        // Formattable interface cast checks
        IFormattable formattable = intResult;
        Assert.AreEqual("12345", formattable.ToString(null, CultureInfo.InvariantCulture));

        ISpanFormattable spanFormattable = decResult;
        Assert.IsTrue(spanFormattable.TryFormat(charBuf, out charsWritten, "F2", CultureInfo.InvariantCulture));
        Assert.AreEqual("123.45", new string(charBuf[..charsWritten]));

        IUtf8SpanFormattable utf8Formattable = decResult;
        Assert.IsTrue(utf8Formattable.TryFormat(utf8Buf, out bytesWritten, "F2", CultureInfo.InvariantCulture));
        Assert.AreEqual("123.45", Encoding.UTF8.GetString(utf8Buf[..bytesWritten]));
    }
}
