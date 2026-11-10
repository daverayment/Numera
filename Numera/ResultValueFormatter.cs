using System.Globalization;
using System.Numerics;

namespace Numera;

/// <summary>
/// Formats <see cref="MathValue"/> instances into human-friendly or scientific strings
/// using exact integer and rational arithmetic, midpoint-to-even rounding, and culture settings.
/// </summary>
internal static class ResultValueFormatter
{
    private const int MaxComponentBits = CalculationLimits.MaxIntegerBits;
    private const int MaxFormattingIntermediateBits = CalculationLimits.MaxIntermediateBits;
    private const int MaxPreservedWholeNumberScale = 2;
    private const int MaxPreservedFractionalScale = 4;

    #region Public / Internal Entry Points

    /// <summary>
    /// Formats a math value according to the specified options and culture provider,
    /// returning the formatted display text and whether display rounding was applied.
    /// </summary>
    internal static (string Text, bool IsRounded) Format(
        MathValue value,
        ResultFormatOptions options,
        IFormatProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        var culture = NumberFormatInfo.GetInstance(provider);

        if (!TryExtractRational(value, culture, options, out var rational, out string? nonFiniteText))
        {
            return (nonFiniteText!, false);
        }

        if (rational.Numerator.IsZero)
        {
            return FormatZero(value, rational.IsNegativeZero, options, culture);
        }

        var rounded = ComputeSignificantDigits(
            rational.Numerator,
            rational.Denominator,
            rational.IsNegative,
            options.SignificantDigits);

        // Rounding can carry into a new decade, so select notation from the rounded
        // exponent rather than the input magnitude (e.g. 999.9 rounding to 1000).
        bool useScientific = ShouldUseScientific(rounded.Exponent, options);

        string formatted = useScientific
            ? FormatScientific(rounded, options, culture)
            : FormatFixed(rounded, value, rational.Denominator.IsOne, options, culture);

        CheckOutputLength(formatted, options);
        return (formatted, rounded.IsRounded);
    }

    /// <summary>
    /// Formats the exact fractional representation of a math value (e.g. "1/3" or "5/4"),
    /// or returns <see langword="null"/> if the value has no verified exact fraction.
    /// </summary>
    internal static string? FormatExactFraction(MathValue value, ResultFormatOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        if (!value.TryGetExactFraction(out var numerator, out var denominator))
        {
            return null;
        }

        CheckComponents(numerator, denominator);

        long length = IntegerLength(numerator);
        if (!denominator.IsOne)
        {
            length += 1L + IntegerLength(denominator);
        }

        CheckLength(length, options);
        string text = numerator.ToString(CultureInfo.InvariantCulture);
        return denominator.IsOne ? text : text + "/" + denominator.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Determines whether a number node's value should be rendered in scientific notation
    /// based on its exponent and the configured fixed-digit limit.
    /// Double nodes follow their native general-format notation rather than result-display thresholds.
    /// </summary>
    internal static bool ShouldRenderScientific(MathValue value, int maxFixedDigits = 24)
    {
        if (value.Kind == MathValue.DataType.Double)
        {
            return double.IsFinite(value.DoubleValue) &&
                   value.DoubleValue.ToString("G", CultureInfo.InvariantCulture).AsSpan().IndexOfAny('E', 'e') >= 0;
        }

        if (MathValue.TryToRational(value, out var num, out var den))
        {
            if (num.IsZero)
            {
                return false;
            }

            int exp = DecimalExponent(BigInteger.Abs(num), den);
            return exp < -maxFixedDigits || exp >= maxFixedDigits;
        }

        return false;
    }

    /// <summary>
    /// Computes the base-10 exponent <c>floor(log10(numerator / denominator))</c>
    /// using integer comparisons without floating-point projection.
    /// </summary>
    /// <param name="numerator">A positive numerator.</param>
    /// <param name="denominator">A positive denominator.</param>
    /// <exception cref="ArgumentOutOfRangeException">Either component is not positive.</exception>
    internal static int DecimalExponent(BigInteger numerator, BigInteger denominator)
    {
        if (numerator.Sign <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numerator), "The numerator must be positive.");
        }

        if (denominator.Sign <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(denominator), "The denominator must be positive.");
        }

        // The floating-point estimate is only a starting point. Exact comparisons
        // below decide the decade, including fractions extremely close to a power of ten.
        int exponent = (int)Math.Floor((numerator.GetBitLength() - denominator.GetBitLength()) * Math.Log10(2));
        while (ComparePowerOfTen(numerator, denominator, exponent) < 0)
        {
            exponent--;
        }

        while (ComparePowerOfTen(numerator, denominator, exponent + 1) >= 0)
        {
            exponent++;
        }

        return exponent;
    }

    internal static void CheckOutputLength(string text, ResultFormatOptions options)
        => CheckLength(text.Length, options.MaxOutputCharacters);

    internal static void CheckOutputLength(string text, int maxOutputCharacters = CalculationLimits.MaxOutputCharacters)
        => CheckLength(text.Length, maxOutputCharacters);

    #endregion

    #region Pipeline Stages: Extraction & Zero

    private readonly record struct RationalInput(
        BigInteger Numerator,
        BigInteger Denominator,
        bool IsNegative,
        bool IsNegativeZero);

    private static bool TryExtractRational(
        MathValue value,
        NumberFormatInfo culture,
        ResultFormatOptions options,
        out RationalInput rational,
        out string? nonFiniteText)
    {
        nonFiniteText = null;

        if (value.Kind == MathValue.DataType.Double)
        {
            if (!double.IsFinite(value.DoubleValue))
            {
                nonFiniteText = value.DoubleValue.ToString(culture);
                CheckOutputLength(nonFiniteText, options);
                rational = default;
                return false;
            }

            MathValue.NumericFraction(value, out var num, out var den);
            CheckComponents(num, den);
            bool isNegativeZero = num.IsZero && BitConverter.DoubleToInt64Bits(value.DoubleValue) < 0;
            rational = new RationalInput(BigInteger.Abs(num), den, num.Sign < 0, isNegativeZero);
            return true;
        }

        if (!MathValue.TryToRational(value, out var n, out var d))
        {
            throw new InvalidOperationException("The value has no finite numeric representation.");
        }

        CheckComponents(n, d);
        rational = new RationalInput(BigInteger.Abs(n), d, n.Sign < 0, false);
        return true;
    }

    private static (string Text, bool IsRounded) FormatZero(
        MathValue value,
        bool isNegativeZero,
        ResultFormatOptions options,
        NumberFormatInfo culture)
    {
        int zeroScale = options.Notation == NumericNotation.Scientific
            ? 0
            : Math.Min(PreservedDecimalScale(value, isWholeNumber: true), options.SignificantDigits - 1);

        string sign = isNegativeZero ? culture.NegativeSign : "";
        long length = (long)sign.Length + 1 + zeroScale
            + (zeroScale > 0 ? culture.NumberDecimalSeparator.Length : 0);
        CheckLength(length, options);

        string decimalPart = zeroScale > 0
            ? culture.NumberDecimalSeparator + new string('0', zeroScale)
            : "";

        string result = sign + "0" + decimalPart;
        CheckOutputLength(result, options);
        return (result, false);
    }

    #endregion

    #region Pipeline Stages: Rounding & Layout

    private readonly record struct RoundedDecimal(
        bool IsNegative,
        int Exponent,
        string Digits,
        bool IsRounded);

    private static RoundedDecimal ComputeSignificantDigits(
        BigInteger numerator,
        BigInteger denominator,
        bool isNegative,
        int significantDigits)
    {
        int exponent = DecimalExponent(numerator, denominator);
        int scale = significantDigits - 1 - exponent;

        BigInteger scaledNumerator = numerator;
        BigInteger scaledDenominator = denominator;
        if (scale >= 0)
        {
            scaledNumerator = ScaleByPowerOfTen(scaledNumerator, scale);
        }
        else
        {
            scaledDenominator = ScaleByPowerOfTen(scaledDenominator, -scale);
        }

        BigInteger coefficient = BigInteger.DivRem(scaledNumerator, scaledDenominator, out var remainder);

        // This records display loss relative to storage, not calculation accuracy.
        // An Unverified value may display without rounding and still be approximate.
        bool isRounded = !remainder.IsZero;

        // Midpoint ties round to nearest even integer
        int midpointComparison = (remainder << 1).CompareTo(scaledDenominator);
        if (midpointComparison > 0 || (midpointComparison == 0 && !coefficient.IsEven))
        {
            coefficient++;
        }

        string digits = coefficient.ToString(CultureInfo.InvariantCulture);

        // Adjust for carry-over when rounding (e.g. 9.99 rounded to 2 sig digits -> 10.0)
        if (digits.Length > significantDigits)
        {
            digits = digits[..^1];
            exponent++;
        }

        digits = digits.TrimEnd('0');

        return new RoundedDecimal(isNegative, exponent, digits, isRounded);
    }

    private static bool ShouldUseScientific(int exponent, ResultFormatOptions options) =>
        options.Notation == NumericNotation.Scientific ||
        (options.Notation == NumericNotation.Auto &&
         (exponent < -options.MaxFixedDigits || exponent >= options.MaxFixedDigits));

    private static string FormatScientific(
        RoundedDecimal rounded,
        ResultFormatOptions options,
        NumberFormatInfo culture)
    {
        string sign = rounded.IsNegative ? culture.NegativeSign : "";
        string separator = culture.NumberDecimalSeparator;
        string suffix = ScientificSuffix(rounded.Exponent, options);

        long length = (long)sign.Length + rounded.Digits.Length + suffix.Length
            + (rounded.Digits.Length > 1 ? separator.Length : 0);
        CheckLength(length, options);

        string mantissa = rounded.Digits.Length == 1
            ? rounded.Digits
            : rounded.Digits[..1] + separator + rounded.Digits[1..];

        return sign + mantissa + suffix;
    }

    private static string FormatFixed(
        RoundedDecimal rounded,
        MathValue originalValue,
        bool isWholeNumber,
        ResultFormatOptions options,
        NumberFormatInfo culture)
    {
        var layout = CreateFixedLayout(rounded, originalValue, isWholeNumber, options);

        // Share the layout with length checking, so padding and culture separators
        // cannot make the rendered string exceed a limit that the preflight accepted.
        CheckFixedLength(rounded, layout, options, culture);

        string sign = rounded.IsNegative ? culture.NegativeSign : "";
        string separator = culture.NumberDecimalSeparator;

        string number;
        if (rounded.Exponent < 0)
        {
            int leadingZeros = layout.DecimalPlaces - rounded.Digits.Length;
            number = "0" + separator + new string('0', leadingZeros) + rounded.Digits;
        }
        else if (layout.DecimalPlaces == 0)
        {
            int trailingZeros = layout.IntegerDigitCount - rounded.Digits.Length;
            string integerPart = rounded.Digits + new string('0', trailingZeros);
            number = options.GroupDigits ? GroupIntegerDigits(integerPart, culture) : integerPart;
        }
        else
        {
            string integerPart = rounded.Digits[..layout.IntegerDigitCount];
            string fractionalPart = rounded.Digits[layout.IntegerDigitCount..];
            string groupedInteger = options.GroupDigits ? GroupIntegerDigits(integerPart, culture) : integerPart;
            number = groupedInteger + separator + fractionalPart;
        }

        if (layout.PreservedTrailingZeroCount > 0)
        {
            number += (layout.DecimalPlaces == 0 ? separator : "")
                + new string('0', layout.PreservedTrailingZeroCount);
        }

        return sign + number;
    }

    #endregion

    #region Pipeline Stages: Formatting Helpers & Limits

    private readonly record struct FixedLayout(
        int IntegerDigitCount,
        int DecimalPlaces,
        int PreservedTrailingZeroCount);

    private static FixedLayout CreateFixedLayout(
        RoundedDecimal rounded,
        MathValue originalValue,
        bool isWholeNumber,
        ResultFormatOptions options)
    {
        int integerDigitCount = Math.Max(1, rounded.Exponent + 1);
        int decimalPlaces = Math.Max(0, rounded.Digits.Length - rounded.Exponent - 1);
        int trailingZeroCount = GetPreservedTrailingZeroCount(
            rounded, originalValue, isWholeNumber, decimalPlaces, options.SignificantDigits);

        return new FixedLayout(integerDigitCount, decimalPlaces, trailingZeroCount);
    }

    private static int GetPreservedTrailingZeroCount(
        RoundedDecimal rounded,
        MathValue originalValue,
        bool isWholeNumber,
        int decimalPlaces,
        int significantDigits)
    {
        if (rounded.IsRounded)
        {
            // Literal-scale padding must not suggest retained precision after the
            // significant-digit budget has already discarded part of the stored value.
            return 0;
        }

        int preservedScale = Math.Min(
            PreservedDecimalScale(originalValue, isWholeNumber),
            Math.Max(0, significantDigits - 1 - rounded.Exponent));

        return Math.Max(0, preservedScale - decimalPlaces);
    }

    private static void CheckFixedLength(
        RoundedDecimal rounded,
        FixedLayout layout,
        ResultFormatOptions options,
        NumberFormatInfo culture)
    {
        int signLength = rounded.IsNegative ? culture.NegativeSign.Length : 0;
        int decimalPlaces = layout.DecimalPlaces + layout.PreservedTrailingZeroCount;
        int groupCount = options.GroupDigits ? GetGroupSeparatorCount(layout.IntegerDigitCount, culture) : 0;

        long length = (long)signLength + layout.IntegerDigitCount + decimalPlaces
            + (decimalPlaces > 0 ? culture.NumberDecimalSeparator.Length : 0)
            + (long)groupCount * culture.NumberGroupSeparator.Length;

        CheckLength(length, options);
    }

    private static string GroupIntegerDigits(string integerDigits, NumberFormatInfo culture)
    {
        if (GetGroupSeparatorCount(integerDigits.Length, culture) == 0)
        {
            return integerDigits;
        }

        var integer = BigInteger.Parse(integerDigits, NumberStyles.None, CultureInfo.InvariantCulture);
        return integer.ToString("N0", culture);
    }

    internal static int GetGroupSeparatorCount(int integerDigitCount, NumberFormatInfo culture)
    {
        if (string.IsNullOrEmpty(culture.NumberGroupSeparator))
        {
            return 0;
        }

        int[] groupSizes = culture.NumberGroupSizes;
        int remainingDigits = integerDigitCount;
        int groupIndex = 0;
        int groupCount = 0;

        // .NET repeats the final nonzero group size; a terminal zero stops grouping.
        // Counting the same way keeps preflight limits consistent with ToString("N0").
        while (groupSizes.Length > 0 && groupSizes[groupIndex] > 0 && remainingDigits > groupSizes[groupIndex])
        {
            remainingDigits -= groupSizes[groupIndex];
            groupCount++;

            if (groupIndex < groupSizes.Length - 1)
            {
                groupIndex++;
            }
        }

        return groupCount;
    }

    private static int PreservedDecimalScale(MathValue value, bool isWholeNumber)
    {
        int maximumScale = isWholeNumber ? MaxPreservedWholeNumberScale : MaxPreservedFractionalScale;
        return value.DecimalDisplayScale <= maximumScale ? value.DecimalDisplayScale : 0;
    }

    private static string ScientificSuffix(int exponent, ResultFormatOptions options)
    {
        string power = exponent.ToString(CultureInfo.InvariantCulture);
        return options.Style switch
        {
            ExpressionFormatStyle.NormalizedAscii =>
                (options.ExponentMarker == ExponentMarker.Uppercase ? "E" : "e") + power,
            ExpressionFormatStyle.MathJax => @" \times 10^{" + power + "}",
            _ => " × 10^" + power,
        };
    }

    #endregion

    #region Arithmetic and Scaling Helpers

    private static void CheckComponents(BigInteger numerator, BigInteger denominator)
    {
        if (BigInteger.Abs(numerator).GetBitLength() > MaxComponentBits
            || denominator.GetBitLength() > MaxComponentBits)
        {
            throw new CalculationLimitException();
        }
    }

    private static void CheckLength(long length, ResultFormatOptions options)
        => CheckLength(length, options.MaxOutputCharacters);

    private static void CheckLength(long length, int maxOutputCharacters)
    {
        if (length > maxOutputCharacters)
        {
            throw new CalculationLimitException();
        }
    }

    private static int IntegerLength(BigInteger value)
    {
        if (value.IsZero)
        {
            return 1;
        }

        int length = DecimalExponent(BigInteger.Abs(value), BigInteger.One) + 1;
        if (value.Sign < 0)
        {
            length++;
        }

        return length;
    }

    private static int ComparePowerOfTen(BigInteger numerator, BigInteger denominator, int exponent)
        => exponent >= 0
            ? numerator.CompareTo(ScaleByPowerOfTen(denominator, exponent))
            : ScaleByPowerOfTen(numerator, -exponent).CompareTo(denominator);

    private static BigInteger ScaleByPowerOfTen(BigInteger value, int exponent)
    {
        // Keep formatting independently bounded if numeric storage ceilings grow.
        // This conservatively bounds both the power and the product before allocation.
        long estimatedBits = value.GetBitLength() + (long)Math.Ceiling(exponent * Math.Log2(10));
        if (exponent < 0 || exponent > CalculationLimits.MaxExponentMagnitude || estimatedBits > MaxFormattingIntermediateBits)
        {
            throw new CalculationLimitException();
        }

        return value * BigInteger.Pow(10, exponent);
    }

    #endregion
}
