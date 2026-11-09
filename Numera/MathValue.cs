using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Numera;

/// <summary>
/// A bounded calculator value. Exact-domain arithmetic uses reduced fractions, while
/// calculation accuracy is tracked independently of the active representation.
/// </summary>
/// <remarks>
/// The default value is integer zero with unverified provenance. Object equality treats
/// NaNs as equal; expression comparisons retain IEEE NaN semantics. Representation getters
/// do not convert values. Use explicit conversion methods when a different representation
/// is needed.
/// </remarks>
public readonly partial struct MathValue :
    IEquatable<MathValue>, IComparable<MathValue>, IComparable,
    IFormattable, ISpanFormattable, IUtf8SpanFormattable
{
    /// <summary>
    /// Identifies the active numerical representation.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Descriptive representation names.")]
    public enum DataType
    {
        /// <summary>
        /// A bounded arbitrary-precision integer.
        /// </summary>
        Integer,

        /// <summary>
        /// An exactly stored base-ten decimal.
        /// </summary>
        Decimal,

        /// <summary>
        /// A binary floating-point value, including infinities and NaN.
        /// </summary>
        Double,

        /// <summary>
        /// A reduced fraction with a positive denominator.
        /// </summary>
        Rational
    }

    private readonly BigInteger numerator;

    private readonly BigInteger denominator;

    // 29 marks a scale beyond Decimal's capacity; it never requests display padding.
    internal const int MaxDecimalDisplayScale = 29;

    internal int DecimalDisplayScale { get; }

    private static readonly BigInteger MaxDecimalMantissa = (BigInteger.One << 96) - 1;

    /// <summary>
    /// The active representation.
    /// </summary>
    public DataType Kind { get; }

    /// <summary>
    /// The provenance of the calculation, not a guarantee inferred from representation.
    /// </summary>
    public CalculationAccuracy Accuracy { get; }

    /// <summary>
    /// The stored integer, or zero when another representation is active.
    /// </summary>
    public BigInteger IntegerValue { get; }

    /// <summary>
    /// The stored decimal, or zero when another representation is active.
    /// </summary>
    public decimal DecimalValue { get; }

    /// <summary>
    /// The stored double, or zero when another representation is active.
    /// </summary>
    public double DoubleValue { get; }

    /// <summary>
    /// The stored reduced numerator. Throws unless this is a rational representation.
    /// </summary>
    public BigInteger Numerator => Kind == DataType.Rational ?
        numerator :
        throw new InvalidOperationException("Value is not stored as a rational.");

    /// <summary>
    /// The stored positive denominator. Throws unless this is a rational representation.
    /// </summary>
    public BigInteger Denominator => Kind == DataType.Rational ?
        denominator :
        throw new InvalidOperationException("Value is not stored as a rational.");

    /// <summary>
    /// Creates a precise integer, enforcing the default safety ceiling.
    /// </summary>
    public MathValue(BigInteger value)
    {
        this = default;
        CheckBits(value, CalculationLimits.MaxIntegerBits);
        Kind = DataType.Integer;
        IntegerValue = value;
        Accuracy = CalculationAccuracy.Exact;
    }

    /// <summary>
    /// Creates a precise integer.
    /// </summary>
    public MathValue(int value) : this(new BigInteger(value)) { }

    /// <summary>
    /// Creates a precise unsigned integer.
    /// </summary>
    public MathValue(uint value) : this(new BigInteger(value)) { }

    /// <summary>
    /// Creates a precise 64-bit integer.
    /// </summary>
    public MathValue(long value) : this(new BigInteger(value)) { }

    /// <summary>
    /// Creates a precise unsigned 64-bit integer.
    /// </summary>
    public MathValue(ulong value) : this(new BigInteger(value)) { }

    /// <summary>
    /// Creates a precisely stored decimal.
    /// </summary>
    public MathValue(decimal value)
    {
        this = default;
        Kind = DataType.Decimal;
        DecimalValue = value;
        DecimalDisplayScale = DecomposeDecimal(value).Scale;
        Accuracy = CalculationAccuracy.Exact;
    }

    /// <summary>
    /// Creates a double with unverified calculation provenance.
    /// </summary>
    public MathValue(double value)
    {
        this = default;
        Kind = DataType.Double;
        DoubleValue = value;
    }

    private MathValue(MathValue value, CalculationAccuracy accuracy)
    {
        this = value;
        Accuracy = accuracy;
    }

    private MathValue(MathValue value, int decimalDisplayScale)
    {
        this = value;
        DecimalDisplayScale = decimalDisplayScale;
    }

    internal MathValue WithDecimalDisplayScale(long scale)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(scale);
        return new MathValue(this, (int)Math.Min(scale, MaxDecimalDisplayScale));
    }

    private MathValue(BigInteger num, BigInteger den)
    {
        this = default;
        Kind = DataType.Rational;
        numerator = num;
        denominator = den;
        Accuracy = CalculationAccuracy.Exact;
    }

    /// <summary>
    /// Explicitly assigns trusted calculation provenance without changing representation.
    /// </summary>
    public MathValue WithAccuracy(CalculationAccuracy accuracy)
    {
        if (accuracy is not CalculationAccuracy.Exact and not CalculationAccuracy.Unverified)
        {
            throw new ArgumentOutOfRangeException(nameof(accuracy));
        }
        return new MathValue(this, accuracy);
    }

    /// <summary>
    /// Checks a stored value against an integer bit limit.
    /// </summary>
    public void ValidateLimits(int maxIntegerBits = CalculationLimits.MaxIntegerBits)
    {
        if (Kind == DataType.Integer)
        {
            CheckBits(IntegerValue, maxIntegerBits);
        }
        else if (TryToRational(this, out var n, out var d))
        {
            CheckBits(n, maxIntegerBits);
            CheckBits(d, maxIntegerBits);
        }
    }

    /// <summary>
    /// Creates a reduced Rational, or an Integer when the reduced denominator is one.
    /// </summary>
    public static MathValue FromFraction(BigInteger numerator, BigInteger denominator) =>
        FromRational(numerator, denominator);

    /// <summary>
    /// Creates a reduced Rational, or an Integer when the reduced denominator is one.
    /// </summary>
    public static MathValue FromRational(BigInteger num, BigInteger den) =>
        FromExactFraction(num, den, false);

    internal static MathValue FromExactFraction(BigInteger num, BigInteger den, bool decimalDomain)
    {
        CheckBits(num, CalculationLimits.MaxIntermediateBits);
        CheckBits(den, CalculationLimits.MaxIntermediateBits);
        if (den.IsZero)
        {
            throw new DivideByZeroException();
        }

        if (den.Sign < 0)
        {
            num = -num;
            den = -den;
        }

        var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(num), den);
        num /= gcd;
        den /= gcd;
        if (decimalDomain && TryExactDecimal(num, den, out var dec))
        {
            var value = new MathValue(dec).WithDecimalDisplayScale(0);
            value.ValidateLimits(CalculationLimits.MaxIntegerBits);
            return value;
        }

        if (den.IsOne)
        {
            CheckBits(num, CalculationLimits.MaxIntegerBits);
            return new MathValue(num);
        }

        CheckBits(num, CalculationLimits.MaxIntegerBits);
        CheckBits(den, CalculationLimits.MaxIntegerBits);

        return new MathValue(num, den);
    }

    /// <summary>
    /// Gets the reduced verified exact fraction, or null when finite Exact provenance is
    /// unavailable.
    /// </summary>
    public (BigInteger Numerator, BigInteger Denominator)? ExactFraction =>
        TryGetExactFraction(out var num, out var den) ? (num, den) : null;

    /// <summary>
    /// Gets the reduced fraction when the finite value has Exact calculation provenance.
    /// </summary>
    /// <returns>The verified exact numerator and positive denominator in lowest terms.
    /// </returns>
    /// <exception cref="InvalidOperationException">The value has no verified exact
    /// fractional representation.</exception>
    public (BigInteger Numerator, BigInteger Denominator) GetExactFraction() =>
        ExactFraction ?? throw new InvalidOperationException(
            "The value does not have a verified exact fractional representation.");

    /// <summary>
    /// Returns an exact stored fraction only when finite calculation provenance is
    /// explicitly trusted.
    /// </summary>
    public bool TryGetExactFraction(out BigInteger num, out BigInteger den)
    {
        if (Accuracy == CalculationAccuracy.Exact && IsFinite)
        {
            NumericFraction(this, out num, out den);
            return true;
        }

        num = default;
        den = default;

        return false;
    }

    internal static bool TryToRational(
        MathValue value, out BigInteger num, out BigInteger den)
    {
        switch (value.Kind)
        {
            case DataType.Integer:
                num = value.IntegerValue;
                den = BigInteger.One;
                return true;

            case DataType.Decimal:
                (num, den) = ToReducedFraction(value.DecimalValue);
                return true;

            case DataType.Rational:
                num = value.numerator;
                den = value.denominator;
                return true;

            default:
                num = default;
                den = default;
                return false;
        }
    }

    /// <summary>
    /// Explicitly converts to a double, rounding once from exact fractions.
    /// </summary>
    public double ToDouble() =>
        TryToRational(this, out var n, out var d) ? DivideIntegersAsDouble(n, d) : DoubleValue;

    /// <summary>
    /// Compatibility alias for explicit double conversion.
    /// </summary>
    public double AsDouble() => ToDouble();

    /// <summary>
    /// Explicitly converts to decimal, rounding midpoint-to-even or throwing on overflow.
    /// </summary>
    public decimal ToDecimal()
    {
        if (Kind == DataType.Decimal)
        {
            return DecimalValue;
        }

        if (Kind == DataType.Double)
        {
            return checked((decimal)DoubleValue);
        }

        TryToRational(this, out var n, out var d);
        if (TryExactDecimal(n, d, out var exact))
        {
            return exact;
        }

        var abs = BigInteger.Abs(n);
        var scaled = abs * BigInteger.Pow(10, 28);
        for (int scale = 28; scale >= 0; scale--)
        {
            var q = BigInteger.DivRem(scaled, d, out var remainder);
            int comparison = (remainder * 2).CompareTo(d);
            if (comparison > 0 || (comparison == 0 && !q.IsEven))
            {
                q++;
            }

            if (q <= MaxDecimalMantissa)
            {
                return MakeDecimal(q, n.Sign < 0, scale);
            }

            scaled /= 10;
        }

        throw new OverflowException("Value exceeds the decimal range.");
    }

    /// <summary>
    /// Whether the value is finite.
    /// </summary>
    public bool IsFinite => Kind != DataType.Double || double.IsFinite(DoubleValue);

    /// <summary>
    /// Whether the value is NaN.
    /// </summary>
    public bool IsNaN => Kind == DataType.Double && double.IsNaN(DoubleValue);

    /// <summary>
    /// Whether the value is numerically zero.
    /// </summary>
    public bool IsZero => Kind switch
    {
        DataType.Integer => IntegerValue.IsZero,
        DataType.Decimal => DecimalValue == 0,
        DataType.Rational => numerator.IsZero,
        _ => DoubleValue == 0,
    };

    private bool IsNegative => Kind switch
    {
        DataType.Integer => IntegerValue.Sign < 0,
        DataType.Decimal => DecimalValue < 0,
        DataType.Rational => numerator.Sign < 0,
        _ => DoubleValue < 0,
    };

    /// <summary>
    /// Extracts a finite integral value without inferring exact provenance.
    /// </summary>
    public bool TryGetInteger(out BigInteger value)
    {
        if (TryToRational(this, out var n, out var d))
        {
            value = BigInteger.DivRem(n, d, out var remainder);
            if (remainder.IsZero) return true;
        }
        else if (double.IsFinite(DoubleValue) && double.IsInteger(DoubleValue))
        {
            value = new BigInteger(DoubleValue);
            return true;
        }

        value = default;

        return false;
    }

    /// <summary>
    /// Formats the active representation without silent double conversion.
    /// </summary>
    public override string ToString() => ToString(null, null);

    /// <summary>
    /// Formats with the current culture.
    /// </summary>
    public string ToString(string? format) => ToString(format, null);

    /// <summary>
    /// Formats with the specified provider.
    /// </summary>
    public string ToString(IFormatProvider? formatProvider) => ToString(null, formatProvider);

    /// <summary>
    /// Formats the value. Rationals support general numeric, fixed/grouped decimal, and
    /// exact fraction ("R") formats.
    /// </summary>
    public string ToString(string? format, IFormatProvider? formatProvider) => Kind switch
    {
        DataType.Integer => IntegerValue.ToString(format, formatProvider),
        DataType.Decimal => DecimalValue.ToString(format, formatProvider),
        DataType.Double => DoubleValue.ToString(format, formatProvider),
        DataType.Rational => FormatRational(format, formatProvider),
        _ => throw new InvalidOperationException("Unknown representation."),
    };

    /// <summary>
    /// Writes formatted characters when the destination is large enough.
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        bool success;
        switch (Kind)
        {
            case DataType.Integer:
                success = IntegerValue.TryFormat(destination, out charsWritten, format, provider);
                break;

            case DataType.Decimal:
                success = DecimalValue.TryFormat(destination, out charsWritten, format, provider);
                break;

            case DataType.Double:
                success = DoubleValue.TryFormat(destination, out charsWritten, format, provider);
                break;

            case DataType.Rational:
                string text = ToString(format.IsEmpty ? null : format.ToString(), provider);
                success = text.AsSpan().TryCopyTo(destination);
                charsWritten = success ? text.Length : 0;
                break;

            default:
                throw new InvalidOperationException("Unknown representation.");
        }

        if (!success)
        {
            charsWritten = 0;
        }

        return success;
    }

    /// <summary>
    /// Writes formatted UTF-8 when the destination is large enough.
    /// </summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        bool success = Kind switch
        {
            DataType.Integer => TryFormatIntegerUtf8(utf8Destination, out bytesWritten, format, provider),
            DataType.Decimal => DecimalValue.TryFormat(utf8Destination, out bytesWritten, format, provider),
            DataType.Double => DoubleValue.TryFormat(utf8Destination, out bytesWritten, format, provider),
            DataType.Rational => Encoding.UTF8.TryGetBytes(ToString(format.IsEmpty ? null : format.ToString(), provider), utf8Destination, out bytesWritten),
            _ => throw new InvalidOperationException("Unknown representation."),
        };

        if (!success)
        {
            bytesWritten = 0;
        }

        return success;
    }

    private bool TryFormatIntegerUtf8(Span<byte> destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        // BigInteger has no native UTF-8 formatter. Its character formatter keeps
        // common values allocation-free; larger outputs use pooled storage, not strings.
        Span<char> buffer = stackalloc char[256];
        if (IntegerValue.TryFormat(buffer, out int charsWritten, format, provider))
        {
            return Encoding.UTF8.TryGetBytes(buffer[..charsWritten], destination, out bytesWritten);
        }

        int size = Math.Min(512, destination.Length);
        while (size > buffer.Length)
        {
            char[] rented = ArrayPool<char>.Shared.Rent(size);
            try
            {
                if (IntegerValue.TryFormat(rented.AsSpan(0, size), out charsWritten, format, provider))
                {
                    return Encoding.UTF8.TryGetBytes(rented.AsSpan(0, charsWritten), destination, out bytesWritten);
                }
            }
            finally
            {
                ArrayPool<char>.Shared.Return(rented);
            }

            if (size == destination.Length)
            {
                break;
            }

            size = size <= destination.Length / 2 ? size * 2 : destination.Length;
        }

        // UTF-8 always needs at least as many bytes as UTF-16 code units.
        bytesWritten = 0;

        return false;
    }

    /// <summary>
    /// A lossless invariant numeric expression for finite values, including the exact
    /// stored binary value of doubles.
    /// </summary>
    /// <remarks>
    /// Decimal display scale is retained where an invariant decimal literal can express
    /// it. No display rounding is applied. NaN and infinities use diagnostic tokens, not
    /// re-evaluable expressions. Use <see cref="Serialize"/> to preserve representation,
    /// provenance, and all display metadata.
    /// </remarks>
    public string ToInvariantExpression()
    {
        // Doubles: maintain the exact dyadic fraction/integer representation.
        if (Kind is DataType.Double)
        {
            if (!IsFinite)
            {
                return DoubleValue.ToString(CultureInfo.InvariantCulture);
            }

            NumericFraction(this, out var num, out var den);
            return den.IsOne ?
                num.ToString(CultureInfo.InvariantCulture) :
                num.ToString(CultureInfo.InvariantCulture) + "/" + den.ToString(CultureInfo.InvariantCulture);
        }

        int scale = DecimalDisplayScale < MaxDecimalDisplayScale ? DecimalDisplayScale : 0;

        // Fixed fraction formatting only applies when explicit decimal scale was
        // requested (e.g. from 1.00/3*3).
        if (Kind is DataType.Rational &&
            scale > 0 &&
            BigInteger.Pow(10, scale) % denominator == 0)
        {
            return FormatFixedRational("F" + scale.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }

        if (Kind is DataType.Integer && scale > 0)
        {
            return IntegerValue.ToString("F" + scale.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }

        // Stored decimal: format directly (padding if scale > stored scale).
        if (Kind is DataType.Decimal && scale > DecomposeDecimal(DecimalValue).Scale)
        {
            return DecimalValue.ToString("F" + scale.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }

        // Lossless stored representation:
        // Rationals emit "numerator/denominator" with no rounding (e.g. "1/2", "7/8", "1/3").
        // Decimals emit standard invariant decimal (e.g. "1.25", "0.125", "0.3333333333333333").
        // Integers emit integer string (e.g. "42", "1000", "-12345678901234567890").
        return InvariantStoredValue();
    }

    private string InvariantStoredValue() => Kind == DataType.Rational ?
        numerator.ToString(CultureInfo.InvariantCulture) + "/" + denominator.ToString(CultureInfo.InvariantCulture) :
        ToString(Kind == DataType.Double ? "R" : null, CultureInfo.InvariantCulture);

    private string FormatRational(string? format, IFormatProvider? provider)
    {
        if (string.Equals(format, "R", StringComparison.OrdinalIgnoreCase))
        {
            return numerator.ToString(provider) + "/" + denominator.ToString(provider);
        }

        if (!string.IsNullOrEmpty(format) && char.ToUpperInvariant(format[0]) is 'F' or 'N')
        {
            return FormatFixedRational(format, provider);
        }

        if (!string.IsNullOrEmpty(format) &&
            !string.Equals(format, "G", StringComparison.OrdinalIgnoreCase))
        {
            if (char.ToUpperInvariant(format[0]) != 'G' || !int.TryParse(format.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int digits))
            {
                throw new FormatException("Rational values support general numeric (G), fixed (F), grouped (N), or exact fraction (R) formatting.");
            }

            return ResultValueFormatter.Format(this, new ResultFormatOptions { SignificantDigits = digits }, provider).Text;
        }

        return ResultValueFormatter.Format(this, new ResultFormatOptions(), provider).Text;
    }

    private string FormatFixedRational(string format, IFormatProvider? provider)
    {
        var culture = NumberFormatInfo.GetInstance(provider);
        int digits = culture.NumberDecimalDigits;
        if (format.Length > 1 && !int.TryParse(format.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out digits))
        {
            throw new FormatException("Invalid decimal precision.");
        }

        if (digits < 0 || digits > 1000)
        {
            throw new CalculationLimitException();
        }

        var scale = BigInteger.Pow(10, digits);
        var scaled = Product(BigInteger.Abs(numerator), scale);
        var coefficient = BigInteger.DivRem(scaled, denominator, out var remainder);
        int midpoint = (remainder << 1).CompareTo(denominator);

        if (midpoint > 0 || (midpoint == 0 && !coefficient.IsEven))
        {
            coefficient++;
        }

        var integer = BigInteger.DivRem(coefficient, scale, out var fraction);
        string whole = integer.ToString(CultureInfo.InvariantCulture);
        string sign = numerator.Sign < 0 ? culture.NegativeSign : "";
        bool grouped = char.ToUpperInvariant(format[0]) == 'N';
        int groups = grouped ? ResultValueFormatter.GetGroupSeparatorCount(whole.Length, culture) : 0;

        long outputLength = (long)sign.Length + whole.Length + (long)groups * culture.NumberGroupSeparator.Length
            + (digits > 0 ? (long)culture.NumberDecimalSeparator.Length + digits : 0);
        if (outputLength > CalculationLimits.MaxOutputCharacters)
        {
            throw new CalculationLimitException();
        }

        if (grouped)
        {
            whole = integer.ToString("N0", culture);
        }

        string result = digits == 0
            ? sign + whole
            : sign + whole + culture.NumberDecimalSeparator + fraction.ToString("D" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        ResultValueFormatter.CheckOutputLength(result, CalculationLimits.MaxOutputCharacters);

        return result;
    }

    /// <summary>
    /// A sign-magnitude hexadecimal integer, or null for nonintegral values.
    /// </summary>
    public string? ToHexString(bool prefix = true) => RadixString("X", "0x", prefix);

    /// <summary>
    /// A sign-magnitude binary integer, or null for nonintegral values.
    /// </summary>
    public string? ToBinaryString(bool prefix = true) => RadixString("B", "0b", prefix);

    /// <summary>
    /// A sign-magnitude octal integer, or null for nonintegral values.
    /// </summary>
    public string? ToOctalString(bool prefix = true) => RadixString(null, "0o", prefix);

    private string? RadixString(string? format, string prefixText, bool prefix)
    {
        if (!TryGetInteger(out var n))
        {
            return null;
        }

        string digits;
        if (format is not null)
        {
            digits = BigInteger.Abs(n).ToString(format, CultureInfo.InvariantCulture).TrimStart('0');
            if (digits.Length == 0)
            {
                digits = "0";
            }
        }
        else
        {
            var value = BigInteger.Abs(n);
            var builder = new StringBuilder();
            do
            {
                value = BigInteger.DivRem(value, 8, out var remainder);
                builder.Append((char)('0' + (int)remainder));
            } while (!value.IsZero);
            char[] chars = builder.ToString().ToCharArray();
            Array.Reverse(chars);
            digits = new string(chars);
        }

        return (n.Sign < 0 ? "-" : "") + (prefix ? prefixText : "") + digits;
    }

    /// <summary>
    /// Serializes representation, value, provenance, and decimal display scale without
    /// precision loss.
    /// </summary>
    public string Serialize() => JsonSerializer.Serialize(new SerializedValue(
        Kind, Accuracy, InvariantStoredValue(),
        Kind == DataType.Rational ? numerator.ToString(CultureInfo.InvariantCulture) : null,
        Kind == DataType.Rational ? denominator.ToString(CultureInfo.InvariantCulture) : null,
        DecimalDisplayScale),
        SerializationContext.Default.SerializedValue);

    /// <summary>
    /// Deserializes and validates representation, value, provenance, and numeric limits.
    /// </summary>
    public static MathValue Deserialize(string json, int maxIntegerBits = CalculationLimits.MaxIntegerBits)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > 25000)
        {
            throw new CalculationLimitException();
        }

        var dto = JsonSerializer.Deserialize(json, SerializationContext.Default.SerializedValue) ??
            throw new FormatException("Missing numeric value.");
        if (dto.Value is null)
        {
            throw new FormatException("Missing numeric value.");
        }

        var culture = CultureInfo.InvariantCulture;
        MathValue value = dto.Kind switch
        {
            DataType.Integer => new MathValue(ParseBoundedInteger(dto.Value, maxIntegerBits)),
            DataType.Decimal => new MathValue(decimal.Parse(dto.Value, NumberStyles.Float, culture)),
            DataType.Double => new MathValue(double.Parse(dto.Value, NumberStyles.Float, culture)),
            DataType.Rational => FromFraction(
                ParseBoundedInteger(dto.Numerator ?? throw new FormatException("Missing numerator."), CalculationLimits.MaxIntermediateBits),
                ParseBoundedInteger(dto.Denominator ?? throw new FormatException("Missing denominator."), CalculationLimits.MaxIntermediateBits)),
            _ => throw new FormatException("Unknown numeric representation."),
        };

        if (value.Kind != dto.Kind || value.InvariantStoredValue() != dto.Value)
        {
            throw new FormatException("Noncanonical numeric representation.");
        }

        if (dto.DecimalDisplayScale is int displayScale)
        {
            if (displayScale < 0 || displayScale > MaxDecimalDisplayScale ||
                (value.Kind == DataType.Double && displayScale != 0))
            {
                throw new FormatException("Invalid decimal display scale.");
            }

            value = value.WithDecimalDisplayScale(displayScale);
        }

        value = value.WithAccuracy(dto.Accuracy);
        value.ValidateLimits(maxIntegerBits);
        return value;
    }

    private sealed record SerializedValue(
        [property: JsonRequired] DataType Kind,
        [property: JsonRequired] CalculationAccuracy Accuracy,
        [property: JsonRequired] string Value,
        string? Numerator,
        string? Denominator,
        int? DecimalDisplayScale = null);

    [JsonSerializable(typeof(SerializedValue))]
    private sealed partial class SerializationContext : JsonSerializerContext;

    private static BigInteger ParseBoundedInteger(string text, int bits)
    {
        if (text.Length > (int)(bits * 0.30103) + 3)
        {
            throw new CalculationLimitException();
        }

        var result = BigInteger.Parse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        CheckBits(result, bits);

        return result;
    }

    /// <summary>
    /// Negates a value, preserving provenance.
    /// </summary>
    public static MathValue operator -(MathValue value) => Negate(value);

    /// <summary>
    /// Negates a value after validating limits, preserving provenance.
    /// </summary>
    public static MathValue Negate(MathValue value)
    {
        value.ValidateLimits();
        if (TryToRational(value, out var n, out _))
        {
            CheckBits(n, CalculationLimits.MaxIntermediateBits);
        }

        var result = (value.Kind switch
        {
            DataType.Integer => new MathValue(-value.IntegerValue),
            DataType.Decimal => new MathValue(-value.DecimalValue),
            DataType.Rational => new MathValue(-value.numerator, value.denominator),
            _ => new MathValue(-value.DoubleValue),
        }).WithAccuracy(value.Accuracy).WithDecimalDisplayScale(value.DecimalDisplayScale);

        result.ValidateLimits();

        return result;
    }

    /// <summary>
    /// Adds values under default limits.
    /// </summary>
    public static MathValue operator +(MathValue a, MathValue b) => Add(a, b);

    /// <summary>
    /// Subtracts values under default limits.
    /// </summary>
    public static MathValue operator -(MathValue a, MathValue b) => Subtract(a, b);

    /// <summary>
    /// Multiplies values under default limits.
    /// </summary>
    public static MathValue operator *(MathValue a, MathValue b) => Multiply(a, b);

    /// <summary>
    /// Divides values under default limits.
    /// </summary>
    public static MathValue operator /(MathValue a, MathValue b) => Divide(a, b);

    /// <summary>
    /// Computes a truncation-based remainder under default limits.
    /// </summary>
    public static MathValue operator %(MathValue a, MathValue b) => Modulo(a, b);

    /// <summary>
    /// Adds values using exact fractions unless a double is involved.
    /// </summary>
    public static MathValue Add(MathValue a, MathValue b) => Arithmetic(a, b, '+');

    /// <summary>
    /// Subtracts values using exact fractions unless a double is involved.
    /// </summary>
    public static MathValue Subtract(MathValue a, MathValue b) => Arithmetic(a, b, '-');

    /// <summary>
    /// Multiplies values with cancellation before bounded intermediate products.
    /// </summary>
    public static MathValue Multiply(MathValue a, MathValue b) => Arithmetic(a, b, '*');

    /// <summary>
    /// Divides values with cancellation before bounded intermediate products.
    /// </summary>
    public static MathValue Divide(MathValue a, MathValue b) => Arithmetic(a, b, '/');

    /// <summary>
    /// Computes the exact remainder with the sign of the dividend.
    /// </summary>
    public static MathValue Modulo(MathValue a, MathValue b) => Arithmetic(a, b, '%');

    private static MathValue Arithmetic(MathValue a, MathValue b, char operation)
    {
        a.ValidateLimits();
        b.ValidateLimits();
        if ((operation is '/' or '%') && b.IsZero)
        {
            throw new DivideByZeroException();
        }

        if (a.Kind == DataType.Double || b.Kind == DataType.Double)
        {
            double x = a.ToDouble(), y = b.ToDouble();
            return new MathValue(operation switch
            {
                '+' => x + y, '-' => x - y, '*' => x * y,
                '/' => DivideMixedAsDouble(a, b), '%' => x % y,
                _ => throw new InvalidOperationException(),
            });
        }

        TryToRational(a, out var an, out var ad);
        TryToRational(b, out var bn, out var bd);
        BigInteger n, d;
        if (operation is '+' or '-' or '%')
        {
            var gcd = BigInteger.GreatestCommonDivisor(ad, bd);
            var left = Product(an, bd / gcd);
            var right = Product(bn, ad / gcd);
            d = Product(ad, bd / gcd);
            n = operation == '%' ? left % right : operation == '+' ? left + right : left - right;
            CheckBits(n, CalculationLimits.MaxIntermediateBits);
        }
        else
        {
            if (operation == '/')
            {
                (bn, bd) = (bd, bn);
            }

            var first = BigInteger.GreatestCommonDivisor(BigInteger.Abs(an), BigInteger.Abs(bd));
            var second = BigInteger.GreatestCommonDivisor(BigInteger.Abs(bn), ad);
            n = Product(an / first, bn / second);
            d = Product(ad / second, bd / first);
        }

        bool decimalDomain = (a.Kind == DataType.Decimal || b.Kind == DataType.Decimal) &&
            a.Kind != DataType.Rational && b.Kind != DataType.Rational;
        var result = FromExactFraction(n, d, decimalDomain);
        int scale = operation switch
        {
            '*' => a.DecimalDisplayScale + b.DecimalDisplayScale,
            '/' => a.DecimalDisplayScale == MaxDecimalDisplayScale ? MaxDecimalDisplayScale
                : Math.Max(0, a.DecimalDisplayScale - b.DecimalDisplayScale),
            _ => Math.Max(a.DecimalDisplayScale, b.DecimalDisplayScale),
        };

        return result.WithAccuracy(InheritedAccuracy(a, b))
            .WithDecimalDisplayScale(Math.Max(result.DecimalDisplayScale, scale));
    }

    internal static BigInteger Product(BigInteger a, BigInteger b, int maxIntermediateBits = CalculationLimits.MaxIntermediateBits)
    {
        if (a.IsZero || b.IsZero)
        {
            return BigInteger.Zero;
        }

        // The lower bound is checked before allocation; the product can exceed it by at most one bit.
        if (BigInteger.Abs(a).GetBitLength() + BigInteger.Abs(b).GetBitLength() - 1 > maxIntermediateBits)
        {
            throw new CalculationLimitException();
        }

        var result = a * b;
        CheckBits(result, maxIntermediateBits);

        return result;
    }

    /// <summary>
    /// Computes integer powers exactly; fractional powers are an unverified double
    /// boundary.
    /// </summary>
    public static MathValue Pow(
        MathValue baseVal, MathValue expVal, int maxExponentMagnitude = CalculationLimits.MaxExponentMagnitude)
    {
        baseVal.ValidateLimits();
        expVal.ValidateLimits();

        bool integral = expVal.TryGetInteger(out var exponent);
        if (integral && BigInteger.Abs(exponent) > maxExponentMagnitude)
        {
            throw new CalculationLimitException();
        }

        if (baseVal.IsZero && expVal.IsFinite && expVal.IsNegative)
        {
            throw new DivideByZeroException("Cannot raise zero to a negative power.");
        }

        if (integral && baseVal.Kind != DataType.Double && expVal.Kind != DataType.Double)
        {
            TryToRational(baseVal, out var n, out var d);
            if (exponent.Sign < 0)
            {
                (n, d) = (d, n);
            }

            int e = (int)BigInteger.Abs(exponent);
            bool decimalDomain = baseVal.Kind != DataType.Rational
                && (baseVal.Kind == DataType.Decimal || expVal.Kind == DataType.Decimal);

            CheckPower(n, e, CalculationLimits.MaxIntegerBits);
            CheckPower(d, e, CalculationLimits.MaxIntegerBits);

            var result = FromExactFraction(BoundedPower(n, e), BoundedPower(d, e), decimalDomain);
            long scale = exponent.Sign > 0 ? (long)baseVal.DecimalDisplayScale * e : 0;

            return result
                .WithAccuracy(InheritedAccuracy(baseVal, expVal))
                .WithDecimalDisplayScale(Math.Max(result.DecimalDisplayScale, scale));
        }

        if (baseVal.IsFinite && baseVal.IsNegative && expVal.IsFinite && !integral)
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.UndefinedMathResult,
                "Negative base with a fractional exponent has no real solution."));
        }

        double x = baseVal.ToDouble(), y = expVal.ToDouble();
        return new MathValue(Math.Pow(x, y));
    }

    private static void CheckPower(BigInteger value, int exponent, int bits)
    {
        var abs = BigInteger.Abs(value);
        if (abs > BigInteger.One && (abs.GetBitLength() - 1) * exponent + 1 > bits)
        {
            throw new CalculationLimitException();
        }
    }

    private static BigInteger BoundedPower(BigInteger value, int exponent, int maxIntermediateBits = CalculationLimits.MaxIntermediateBits)
    {
        BigInteger result = BigInteger.One;
        while (exponent > 0)
        {
            if ((exponent & 1) != 0)
            {
                result = Product(result, value, maxIntermediateBits);
            }

            exponent >>= 1;
            if (exponent != 0)
            {
                value = Product(value, value, maxIntermediateBits);
            }
        }

        return result;
    }

    /// <summary>
    /// Orders numerical values exactly, with NaN below all other values.
    /// </summary>
    public static int Compare(MathValue a, MathValue b)
    {
        a.ValidateLimits();
        b.ValidateLimits();
        if (a.IsNaN)
        {
            return b.IsNaN ? 0 : -1;
        }

        if (b.IsNaN)
        {
            return 1;
        }

        if (!a.IsFinite)
        {
            return !b.IsFinite ? a.DoubleValue.CompareTo(b.DoubleValue) : a.DoubleValue > 0 ? 1 : -1;
        }

        if (!b.IsFinite)
        {
            return b.DoubleValue > 0 ? -1 : 1;
        }

        NumericFraction(a, out var an, out var ad);
        NumericFraction(b, out var bn, out var bd);
        var commonDenominator = BigInteger.GreatestCommonDivisor(ad, bd);

        return Product(an, bd / commonDenominator)
            .CompareTo(Product(bn, ad / commonDenominator));
    }

    /// <inheritdoc />
    public int CompareTo(MathValue other) => Compare(this, other);

    /// <inheritdoc />
    int IComparable.CompareTo(object? obj) => obj is null ?
        1 :
        obj is MathValue value ? Compare(this, value) : throw new ArgumentException("Object must be a MathValue.", nameof(obj));

    /// <summary>
    /// Numerical equality ignoring provenance, including reflexive NaN object equality.
    /// </summary>
    public bool Equals(MathValue other) => Compare(this, other) == 0;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is MathValue value && Equals(value);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        if (IsNaN)
        {
            return typeof(MathValue).GetHashCode();
        }

        if (!IsFinite)
        {
            return DoubleValue.GetHashCode();
        }

        NumericFraction(this, out var n, out var d);

        return d.IsOne ? n.GetHashCode() : HashCode.Combine(n, d);
    }

    /// <summary>
    /// Tests object equality.
    /// </summary>
    public static bool operator ==(MathValue left, MathValue right) => left.Equals(right);

    /// <summary>
    /// Tests object inequality.
    /// </summary>
    public static bool operator !=(MathValue left, MathValue right) => !left.Equals(right);

    /// <summary>
    /// Tests numerical ordering.
    /// </summary>
    public static bool operator <(MathValue left, MathValue right) => Compare(left, right) < 0;

    /// <summary>
    /// Tests numerical ordering.
    /// </summary>
    public static bool operator <=(MathValue left, MathValue right) => Compare(left, right) <= 0;

    /// <summary>
    /// Tests numerical ordering.
    /// </summary>
    public static bool operator >(MathValue left, MathValue right) => Compare(left, right) > 0;

    /// <summary>
    /// Tests numerical ordering.
    /// </summary>
    public static bool operator >=(MathValue left, MathValue right) => Compare(left, right) >= 0;

    /// <summary>
    /// Expression equality, false for NaN, with inherited provenance.
    /// </summary>
    public static MathValue AreEqual(MathValue a, MathValue b) => 
        Boolean(!a.IsNaN && !b.IsNaN && Compare(a, b) == 0, a, b);

    /// <summary>
    /// Expression inequality, true for NaN, with inherited provenance.
    /// </summary>
    public static MathValue AreNotEqual(MathValue a, MathValue b) => 
        Boolean(a.IsNaN || b.IsNaN || Compare(a, b) != 0, a, b);

    /// <summary>
    /// Expression ordering, false for NaN, with inherited provenance.
    /// </summary>
    public static MathValue LessThan(MathValue a, MathValue b) =>
        Boolean(!a.IsNaN && !b.IsNaN && Compare(a, b) < 0, a, b);

    /// <summary>
    /// Expression ordering, false for NaN, with inherited provenance.
    /// </summary>
    public static MathValue LessThanOrEqual(MathValue a, MathValue b) =>
        Boolean(!a.IsNaN && !b.IsNaN && Compare(a, b) <= 0, a, b);

    /// <summary>
    /// Expression ordering, false for NaN, with inherited provenance.
    /// </summary>
    public static MathValue GreaterThan(MathValue a, MathValue b) =>
        Boolean(!a.IsNaN && !b.IsNaN && Compare(a, b) > 0, a, b);

    /// <summary>
    /// Expression ordering, false for NaN, with inherited provenance.
    /// </summary>
    public static MathValue GreaterThanOrEqual(MathValue a, MathValue b) =>
        Boolean(!a.IsNaN && !b.IsNaN && Compare(a, b) >= 0, a, b);

    /// <summary>
    /// Logical AND; zero is false, including inherited provenance.
    /// </summary>
    public static MathValue LogicalAnd(MathValue a, MathValue b) =>
        Boolean(!a.IsZero && !b.IsZero, a, b);

    /// <summary>
    /// Logical OR; zero is false, including inherited provenance.
    /// </summary>
    public static MathValue LogicalOr(MathValue a, MathValue b) =>
        Boolean(!a.IsZero || !b.IsZero, a, b);

    /// <summary>
    /// Logical XOR; zero is false, including inherited provenance.
    /// </summary>
    public static MathValue LogicalXor(MathValue a, MathValue b) =>
        Boolean(!a.IsZero ^ !b.IsZero, a, b);

    /// <summary>
    /// Logical NOT; zero is false, including inherited provenance.
    /// </summary>
    public static MathValue LogicalNot(MathValue a)
    {
        a.ValidateLimits();
        var result = new MathValue(a.IsZero ? 1 : 0).WithAccuracy(a.Accuracy);
        result.ValidateLimits();

        return result;
    }

    private static MathValue Boolean(bool value, MathValue a, MathValue b)
    {
        a.ValidateLimits();
        b.ValidateLimits();
        var result = new MathValue(value ? 1 : 0).WithAccuracy(InheritedAccuracy(a, b));
        result.ValidateLimits();

        return result;
    }

    private static CalculationAccuracy InheritedAccuracy(MathValue a, MathValue b) =>
        a.Accuracy == CalculationAccuracy.Exact && b.Accuracy == CalculationAccuracy.Exact ? CalculationAccuracy.Exact : CalculationAccuracy.Unverified;

    /// <summary>
    /// Bitwise AND of checked integral operands.
    /// </summary>
    public static MathValue BitwiseAnd(MathValue a, MathValue b) =>
        IntegerOperation(a, b, '&');

    /// <summary>
    /// Bitwise OR of checked integral operands.
    /// </summary>
    public static MathValue BitwiseOr(MathValue a, MathValue b) =>
        IntegerOperation(a, b, '|');

    /// <summary>
    /// Bitwise XOR of checked integral operands.
    /// </summary>
    public static MathValue BitwiseXor(MathValue a, MathValue b) =>
        IntegerOperation(a, b, '^');

    /// <summary>
    /// Bounded left shift of checked integral operands.
    /// </summary>
    public static MathValue ShiftLeft(MathValue a, MathValue b) =>
        IntegerOperation(a, b, '<');

    /// <summary>
    /// Sign-extending right shift of checked integral operands.
    /// </summary>
    public static MathValue ShiftRight(MathValue a, MathValue b) =>
        IntegerOperation(a, b, '>');

    /// <summary>
    /// Bitwise complement of a checked integral operand.
    /// </summary>
    public static MathValue BitwiseNot(MathValue a)
    {
        a.ValidateLimits();
        if (!a.TryGetInteger(out var n))
        {
            throw IntegerRequired();
        }

        CheckBits(n, CalculationLimits.MaxIntegerBits);
        var magnitude = BigInteger.Abs(n);
        long bits = magnitude.GetBitLength();
        long resultBits = n.Sign >= 0
            ? bits + (BigInteger.PopCount(n) == bits ? 1 : 0)
            : bits - (BigInteger.IsPow2(magnitude) ? 1 : 0);
        CheckIntegerResultBits(resultBits);
        var result = new MathValue(~n).WithAccuracy(a.Accuracy);
        result.ValidateLimits();

        return result;
    }

    private static MathValue IntegerOperation(MathValue a, MathValue b, char operation)
    {
        a.ValidateLimits();
        b.ValidateLimits();
        if (!a.TryGetInteger(out var n) || !b.TryGetInteger(out var m))
        {
            throw IntegerRequired();
        }

        CheckBits(n, CalculationLimits.MaxIntegerBits);
        if (operation is '<' or '>')
        {
            if (m.Sign < 0)
            {
                throw new ExpressionException(new EvaluationDiagnostic(
                    EvaluationError.ArgumentOutOfRange, "Shift count cannot be negative."));
            }

            if (n.IsZero)
            {
                return new MathValue(0).WithAccuracy(InheritedAccuracy(a, b));
            }

            long bits = BigInteger.Abs(n).GetBitLength();
            if (operation == '<')
            {
                if (m > Math.Min(CalculationLimits.MaxIntegerBits, CalculationLimits.MaxIntermediateBits))
                {
                    throw new CalculationLimitException();
                }

                CheckIntegerResultBits(bits + (int)m);
            }
            else
            {
                m = BigInteger.Min(m, bits);
                long resultBits = Math.Max(n.Sign < 0 ? 1 : 0, bits - (int)m);
                CheckIntegerResultBits(resultBits);
                if (n.Sign < 0 && m > 0 && m < bits)
                {
                    // A negative arithmetic shift rounds its magnitude upward. Detect
                    // the extra bit before allocating the final shifted BigInteger.
                    var magnitude = BigInteger.Abs(n);
                    if (BigInteger.PopCount(magnitude >> (int)m) == resultBits &&
                        BigInteger.TrailingZeroCount(magnitude) < m)
                    {
                        CheckIntegerResultBits(resultBits + 1);
                    }
                }
            }
        }
        else
        {
            CheckBits(m, CalculationLimits.MaxIntegerBits);
            CheckIntegerResultBits(BitwiseResultBits(n, m, operation));
        }

        var value = operation switch
        {
            '&' => n & m, '|' => n | m, '^' => n ^ m,
            '<' => n << (int)m, '>' => n >> (int)m,
            _ => throw new InvalidOperationException(),
        };

        CheckBits(value, CalculationLimits.MaxIntegerBits);
        CheckBits(value, CalculationLimits.MaxIntermediateBits);

        return new MathValue(value).WithAccuracy(InheritedAccuracy(a, b));
    }

    private static void CheckIntegerResultBits(long bits)
    {
        if (bits > CalculationLimits.MaxIntegerBits || bits > CalculationLimits.MaxIntermediateBits)
        {
            throw new CalculationLimitException();
        }
    }

    private static int BitwiseResultBits(BigInteger n, BigInteger m, char operation)
    {
        int width = Math.Max(n.GetByteCount(), m.GetByteCount());
        Span<byte> left = stackalloc byte[width];
        Span<byte> right = stackalloc byte[width];
        left.Fill(n.Sign < 0 ? byte.MaxValue : (byte)0);
        right.Fill(m.Sign < 0 ? byte.MaxValue : (byte)0);
        n.TryWriteBytes(left, out _);
        m.TryWriteBytes(right, out _);

        bool negative = operation switch
        {
            '&' => n.Sign < 0 && m.Sign < 0,
            '|' => n.Sign < 0 || m.Sign < 0,
            '^' => (n.Sign < 0) != (m.Sign < 0),
            _ => throw new InvalidOperationException(),
        };

        int carry = negative ? 1 : 0, bits = 0;
        for (int i = 0; i < width; i++)
        {
            int raw = operation switch
            {
                '&' => left[i] & right[i],
                '|' => left[i] | right[i],
                '^' => left[i] ^ right[i],
                _ => throw new InvalidOperationException(),
            };

            int magnitude = negative ? ((~raw) & 255) + carry : raw;
            carry = magnitude >> 8;
            magnitude &= 255;
            if (magnitude != 0)
            {
                bits = i * 8 + 32 - BitOperations.LeadingZeroCount((uint)magnitude);
            }
        }

        return bits;
    }

    private static ExpressionException IntegerRequired() =>
        new(new EvaluationDiagnostic(EvaluationError.IntegerRequired, "Operation requires integer operands."));

    /// <summary>
    /// Bitwise complement.
    /// </summary>
    public static MathValue operator ~(MathValue value) => BitwiseNot(value);

    /// <summary>
    /// Bitwise AND.
    /// </summary>
    public static MathValue operator &(MathValue left, MathValue right) => BitwiseAnd(left, right);

    /// <summary>
    /// Bitwise OR.
    /// </summary>
    public static MathValue operator |(MathValue left, MathValue right) => BitwiseOr(left, right);

    /// <summary>
    /// Bitwise XOR.
    /// </summary>
    public static MathValue operator ^(MathValue left, MathValue right) => BitwiseXor(left, right);

    /// <summary>
    /// Bounded left shift.
    /// </summary>
    public static MathValue operator <<(MathValue left, int right) => ShiftLeft(left, new MathValue(right));

    /// <summary>
    /// Arithmetic right shift.
    /// </summary>
    public static MathValue operator >>(MathValue left, int right) => ShiftRight(left, new MathValue(right));

    /// <summary>
    /// Converts an integer to a precise value.
    /// </summary>
    public static implicit operator MathValue(BigInteger value) => new(value);

    /// <summary>
    /// Converts an integer to a precise value.
    /// </summary>
    public static implicit operator MathValue(int value) => new(value);

    /// <summary>
    /// Converts an integer to a precise value.
    /// </summary>
    public static implicit operator MathValue(uint value) => new(value);

    /// <summary>
    /// Converts an integer to a precise value.
    /// </summary>
    public static implicit operator MathValue(long value) => new(value);

    /// <summary>
    /// Converts an integer to a precise value.
    /// </summary>
    public static implicit operator MathValue(ulong value) => new(value);

    /// <summary>
    /// Converts a decimal to a precise stored value.
    /// </summary>
    public static implicit operator MathValue(decimal value) => new(value);

    /// <summary>
    /// Converts a double to an unverified value.
    /// </summary>
    public static implicit operator MathValue(double value) => new(value);

    /// <summary>
    /// Checks if the value fits within the specified number of bits.
    /// </summary>
    private static void CheckBits(BigInteger value, int bits)
    {
        if (BigInteger.Abs(value).GetBitLength() > bits)
        {
            throw new CalculationLimitException();
        }
    }

    private static (BigInteger Coefficient, int Scale) DecomposeDecimal(decimal value)
    {
        Span<int> bits = stackalloc int[4];
        decimal.GetBits(value, bits);
        var coefficient =
            (BigInteger)(uint)bits[0] +
            ((BigInteger)(uint)bits[1] << 32) +
            ((BigInteger)(uint)bits[2] << 64);

        return (bits[3] < 0 ? -coefficient : coefficient, (bits[3] >> 16) & 0x7f);
    }

    private static (BigInteger Num, BigInteger Den) ToReducedFraction(decimal value)
    {
        var (n, scale) = DecomposeDecimal(value);
        var d = BigInteger.Pow(10, scale);
        var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(n), d);

        return (n / gcd, d / gcd);
    }

    /// <summary>
    /// Decodes a finite stored value exactly, including binary doubles, independently of
    /// provenance.
    /// </summary>
    /// <exception cref="ArgumentException">The value is not finite.</exception>
    internal static void NumericFraction(
        MathValue value, out BigInteger n, out BigInteger d)
    {
        if (!value.IsFinite)
        {
            throw new ArgumentException(
                "A stored fraction requires a finite value.", nameof(value));
        }

        if (TryToRational(value, out n, out d))
        {
            return;
        }

        long bits = BitConverter.DoubleToInt64Bits(value.DoubleValue);
        int exponent = (int)((bits >> 52) & 0x7ff);
        long mantissa = bits & 0x000f_ffff_ffff_ffff;
        n = exponent == 0 ? mantissa : mantissa | (1L << 52);
        int shift = exponent == 0 ? -1074 : exponent - 1075;
        if (bits < 0)
        {
            n = -n;
        }

        d = BigInteger.One;
        if (shift >= 0)
        {
            n <<= shift;
        }
        else
        {
            d <<= -shift;
        }

        var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(n), d);
        n /= gcd;
        d /= gcd;
    }

    internal static bool TryExactDecimal(
        BigInteger num, BigInteger den, out decimal result)
    {
        result = default;
        if (den.Sign <= 0)
        {
            return false;
        }

        if (num.IsZero)
        {
            return true;
        }

        var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(num), den);
        num /= gcd;
        den /= gcd;
        if (BigInteger.Abs(num) > MaxDecimalMantissa)
        {
            return false;
        }

        int twos = 0, fives = 0;
        while (den.IsEven && twos <= 28)
        {
            twos++; den >>= 1;
        }

        while (den % 5 == 0 && fives <= 28)
        {
            fives++; den /= 5;
        }

        int scale = Math.Max(twos, fives);
        if (!den.IsOne || scale > 28)
        {
            return false;
        }

        var mantissa = Product(
            Product(BigInteger.Abs(num), BigInteger.One << (scale - twos)),
            BoundedPower(5, scale - fives));
        if (mantissa > MaxDecimalMantissa)
        {
            return false;
        }

        result = MakeDecimal(mantissa, num.Sign < 0, scale);

        return true;
    }

    private static decimal MakeDecimal(BigInteger mantissa, bool negative, int scale) => new(
        (int)(uint)(mantissa & uint.MaxValue),
        (int)(uint)((mantissa >> 32) & uint.MaxValue),
        (int)(uint)((mantissa >> 64) & uint.MaxValue), negative, (byte)scale);

    private static double DivideMixedAsDouble(MathValue a, MathValue b)
    {
        if (a.IsNaN || b.IsNaN)
        {
            return double.NaN;
        }

        bool negative = double.IsNegative(a.ToDouble()) ^ double.IsNegative(b.ToDouble());
        if (!a.IsFinite && !b.IsFinite)
        {
            return double.NaN;
        }

        if (!b.IsFinite)
        {
            return negative ? -0.0 : 0.0;
        }

        if (!a.IsFinite)
        {
            return negative ? double.NegativeInfinity : double.PositiveInfinity;
        }

        NumericFraction(a, out var an, out var ad);
        NumericFraction(b, out var bn, out var bd);
        var first = BigInteger.GreatestCommonDivisor(BigInteger.Abs(an), BigInteger.Abs(bn));
        var second = BigInteger.GreatestCommonDivisor(ad, bd);
        double result = DivideIntegersAsDouble(
            Product(an / first, bd / second),
            Product(ad / second, bn / first));

        // Exact fractions have no signed zero; preserve IEEE signs at the approximate
        // boundary.
        if (result == 0 && negative)
        {
            return -0.0;
        }

        return result;
    }

    internal static double DivideIntegersAsDouble(BigInteger numerator, BigInteger denominator)
    {
        if (denominator.IsZero)
        {
            throw new DivideByZeroException();
        }

        bool negative = (numerator.Sign < 0) != (denominator.Sign < 0);
        BigInteger n = BigInteger.Abs(numerator), d = BigInteger.Abs(denominator);
        if (n.IsZero)
        {
            return negative ? -0.0 : 0.0;
        }

        long diff = n.GetBitLength() - d.GetBitLength();
        if (diff >= 1025)
        {
            return negative ? double.NegativeInfinity : double.PositiveInfinity;
        }

        if (diff <= -1076)
        {
            return negative ? -0.0 : 0.0;
        }

        if (diff <= -1022)
        {
            n <<= 1077;
            var q = BigInteger.DivRem(n, d, out var remainder);
            int dropped = (int)(q & 7);
            q >>= 3;
            if (dropped > 4 || (dropped == 4 && (!remainder.IsZero || !q.IsEven)))
            {
                q++;
            }

            double result = Math.ScaleB((double)q, -1074);

            return negative ? -result : result;
        }

        long shift = 55 - diff;
        if (shift >= 0)
        {
            n <<= (int)shift;
        }
        else
        {
            d <<= (int)-shift;
        }

        var normal = BigInteger.DivRem(n, d, out var rem);
        int extra = (int)normal.GetBitLength() - 53;
        int droppedNormal = (int)(normal & ((BigInteger.One << extra) - 1));
        normal >>= extra;
        int half = 1 << (extra - 1);
        if (droppedNormal > half || (droppedNormal == half && (!rem.IsZero || !normal.IsEven)))
        {
            normal++;
        }

        double rounded = Math.ScaleB((double)normal, (int)(extra - shift));

        return negative ? -rounded : rounded;
    }
}
