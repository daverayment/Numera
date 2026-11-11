using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Numera;

/// <summary>
/// A calculator number stored as an arbitrary-precision integer, a decimal, a reduced
/// fraction, or a double. Known exactness is tracked separately from the representation.
/// </summary>
/// <remarks>
/// The default instance is integer zero with
/// <see cref="CalculationExactness.Unverified"/> exactness.
/// <para>
/// <b>Exactness.</b> <see cref="Exactness"/> helps callers label results and recognize an
/// unverified calculation path, but does not record individual steps or estimate error.
/// Arithmetic is Unverified if either operand is. Display rounding never
/// changes the stored value or its exactness. <see cref="ExactFraction"/> and
/// <see cref="TryGetExactFraction"/> are available only for finite, Exact values.
/// </para>
/// <para>
/// <b>NaN.</b>Object equality treats NaN as equal; expression comparison retains IEEE
/// NaN semantics.
/// </para>
/// <para>
/// <b>Conversion.</b>Representation getters do not convert values. Use explicit conversion
/// methods to convert between representations, like <see cref="ToDouble"/> and
/// <see cref="ToDecimal"/>.
/// </para>
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

    private readonly BigInteger integerValue;

    private readonly decimal decimalValue;

    private readonly double doubleValue;

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
    /// Gets the known or caller-asserted exactness, for UI guidance and interpreting the
    /// calculation path. This is not inferred from a clean display or storage type.
    /// </summary>
    /// <remarks>
    /// For example, 1/3 is Exact even when its decimal display rounds. A supplied Double
    /// containing 0.5 is Unverified even when it displays without rounding. Unverified
    /// inputs conservatively propagate; this status does not identify which step caused
    /// it.
    /// </remarks>
    public CalculationExactness Exactness { get; }

    /// <summary>
    /// Gets the stored integer without converting other storage types.
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="Kind"/> is not Integer.
    /// </exception>
    public BigInteger IntegerValue => Kind == DataType.Integer ?
        integerValue :
        throw new InvalidOperationException("Value is not stored as an integer.");

    /// <summary>
    /// Gets the stored decimal without converting other storage types.
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="Kind"/> is not Decimal.
    /// </exception>
    public decimal DecimalValue => Kind == DataType.Decimal ?
        decimalValue :
        throw new InvalidOperationException("Value is not stored as a decimal.");

    /// <summary>
    /// Gets the stored double without converting other storage types.
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="Kind"/> is not Double.
    /// </exception>
    public double DoubleValue => Kind == DataType.Double ?
        doubleValue :
        throw new InvalidOperationException("Value is not stored as a double.");

    /// <summary>
    /// The numerator of the reduced fraction.
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="Kind"/> is not Rational.
    /// </exception>
    public BigInteger Numerator => Kind == DataType.Rational ?
        numerator :
        throw new InvalidOperationException("Value is not stored as a rational.");

    /// <summary>
    /// The positive denominator of the reduced fraction.
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="Kind"/> is not Rational.
    /// </exception>
    public BigInteger Denominator => Kind == DataType.Rational ?
        denominator :
        throw new InvalidOperationException("Value is not stored as a rational.");

    /// <summary>
    /// Creates an Exact Integer value.
    /// </summary>
    /// <remarks>
    /// Exact describes the supplied integer, not calculations done before supplying it.
    /// If this integer is an estimate, use <see cref="WithExactness"/> with Unverified
    /// so a UI does not claim that the earlier calculation was exact.
    /// </remarks>
    /// <exception cref="CalculationLimitException">The value exceeds
    /// <see cref="CalculationLimits.MaxIntegerBits"/>.</exception>
    public MathValue(BigInteger value)
    {
        this = default;
        CheckBits(value, CalculationLimits.MaxIntegerBits);
        Kind = DataType.Integer;
        integerValue = value;
        Exactness = CalculationExactness.Exact;
    }

    /// <summary>
    /// Creates an Exact Integer value.
    /// </summary>
    public MathValue(int value) : this(new BigInteger(value)) { }

    /// <summary>
    /// Creates an Exact Integer value.
    /// </summary>
    public MathValue(uint value) : this(new BigInteger(value)) { }

    /// <summary>
    /// Creates an Exact Integer value.
    /// </summary>
    public MathValue(long value) : this(new BigInteger(value)) { }

    /// <summary>
    /// Creates an Exact Integer value.
    /// </summary>
    public MathValue(ulong value) : this(new BigInteger(value)) { }

    /// <summary>
    /// Creates an Exact Decimal value.
    /// </summary>
    /// <remarks>
    /// A supplied 0.1m is exactly 1/10. Converting an approximate square root to Decimal
    /// first does not make that root exact; mark such imported estimates Unverified with
    /// <see cref="WithExactness"/> when the distinction matters to callers.
    /// </remarks>
    public MathValue(decimal value)
    {
        this = default;
        Kind = DataType.Decimal;
        decimalValue = value;
        DecimalDisplayScale = DecomposeDecimal(value).Scale;
        Exactness = CalculationExactness.Exact;
    }

    /// <summary>
    /// Creates a Double value with Unverified exactness, even for exactly representable
    /// values such as 0.5. Use this status to avoid claiming that a floating-point
    /// calculation was established to be exact.
    /// </summary>
    public MathValue(double value)
    {
        this = default;
        Kind = DataType.Double;
        doubleValue = value;
    }

    private MathValue(MathValue value, CalculationExactness exactness)
    {
        this = value;
        Exactness = exactness;
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
        Exactness = CalculationExactness.Exact;
    }

    /// <summary>
    /// Changes the exactness metadata without changing the stored value. Use Unverified
    /// when supplying an approximation; Exact is a trusted caller assertion, not a proof.
    /// </summary>
    /// <remarks>
    /// Marking a Double containing 0.1 Exact does not produce 1/10: its exact fraction
    /// remains the stored binary fraction. No calculation is repeated and no lost digits
    /// are recovered. NaN and infinities cannot be marked Exact.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="exactness"/> is not
    /// a defined exactness status.</exception>
    /// <exception cref="ArgumentException">Exact was requested for NaN or infinity.
    /// </exception>
    public MathValue WithExactness(CalculationExactness exactness)
    {
        if (exactness is not CalculationExactness.Exact and not CalculationExactness.Unverified)
        {
            throw new ArgumentOutOfRangeException(nameof(exactness));
        }

        if (exactness == CalculationExactness.Exact && !IsFinite)
        {
            throw new ArgumentException("NaN and infinities cannot be marked Exact.", nameof(exactness));
        }

        return new MathValue(this, exactness);
    }

    /// <summary>
    /// Checks that the integer, or the numerator and denominator, fit in
    /// <paramref name="maxIntegerBits"/> bits.
    /// </summary>
    /// <exception cref="CalculationLimitException">The value is too large.</exception>
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
    /// Creates an Exact value from a fraction, reduced to lowest terms. The result is an
    /// Integer when the reduced denominator is one.
    /// </summary>
    /// <exception cref="DivideByZeroException">The denominator is zero.</exception>
    /// <exception cref="CalculationLimitException">The fraction is too large.</exception>
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

        // Intermediate components may exceed storage limits but reduce to a small value.
        // Enforce the stored-value ceiling after cancellation, never by rounding to fit.
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
    /// Gets the reduced exact fraction, or <see langword="null"/> when exactness is
    /// Unverified. Use its presence to offer an exact-fraction view in a calculator UI.
    /// </summary>
    /// <remarks>
    /// For example, 1/3 exposes (1, 3) even when its numeric display rounds. A Double
    /// containing 0.5 has no exact alternate unless the caller asserts Exact; a neat
    /// display does not establish exactness.
    /// </remarks>
    public (BigInteger Numerator, BigInteger Denominator)? ExactFraction =>
        TryGetExactFraction(out var num, out var den) ? (num, den) : null;

    /// <summary>
    /// Gets the value as a reduced fraction with a positive denominator.
    /// </summary>
    /// <returns>The known or asserted exact numerator and positive denominator in lowest
    /// terms.
    /// </returns>
    /// <exception cref="InvalidOperationException">The value has no known or asserted
    /// exact fractional representation.</exception>
    public (BigInteger Numerator, BigInteger Denominator) GetExactFraction() =>
        ExactFraction ?? throw new InvalidOperationException(
            "The value does not have a known or asserted exact fractional representation.");

    /// <summary>
    /// Gets the value as a reduced fraction if it is finite and Exact.
    /// </summary>
    /// <remarks>
    /// Use a successful result to enable an exact-fraction view. For example, 1/3
    /// qualifies despite a rounded decimal display; an ordinary Double containing 0.5
    /// does not, despite displaying without rounding. See <see cref="Exactness"/>.
    /// </remarks>
    public bool TryGetExactFraction(out BigInteger num, out BigInteger den)
    {
        if (Exactness == CalculationExactness.Exact && IsFinite)
        {
            NumericFraction(this, out num, out den);
            return true;
        }

        num = default;
        den = default;

        return false;
    }

    // Representation access, not an exactness check: Unverified fractions still qualify.
    // Double is deliberately excluded so arithmetic does not silently become exact-
    // domain.
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
    /// Converts to a double. Non-Double values are rounded once, to nearest even, and may
    /// overflow to infinity.
    /// </summary>
    public double ToDouble() =>
        TryToRational(this, out var n, out var d) ? DivideIntegersAsDouble(n, d) : DoubleValue;

    /// <summary>
    /// Alias for <see cref="ToDouble"/>.
    /// </summary>
    public double AsDouble() => ToDouble();

    /// <summary>
    /// Converts to a decimal. Fractions that are not exactly representable round
    /// midpoint-to-even.
    /// </summary>
    /// <exception cref="OverflowException">The value is outside the decimal range, or is
    /// NaN or infinite.</exception>
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

        TryToRational(this, out var numerator, out var denominator);
        if (TryExactDecimal(numerator, denominator, out var exact))
        {
            return exact;
        }

        // Try the finest Decimal scale first. Each candidate rounds the original
        // fraction, not the previously rounded coefficient, avoiding double rounding.
        var scaledNumerator = BigInteger.Abs(numerator) * BigInteger.Pow(10, 28);
        for (int scale = 28; scale >= 0; scale--)
        {
            var coefficient = BigInteger.DivRem(scaledNumerator, denominator, out var remainder);
            int midpointComparison = (remainder * 2).CompareTo(denominator);
            if (midpointComparison > 0 || (midpointComparison == 0 && !coefficient.IsEven))
            {
                coefficient++;
            }

            if (coefficient <= MaxDecimalMantissa)
            {
                return MakeDecimal(coefficient, numerator.Sign < 0, scale);
            }

            scaledNumerator /= 10;
        }

        throw new OverflowException("Value exceeds the decimal range.");
    }

    /// <summary>
    /// Whether the value is not NaN or infinite.
    /// </summary>
    public bool IsFinite => Kind != DataType.Double || double.IsFinite(DoubleValue);

    /// <summary>
    /// Whether the value is NaN.
    /// </summary>
    public bool IsNaN => Kind == DataType.Double && double.IsNaN(DoubleValue);

    /// <summary>
    /// Whether the value is zero, including negative zero.
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
    /// Gets the value as an integer if it is finite and has no fractional part.
    /// Exactness is not considered.
    /// </summary>
    public bool TryGetInteger(out BigInteger value)
    {
        if (TryToRational(this, out var n, out var d))
        {
            value = BigInteger.DivRem(n, d, out var remainder);
            if (remainder.IsZero)
            {
                return true;
            }
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
    /// Formats the value using the current culture.
    /// </summary>
    public override string ToString() => ToString(null, null);

    /// <inheritdoc cref="ToString(string?, IFormatProvider?)"/>
    public string ToString(string? format) => ToString(format, null);

    /// <inheritdoc cref="ToString(string?, IFormatProvider?)"/>
    public string ToString(IFormatProvider? formatProvider) => ToString(null, formatProvider);

    /// <summary>
    /// Formats the value using the standard .NET numeric formats of its representation.
    /// </summary>
    /// <remarks>
    /// Rational values support G (optionally with a digit count), F, N and R. The R
    /// format gives the exact fraction as <c>numerator/denominator</c>; the others round
    /// midpoint-to-even.
    /// </remarks>
    /// <exception cref="FormatException">The format is not supported by a Rational value.
    /// </exception>
    public string ToString(string? format, IFormatProvider? formatProvider) => Kind switch
    {
        DataType.Integer => IntegerValue.ToString(format, formatProvider),
        DataType.Decimal => DecimalValue.ToString(format, formatProvider),
        DataType.Double => DoubleValue.ToString(format, formatProvider),
        DataType.Rational => FormatRational(format, formatProvider),
        _ => throw new InvalidOperationException("Unknown representation."),
    };

    /// <summary>
    /// Formats the value into <paramref name="destination"/>, returning false if it is
    /// too large to fit.
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
    /// Formats the value as UTF-8 into <paramref name="utf8Destination"/>, returning
    /// false if it is too small.
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
    /// Formats the value losslessly using the invariant culture, with no rounding.
    /// </summary>
    /// <remarks>
    /// Doubles are written as the exact fraction they store. Decimal display scale is
    /// kept where a decimal literal can express it. NaN and infinities are written as
    /// text that cannot be evaluated again. Use <see cref="Serialize"/> to preserve the
    /// representation and exactness as well.
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

    private string InvariantStoredValue() => Kind switch
    {
        DataType.Rational => numerator.ToString(CultureInfo.InvariantCulture) + "/" + denominator.ToString(CultureInfo.InvariantCulture),
        DataType.Double => ToString("R", CultureInfo.InvariantCulture),
        _ => ToString(null, CultureInfo.InvariantCulture),
    };

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

        if (grouped)
        {
            whole = integer.ToString("N0", culture);
        }

        string result = digits == 0
            ? sign + whole
            : sign + whole + culture.NumberDecimalSeparator + fraction.ToString("D" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        return result;
    }

    /// <summary>
    /// Formats an integral value as hexadecimal with a leading minus sign for negatives,
    /// or returns null if the value is not integral.
    /// </summary>
    public string? ToHexString(bool prefix = true) => RadixString("X", "0x", prefix);

    /// <summary>
    /// Formats an integral value as binary with a leading minus sign for negatives, or
    /// returns null if the value is not integral.
    /// </summary>
    public string? ToBinaryString(bool prefix = true) => RadixString("B", "0b", prefix);

    /// <summary>
    /// Formats an integral value as octal with a leading minus sign for negatives, or
    /// returns null if the value is not integral.
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
    /// Serializes the representation, value, exactness and decimal display scale to JSON
    /// without losing precision.
    /// </summary>
    public string Serialize() => JsonSerializer.Serialize(new SerializedValue(
        Kind, Exactness, InvariantStoredValue(),
        Kind == DataType.Rational ? numerator.ToString(CultureInfo.InvariantCulture) : null,
        Kind == DataType.Rational ? denominator.ToString(CultureInfo.InvariantCulture) : null,
        DecimalDisplayScale),
        SerializationContext.Default.SerializedValue);

    /// <summary>
    /// Reads a value written by <see cref="Serialize"/>, validating it against
    /// <paramref name="maxIntegerBits"/>.
    /// </summary>
    /// <exception cref="FormatException">The data is incomplete, invalid or not in
    /// canonical form.</exception>
    /// <exception cref="CalculationLimitException">The input or the value is too large.
    /// </exception>
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

        if (dto.Exactness == CalculationExactness.Exact && !value.IsFinite)
        {
            throw new FormatException("NaN and infinities cannot be marked Exact.");
        }

        value = value.WithExactness(dto.Exactness);
        value.ValidateLimits(maxIntegerBits);
        return value;
    }

    private sealed record SerializedValue(
        [property: JsonRequired] DataType Kind,
        [property: JsonRequired] CalculationExactness Exactness,
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
    /// Negates a value, with unchanged exactness.
    /// </summary>
    public static MathValue operator -(MathValue value) => Negate(value);

    /// <summary>
    /// Negates a value, with unchanged exactness.
    /// </summary>
    /// <exception cref="CalculationLimitException">The value or result is too large.
    /// </exception>
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
        }).WithExactness(value.Exactness).WithDecimalDisplayScale(value.DecimalDisplayScale);

        result.ValidateLimits();

        return result;
    }

    /// <inheritdoc cref="Add"/>
    public static MathValue operator +(MathValue a, MathValue b) => Add(a, b);

    /// <inheritdoc cref="Subtract"/>
    public static MathValue operator -(MathValue a, MathValue b) => Subtract(a, b);

    /// <inheritdoc cref="Multiply"/>
    public static MathValue operator *(MathValue a, MathValue b) => Multiply(a, b);

    /// <inheritdoc cref="Divide"/>
    public static MathValue operator /(MathValue a, MathValue b) => Divide(a, b);

    /// <inheritdoc cref="Modulo"/>
    public static MathValue operator %(MathValue a, MathValue b) => Modulo(a, b);

    /// <summary>
    /// Adds two values.
    /// </summary>
    /// <exception cref="CalculationLimitException">An operand or the result is too large.
    /// </exception>
    public static MathValue Add(MathValue a, MathValue b) => Arithmetic(a, b, '+');

    /// <summary>
    /// Subtracts <paramref name="b"/> from <paramref name="a"/>.
    /// </summary>
    /// <exception cref="CalculationLimitException">An operand or the result is too large.
    /// </exception>
    public static MathValue Subtract(MathValue a, MathValue b) => Arithmetic(a, b, '-');

    /// <summary>
    /// Multiplies two values.
    /// </summary>
    /// <exception cref="CalculationLimitException">An operand or the result is too large.
    /// </exception>
    public static MathValue Multiply(MathValue a, MathValue b) => Arithmetic(a, b, '*');

    /// <summary>
    /// Divides <paramref name="a"/> by <paramref name="b"/>.
    /// </summary>
    /// <exception cref="DivideByZeroException"><paramref name="b"/> is zero.</exception>
    /// <exception cref="CalculationLimitException">An operand or the result is too large.
    /// </exception>
    public static MathValue Divide(MathValue a, MathValue b) => Arithmetic(a, b, '/');

    /// <summary>
    /// Computes the remainder of <paramref name="a"/> divided by <paramref name="b"/>,
    /// with the sign of <paramref name="a"/>.
    /// </summary>
    /// <exception cref="DivideByZeroException"><paramref name="b"/> is zero.</exception>
    /// <exception cref="CalculationLimitException">An operand or the result is too large.
    /// </exception>
    public static MathValue Modulo(MathValue a, MathValue b) => Arithmetic(a, b, '%');

    private static MathValue Arithmetic(MathValue left, MathValue right, char operation)
    {
        left.ValidateLimits();
        right.ValidateLimits();
        if ((operation is '/' or '%') && right.IsZero)
        {
            throw new DivideByZeroException();
        }

        if (left.Kind == DataType.Double || right.Kind == DataType.Double)
        {
            double leftValue = left.ToDouble();
            double rightValue = right.ToDouble();
            return new MathValue(operation switch
            {
                '+' => leftValue + rightValue,
                '-' => leftValue - rightValue,
                '*' => leftValue * rightValue,
                '/' => DivideMixedAsDouble(left, right),
                '%' => leftValue % rightValue,
                _ => throw new InvalidOperationException(),
            });
        }

        TryToRational(left, out var leftNumerator, out var leftDenominator);
        TryToRational(right, out var rightNumerator, out var rightDenominator);
        BigInteger resultNumerator;
        BigInteger resultDenominator;
        if (operation is '+' or '-' or '%')
        {
            // Use the least common denominator rather than multiplying denominators
            // blindly; this reduces temporary growth before the final GCD reduction.
            var commonFactor = BigInteger.GreatestCommonDivisor(leftDenominator, rightDenominator);
            var scaledLeft = Product(leftNumerator, rightDenominator / commonFactor);
            var scaledRight = Product(rightNumerator, leftDenominator / commonFactor);
            resultDenominator = Product(leftDenominator, rightDenominator / commonFactor);
            resultNumerator = operation switch
            {
                '+' => scaledLeft + scaledRight,
                '-' => scaledLeft - scaledRight,
                '%' => scaledLeft % scaledRight,
                _ => throw new InvalidOperationException(),
            };
            CheckBits(resultNumerator, CalculationLimits.MaxIntermediateBits);
        }
        else
        {
            if (operation == '/')
            {
                (rightNumerator, rightDenominator) = (rightDenominator, rightNumerator);
            }

            // Cancel across the product before multiplication, so removable factors
            // cannot trigger an avoidable intermediate-size limit failure.
            var leftCancellation = BigInteger.GreatestCommonDivisor(BigInteger.Abs(leftNumerator), BigInteger.Abs(rightDenominator));
            var rightCancellation = BigInteger.GreatestCommonDivisor(BigInteger.Abs(rightNumerator), leftDenominator);
            resultNumerator = Product(leftNumerator / leftCancellation, rightNumerator / rightCancellation);
            resultDenominator = Product(leftDenominator / rightCancellation, rightDenominator / leftCancellation);
        }

        // Domain follows the operands, not whether the answer happens to terminate:
        // Decimal + Integer may stay Decimal, but involving Rational keeps that domain.
        bool decimalDomain = (left.Kind == DataType.Decimal || right.Kind == DataType.Decimal) &&
            left.Kind != DataType.Rational && right.Kind != DataType.Rational;
        var result = FromExactFraction(resultNumerator, resultDenominator, decimalDomain);
        int scale = operation switch
        {
            '*' => left.DecimalDisplayScale + right.DecimalDisplayScale,
            '/' when left.DecimalDisplayScale == MaxDecimalDisplayScale => MaxDecimalDisplayScale,
            '/' => Math.Max(0, left.DecimalDisplayScale - right.DecimalDisplayScale),
            _ => Math.Max(left.DecimalDisplayScale, right.DecimalDisplayScale),
        };

        return result.WithExactness(InheritedExactness(left, right))
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
    /// Raises a value to a power. Integer exponents of non-Double values are exact;
    /// otherwise the result is a Double with Unverified exactness.
    /// </summary>
    /// <exception cref="DivideByZeroException">Zero is raised to a negative power.
    /// </exception>
    /// <exception cref="CalculationLimitException">The exponent is larger than
    /// <paramref name="maxExponentMagnitude"/>, or the result is too large.</exception>
    /// <exception cref="ExpressionException">A negative base is raised to a fractional
    /// exponent.</exception>
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
            TryToRational(baseVal, out var numerator, out var denominator);
            if (exponent.Sign < 0)
            {
                (numerator, denominator) = (denominator, numerator);
            }

            int absoluteExponent = (int)BigInteger.Abs(exponent);
            bool decimalDomain = baseVal.Kind != DataType.Rational
                && (baseVal.Kind == DataType.Decimal || expVal.Kind == DataType.Decimal);

            CheckPower(numerator, absoluteExponent, CalculationLimits.MaxIntegerBits);
            CheckPower(denominator, absoluteExponent, CalculationLimits.MaxIntegerBits);

            var result = FromExactFraction(
                BoundedPower(numerator, absoluteExponent),
                BoundedPower(denominator, absoluteExponent),
                decimalDomain);
            long scale = exponent.Sign > 0 ? (long)baseVal.DecimalDisplayScale * absoluteExponent : 0;

            return result
                .WithExactness(InheritedExactness(baseVal, expVal))
                .WithDecimalDisplayScale(Math.Max(result.DecimalDisplayScale, scale));
        }

        if (baseVal.IsFinite && baseVal.IsNegative && expVal.IsFinite && !integral)
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.UndefinedMathResult,
                "Negative base with a fractional exponent has no real solution."));
        }

        double approximateBase = baseVal.ToDouble();
        double approximateExponent = expVal.ToDouble();
        return new MathValue(Math.Pow(approximateBase, approximateExponent));
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
    /// Compares two values numerically. NaN sorts below every other value and equals NaN.
    /// </summary>
    /// <exception cref="CalculationLimitException">An operand is too large.</exception>
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
            if (!b.IsFinite)
            {
                return a.DoubleValue.CompareTo(b.DoubleValue);
            }

            return a.DoubleValue > 0 ? 1 : -1;
        }

        if (!b.IsFinite)
        {
            return b.DoubleValue > 0 ? -1 : 1;
        }

        // Compare stored values as fractions, including a Double's exact binary value.
        // Converting both sides to Double would merge distinct large or nearby values.
        NumericFraction(a, out var an, out var ad);
        NumericFraction(b, out var bn, out var bd);
        var commonDenominator = BigInteger.GreatestCommonDivisor(ad, bd);

        return Product(an, bd / commonDenominator)
            .CompareTo(Product(bn, ad / commonDenominator));
    }

    /// <inheritdoc />
    public int CompareTo(MathValue other) => Compare(this, other);

    /// <inheritdoc />
    int IComparable.CompareTo(object? obj)
    {
        if (obj is null)
        {
            return 1;
        }

        if (obj is MathValue value)
        {
            return Compare(this, value);
        }

        throw new ArgumentException("Object must be a MathValue.", nameof(obj));
    }

    /// <summary>
    /// Whether the values are numerically equal, ignoring representation and exactness.
    /// NaN equals NaN.
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

        // Equality ignores representation and exactness, so hashing must use the same
        // reduced fraction: Integer 1, Decimal 1.0, and Double 1.0 need identical hashes.
        NumericFraction(this, out var n, out var d);

        return d.IsOne ? n.GetHashCode() : HashCode.Combine(n, d);
    }

    /// <summary>
    /// Tests equality, as <see cref="Equals(MathValue)"/> does.
    /// </summary>
    public static bool operator ==(MathValue left, MathValue right) => left.Equals(right);

    /// <summary>
    /// Tests inequality, as <see cref="Equals(MathValue)"/> does.
    /// </summary>
    public static bool operator !=(MathValue left, MathValue right) => !left.Equals(right);

    /// <summary>
    /// Whether <paramref name="left"/> is less than <paramref name="right"/>.
    /// </summary>
    public static bool operator <(MathValue left, MathValue right) => Compare(left, right) < 0;

    /// <summary>
    /// Whether <paramref name="left"/> is less than or equal to <paramref name="right"/>.
    /// </summary>
    public static bool operator <=(MathValue left, MathValue right) => Compare(left, right) <= 0;

    /// <summary>
    /// Whether <paramref name="left"/> is greater than <paramref name="right"/>.
    /// </summary>
    public static bool operator >(MathValue left, MathValue right) => Compare(left, right) > 0;

    /// <summary>
    /// Whether <paramref name="left"/> is greater than or equal to
    /// <paramref name="right"/>.
    /// </summary>
    public static bool operator >=(MathValue left, MathValue right) => Compare(left, right) >= 0;

    /// <summary>
    /// Returns Integer 1 if the values are equal, otherwise 0. NaN is never equal.
    /// </summary>
    public static MathValue AreEqual(MathValue a, MathValue b) => 
        Boolean(!a.IsNaN && !b.IsNaN && Compare(a, b) == 0, a, b);

    /// <summary>
    /// Returns Integer 1 if the values differ, otherwise 0. NaN always differs.
    /// </summary>
    public static MathValue AreNotEqual(MathValue a, MathValue b) => 
        Boolean(a.IsNaN || b.IsNaN || Compare(a, b) != 0, a, b);

    /// <summary>
    /// Returns Integer 1 if <paramref name="a"/> is less than <paramref name="b"/>,
    /// otherwise 0. Comparisons involving NaN return 0.
    /// </summary>
    public static MathValue LessThan(MathValue a, MathValue b) =>
        Boolean(!a.IsNaN && !b.IsNaN && Compare(a, b) < 0, a, b);

    /// <summary>
    /// Returns Integer 1 if <paramref name="a"/> is less than or equal to
    /// <paramref name="b"/>, otherwise 0. Comparisons involving NaN return 0.
    /// </summary>
    public static MathValue LessThanOrEqual(MathValue a, MathValue b) =>
        Boolean(!a.IsNaN && !b.IsNaN && Compare(a, b) <= 0, a, b);

    /// <summary>
    /// Returns Integer 1 if <paramref name="a"/> is greater than <paramref name="b"/>,
    /// otherwise 0. Comparisons involving NaN return 0.
    /// </summary>
    public static MathValue GreaterThan(MathValue a, MathValue b) =>
        Boolean(!a.IsNaN && !b.IsNaN && Compare(a, b) > 0, a, b);

    /// <summary>
    /// Returns Integer 1 if <paramref name="a"/> is greater than or equal to
    /// <paramref name="b"/>, otherwise 0. Comparisons involving NaN return 0.
    /// </summary>
    public static MathValue GreaterThanOrEqual(MathValue a, MathValue b) =>
        Boolean(!a.IsNaN && !b.IsNaN && Compare(a, b) >= 0, a, b);

    /// <summary>
    /// Returns Integer 1 if both operands are nonzero, otherwise 0.
    /// </summary>
    public static MathValue LogicalAnd(MathValue a, MathValue b) =>
        Boolean(!a.IsZero && !b.IsZero, a, b);

    /// <summary>
    /// Returns Integer 1 if either operand is nonzero, otherwise 0.
    /// </summary>
    public static MathValue LogicalOr(MathValue a, MathValue b) =>
        Boolean(!a.IsZero || !b.IsZero, a, b);

    /// <summary>
    /// Returns Integer 1 if exactly one operand is nonzero, otherwise 0.
    /// </summary>
    public static MathValue LogicalXor(MathValue a, MathValue b) =>
        Boolean(!a.IsZero ^ !b.IsZero, a, b);

    /// <summary>
    /// Returns Integer 1 if the operand is zero, otherwise 0.
    /// </summary>
    public static MathValue LogicalNot(MathValue a)
    {
        a.ValidateLimits();
        var result = new MathValue(a.IsZero ? 1 : 0).WithExactness(a.Exactness);
        result.ValidateLimits();

        return result;
    }

    private static MathValue Boolean(bool value, MathValue a, MathValue b)
    {
        a.ValidateLimits();
        b.ValidateLimits();
        var result = new MathValue(value ? 1 : 0).WithExactness(InheritedExactness(a, b));
        result.ValidateLimits();

        return result;
    }

    private static CalculationExactness InheritedExactness(MathValue a, MathValue b) =>
        a.Exactness == CalculationExactness.Exact && b.Exactness == CalculationExactness.Exact ?
            CalculationExactness.Exact :
            CalculationExactness.Unverified;

    /// <summary>
    /// Bitwise AND of two integral values.
    /// </summary>
    /// <exception cref="ExpressionException">An operand is not integral.</exception>
    /// <exception cref="CalculationLimitException">An operand or the result is too large.
    /// </exception>
    public static MathValue BitwiseAnd(MathValue a, MathValue b) =>
        IntegerOperation(a, b, '&');

    /// <summary>
    /// Bitwise OR of two integral values.
    /// </summary>
    /// <exception cref="ExpressionException">An operand is not integral.</exception>
    /// <exception cref="CalculationLimitException">An operand or the result is too large.
    /// </exception>
    public static MathValue BitwiseOr(MathValue a, MathValue b) =>
        IntegerOperation(a, b, '|');

    /// <summary>
    /// Bitwise XOR of two integral values.
    /// </summary>
    /// <exception cref="ExpressionException">An operand is not integral.</exception>
    /// <exception cref="CalculationLimitException">An operand or the result is too large.
    /// </exception>
    public static MathValue BitwiseXor(MathValue a, MathValue b) =>
        IntegerOperation(a, b, '^');

    /// <summary>
    /// Shifts an integral value left by a number of bits.
    /// </summary>
    /// <exception cref="ExpressionException">An operand is not integral, or the shift
    /// count is negative.</exception>
    /// <exception cref="CalculationLimitException">The shift count or the result is too
    /// large.</exception>
    public static MathValue ShiftLeft(MathValue a, MathValue b) =>
        IntegerOperation(a, b, '<');

    /// <summary>
    /// Shifts an integral value right by a number of bits, keeping the sign.
    /// </summary>
    /// <exception cref="ExpressionException">An operand is not integral, or the shift
    /// count is negative.</exception>
    /// <exception cref="CalculationLimitException">An operand is too large.</exception>
    public static MathValue ShiftRight(MathValue a, MathValue b) =>
        IntegerOperation(a, b, '>');

    /// <summary>
    /// Bitwise complement of an integral value.
    /// </summary>
    /// <exception cref="ExpressionException">The operand is not integral.</exception>
    /// <exception cref="CalculationLimitException">The operand or the result is too
    /// large.</exception>
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
        long resultBits = bits;
        if (n.Sign >= 0)
        {
            if (BigInteger.PopCount(n) == bits)
            {
                resultBits++;
            }
        }
        else if (BigInteger.IsPow2(magnitude))
        {
            resultBits--;
        }

        CheckIntegerResultBits(resultBits);
        var result = new MathValue(~n).WithExactness(a.Exactness);
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
                return new MathValue(0).WithExactness(InheritedExactness(a, b));
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
            '&' => n & m,
            '|' => n | m,
            '^' => n ^ m,
            '<' => n << (int)m,
            '>' => n >> (int)m,
            _ => throw new InvalidOperationException(),
        };

        CheckBits(value, CalculationLimits.MaxIntegerBits);
        CheckBits(value, CalculationLimits.MaxIntermediateBits);

        return new MathValue(value).WithExactness(InheritedExactness(a, b));
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

        int carry = negative ? 1 : 0;
        int bits = 0;
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

    /// <inheritdoc cref="BitwiseNot"/>
    public static MathValue operator ~(MathValue value) => BitwiseNot(value);

    /// <inheritdoc cref="BitwiseAnd"/>
    public static MathValue operator &(MathValue left, MathValue right) => BitwiseAnd(left, right);

    /// <inheritdoc cref="BitwiseOr"/>
    public static MathValue operator |(MathValue left, MathValue right) => BitwiseOr(left, right);

    /// <inheritdoc cref="BitwiseXor"/>
    public static MathValue operator ^(MathValue left, MathValue right) => BitwiseXor(left, right);

    /// <inheritdoc cref="ShiftLeft"/>
    public static MathValue operator <<(MathValue left, int right) => ShiftLeft(left, new MathValue(right));

    /// <inheritdoc cref="ShiftRight"/>
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
    /// Throws <see cref="CalculationLimitException"/> if the value needs more than
    /// <paramref name="bits"/> bits.
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
    /// Gets the exact fraction of a finite value, including the binary value of a double,
    /// independent of its exactness.
    /// </summary>
    /// <exception cref="ArgumentException">The value is not finite.</exception>
    internal static void NumericFraction(
        MathValue value, out BigInteger numerator, out BigInteger denominator)
    {
        if (!value.IsFinite)
        {
            throw new ArgumentException(
                "A stored fraction requires a finite value.", nameof(value));
        }

        if (TryToRational(value, out numerator, out denominator))
        {
            return;
        }

        long bits = BitConverter.DoubleToInt64Bits(value.DoubleValue);
        int exponent = (int)((bits >> 52) & 0x7ff);
        long mantissa = bits & 0x000f_ffff_ffff_ffff;
        numerator = exponent == 0 ? mantissa : mantissa | (1L << 52);
        int shift = exponent == 0 ? -1074 : exponent - 1075;
        if (bits < 0)
        {
            numerator = -numerator;
        }

        denominator = BigInteger.One;
        if (shift >= 0)
        {
            numerator <<= shift;
        }
        else
        {
            denominator <<= -shift;
        }

        var commonFactor = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
        numerator /= commonFactor;
        denominator /= commonFactor;
    }

    internal static bool TryExactDecimal(
        BigInteger numerator, BigInteger denominator, out decimal result)
    {
        result = default;
        if (denominator.Sign <= 0)
        {
            return false;
        }

        if (numerator.IsZero)
        {
            return true;
        }

        var commonFactor = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
        numerator /= commonFactor;
        denominator /= commonFactor;
        if (BigInteger.Abs(numerator) > MaxDecimalMantissa)
        {
            return false;
        }

        int factorsOfTwo = 0;
        int factorsOfFive = 0;
        while (denominator.IsEven && factorsOfTwo <= 28)
        {
            factorsOfTwo++;
            denominator >>= 1;
        }

        while (denominator % 5 == 0 && factorsOfFive <= 28)
        {
            factorsOfFive++;
            denominator /= 5;
        }

        int scale = Math.Max(factorsOfTwo, factorsOfFive);
        if (!denominator.IsOne || scale > 28)
        {
            return false;
        }

        // A reduced fraction terminates in base ten only with denominator 2^a * 5^b.
        // Fill in the missing factors to obtain 10^scale, adjusting the numerator too.
        var mantissa = Product(
            Product(BigInteger.Abs(numerator), BigInteger.One << (scale - factorsOfTwo)),
            BoundedPower(5, scale - factorsOfFive));
        if (mantissa > MaxDecimalMantissa)
        {
            return false;
        }

        result = MakeDecimal(mantissa, numerator.Sign < 0, scale);

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

        // Divide the original finite values before rounding, avoiding inf/inf or 0/0
        // when individual conversions would overflow or underflow but their ratio fits.
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
        BigInteger scaledNumerator = BigInteger.Abs(numerator);
        BigInteger scaledDenominator = BigInteger.Abs(denominator);
        if (scaledNumerator.IsZero)
        {
            return negative ? -0.0 : 0.0;
        }

        long bitLengthDifference =
            scaledNumerator.GetBitLength() - scaledDenominator.GetBitLength();

        // Bit lengths give conservative range bounds; borderline overflow and
        // underflow cases continue through division so rounding decides the result.
        if (bitLengthDifference >= 1025)
        {
            return negative ? double.NegativeInfinity : double.PositiveInfinity;
        }

        if (bitLengthDifference <= -1076)
        {
            return negative ? -0.0 : 0.0;
        }

        if (bitLengthDifference <= -1022)
        {
            // Subnormal results round on a fixed 2^-1074 grid, using three guard bits.
            scaledNumerator <<= 1077;
            var coefficient = BigInteger.DivRem(scaledNumerator, scaledDenominator, out var subnormalRemainder);
            int discardedBits = (int)(coefficient & 7);
            coefficient >>= 3;

            // A nonzero division remainder is a sticky bit: an apparent halfway
            // guard-bit pattern is actually above halfway if more fraction remains.
            if (discardedBits > 4 || (discardedBits == 4 &&
                (!subnormalRemainder.IsZero || !coefficient.IsEven)))
            {
                coefficient++;
            }

            double result = Math.ScaleB((double)coefficient, -1074);

            return negative ? -result : result;
        }

        long precisionShift = 55 - bitLengthDifference;
        if (precisionShift >= 0)
        {
            scaledNumerator <<= (int)precisionShift;
        }
        else
        {
            scaledDenominator <<= (int)(-precisionShift);
        }

        // Normal results retain 53 significant bits and round the discarded bits to even.
        var significand = BigInteger.DivRem(scaledNumerator, scaledDenominator, out var remainder);
        int discardedBitCount = (int)significand.GetBitLength() - 53;
        int discardedNormalBits = (int)(significand & ((BigInteger.One << discardedBitCount) - 1));
        significand >>= discardedBitCount;
        int midpoint = 1 << (discardedBitCount - 1);
        if (discardedNormalBits > midpoint || (discardedNormalBits == midpoint && (!remainder.IsZero || !significand.IsEven)))
        {
            significand++;
        }

        double rounded = Math.ScaleB((double)significand, (int)(discardedBitCount - precisionShift));

        return negative ? -rounded : rounded;
    }
}
