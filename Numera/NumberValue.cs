using System.Numerics;

namespace Numera;

/// <summary>
/// A calculator value retaining its integer, decimal or binary floating-point representation.
/// Representation describes storage, not a guarantee of mathematical accuracy.
/// </summary>
public readonly struct NumberValue
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "The enum names are descriptive of their usage, which does not conflict with type names.")]
    public enum DataType
    {
        Integer,
        Decimal,
        Double,
    }

    public DataType Type { get; }

    public BigInteger IntVal { get; }

    public decimal DecVal { get; }

    public double DblVal { get; }

    public NumberValue(BigInteger value)
    {
        if (BigInteger.Abs(value).GetBitLength() > CalculationLimits.MaxIntegerBits)
        {
            throw new CalculationLimitException();
        }

        Type = DataType.Integer;
        IntVal = value;
        DecVal = default;
        DblVal = default;
    }

    public NumberValue(decimal value)
    {
        Type = DataType.Decimal;
        IntVal = default;
        DecVal = value;
        DblVal = default;
    }

    public NumberValue(double value)
    {
        Type = DataType.Double;
        IntVal = default;
        DecVal = default;
        DblVal = value;
    }

    public double AsDouble() => Type switch
    {
        DataType.Integer => (double)IntVal,
        DataType.Decimal => (double)DecVal,
        _ => DblVal,
    };

    internal bool IsZero => Type switch
    {
        DataType.Integer => IntVal.IsZero,
        DataType.Decimal => DecVal == 0m,
        _ => DblVal == 0.0,
    };

    public bool TryGetInteger(out BigInteger value)
    {
        switch (Type)
        {
            case DataType.Integer:
                value = IntVal;
                return true;
            case DataType.Decimal when decimal.Truncate(DecVal) == DecVal:
                value = new BigInteger(DecVal);
                return true;
            case DataType.Double when double.IsFinite(DblVal) && Math.Truncate(DblVal) == DblVal:
                value = new BigInteger(DblVal);
                return true;
            default:
                value = default;
                return false;
        }
    }

    public static NumberValue operator -(NumberValue value) => value.Type switch
    {
        DataType.Integer => new NumberValue(-value.IntVal),
        DataType.Decimal => new NumberValue(-value.DecVal),
        _ => new NumberValue(-value.DblVal),
    };

    public static int Compare(NumberValue a, NumberValue b)
    {
        // If both values are integers, compare them as big integers to avoid precision
        // and magnitude loss from double conversion.
        if (a.Type is DataType.Integer && b.Type is DataType.Integer)
        {
            return a.IntVal.CompareTo(b.IntVal);
        }

        if (a.Type is not DataType.Double && b.Type is not DataType.Double)
        {
            if (a.Type is DataType.Integer)
            {
                return CompareIntegerAndDecimal(a.IntVal, b.DecVal);
            }

            if (b.Type is DataType.Integer)
            {
                return -CompareIntegerAndDecimal(b.IntVal, a.DecVal);
            }

            return a.DecVal.CompareTo(b.DecVal);
        }

        return a.AsDouble().CompareTo(b.AsDouble());
    }

    public static NumberValue LogicalAnd(NumberValue a, NumberValue b) =>
        (!a.IsZero && !b.IsZero) ? new NumberValue(1m) : new NumberValue(0m);

    public static NumberValue LogicalOr(NumberValue a, NumberValue b) =>
        (!a.IsZero || !b.IsZero) ? new NumberValue(1m) : new NumberValue(0m);

    public static NumberValue AreEqual(NumberValue a, NumberValue b) =>
        Compare(a, b) == 0 ? new NumberValue(1m) : new NumberValue(0m);

    public static NumberValue AreNotEqual(NumberValue a, NumberValue b) =>
        Compare(a, b) != 0 ? new NumberValue(1m) : new NumberValue(0m);

    public static NumberValue operator +(NumberValue a, NumberValue b)
    {
        if (a.Type is DataType.Double || b.Type is DataType.Double)
        {
            return new NumberValue(a.AsDouble() + b.AsDouble());
        }

        if (a.Type is DataType.Integer && b.Type is DataType.Integer)
        {
            return new NumberValue(a.IntVal + b.IntVal);
        }

        try
        {
            var decA = a.Type is DataType.Integer ? (decimal)a.IntVal : a.DecVal;
            var decB = b.Type is DataType.Integer ? (decimal)b.IntVal : b.DecVal;
            return new NumberValue(decA + decB);
        }
        catch (OverflowException)
        {
            // Decimal overflow, convert to double, which can represent a wider range of
            // values, but with less precision.
            return new NumberValue(a.AsDouble() + b.AsDouble());
        }
    }

    public static NumberValue operator -(NumberValue a, NumberValue b)
    {
        if (a.Type is DataType.Double || b.Type is DataType.Double)
        {
            return new NumberValue(a.AsDouble() - b.AsDouble());
        }

        if (a.Type is DataType.Integer && b.Type is DataType.Integer)
        {
            return new NumberValue(a.IntVal - b.IntVal);
        }

        try
        {
            var decA = a.Type is DataType.Integer ? (decimal)a.IntVal : a.DecVal;
            var decB = b.Type is DataType.Integer ? (decimal)b.IntVal : b.DecVal;
            return new NumberValue(decA - decB);
        }
        catch (OverflowException)
        {
            return new NumberValue(a.AsDouble() - b.AsDouble());
        }
    }

    public static NumberValue operator *(NumberValue a, NumberValue b)
    {
        if (a.Type is DataType.Double || b.Type is DataType.Double)
        {
            return new NumberValue(a.AsDouble() * b.AsDouble());
        }

        if (a.Type is DataType.Integer && b.Type is DataType.Integer)
        {
            return new NumberValue(a.IntVal * b.IntVal);
        }

        try
        {
            var decA = a.Type is DataType.Integer ? (decimal)a.IntVal : a.DecVal;
            var decB = b.Type is DataType.Integer ? (decimal)b.IntVal : b.DecVal;
            return new NumberValue(decA * decB);
        }
        catch (OverflowException)
        {
            return new NumberValue(a.AsDouble() * b.AsDouble());
        }
    }

    public static NumberValue operator /(NumberValue a, NumberValue b)
    {
        if (b.IsZero)
        {
            throw new DivideByZeroException();
        }

        if (a.Type is DataType.Double || b.Type is DataType.Double)
        {
            return new NumberValue(DivideAsDouble(a, b));
        }

        if (a.Type is DataType.Integer &&
            b.Type is DataType.Integer &&
            BigInteger.Remainder(a.IntVal, b.IntVal) == BigInteger.Zero)
        {
            return new NumberValue(a.IntVal / b.IntVal);
        }

        try
        {
            var decA = a.Type is DataType.Integer ? (decimal)a.IntVal : a.DecVal;
            var decB = b.Type is DataType.Integer ? (decimal)b.IntVal : b.DecVal;
            return new NumberValue(decA / decB);
        }
        catch (OverflowException)
        {
            return new NumberValue(DivideAsDouble(a, b));
        }
    }

    public static NumberValue operator %(NumberValue a, NumberValue b)
    {
        if (b.IsZero)
        {
            throw new DivideByZeroException();
        }

        if (a.Type is DataType.Double || b.Type is DataType.Double)
        {
            return new NumberValue(a.AsDouble() % b.AsDouble());
        }

        if (a.Type is DataType.Integer && b.Type is DataType.Integer)
        {
            return new NumberValue(a.IntVal % b.IntVal);
        }

        try
        {
            var decA = a.Type is DataType.Integer ? (decimal)a.IntVal : a.DecVal;
            var decB = b.Type is DataType.Integer ? (decimal)b.IntVal : b.DecVal;
            return new NumberValue(decA % decB);
        }
        catch (OverflowException)
        {
            return new NumberValue(a.AsDouble() % b.AsDouble());
        }
    }

    public static NumberValue Pow(NumberValue baseVal, NumberValue expVal)
    {
        if (expVal.TryGetInteger(out var exponent))
        {
            if (BigInteger.Abs(exponent) > CalculationLimits.MaxExponentMagnitude)
            {
                throw new CalculationLimitException();
            }

            if (baseVal.Type is DataType.Integer && exponent >= BigInteger.Zero)
            {
                var magnitude = BigInteger.Abs(baseVal.IntVal);
                if (magnitude > BigInteger.One && ((magnitude.GetBitLength() - 1) * (int)exponent) + 1 > CalculationLimits.MaxIntegerBits)
                {
                    throw new CalculationLimitException();
                }

                return new NumberValue(BigInteger.Pow(baseVal.IntVal, (int)exponent));
            }
        }

        return new NumberValue(Math.Pow(baseVal.AsDouble(), expVal.AsDouble()));
    }

    private static int CompareIntegerAndDecimal(BigInteger integer, decimal value)
    {
        // Scale the integer rather than narrowing it, so comparison remains exact even
        // when the integer cannot be represented by a decimal.
        var bits = decimal.GetBits(value);
        var coefficient = (BigInteger)(uint)bits[0]
            + ((BigInteger)(uint)bits[1] << 32)
            + ((BigInteger)(uint)bits[2] << 64);
        if (bits[3] < 0)
        {
            coefficient = -coefficient;
        }

        var scale = (bits[3] >> 16) & 0xff;
        return (integer * BigInteger.Pow(10, scale)).CompareTo(coefficient);
    }

    private static double DivideAsDouble(NumberValue a, NumberValue b)
    {
        // Convert bounded mantissas instead of entire integers to avoid infinity/infinity
        // when a very large numerator and denominator have a finite quotient.
        var numerator = a.AsDoubleMantissa(out var numeratorExponent);
        var denominator = b.AsDoubleMantissa(out var denominatorExponent);
        return Math.ScaleB(numerator / denominator, numeratorExponent - denominatorExponent);
    }

    private double AsDoubleMantissa(out int exponent)
    {
        exponent = 0;
        if (Type is not DataType.Integer)
        {
            return AsDouble();
        }

        var magnitude = BigInteger.Abs(IntVal);
        exponent = (int)Math.Max(0, magnitude.GetBitLength() - 53);
        var mantissa = (double)(magnitude >> exponent);
        return IntVal.Sign < 0 ? -mantissa : mantissa;
    }
}
