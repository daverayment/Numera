using System.Globalization;
using System.Numerics;

namespace Numera;

/// <summary>
/// Parses mathematical expressions, mapping standard order of operations and interpreting
/// transcendental functions, rounding, negative roots, and factorials.
/// </summary>
internal sealed class ExpressionParser
{
    private readonly string _input;
    private readonly EvaluationOptions _options;
    private int _pos;
    private int _depth;

    public ExpressionParser(string input, EvaluationOptions options)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Length > options.MaxInputLength)
        {
            throw new CalculationLimitException();
        }

        _input = input;
        _options = options;
        _pos = 0;
    }

    public NumberValue Evaluate()
    {
        var result = ParseLogical();
        SkipWhitespace();

        if (_pos < _input.Length)
        {
            throw SyntaxError($"Unexpected character '{_input[_pos]}'.");
        }

        return result;
    }

    private NumberValue ParseLogical(bool evaluate = true)
    {
        var left = ParseLogicalAnd(evaluate);
        while (true)
        {
            SkipWhitespace();
            if (Peek() != '|' || Peek(1) != '|')
            {
                break;
            }

            Consume();
            Consume();
            var right = ParseLogicalAnd(evaluate && left.IsZero);
            if (evaluate)
            {
                left = NumberValue.LogicalOr(left, right);
            }
        }

        return left;
    }

    private NumberValue ParseLogicalAnd(bool evaluate)
    {
        var left = ParseEquality(evaluate);
        while (true)
        {
            SkipWhitespace();
            if (Peek() != '&' || Peek(1) != '&')
            {
                break;
            }

            Consume();
            Consume();
            var right = ParseEquality(evaluate && !left.IsZero);
            if (evaluate)
            {
                left = NumberValue.LogicalAnd(left, right);
            }
        }

        return left;
    }

    private NumberValue ParseEquality(bool evaluate)
    {
        var left = ParseExpression(evaluate);
        while (true)
        {
            SkipWhitespace();
            if (Peek() == '=' && Peek(1) == '=')
            {
                Consume();
                Consume();
                var right = ParseExpression(evaluate);
                if (evaluate)
                {
                    left = NumberValue.AreEqual(left, right);
                }
            }
            else if (Peek() == '~' && Peek(1) == '=')
            {
                Consume();
                Consume();
                var right = ParseExpression(evaluate);
                if (evaluate)
                {
                    left = NumberValue.AreNotEqual(left, right);
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
            var right = ParseUnary(evaluate); // Right-associative, e.g. 2^3^2 = 2^(3^2)
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

                // Validate skipped operands too, without evaluating their functions.
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

                return evaluate ? EvaluateFunction(identifier, args) : default;
            }

            return EvaluateConstant(identifier);
        }

        return ParseNumber();
    }

    private NumberValue ParseNumber()
    {
        var start = _pos;
        var hasDecimal = false;

        while (_pos < _input.Length && (char.IsDigit(Peek()) || Peek() == '.'))
        {
            if (Peek() == '.')
            {
                if (hasDecimal)
                {
                    throw SyntaxError("Unexpected character '.'.");
                }

                hasDecimal = true;
            }

            Consume();
        }

        var numberStr = _input[start.._pos];

        if (!hasDecimal && BigInteger.TryParse(
            numberStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            return new NumberValue(intValue);
        }
        else if (decimal.TryParse(
            numberStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var decValue))
        {
            return new NumberValue(decValue);
        }

        if (double.TryParse(
            numberStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
        {
            return new NumberValue(doubleValue);
        }

        throw new ExpressionException(
            new EvaluationDiagnostic(EvaluationError.InvalidSyntax, "Expected a number.", start, Math.Max(1, _pos - start)));
    }

    private string ParseIdentifier()
    {
        var start = _pos;
        while (_pos < _input.Length && (char.IsLetterOrDigit(Peek()) || Peek() == 'π'))
        {
            Consume();
        }

        return _input.Substring(start, _pos - start).ToLowerInvariant();
    }

    private NumberValue EvaluateConstant(string name)
    {
        if (name is "pi" or "π")
        {
            return new NumberValue(Math.PI);
        }

        if (name is "e")
        {
            return new NumberValue(Math.E);
        }

        throw new ExpressionException(
            new EvaluationDiagnostic(EvaluationError.UnknownConstant, $"Unknown constant '{name}'.", _pos - name.Length, name.Length));
    }

    private static NumberValue EvaluateFunction(string name, List<NumberValue> args)
    {
        if (name is "min" or "max")
        {
            return EvaluateMinMax(args, name == "min");
        }

        if (args.Count == 0)
        {
            return new NumberValue(Random.Shared.NextDouble());
        }

        if (args.Count == 1)
        {
            var v = args[0].AsDouble();
            switch (name)
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
                    return new NumberValue(Math.Log10(v));

                case "log2":
                    return new NumberValue(Math.Log2(v));

                case "log10":
                    return new NumberValue(Math.Log10(v));

                case "ln":
                    return new NumberValue(Math.Log(v));

                case "sqrt":
                    return new NumberValue(Math.Sqrt(v));

                case "factorial":
                    if (!args[0].TryGetInteger(out var factorialArgument) || factorialArgument < 0)
                    {
                        return new NumberValue(double.NaN);
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
                        // Required for Sign(Sqrt(-1)) to return 0 instead of throwing an exception.
                        return new NumberValue(0m);
                    }

                    return new NumberValue((double)Math.Sign(v));

                case "round":
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
                    if (!args[0].TryGetInteger(out var randomUpperBound) || randomUpperBound <= 0 || randomUpperBound > long.MaxValue)
                    {
                        return new NumberValue(double.NaN);
                    }

                    return new NumberValue((BigInteger)Random.Shared.NextInt64((long)randomUpperBound));

                case "sin":
                    return new NumberValue(Math.Sin(v));

                case "cos":
                    return new NumberValue(Math.Cos(v));

                case "tan":
                    return new NumberValue(Math.Tan(v));

                case "arcsin":
                    return new NumberValue(Math.Asin(v));

                case "arccos":
                    return new NumberValue(Math.Acos(v));

                case "arctan":
                    return new NumberValue(Math.Atan(v));

                case "cot":
                    return new NumberValue(1.0 / Math.Tan(v));

                case "sec":
                    return new NumberValue(1.0 / Math.Cos(v));

                case "csc":
                    return new NumberValue(1.0 / Math.Sin(v));

                case "arccot":
                    return new NumberValue(Math.Atan2(1.0, v));

                case "arcsec":
                    return new NumberValue(Math.Acos(1.0 / v));

                case "arccsc":
                    return new NumberValue(Math.Asin(1.0 / v));

                case "sinh":
                    return new NumberValue(Math.Sinh(v));

                case "cosh":
                    return new NumberValue(Math.Cosh(v));

                case "tanh":
                    return new NumberValue(Math.Tanh(v));

                case "arsinh":
                    return new NumberValue(Math.Asinh(v));

                case "arcosh":
                    return new NumberValue(Math.Acosh(v));

                case "artanh":
                    return new NumberValue(Math.Atanh(v));

                case "coth":
                    return new NumberValue(1.0 / Math.Tanh(v));

                case "sech":
                    return new NumberValue(1.0 / Math.Cosh(v));

                case "csch":
                    return new NumberValue(1.0 / Math.Sinh(v));

                case "arcoth":
                    return new NumberValue(Math.Atanh(1.0 / v));

                case "arsech":
                    return new NumberValue(Math.Acosh(1.0 / v));

                case "arcsch":
                    return new NumberValue(Math.Asinh(1.0 / v));

                case "rad":
                    return new NumberValue(v * (Math.PI / 180.0));

                case "deg":
                    return new NumberValue(v * (180.0 / Math.PI));

                case "grad":
                    return new NumberValue(v * (200.0 / Math.PI));

                default:
                    throw new FormatException($"Unknown function '{name}'.");
            }
        }

        if (args.Count == 2)
        {
            switch (name)
            {
                case "pow":
                    return NumberValue.Pow(args[0], args[1]);

                case "logn":
                    return new NumberValue(Math.Log(args[0].AsDouble(), args[1].AsDouble()));

                case "root":
                    var baseVal = args[0].AsDouble();
                    var rootVal = args[1].AsDouble();
                    if (baseVal < 0 && Math.Abs(rootVal % 2) == 1)
                    {
                        // Negative real root handling for odd numbers, e.g. -8^(1/3) = -2.
                        return new NumberValue(-Math.Pow(-baseVal, 1.0 / rootVal));
                    }

                    return new NumberValue(Math.Pow(baseVal, 1.0 / rootVal));

                default:
                    throw new FormatException($"Unknown function '{name}'.");
            }
        }

        throw new FormatException($"Too many arguments for function '{name}'.");
    }

    private static NumberValue EvaluateMinMax(List<NumberValue> args, bool findMinimum)
    {
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
}
