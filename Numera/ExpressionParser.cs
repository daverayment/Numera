using System.Globalization;
using System.Numerics;

namespace Numera;

/// <summary>
/// Evaluates one mathematical expression using recursive descent, without retaining an
/// abstract syntax tree. Each instance owns the parsing state for a single evaluation.
/// </summary>
/// <remarks>
/// Precedence, from lowest to highest, is OR, XOR, AND, chained comparison
/// (equality and relational), bitwise shift (&lt;&lt;, &gt;&gt;), addition/subtraction,
/// multiplication/division/remainder, unary signs/negation, and exponentiation.
/// Grouping and function arguments restart at the lowest precedence.
/// </remarks>
internal sealed class ExpressionParser
{
    private readonly string _input;
    private readonly EvaluationOptions _options;
    // Cursor positions also identify diagnostic locations in the original input.

    private int _pos;
    private int _depth;

    private enum ComparisonOp
    {
        None,
        Equal,
        NotEqual,
        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual,
    }

    public ExpressionParser(string input, EvaluationOptions options)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Length > options.MaxInputLength)
        {
            throw new CalculationLimitException();
        }

        _input = input;
        _options = options;
    }

    public NumberValue Evaluate()
    {
        var result = ParseLogical();
        SkipWhitespace();

        // A valid prefix is not enough: the entire expression must be consumed.
        if (_pos < _input.Length)
        {
            throw SyntaxError($"Unexpected character '{_input[_pos]}'.");
        }

        return result;
    }

    // With evaluate=false, parsing and validation still run, but operators and function
    // execution are skipped. Returned values on that path are not evaluation results.
    private NumberValue ParseLogical(bool evaluate = true)
    {
        // Recursive descent starts at the loosest-binding level (lowest precedence)
        // and descends toward tighter-binding levels:
        // ParseLogical (OR) -> ParseLogicalXor (XOR) -> ParseLogicalAnd (AND) -> ParseEquality -> ...
        // A method delegates to the next level for its operands, so the deeper levels
        // (AND) are evaluated before the outer levels (OR).
        var left = ParseLogicalXor(evaluate);
        while (true)
        {
            SkipWhitespace();
            if (Peek() == '|' && Peek(1) == '|')
            {
                Consume();
                Consume();
                var right = ParseLogicalXor(evaluate && left.IsZero);
                if (evaluate)
                {
                    left = NumberValue.LogicalOr(left, right);
                }
            }
            else if (MatchKeyword("or"))
            {
                var right = ParseLogicalXor(evaluate && left.IsZero);
                if (evaluate)
                {
                    left = NumberValue.LogicalOr(left, right);
                }
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private NumberValue ParseLogicalXor(bool evaluate)
    {
        var left = ParseLogicalAnd(evaluate);
        while (true)
        {
            SkipWhitespace();
            if (MatchKeyword("xor"))
            {
                var right = ParseLogicalAnd(evaluate);
                if (evaluate)
                {
                    left = NumberValue.LogicalXor(left, right);
                }
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private NumberValue ParseLogicalAnd(bool evaluate)
    {
        var left = ParseComparison(evaluate);
        while (true)
        {
            SkipWhitespace();
            if (Peek() == '&' && Peek(1) == '&')
            {
                Consume();
                Consume();
                var right = ParseComparison(evaluate && !left.IsZero);
                if (evaluate)
                {
                    left = NumberValue.LogicalAnd(left, right);
                }
            }
            else if (MatchKeyword("and"))
            {
                var right = ParseComparison(evaluate && !left.IsZero);
                if (evaluate)
                {
                    left = NumberValue.LogicalAnd(left, right);
                }
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private NumberValue ParseComparison(bool evaluate)
    {
        var left = ParseShift(evaluate);
        bool hasComparison = false;
        bool allPassed = true;

        while (true)
        {
            SkipWhitespace();
            var op = MatchComparisonOperator();
            if (op == ComparisonOp.None)
            {
                break;
            }

            hasComparison = true;

            // Short-circuit. If a previous comparison failed, skip the next operand and
            // comparison evaluation to avoid unnecessary work and potential exceptions
            // (e.g. division by zero).
            bool evaluateCurrent = evaluate && allPassed;
            var right = ParseShift(evaluateCurrent);

            if (evaluateCurrent)
            {
                if (!EvaluateComparison(op, left, right))
                {
                    allPassed = false;
                }
            }

            // For chained comparisons, the right operand becomes the left operand for the
            // next comparison.
            left = right;
        }

        if (!hasComparison)
        {
            return left;
        }

        return allPassed ? new NumberValue(1m) : new NumberValue(0m);
    }

    private static bool EvaluateComparison(ComparisonOp op, NumberValue left, NumberValue right)
    {
        int cmp = NumberValue.Compare(left, right);
        return op switch
        {
            ComparisonOp.Equal => cmp == 0,
            ComparisonOp.NotEqual => cmp != 0,
            ComparisonOp.LessThan => cmp < 0,
            ComparisonOp.LessThanOrEqual => cmp <= 0,
            ComparisonOp.GreaterThan => cmp > 0,
            ComparisonOp.GreaterThanOrEqual => cmp >= 0,
            _ => false,
        };
    }

    private ComparisonOp MatchComparisonOperator()
    {
        ComparisonOp op = Peek() switch
        {
            '=' when Peek(1) == '=' => ComparisonOp.Equal,
            '!' when Peek(1) == '=' => ComparisonOp.NotEqual,
            '~' when Peek(1) == '=' => ComparisonOp.NotEqual,
            '<' when Peek(1) == '=' => ComparisonOp.LessThanOrEqual,
            '>' when Peek(1) == '=' => ComparisonOp.GreaterThanOrEqual,
            '<' => ComparisonOp.LessThan,
            '>' => ComparisonOp.GreaterThan,
            _ => ComparisonOp.None,
        };

        if (op == ComparisonOp.None)
        {
            return ComparisonOp.None;
        }

        Consume();
        if (op is ComparisonOp.Equal or ComparisonOp.NotEqual or ComparisonOp.LessThanOrEqual or ComparisonOp.GreaterThanOrEqual)
        {
            Consume();
        }

        return op;
    }

    private NumberValue ParseShift(bool evaluate)
    {
        var left = ParseExpression(evaluate);
        while (true)
        {
            SkipWhitespace();
            if (Peek() == '<' && Peek(1) == '<')
            {
                Consume();
                Consume();
                var right = ParseExpression(evaluate);
                if (evaluate)
                {
                    left = NumberValue.ShiftLeft(left, right);
                }
            }
            else if (Peek() == '>' && Peek(1) == '>')
            {
                Consume();
                Consume();
                var right = ParseExpression(evaluate);
                if (evaluate)
                {
                    left = NumberValue.ShiftRight(left, right);
                }
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private NumberValue ParseExpression(bool evaluate)
    {
        var left = ParseTerm(evaluate);
        while (true)
        {
            SkipWhitespace();
            if (Peek() == '+')
            {
                Consume();
                var right = ParseTerm(evaluate);
                if (evaluate)
                {
                    left += right;
                }
            }
            else if (Peek() == '-')
            {
                Consume();
                var right = ParseTerm(evaluate);
                if (evaluate)
                {
                    left -= right;
                }
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private NumberValue ParseTerm(bool evaluate)
    {
        var left = ParseUnary(evaluate);
        while (true)
        {
            SkipWhitespace();
            if (Peek() == '*')
            {
                Consume();
                var right = ParseUnary(evaluate);
                if (evaluate)
                {
                    left *= right;
                }
            }
            else if (Peek() == '/')
            {
                Consume();
                var right = ParseUnary(evaluate);
                if (evaluate)
                {
                    left /= right;
                }
            }
            else if (Peek() == '%')
            {
                Consume();
                var right = ParseUnary(evaluate);
                if (evaluate)
                {
                    left %= right;
                }
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private NumberValue ParseUnary(bool evaluate)
    {
        // Recursive grammar paths return through here, so one counter bounds grouping,
        // function arguments, unary chains, and right-associative powers alike.
        if (_depth >= _options.MaxNestingDepth)
        {
            throw new CalculationLimitException();
        }

        _depth++;
        try
        {
            SkipWhitespace();
            if (Peek() == '-')
            {
                Consume();
                var value = ParseUnary(evaluate);
                return evaluate ? -value : default;
            }
            else if (Peek() == '+')
            {
                Consume();
                return ParseUnary(evaluate);
            }
            else if (Peek() == '!')
            {
                Consume();
                var value = ParseUnary(evaluate);
                return evaluate ? NumberValue.LogicalNot(value) : default;
            }
            else if (MatchKeyword("not"))
            {
                var value = ParseUnary(evaluate);
                return evaluate ? NumberValue.LogicalNot(value) : default;
            }

            // Powers bind more tightly than a leading sign: -2^2 means -(2^2).
            return ParsePower(evaluate);
        }
        finally
        {
            _depth--;
        }
    }

    private NumberValue ParsePower(bool evaluate)
    {
        var left = ParsePrimary(evaluate);
        SkipWhitespace();
        if (Peek() == '^')
        {
            Consume();

            // Recursing through unary permits signed exponents and makes powers
            // right-associative: 2^-3 is valid, and 2^3^2 means 2^(3^2).
            var right = ParseUnary(evaluate);
            if (evaluate)
            {
                left = NumberValue.Pow(left, right);
            }
        }

        return left;
    }

    private NumberValue ParsePrimary(bool evaluate)
    {
        SkipWhitespace();
        if (_pos >= _input.Length)
        {
            throw SyntaxError("Unexpected end of input.");
        }

        var c = Peek();

        // Handle both () and [] as standard grouping brackets.
        if (c is '(' or '[')
        {
            Consume();
            var value = ParseLogical(evaluate);
            SkipWhitespace();

            var expectedClosing = c == '(' ? ')' : ']';

            if (Peek() != expectedClosing)
            {
                throw SyntaxError($"Expected '{expectedClosing}'.");
            }

            Consume();
            return value;
        }

        if (char.IsLetter(c) || c == 'π')
        {
            var identifierStart = _pos;
            var identifier = ParseIdentifier();
            SkipWhitespace();
            if (Peek() == '(')
            {
                Consume();
                SkipWhitespace();
                var args = new List<NumberValue>();
                if (Peek() != ')')
                {
                    while (true)
                    {
                        args.Add(ParseLogical(evaluate));
                        SkipWhitespace();
                        if (Peek() == ',')
                        {
                            Consume();
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                if (Peek() == ')')
                {
                    Consume();
                }
                else
                {
                    throw SyntaxError("Expected ')'.");
                }

                // Short-circuiting suppresses execution, not errors in function names
                // or argument counts. Validation also protects the dispatch below.
                if (!FunctionArity.TryGet(identifier, out var signature))
                {
                    throw new ExpressionException(new EvaluationDiagnostic(
                        EvaluationError.UnknownFunction, $"Unknown function '{identifier}'.", identifierStart, identifier.Length));
                }

                if (args.Count < signature.MinimumArguments || (signature.MaximumArguments is int maximum && args.Count > maximum))
                {
                    throw new ExpressionException(new EvaluationDiagnostic(
                        EvaluationError.InvalidArgumentCount, $"Invalid argument count for '{identifier}'.", identifierStart, identifier.Length));
                }

                if (evaluate)
                {
                    return EvaluateFunction(identifier, args);
                }

                return default;
            }

            // Constants and literals are still resolved in skipped operands, so invalid
            // names and malformed numbers cannot hide behind short-circuiting.
            return EvaluateConstant(identifier);
        }

        return ParseNumber();
    }

    private NumberValue ParseNumber()
    {
        var start = _pos;

        // Check for radix-prefixed integer literals: 0x (hex), 0b (binary), 0o (octal).
        if (Peek() == '0' && _pos + 1 < _input.Length)
        {
            var prefix = char.ToLowerInvariant(Peek(1));
            if (prefix is 'x' or 'b' or 'o')
            {
                Consume(); // '0'
                Consume(); // 'x', 'b', or 'o'

                var digitsStart = _pos;
                while (_pos < _input.Length && (prefix switch
                {
                    'x' => char.IsAsciiHexDigit(Peek()),
                    'b' => Peek() is '0' or '1',
                    'o' => Peek() is >= '0' and <= '7',
                    _ => false,
                }))
                {
                    Consume();
                }

                if (_pos == digitsStart)
                {
                    throw SyntaxError($"Expected {prefix switch { 'x' => "hexadecimal", 'b' => "binary", 'o' => "octal", _ => "radix" }} digits after '0{prefix}'.");
                }

                var digitsSpan = _input.AsSpan(digitsStart, _pos - digitsStart);
                var value = prefix switch
                {
                    'x' => ParseHexBigInteger(digitsSpan),
                    'b' => ParseBinaryBigInteger(digitsSpan),
                    'o' => ParseOctalBigInteger(digitsSpan),
                    _ => throw SyntaxError("Unsupported number prefix."),
                };

                return new NumberValue(value);
            }
        }

        var hasDecimal = false;
        var hasExponent = false;

        // Signs belong to ParseUnary. Literal syntax allows digits, an optional decimal point,
        // and an optional scientific exponent (e/E optionally followed by +/- and digits).
        while (_pos < _input.Length)
        {
            var ch = Peek();
            if (char.IsDigit(ch))
            {
                Consume();
            }
            else if (ch == '.')
            {
                if (hasDecimal || hasExponent)
                {
                    break;
                }

                hasDecimal = true;
                Consume();
            }
            else if (ch is 'e' or 'E')
            {
                if (hasExponent)
                {
                    break;
                }

                // Check ahead: exponent indicator must be followed by optional sign and at least one digit.
                var nextPos = 1;
                if (Peek(nextPos) is '+' or '-')
                {
                    nextPos++;
                }

                if (!char.IsDigit(Peek(nextPos)))
                {
                    break;
                }

                hasExponent = true;
                Consume(); // 'e' or 'E'
                if (Peek() is '+' or '-')
                {
                    Consume();
                }

                while (_pos < _input.Length && char.IsDigit(Peek()))
                {
                    Consume();
                }
            }
            else
            {
                break;
            }
        }

        var numberSpan = _input.AsSpan(start, _pos - start);

        // Keep whole-number literals without exponents as arbitrary-precision integers. For literals with
        // a decimal point or scientific exponent, prefer decimal to binary floating point; parsing may still
        // round excess digits. Double is a fallback for overflow or underflow to zero.
        if (!hasDecimal && !hasExponent && BigInteger.TryParse(
            numberSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            return new NumberValue(intValue);
        }
        else if (decimal.TryParse(
            numberSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out var decValue))
        {
            // If decimal parsed to zero but the literal contains non-zero digits, the literal
            // underflowed decimal's 28-29 digit scale limit. Fall through to double so the
            // non-zero magnitude is preserved and expressions like 1 / 0.000...001 do not divide by zero.
            if (decValue != 0m || numberSpan.IndexOfAnyInRange('1', '9') < 0)
            {
                return new NumberValue(decValue);
            }
        }

        if (double.TryParse(
            numberSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
        {
            return new NumberValue(doubleValue);
        }

        throw new ExpressionException(
            new EvaluationDiagnostic(EvaluationError.InvalidSyntax, "Expected a number.", start, Math.Max(1, _pos - start)));
    }

    private static BigInteger ParseHexBigInteger(ReadOnlySpan<char> span)
    {
        var result = BigInteger.Zero;
        foreach (var ch in span)
        {
            var digit = ch switch
            {
                >= '0' and <= '9' => ch - '0',
                >= 'a' and <= 'f' => ch - 'a' + 10,
                >= 'A' and <= 'F' => ch - 'A' + 10,
                _ => 0,
            };
            result = (result << 4) | (uint)digit;
        }

        return result;
    }

    private static BigInteger ParseBinaryBigInteger(ReadOnlySpan<char> span)
    {
        var result = BigInteger.Zero;
        foreach (var ch in span)
        {
            result = (result << 1) | (uint)(ch - '0');
        }

        return result;
    }

    private static BigInteger ParseOctalBigInteger(ReadOnlySpan<char> span)
    {
        var result = BigInteger.Zero;
        foreach (var ch in span)
        {
            result = (result << 3) | (uint)(ch - '0');
        }

        return result;
    }

    private ReadOnlySpan<char> ParseIdentifier()
    {
        var start = _pos;
        while (_pos < _input.Length && (char.IsLetterOrDigit(Peek()) || Peek() == 'π' || Peek() == 'Π'))
        {
            Consume();
        }

        return _input.AsSpan(start, _pos - start);
    }

    private NumberValue EvaluateConstant(ReadOnlySpan<char> name)
    {
        if (name.Equals("pi", StringComparison.OrdinalIgnoreCase) || name.SequenceEqual("π") || name.SequenceEqual("Π"))
        {
            return new NumberValue(Math.PI);
        }

        if (name.Equals("e", StringComparison.OrdinalIgnoreCase))
        {
            return new NumberValue(Math.E);
        }

        throw new ExpressionException(
            new EvaluationDiagnostic(EvaluationError.UnknownConstant, $"Unknown constant '{name}'.", _pos - name.Length, name.Length));
    }

    private NumberValue EvaluateFunction(ReadOnlySpan<char> name, List<NumberValue> args)
    {
        Span<char> lower = stackalloc char[name.Length];
        name.ToLowerInvariant(lower);

        // ParsePrimary has already validated the canonical name and argument count.
        if (lower is "min" or "max")
        {
            return EvaluateMinMax(args, lower is "min");
        }

        if (lower is "avg")
        {
            return EvaluateAvg(args);
        }

        if (args.Count == 0)
        {
            // rand is the only registered zero-argument function.
            return new NumberValue(Random.Shared.NextDouble());
        }

        if (args.Count == 1)
        {
            // Math-based functions use double approximations. Branches that can operate
            // on integer or decimal values use the original argument instead.
            var v = args[0].AsDouble();
            switch (lower)
            {
                case "ceil":
                    return args[0].Type switch
                    {
                        NumberValue.DataType.Integer => args[0],
                        NumberValue.DataType.Decimal => new NumberValue(Math.Ceiling(args[0].DecVal)),
                        _ => new NumberValue(Math.Ceiling(v)),
                    };

                case "floor":
                    return args[0].Type switch
                    {
                        NumberValue.DataType.Integer => args[0],
                        NumberValue.DataType.Decimal => new NumberValue(Math.Floor(args[0].DecVal)),
                        _ => new NumberValue(Math.Floor(v)),
                    };

                case "trunc":
                    return args[0].Type switch
                    {
                        NumberValue.DataType.Integer => args[0],
                        NumberValue.DataType.Decimal => new NumberValue(decimal.Truncate(args[0].DecVal)),
                        _ => new NumberValue(Math.Truncate(v)),
                    };

                case "frac":
                    return args[0].Type switch
                    {
                        NumberValue.DataType.Integer => new NumberValue(0m),
                        NumberValue.DataType.Decimal => new NumberValue(args[0].DecVal - decimal.Truncate(args[0].DecVal)),
                        _ => new NumberValue(v - Math.Truncate(v)),
                    };

                case "cbrt":
                    return new NumberValue(Math.Cbrt(v));

                case "bnot":
                    return NumberValue.BitwiseNot(args[0]);

                case "exp":
                    return new NumberValue(Math.Exp(v));

                case "abs":
                    if (args[0].Type is NumberValue.DataType.Integer)
                    {
                        return new NumberValue(BigInteger.Abs(args[0].IntVal));
                    }

                    if (args[0].Type is NumberValue.DataType.Decimal)
                    {
                        return new NumberValue(Math.Abs(args[0].DecVal));
                    }

                    return new NumberValue(Math.Abs(v));

                case "log":
                case "log10":
                    if (v <= 0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "Logarithm is undefined for non-positive values."));
                    }
                    return new NumberValue(Math.Log10(v));

                case "log2":
                    if (v <= 0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "Logarithm is undefined for non-positive values."));
                    }
                    return new NumberValue(Math.Log2(v));

                case "ln":
                    if (v <= 0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "Natural logarithm is undefined for non-positive values."));
                    }
                    return new NumberValue(Math.Log(v));

                case "sqrt":
                    if (v < 0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "Square root of a negative number has no real solution."));
                    }
                    return new NumberValue(Math.Sqrt(v));

                case "factorial":
                    if (!args[0].TryGetInteger(out var factorialArgument))
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.IntegerRequired, "Factorial requires an integer argument."));
                    }

                    if (factorialArgument < 0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.ArgumentOutOfRange, "Factorial is undefined for negative integers."));
                    }

                    if (factorialArgument > CalculationLimits.MaxFactorialArgument)
                    {
                        throw new CalculationLimitException();
                    }

                    BigInteger f = 1;
                    for (var i = 2; i <= (int)factorialArgument; i++)
                    {
                        f *= i;
                    }

                    return new NumberValue(f);

                case "sign":
                case "sgn":
                    if (args[0].Type is NumberValue.DataType.Integer)
                    {
                        return new NumberValue((BigInteger)args[0].IntVal.Sign);
                    }

                    if (args[0].Type is NumberValue.DataType.Decimal)
                    {
                        return new NumberValue((decimal)Math.Sign(args[0].DecVal));
                    }

                    if (double.IsNaN(v))
                    {
                        // The calculator defines sign(NaN) as zero (e.g. sign(sqrt(-1))),
                        // whereas Math.Sign would throw for NaN.
                        return new NumberValue(0m);
                    }

                    return new NumberValue((double)Math.Sign(v));

                case "round":
                    // Calculator midpoint rounding is away from zero, not Math.Round's
                    // default ties-to-even behavior.
                    if (args[0].Type is NumberValue.DataType.Integer)
                    {
                        return args[0];
                    }

                    if (args[0].Type is NumberValue.DataType.Decimal)
                    {
                        return new NumberValue(Math.Round(args[0].DecVal, MidpointRounding.AwayFromZero));
                    }

                    return new NumberValue(Math.Round(v, MidpointRounding.AwayFromZero));

                case "randi":
                    if (!args[0].TryGetInteger(out var randomUpperBound))
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.IntegerRequired, "randi requires an integer bound."));
                    }

                    if (randomUpperBound <= 0 || randomUpperBound > long.MaxValue)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.ArgumentOutOfRange, "randi bound must be a positive integer within valid limits."));
                    }

                    return new NumberValue((BigInteger)Random.Shared.NextInt64((long)randomUpperBound));

                case "sin":
                    return new NumberValue(EvaluateSin(v));

                case "cos":
                    return new NumberValue(EvaluateCos(v));

                case "tan":
                    return new NumberValue(EvaluateTan(v));

                case "arcsin":
                    if (v < -1.0 || v > 1.0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "arcsin argument must be between -1 and 1."));
                    }
                    return new NumberValue(EvaluateArcSin(v));

                case "arccos":
                    if (v < -1.0 || v > 1.0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "arccos argument must be between -1 and 1."));
                    }
                    return new NumberValue(EvaluateArcCos(v));

                case "arctan":
                    return new NumberValue(EvaluateArcTan(v));

                case "cot":
                    return new NumberValue(EvaluateCot(v));

                case "sec":
                    return new NumberValue(EvaluateSec(v));

                case "csc":
                    return new NumberValue(EvaluateCsc(v));

                case "arccot":
                    return new NumberValue(EvaluateArcCot(v));

                case "arcsec":
                    if (Math.Abs(v) < 1.0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "arcsec argument magnitude must be at least 1."));
                    }
                    return new NumberValue(EvaluateArcSec(v));

                case "arccsc":
                    if (Math.Abs(v) < 1.0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "arccsc argument magnitude must be at least 1."));
                    }
                    return new NumberValue(EvaluateArcCsc(v));

                case "sinh":
                    return new NumberValue(Math.Sinh(v));

                case "cosh":
                    return new NumberValue(Math.Cosh(v));

                case "tanh":
                    return new NumberValue(Math.Tanh(v));

                case "arsinh":
                    return new NumberValue(Math.Asinh(v));

                case "arcosh":
                    if (v < 1.0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "arcosh argument must be at least 1."));
                    }
                    return new NumberValue(Math.Acosh(v));

                case "artanh":
                    if (v <= -1.0 || v >= 1.0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "artanh argument must be strictly between -1 and 1."));
                    }
                    return new NumberValue(Math.Atanh(v));

                case "coth":
                    return new NumberValue(1.0 / Math.Tanh(v));

                case "sech":
                    return new NumberValue(1.0 / Math.Cosh(v));

                case "csch":
                    return new NumberValue(1.0 / Math.Sinh(v));

                case "arcoth":
                    if (Math.Abs(v) <= 1.0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "arcoth argument magnitude must be strictly greater than 1."));
                    }
                    return new NumberValue(Math.Atanh(1.0 / v));

                case "arsech":
                    if (v <= 0.0 || v > 1.0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "arsech argument must be in the range (0, 1]."));
                    }
                    return new NumberValue(Math.Acosh(1.0 / v));

                case "arcsch":
                    if (v == 0.0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "arcsch argument cannot be zero."));
                    }
                    return new NumberValue(Math.Asinh(1.0 / v));

                case "rad":
                case "deg2rad":
                    return new NumberValue(v * (Math.PI / 180.0));

                case "deg":
                case "rad2deg":
                    return new NumberValue(v * (180.0 / Math.PI));

                case "grad":
                case "grad2rad":
                    return new NumberValue(v * (Math.PI / 200.0));

                case "rad2grad":
                    return new NumberValue(v * (200.0 / Math.PI));

                case "deg2grad":
                    return new NumberValue(v * (200.0 / 180.0));

                case "grad2deg":
                    return new NumberValue(v * (180.0 / 200.0));

                default:
                    throw new FormatException($"Unknown function '{name}'.");
            }
        }

        if (args.Count == 2)
        {
            switch (lower)
            {
                case "pow":
                    return NumberValue.Pow(args[0], args[1]);

                case "logn":
                    var logVal = args[0].AsDouble();
                    var logBase = args[1].AsDouble();
                    if (logVal <= 0.0 || logBase <= 0.0 || logBase == 1.0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "logn requires positive arguments and base cannot be 1."));
                    }
                    return new NumberValue(Math.Log(logVal, logBase));

                case "atan2":
                    var y = args[0].AsDouble();
                    var x = args[1].AsDouble();
                    return new NumberValue(EvaluateArcTan2(y, x));

                case "hypot":
                    return new NumberValue(Math.Sqrt(args[0].AsDouble() * args[0].AsDouble() + args[1].AsDouble() * args[1].AsDouble()));

                case "root":
                    var baseVal = args[0].AsDouble();
                    var rootVal = args[1].AsDouble();
                    if (rootVal == 0.0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "Root degree cannot be zero."));
                    }
                    if (baseVal < 0)
                    {
                        if (Math.Abs(rootVal % 2) == 1)
                        {
                            // Odd integer roots have a real branch: root(-8, 3) = -2.
                            return new NumberValue(-Math.Pow(-baseVal, 1.0 / rootVal));
                        }

                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "Even root of a negative number has no real solution."));
                    }

                    return new NumberValue(Math.Pow(baseVal, 1.0 / rootVal));

                case "shl":
                    return NumberValue.ShiftLeft(args[0], args[1]);

                case "shr":
                    return NumberValue.ShiftRight(args[0], args[1]);

                case "band":
                    return NumberValue.BitwiseAnd(args[0], args[1]);

                case "bor":
                    return NumberValue.BitwiseOr(args[0], args[1]);

                case "bxor":
                    return NumberValue.BitwiseXor(args[0], args[1]);

                default:
                    throw new FormatException($"Unknown function '{name}'.");
            }
        }

        if (args.Count == 3)
        {
            switch (lower)
            {
                case "clamp":
                    return EvaluateClamp(args[0], args[1], args[2]);

                case "lerp":
                    // a + t * (b - a)
                    return args[0] + (args[2] * (args[1] - args[0]));

                default:
                    throw new FormatException($"Unknown function '{name}'.");
            }
        }

        throw new FormatException($"Too many arguments for function '{name}'.");
    }

    private static NumberValue EvaluateMinMax(List<NumberValue> args, bool findMinimum)
    {
        // Compare without converting every argument to double, and return the selected
        // operand unchanged so its numeric representation is preserved.
        var result = args[0];
        for (var i = 1; i < args.Count; i++)
        {
            var comparison = NumberValue.Compare(result, args[i]);
            if (findMinimum ? comparison > 0 : comparison < 0)
            {
                result = args[i];
            }
        }

        return result;
    }

    private static NumberValue EvaluateAvg(List<NumberValue> args)
    {
        var sum = args[0];
        for (var i = 1; i < args.Count; i++)
        {
            sum += args[i];
        }

        return sum / new NumberValue((BigInteger)args.Count);
    }

    private static NumberValue EvaluateClamp(NumberValue val, NumberValue min, NumberValue max)
    {
        if (NumberValue.Compare(min, max) > 0)
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.ArgumentOutOfRange, "Clamp minimum cannot be greater than maximum."));
        }

        if (NumberValue.Compare(val, min) < 0)
        {
            return min;
        }

        if (NumberValue.Compare(val, max) > 0)
        {
            return max;
        }

        return val;
    }

    // 100 revolutions (800 quarter-turns) provides ample range for practical angular calculations
    // while keeping error accumulation from IEEE 754 float representation well within CardinalEpsilon (1e-12).
    private const double MaxSnappingRevolutions = 100.0;
    private const double CardinalEpsilon = 1e-12;

    /// <summary>
    /// Normalizes an angle into multiples of 45 degrees (pi/4 radians, 50 gradians)
    /// across all supported angle units within a bounded revolution ceiling.
    /// Used by all trigonometric functions to ensure exact cardinal values and detect asymptotes.
    /// </summary>
    private bool TryGetQuarterTurn(double v, out long step45)
    {
        var (k, rev) = _options.AngleUnit switch
        {
            AngleUnit.Degrees => (v / 45.0, v / 360.0),
            AngleUnit.Gradians => (v / 50.0, v / 400.0),
            _ => (4.0 * (v / Math.PI), v / (2.0 * Math.PI)),
        };

        if (Math.Abs(rev) <= MaxSnappingRevolutions)
        {
            var roundK = Math.Round(k);
            if (Math.Abs(k - roundK) < CardinalEpsilon)
            {
                step45 = (long)roundK;
                return true;
            }
        }

        step45 = 0;
        return false;
    }

    private double EvaluateSin(double v)
    {
        // A 90-degree step occurs whenever step45 is even.
        if (TryGetQuarterTurn(v, out var step45) && (step45 % 2 == 0))
        {
            return (((step45 / 2 % 4) + 4) % 4) switch
            {
                0 => 0.0,
                1 => 1.0,
                2 => 0.0,
                3 => -1.0,
                _ => 0.0,
            };
        }

        return _options.AngleUnit switch
        {
            AngleUnit.Degrees => double.SinPi(v / 180.0),
            AngleUnit.Gradians => double.SinPi(v / 200.0),
            _ => Math.Sin(v),
        };
    }

    private double EvaluateCos(double v)
    {
        if (TryGetQuarterTurn(v, out var step45) && (step45 % 2 == 0))
        {
            return (((step45 / 2 % 4) + 4) % 4) switch
            {
                0 => 1.0,
                1 => 0.0,
                2 => -1.0,
                3 => 0.0,
                _ => 0.0,
            };
        }

        return _options.AngleUnit switch
        {
            AngleUnit.Degrees => double.CosPi(v / 180.0),
            AngleUnit.Gradians => double.CosPi(v / 200.0),
            _ => Math.Cos(v),
        };
    }

    private double EvaluateTan(double v)
    {
        if (TryGetQuarterTurn(v, out var step45))
        {
            return (((step45 % 4) + 4) % 4) switch
            {
                0 => 0.0,
                1 => 1.0,
                2 => throw new ExpressionException(new EvaluationDiagnostic(
                    EvaluationError.UndefinedMathResult, "Tangent is undefined at odd multiples of 90 degrees (pi/2).")),
                3 => -1.0,
                _ => 0.0,
            };
        }

        return _options.AngleUnit switch
        {
            AngleUnit.Degrees => double.TanPi(v / 180.0),
            AngleUnit.Gradians => double.TanPi(v / 200.0),
            _ => Math.Tan(v),
        };
    }

    private double EvaluateCot(double v)
    {
        if (TryGetQuarterTurn(v, out var step45))
        {
            return (((step45 % 4) + 4) % 4) switch
            {
                0 => throw new ExpressionException(new EvaluationDiagnostic(
                    EvaluationError.UndefinedMathResult, "Cotangent is undefined at multiples of 180 degrees (pi).")),
                1 => 1.0,
                2 => 0.0,
                3 => -1.0,
                _ => 0.0,
            };
        }

        var sin = EvaluateSin(v);
        if (sin == 0.0)
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.UndefinedMathResult, "Cotangent is undefined where sine is zero."));
        }

        return EvaluateCos(v) / sin;
    }

    private double EvaluateSec(double v)
    {
        var cos = EvaluateCos(v);
        if (cos == 0.0)
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.UndefinedMathResult, "Secant is undefined where cosine is zero."));
        }

        return 1.0 / cos;
    }

    private double EvaluateCsc(double v)
    {
        var sin = EvaluateSin(v);
        if (sin == 0.0)
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.UndefinedMathResult, "Cosecant is undefined where sine is zero."));
        }

        return 1.0 / sin;
    }

    private double EvaluateArcSin(double v) =>
        _options.AngleUnit switch
        {
            AngleUnit.Degrees => double.AsinPi(v) * 180.0,
            AngleUnit.Gradians => double.AsinPi(v) * 200.0,
            _ => Math.Asin(v),
        };

    private double EvaluateArcCos(double v) =>
        _options.AngleUnit switch
        {
            AngleUnit.Degrees => double.AcosPi(v) * 180.0,
            AngleUnit.Gradians => double.AcosPi(v) * 200.0,
            _ => Math.Acos(v),
        };

    private double EvaluateArcTan(double v) =>
        _options.AngleUnit switch
        {
            AngleUnit.Degrees => double.AtanPi(v) * 180.0,
            AngleUnit.Gradians => double.AtanPi(v) * 200.0,
            _ => Math.Atan(v),
        };

    private double EvaluateArcTan2(double y, double x) =>
        _options.AngleUnit switch
        {
            AngleUnit.Degrees => double.Atan2Pi(y, x) * 180.0,
            AngleUnit.Gradians => double.Atan2Pi(y, x) * 200.0,
            _ => Math.Atan2(y, x),
        };

    private double EvaluateArcCot(double v) =>
        _options.AngleUnit switch
        {
            AngleUnit.Degrees => double.Atan2Pi(1.0, v) * 180.0,
            AngleUnit.Gradians => double.Atan2Pi(1.0, v) * 200.0,
            _ => Math.Atan2(1.0, v),
        };

    private double EvaluateArcSec(double v) =>
        _options.AngleUnit switch
        {
            AngleUnit.Degrees => double.AcosPi(1.0 / v) * 180.0,
            AngleUnit.Gradians => double.AcosPi(1.0 / v) * 200.0,
            _ => Math.Acos(1.0 / v),
        };

    private double EvaluateArcCsc(double v) =>
        _options.AngleUnit switch
        {
            AngleUnit.Degrees => double.AsinPi(1.0 / v) * 180.0,
            AngleUnit.Gradians => double.AsinPi(1.0 / v) * 200.0,
            _ => Math.Asin(1.0 / v),
        };

    private ExpressionException SyntaxError(string message) =>
        new(new EvaluationDiagnostic(EvaluationError.InvalidSyntax, message, _pos, _pos < _input.Length ? 1 : 0));

    private char Peek(int ahead = 0) => _pos + ahead < _input.Length ? _input[_pos + ahead] : '\0';

    private void Consume() => _pos++;

    private void SkipWhitespace()
    {
        while (_pos < _input.Length && char.IsWhiteSpace(_input[_pos]))
        {
            _pos++;
        }
    }

    /// <summary>
    /// Matches a whole-word keyword case-insensitively and advances the cursor past it if found.
    /// Ensures the match is delimited and not merely a prefix of a longer identifier.
    /// </summary>
    private bool MatchKeyword(string keyword)
    {
        var len = keyword.Length;
        if (_pos + len > _input.Length)
        {
            return false;
        }

        if (!_input.AsSpan(_pos, len).Equals(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Must be delimited: next character cannot be letter, digit, or underscore.
        if (_pos + len < _input.Length)
        {
            var next = _input[_pos + len];
            if (char.IsLetterOrDigit(next) || next == '_')
            {
                return false;
            }
        }

        _pos += len;
        return true;
    }
}
