using System.Numerics;

namespace Numera.Ast;

/// <summary>
/// Evaluates an <see cref="ExpressionNode"/> tree. The parser has already validated
/// syntax, constants, function names and arity for every branch, so short-circuiting here
/// only skips evaluation work. Runtime failures are tagged with the offending node's
/// source span.
/// </summary>
internal sealed class AstEvaluator : IExpressionVisitor<MathValue>
{
    private readonly CalculatorOptions _options;
    private readonly EvaluationContext _context;
    private List<Variable>? _assignments;

    public IReadOnlyList<Variable>? Assignments => _assignments;

    public AstEvaluator(CalculatorOptions options, EvaluationContext? context = null)
    {
        _options = options;
        _context = context ?? EvaluationContext.Empty;
    }

    public MathValue Visit(NumberNode node) => Guard(node.Span, () => ValidateValue(node.Value));

    public MathValue Visit(IdentifierNode node)
    {
        // Constant resolution produces a high-precision decimal (up to 28 digits)
        // for direct inspection and linear decimal/integer arithmetic.
        if (ConstantCatalog.TryResolve(node.Name, out var constant, out decimal value))
        {
            return Guard(node.Span, () => ValidateValue(new MathValue(value).WithAccuracy(
                constant is ConstantKind.True or ConstantKind.False
                    ? CalculationAccuracy.Exact : CalculationAccuracy.Unverified)));
        }

        // Check recent assignments within this evaluation.
        if (_assignments is not null)
        {
            for (int i = _assignments.Count - 1; i >= 0; i--)
            {
                if (_assignments[i].Name.Equals(node.Name, Identifier.Comparison))
                {
                    return Guard(node.Span, () => ValidateValue(_assignments[i].Value));
                }
            }
        }

        // Check the evaluation context variables.
        if (_context.TryResolveFullVariable(node.Name, out var contextVar))
        {
            return Guard(node.Span, () => ValidateValue(contextVar.Value));
        }

        throw new ExpressionException(new EvaluationDiagnostic(
            EvaluationError.UnknownConstant, $"Unknown constant or variable '{node.Name}'.",
            node.Span.Start, node.Span.Length));
    }

    public MathValue Visit(UnaryNode node)
    {
        var value = node.Operand.Accept(this);
        try
        {
            return ValidateValue(PropagateAccuracy(node.Op switch
            {
                UnaryOperator.Negate => MathValue.Negate(value),
                UnaryOperator.Positive => value,
                UnaryOperator.LogicalNot => MathValue.LogicalNot(value),
                UnaryOperator.BitwiseNot => MathValue.BitwiseNot(value),
                UnaryOperator.Factorial => EvaluateFactorial(value),
                UnaryOperator.Percent => Divide(value, new MathValue(100)),
                _ => throw new ExpressionException(new EvaluationDiagnostic(
                    EvaluationError.InvalidSyntax, "Unknown unary operator.")),
            }, [value]));
        }
        catch (Exception ex) when (ex is ExpressionException or CalculationLimitException or DivideByZeroException or OverflowException)
        {
            throw Enrich(ex, node.Span);
        }
    }

    public MathValue Visit(BinaryNode node)
    {
        // Fast path with no spine allocation. Power nodes must take it too, or the loop
        // below would recurse into this method forever; they are not flattened because
        // they count towards nesting height (see BinaryNode.Height).
        if (node.Op == BinaryOperator.Power ||
            node.Left is not BinaryNode { Op: not BinaryOperator.Power })
        {
            return Apply(node, node.Left.Accept(this));
        }

        // Walk the non-power left spine iteratively so long
        // flat chains (e.g. thousands of additive terms) cannot overflow the call stack.
        // This is the same rule as BinaryNode.Height.
        var spine = new Stack<BinaryNode>();
        ExpressionNode current = node;
        while (current is BinaryNode bin &&
            bin.Op is not BinaryOperator.Power)
        {
            spine.Push(bin);
            current = bin.Left;
        }

        MathValue left = current.Accept(this);
        while (spine.Count > 0)
        {
            left = Apply(spine.Pop(), left);
        }

        return left;
    }

    private MathValue Apply(BinaryNode node, MathValue left)
    {
        if (node.Op == BinaryOperator.LogicalOr && !left.IsZero)
        {
            return Guard(node.Span, () => MathValue.LogicalOr(left, new MathValue(1)));
        }

        if (node.Op == BinaryOperator.LogicalAnd && left.IsZero)
        {
            return Guard(node.Span, () => MathValue.LogicalAnd(left, new MathValue(0)));
        }

        var right = node.Right.Accept(this);
        bool markup = PercentRules.IsMarkup(node);
        var one = new MathValue(1);
        return Guard(node.Span, () => ValidateValue(PropagateAccuracy(node.Op switch
        {
            BinaryOperator.Add when markup => Multiply(left, Add(one, right)),
            BinaryOperator.Subtract when markup => Multiply(left, Subtract(one, right)),
            BinaryOperator.Of => Multiply(left, right),
            BinaryOperator.Add => Add(left, right),
            BinaryOperator.Subtract => Subtract(left, right),
            BinaryOperator.Multiply => Multiply(left, right),
            BinaryOperator.Divide => Divide(left, right),
            BinaryOperator.Modulo => MathValue.Modulo(left, right),
            BinaryOperator.Power => MathValue.Pow(left, right, _options.MaxExponentMagnitude),
            BinaryOperator.ShiftLeft => MathValue.ShiftLeft(left, right),
            BinaryOperator.ShiftRight => MathValue.ShiftRight(left, right),
            BinaryOperator.LogicalAnd => MathValue.LogicalAnd(left, right),
            BinaryOperator.LogicalOr => MathValue.LogicalOr(left, right),
            BinaryOperator.LogicalXor => MathValue.LogicalXor(left, right),
            _ => throw new ExpressionException(
                new EvaluationDiagnostic(EvaluationError.InvalidSyntax, "Unknown binary operator.")),
        }, [left, right])));
    }

    /// <summary>
    /// Evaluates a comparison chain node by evaluating its operands and applying the
    /// comparison operators in sequence.
    /// </summary>
    /// <param name="node">The comparison chain node to evaluate.</param>
    /// <returns>The result of evaluating the comparison chain.</returns>
    public MathValue Visit(ComparisonChainNode node)
    {
        var left = node.Operands[0].Accept(this);
        bool exact = left.Accuracy == CalculationAccuracy.Exact;
        for (int i = 0; i < node.Operators.Count; i++)
        {
            // Later operands are skipped once a comparison fails, avoiding needless work
            // and errors.
            var right = node.Operands[i + 1].Accept(this);
            exact &= right.Accuracy == CalculationAccuracy.Exact;
            if (!Guard(node.Span, () => EvaluateComparison(node.Operators[i], left, right)))
            {
                return new MathValue(BigInteger.Zero).WithAccuracy(
                    exact ? CalculationAccuracy.Exact : CalculationAccuracy.Unverified);
            }

            // Reuse the middle operand: a < f() < b must call f() only once.
            left = right;
        }

        return new MathValue(BigInteger.One).WithAccuracy(
            exact ? CalculationAccuracy.Exact : CalculationAccuracy.Unverified);
    }

    /// <summary>
    /// Evaluates a function call node by evaluating its arguments and dispatching to the
    /// appropriate function implementation.
    /// </summary>
    /// <param name="node">The function call node to evaluate.</param>
    /// <returns>The result of evaluating the function call.</returns>
    public MathValue Visit(FunctionCallNode node)
    {
        var args = new List<MathValue>(node.Arguments.Count);
        foreach (var arg in node.Arguments)
        {
            args.Add(arg.Accept(this));
        }

        if (_options.CustomFunctions.TryGetFunction(node.FunctionName, out var customFunction))
        {
            try
            {
                var result = customFunction.Implementation(args);
                return ValidateValue(PropagateAccuracy(result, args));
            }
            catch (Exception ex)
            {
                throw Enrich(ex, node.Span);
            }
        }

        try
        {
            return ValidateValue(PropagateAccuracy(EvaluateFunction(node.FunctionName, args), args));
        }
        catch (Exception ex) when (ex is ExpressionException or CalculationLimitException or DivideByZeroException or OverflowException)
        {
            throw Enrich(ex, node.Span);
        }
    }

    /// <summary>
    /// Evaluates an assignment node by evaluating the expression referred to by its Value
    /// node.
    /// </summary>
    /// <param name="node">The assignment node to evaluate.</param>
    /// <returns>The result of evaluating the assignment expression.</returns>
    public MathValue Visit(AssignmentNode node)
    {
        // Evaluate the inner expression first, so chaining assignments like "x = y = 5"
        // will assign correctly.
        var value = node.Value.Accept(this);

        // Record the assignment in the list of assignments for later retrieval. Create
        // the list lazily. The AST is metadata only; future reads use the captured value.
        (_assignments ??= []).Add(
            new Variable(node.VariableName, value, node.Value, node.Definition));
        return value;
    }

    /// <summary>
    /// Guards an evaluation action against runtime exceptions and enriches them with
    /// source span information.
    /// </summary>
    private static T Guard<T>(TextSpan span, Func<T> action)
    {
        try
        {
            return action();
        }
        catch (Exception ex) when (ex is ExpressionException or CalculationLimitException or DivideByZeroException or OverflowException)
        {
            throw Enrich(ex, span);
        }
    }

    /// <summary>
    /// Creates an <see cref="ExpressionException"/> with an evaluation diagnostic and
    /// adds source span information when the diagnostic has no position already set.
    /// </summary>
    /// <remarks>Keeps an existing diagnostic position unchanged so more precise locations
    /// are not overwritten.</remarks>
    /// <param name="ex">Exception to convert or augment with an evaluation diagnostic.
    /// </param>
    /// <param name="span">Source span used to populate diagnostic position and length
    /// when available.</param>
    /// <returns>Original exception when it is already an <see
    /// cref="ExpressionException"/> with the same diagnostic; otherwise, a new
    /// <see cref="ExpressionException"/> that wraps the original exception and carries
    /// the resolved diagnostic.</returns>
    private static Exception Enrich(Exception ex, TextSpan span)
    {
        EvaluationDiagnostic diagnostic = ex switch
        {
            ExpressionException ee => ee.Diagnostic,
            CalculationLimitException limit => limit.Diagnostic,
            DivideByZeroException => new EvaluationDiagnostic(
                EvaluationError.DivisionByZero, "Division by zero."),
            OverflowException => new EvaluationDiagnostic(
                EvaluationError.NumericOverflow, "Numeric overflow."),
            _ => new EvaluationDiagnostic(
                EvaluationError.EvaluationFailed, ex.Message),
        };

        if (diagnostic.Position is null && span.HasValue)
        {
            diagnostic = diagnostic with { Position = span.Start, Length = span.Length };
        }

        if (ex is CalculationLimitException)
        {
            return new CalculationLimitException(diagnostic);
        }

        return ex is ExpressionException exception && ReferenceEquals(diagnostic, exception.Diagnostic)
            ? ex
            : new ExpressionException(diagnostic, ex);
    }

    private MathValue ValidateValue(MathValue value)
    {
        value.ValidateLimits(_options.MaxIntegerBits);
        return value;
    }

    private MathValue Add(MathValue left, MathValue right) => MathValue.Add(left, right);
    private MathValue Subtract(MathValue left, MathValue right) => MathValue.Subtract(left, right);
    private MathValue Multiply(MathValue left, MathValue right) => MathValue.Multiply(left, right);
    private MathValue Divide(MathValue left, MathValue right) => MathValue.Divide(left, right);

    // Provenance can only be downgraded here. Exact inputs do not certify an
    // approximate function result, and integer/boolean outputs do not erase uncertainty.
    private static MathValue PropagateAccuracy(MathValue value, IReadOnlyList<MathValue> args) =>
        args.Any(arg => arg.Accuracy != CalculationAccuracy.Exact)
            ? value.WithAccuracy(CalculationAccuracy.Unverified)
            : value;

    // Factorization above the deterministic primality range can depend on probable primes.
    private static MathValue FactorizationResult(BigInteger result, BigInteger input) =>
        new MathValue(result).WithAccuracy(input > ulong.MaxValue
            ? CalculationAccuracy.Unverified
            : CalculationAccuracy.Exact);

    private bool EvaluateComparison(ComparisonOperator op, MathValue left, MathValue right)
    {
        // IEEE 754: every comparison with NaN is false except inequality.
        if (left.IsNaN || right.IsNaN)
        {
            return op is ComparisonOperator.NotEqual;
        }

        int cmp = MathValue.Compare(left, right);
        return op switch
        {
            ComparisonOperator.Equal => cmp == 0,
            ComparisonOperator.NotEqual => cmp != 0,
            ComparisonOperator.LessThan => cmp < 0,
            ComparisonOperator.LessThanOrEqual => cmp <= 0,
            ComparisonOperator.GreaterThan => cmp > 0,
            ComparisonOperator.GreaterThanOrEqual => cmp >= 0,
            _ => false,
        };
    }

    private MathValue EvaluateFunction(ReadOnlySpan<char> name, List<MathValue> args)
    {
        Span<char> lower = stackalloc char[name.Length];
        name.ToLowerInvariant(lower);

        // ParsePrimary has already validated the canonical name and argument count.
        if (lower is "gcd")
        {
            return EvaluateGcd(args);
        }

        if (lower is "lcm")
        {
            return EvaluateLcm(args);
        }

        if (lower is "min" or "max")
        {
            return EvaluateMinMax(args, lower is "min");
        }

        if (lower is "avg")
        {
            return EvaluateAvg(args);
        }

        if (lower is "median")
        {
            return EvaluateMedian(args);
        }

        if (lower is "var" or "variance")
        {
            return EvaluateVariance(args);
        }

        if (lower is "stdev" or "stddev")
        {
            return EvaluateStDev(args);
        }

        if (lower is "sum")
        {
            return EvaluateSum(args);
        }

        if (lower is "mul")
        {
            return EvaluateMul(args);
        }

        if (args.Count == 0)
        {
            // rand is the only registered zero-argument function.
            return new MathValue(Random.Shared.NextDouble());
        }

        if (args.Count == 1)
        {
            if (lower is "sqrt")
            {
                return EvaluateRoot(args[0], new MathValue(2));
            }

            if (lower is "cbrt")
            {
                return EvaluateRoot(args[0], new MathValue(3));
            }

            if (MathValue.TryToRational(args[0], out var numerator, out var denominator))
            {
                var integral = BigInteger.DivRem(numerator, denominator, out var remainder);
                switch (lower)
                {
                    case "ceil":
                        if (remainder.Sign > 0)
                        {
                            integral++;
                        }

                        return ExactFunctionFraction(integral, BigInteger.One, args[0]);
                    case "floor":
                        if (remainder.Sign < 0)
                        {
                            integral--;
                        }

                        return ExactFunctionFraction(integral, BigInteger.One, args[0]);
                    case "trunc":
                        return ExactFunctionFraction(integral, BigInteger.One, args[0]);
                    case "frac":
                        return ExactFunctionFraction(remainder, denominator, args[0])
                            .WithDecimalDisplayScale(args[0].DecimalDisplayScale);
                    case "abs":
                        return numerator.Sign < 0 ? MathValue.Negate(args[0]) : args[0];
                    case "sign" or "sgn":
                        return new MathValue((BigInteger)numerator.Sign);
                    case "round":
                        return RoundFraction(numerator, denominator, 0, args[0]);
                }
            }

            // Math-based functions use double approximations. Branches that can operate
            // on integer or decimal values use the original argument instead.
            double v = lower is "sin" or "cos" or "tan" or "cot" or "sec" or "csc"
                ? ProjectAngle(args[0]) : args[0].AsDouble();
            switch (lower)
            {
                case "ceil":
                    return args[0].Kind switch
                    {
                        MathValue.DataType.Integer => args[0],
                        MathValue.DataType.Decimal => new MathValue((BigInteger)Math.Ceiling(args[0].DecimalValue)),
                        _ => ToIntegralMathValue(Math.Ceiling(v)),
                    };

                case "floor":
                    return args[0].Kind switch
                    {
                        MathValue.DataType.Integer => args[0],
                        MathValue.DataType.Decimal => new MathValue((BigInteger)Math.Floor(args[0].DecimalValue)),
                        _ => ToIntegralMathValue(Math.Floor(v)),
                    };

                case "trunc":
                    return args[0].Kind switch
                    {
                        MathValue.DataType.Integer => args[0],
                        MathValue.DataType.Decimal => new MathValue((BigInteger)decimal.Truncate(args[0].DecimalValue)),
                        _ => ToIntegralMathValue(Math.Truncate(v)),
                    };

                case "frac":
                    return args[0].Kind switch
                    {
                        MathValue.DataType.Integer => new MathValue(0m),
                        MathValue.DataType.Decimal => new MathValue(args[0].DecimalValue - decimal.Truncate(args[0].DecimalValue)),
                        _ => new MathValue(v - Math.Truncate(v)),
                    };

                case "bnot":
                    return MathValue.BitwiseNot(args[0]);

                case "popcount" or "bitcount":
                    if (!args[0].TryGetInteger(out var popArg))
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.IntegerRequired, $"{lower} requires an integer argument."));
                    }
                    return new MathValue(NumberTheory.PopCount(popArg));

                case "exp":
                    return new MathValue(Math.Exp(v));

                case "expm1":
                    return new MathValue(double.ExpM1(v));

                case "abs":
                    if (args[0].Kind is MathValue.DataType.Integer)
                    {
                        return new MathValue(BigInteger.Abs(args[0].IntegerValue));
                    }

                    if (args[0].Kind is MathValue.DataType.Decimal)
                    {
                        return new MathValue(Math.Abs(args[0].DecimalValue));
                    }

                    return new MathValue(Math.Abs(v));

                case "log":
                case "log10":
                    return new MathValue(EvaluateLogarithm(args[0], 10));

                case "log2":
                    return new MathValue(EvaluateLogarithm(args[0], 2));

                case "ln":
                    return new MathValue(EvaluateLogarithm(args[0]));

                case "log1p":
                    if (MathValue.TryToRational(args[0], out var logNumerator, out var logDenominator))
                    {
                        // Form 1+x exactly: projecting x first could round a valid input to -1.
                        return new MathValue(LogFraction(logNumerator + logDenominator, logDenominator));
                    }
                    if (v <= -1.0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "log1p argument must be strictly greater than -1."));
                    }
                    return new MathValue(SpecialFunctions.Log1p(v));

                case "factorial":
                    return EvaluateFactorial(args[0]);

                case "sign":
                case "sgn":
                    if (args[0].Kind is MathValue.DataType.Integer)
                    {
                        return new MathValue((BigInteger)args[0].IntegerValue.Sign);
                    }

                    if (args[0].Kind is MathValue.DataType.Decimal)
                    {
                        return new MathValue((decimal)Math.Sign(args[0].DecimalValue));
                    }

                    if (double.IsNaN(v))
                    {
                        // The calculator defines sign(NaN) as zero (e.g. sign(sqrt(-1))),
                        // whereas Math.Sign would throw for NaN.
                        return new MathValue(0m);
                    }

                    return new MathValue((double)Math.Sign(v));

                case "round":
                    // Calculator midpoint rounding is away from zero, not Math.Round's
                    // default ties-to-even behavior.
                    if (args[0].Kind is MathValue.DataType.Integer)
                    {
                        return args[0];
                    }

                    if (args[0].Kind is MathValue.DataType.Decimal)
                    {
                        return new MathValue((BigInteger)Math.Round(args[0].DecimalValue, MidpointRounding.AwayFromZero));
                    }

                    return ToIntegralMathValue(Math.Round(v, MidpointRounding.AwayFromZero));

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

                    return new MathValue((BigInteger)Random.Shared.NextInt64((long)randomUpperBound));

                case "sin":
                    return new MathValue(EvaluateSin(v));

                case "cos":
                    return new MathValue(EvaluateCos(v));

                case "tan":
                    return new MathValue(EvaluateTan(v));

                case "arcsin":
                case "asin":
                    if (v < -1.0 || v > 1.0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "arcsin argument must be between -1 and 1."));
                    }
                    return new MathValue(EvaluateArcSin(v));

                case "arccos":
                case "acos":
                    if (v < -1.0 || v > 1.0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "arccos argument must be between -1 and 1."));
                    }
                    return new MathValue(EvaluateArcCos(v));

                case "arctan":
                case "atan":
                    return new MathValue(EvaluateArcTan(v));

                case "cot":
                    return new MathValue(EvaluateCot(v));

                case "sec":
                    return new MathValue(EvaluateSec(v));

                case "csc":
                    return new MathValue(EvaluateCsc(v));

                case "arccot":
                    return new MathValue(EvaluateArcCot(v));

                case "arcsec":
                    if (Math.Abs(v) < 1.0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "arcsec argument magnitude must be at least 1."));
                    }
                    return new MathValue(EvaluateArcSec(v));

                case "arccsc":
                    if (Math.Abs(v) < 1.0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "arccsc argument magnitude must be at least 1."));
                    }
                    return new MathValue(EvaluateArcCsc(v));

                case "sinh":
                    return new MathValue(Math.Sinh(v));

                case "cosh":
                    return new MathValue(Math.Cosh(v));

                case "tanh":
                    return new MathValue(Math.Tanh(v));

                case "arsinh":
                case "asinh":
                    return new MathValue(Math.Asinh(v));

                case "arcosh":
                case "acosh":
                    if (v < 1.0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "arcosh argument must be at least 1."));
                    }
                    return new MathValue(Math.Acosh(v));

                case "artanh":
                case "atanh":
                    if (v <= -1.0 || v >= 1.0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "artanh argument must be strictly between -1 and 1."));
                    }
                    return new MathValue(Math.Atanh(v));

                case "coth":
                    return new MathValue(1.0 / Math.Tanh(v));

                case "sech":
                    return new MathValue(1.0 / Math.Cosh(v));

                case "csch":
                    return new MathValue(1.0 / Math.Sinh(v));

                case "arcoth":
                    if (Math.Abs(v) <= 1.0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "arcoth argument magnitude must be strictly greater than 1."));
                    }
                    return new MathValue(Math.Atanh(1.0 / v));

                case "arsech":
                    if (v <= 0.0 || v > 1.0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "arsech argument must be in the range (0, 1]."));
                    }
                    return new MathValue(Math.Acosh(1.0 / v));

                case "arcsch":
                    if (v == 0.0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "arcsch argument cannot be zero."));
                    }
                    return new MathValue(Math.Asinh(1.0 / v));

                case "rad":
                case "deg2rad":
                    return new MathValue(v * (Math.PI / 180.0));

                case "deg":
                case "rad2deg":
                    return new MathValue(v * (180.0 / Math.PI));

                case "grad":
                case "grad2rad":
                    return new MathValue(v * (Math.PI / 200.0));

                case "rad2grad":
                    return new MathValue(v * (200.0 / Math.PI));

                case "deg2grad":
                    return new MathValue(v * (200.0 / 180.0));

                case "grad2deg":
                    return new MathValue(v * (180.0 / 200.0));

                case "erf":
                    return new MathValue(SpecialFunctions.Erf(v));

                case "erfc":
                    return new MathValue(SpecialFunctions.Erfc(v));

                case "ncdf":
                    return new MathValue(SpecialFunctions.NormalCdf(v));

                case "sinc":
                    return new MathValue(SpecialFunctions.Sinc(v));

                case "gamma":
                    return new MathValue(SpecialFunctions.Gamma(v));

                case "lgamma":
                    return new MathValue(SpecialFunctions.LogGamma(v));

                case "zeta":
                    return new MathValue(SpecialFunctions.Zeta(v));

                case "isprime":
                    if (!args[0].TryGetInteger(out var primeArg))
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.IntegerRequired, "isprime requires an integer argument."));
                    }
                    bool prime = NumberTheory.IsPrime(primeArg);
                    return new MathValue(prime ? BigInteger.One : BigInteger.Zero).WithAccuracy(
                        prime && primeArg > ulong.MaxValue
                            ? CalculationAccuracy.Unverified
                            : CalculationAccuracy.Exact);

                case "divcount" or "divisorcount":
                    if (!args[0].TryGetInteger(out var divCountArg))
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.IntegerRequired, $"{lower} requires an integer argument."));
                    }
                    return FactorizationResult(NumberTheory.DivCount(divCountArg), divCountArg);

                case "sigma" or "divsum":
                    if (!args[0].TryGetInteger(out var sigmaArg))
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.IntegerRequired, $"{lower} requires an integer argument."));
                    }
                    return FactorizationResult(NumberTheory.Sigma(sigmaArg), sigmaArg);

                case "totient" or "phi" or "eulerphi":
                    if (!args[0].TryGetInteger(out var totientArg))
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.IntegerRequired, $"{lower} requires an integer argument."));
                    }
                    return FactorizationResult(NumberTheory.Totient(totientArg), totientArg);

                default:
                    throw new ExpressionException(new EvaluationDiagnostic(
                        EvaluationError.UnknownFunction, $"Unknown function '{name}'."));
            }
        }

        if (args.Count == 2)
        {
            switch (lower)
            {
                case "pow":
                    return MathValue.Pow(args[0], args[1], _options.MaxExponentMagnitude);

                case "logn":
                    if (MathValue.Compare(args[1], new MathValue(1)) == 0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "logn requires positive arguments and base cannot be 1."));
                    }

                    var valueLog = LogarithmParts(args[0]);
                    var baseLog = LogarithmParts(args[1]);
                    if (args[1].Kind == MathValue.DataType.Double && !args[1].IsFinite)
                    {
                        // Native logn has special rules for NaN and infinite bases, not just ln(x)/ln(b).
                        return new MathValue(Math.Log(args[0].AsDouble(), args[1].DoubleValue));
                    }

                    // ln(x)/ln(b) = (valuePart/basePart) * 2^(valueExponent-baseExponent).
                    // Dividing the parts first avoids turning two tiny logarithms into 0/0.
                    return new MathValue(Math.ScaleB(valueLog.Value / baseLog.Value, valueLog.Exponent - baseLog.Exponent));

                case "atan2":
                    var y = args[0].AsDouble();
                    var x = args[1].AsDouble();
                    return new MathValue(EvaluateArcTan2(y, x));

                case "hypot":
                    return new MathValue(EvaluateHypot(args[0], args[1]));

                case "mod":
                    return MathValue.Modulo(args[0], args[1]);

                case "round":
                    return EvaluateRound(args[0], args[1]);

                case "root" or "nthroot":
                    return EvaluateRoot(args[0], args[1]);

                case "shl":
                    return MathValue.ShiftLeft(args[0], args[1]);

                case "shr":
                    return MathValue.ShiftRight(args[0], args[1]);

                case "band":
                    return MathValue.BitwiseAnd(args[0], args[1]);

                case "bor":
                    return MathValue.BitwiseOr(args[0], args[1]);

                case "bxor":
                    return MathValue.BitwiseXor(args[0], args[1]);

                case "beta":
                    return new MathValue(SpecialFunctions.Beta(args[0].AsDouble(), args[1].AsDouble()));

                case "comb" or "ncr" or "choose" or "binomial":
                    if (!args[0].TryGetInteger(out var combN) || !args[1].TryGetInteger(out var combK))
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.IntegerRequired, $"{lower} requires integer arguments."));
                    }
                    return new MathValue(NumberTheory.Comb(combN, combK));

                case "perm" or "npr":
                    if (!args[0].TryGetInteger(out var permN) || !args[1].TryGetInteger(out var permK))
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.IntegerRequired, $"{lower} requires integer arguments."));
                    }
                    return new MathValue(NumberTheory.Perm(permN, permK));

                case "sigma" or "divsum":
                    if (!args[0].TryGetInteger(out var sigmaN) || !args[1].TryGetInteger(out var sigmaK))
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.IntegerRequired, $"{lower} requires integer arguments."));
                    }
                    if (sigmaK < 0 || sigmaK > _options.MaxExponentMagnitude)
                    {
                        if (sigmaK < 0)
                        {
                            throw new ExpressionException(new EvaluationDiagnostic(
                                EvaluationError.ArgumentOutOfRange, $"{lower} exponent k must be a non-negative integer."));
                        }
                        throw new CalculationLimitException();
                    }
                    return FactorizationResult(NumberTheory.Sigma(sigmaN, (int)sigmaK), sigmaN);

                default:
                    throw new ExpressionException(new EvaluationDiagnostic(
                        EvaluationError.UnknownFunction, $"Unknown function '{name}'."));
            }
        }

        if (args.Count == 3)
        {
            return lower switch
            {
                "clamp" => EvaluateClamp(args[0], args[1], args[2]),
                "lerp" => Add(args[0], Multiply(args[2], Subtract(args[1], args[0]))), // a + t * (b - a)
                _ => throw new ExpressionException(new EvaluationDiagnostic(
                    EvaluationError.UnknownFunction, $"Unknown function '{name}'.")),
            };
        }

        throw new ExpressionException(new EvaluationDiagnostic(
            EvaluationError.InvalidArgumentCount, $"Invalid argument count for '{name}'."));
    }

    private MathValue EvaluateGcd(List<MathValue> args)
    {
        if (!args[0].TryGetInteger(out var result))
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.IntegerRequired, "gcd requires integer arguments."));
        }

        for (var i = 1; i < args.Count; i++)
        {
            if (!args[i].TryGetInteger(out var next))
            {
                throw new ExpressionException(new EvaluationDiagnostic(
                    EvaluationError.IntegerRequired, "gcd requires integer arguments."));
            }

            result = NumberTheory.Gcd(result, next);
        }

        return new MathValue(BigInteger.Abs(result));
    }

    private MathValue EvaluateLcm(List<MathValue> args)
    {
        if (!args[0].TryGetInteger(out var result))
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.IntegerRequired, "lcm requires integer arguments."));
        }

        for (int i = 1; i < args.Count; i++)
        {
            if (!args[i].TryGetInteger(out var next))
            {
                throw new ExpressionException(new EvaluationDiagnostic(
                    EvaluationError.IntegerRequired, "lcm requires integer arguments."));
            }

            result = NumberTheory.Lcm(result, next);
            ValidateValue(new MathValue(result));
        }

        return new MathValue(BigInteger.Abs(result));
    }

    private MathValue EvaluateMinMax(List<MathValue> args, bool findMinimum)
    {
        // IEEE 754: every comparison with NaN is false except inequality. If any argument
        // is NaN, the result of min/max is NaN.
        for (int i = 0; i < args.Count; i++)
        {
            if (args[i].IsNaN)
            {
                return new MathValue(double.NaN);
            }
        }

        // Compare without converting every argument to double, and return the selected
        // operand unchanged so its numeric representation is preserved.
        var result = args[0];
        for (int i = 1; i < args.Count; i++)
        {
            var comparison = MathValue.Compare(result, args[i]);
            if (findMinimum ? comparison > 0 : comparison < 0)
            {
                result = args[i];
            }
        }

        return result;
    }

    private MathValue EvaluateAvg(List<MathValue> args)
    {
        var sum = args[0];
        for (int i = 1; i < args.Count; i++)
        {
            sum = Add(sum, args[i]);
        }

        return Divide(sum, new MathValue((BigInteger)args.Count));
    }

    private MathValue EvaluateMedian(List<MathValue> args)
    {
        // IEEE 754: every comparison with NaN is false except inequality. If any argument
        // is NaN, the result of median is NaN.
        for (int i = 0; i < args.Count; i++)
        {
            if (args[i].IsNaN)
            {
                return new MathValue(double.NaN);
            }
        }

        var sorted = new List<MathValue>(args);
        try
        {
            sorted.Sort((left, right) => MathValue.Compare(left, right));
        }
        catch (InvalidOperationException ex) when (ex.InnerException is CalculationLimitException limit)
        {
            throw limit;
        }

        int count = sorted.Count;
        if (count % 2 == 1)
        {
            return sorted[count / 2];
        }

        var mid1 = sorted[(count / 2) - 1];
        var mid2 = sorted[count / 2];
        return Divide(Add(mid1, mid2), new MathValue(2));
    }

    private MathValue EvaluateVariance(List<MathValue> args)
    {
        // Sample variance requires n >= 2: s^2 = (1 / (n - 1)) * sum((x_i - mean)^2)
        var mean = EvaluateAvg(args);
        var sumOfSquares = new MathValue(0);

        for (int i = 0; i < args.Count; i++)
        {
            var diff = Subtract(args[i], mean);
            sumOfSquares = Add(sumOfSquares, Multiply(diff, diff));
        }

        return Divide(sumOfSquares, new MathValue((BigInteger)(args.Count - 1)));
    }

    private MathValue EvaluateStDev(List<MathValue> args)
    {
        var variance = EvaluateVariance(args);
        return new MathValue(EvaluateRoot(variance, new MathValue(2)).AsDouble());
    }

    private MathValue EvaluateSum(List<MathValue> args)
    {
        var sum = args[0];
        for (int i = 1; i < args.Count; i++)
        {
            sum = Add(sum, args[i]);
        }

        return sum;
    }

    private MathValue EvaluateMul(List<MathValue> args)
    {
        var product = args[0];
        for (int i = 1; i < args.Count; i++)
        {
            product = Multiply(product, args[i]);
        }

        return product;
    }

    private MathValue EvaluateClamp(MathValue val, MathValue min, MathValue max)
    {
        // Propagate NaN without throwing ArgumentOutOfRange.
        if (val.IsNaN || min.IsNaN || max.IsNaN)
        {
            return new MathValue(double.NaN);
        }

        if (MathValue.Compare(min, max) > 0)
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.ArgumentOutOfRange, "Clamp minimum cannot be greater than maximum."));
        }

        if (MathValue.Compare(val, min) < 0)
        {
            return min;
        }

        if (MathValue.Compare(val, max) > 0)
        {
            return max;
        }

        return val;
    }

    private MathValue EvaluateRound(MathValue value, MathValue placesArg)
    {
        if (!placesArg.TryGetInteger(out var placesInt))
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.IntegerRequired, "round places argument must be an integer."));
        }

        // Support rounding up to the precision capacity of the supported numeric models:
        // decimal allows 0..28 fractional digits, and IEEE 754 double precision spans
        // ~15-17 digits.
        if (placesInt < 0 || placesInt > 28)
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.ArgumentOutOfRange, "round places argument must be between 0 and 28."));
        }

        int places = (int)placesInt;

        if (MathValue.TryToRational(value, out var numerator, out var denominator))
        {
            return RoundFraction(numerator, denominator, places, value)
                .WithDecimalDisplayScale(Math.Min(value.DecimalDisplayScale, places));
        }

        if (places == 0)
        {
            return ToIntegralMathValue(Math.Round(value.AsDouble(), MidpointRounding.AwayFromZero));
        }

        if (places <= 15)
        {
            return new MathValue(Math.Round(value.AsDouble(), places, MidpointRounding.AwayFromZero));
        }

        // Double values beyond 15 fractional decimal digits exceed double.Round's
        // argument ceiling. We preserve full IEEE 754 value without crashing.
        return value;
    }

    private MathValue ExactFunctionFraction(BigInteger numerator, BigInteger denominator, MathValue input) =>
        MathValue.FromExactFraction(numerator, denominator, input.Kind == MathValue.DataType.Decimal);

    // Radix zero selects the natural logarithm; 2 and 10 select the corresponding bases.
    private static double EvaluateLogarithm(MathValue value, int radix = 0)
    {
        if (MathValue.TryToRational(value, out var numerator, out var denominator))
        {
            var (Value, Exponent) = LogFractionParts(numerator, denominator);
            double projected = value.AsDouble();

            // Away from one, a normal Double retains enough input precision for the
            // native base-specific routines, avoiding an extra change-of-base rounding.
            if (double.IsNormal(projected) && projected is < 0.5 or > 1.5)
            {
                return radix switch
                {
                    2 => Math.Log2(projected),
                    10 => Math.Log10(projected),
                    _ => Math.ScaleB(Value, Exponent),
                };
            }

            // Change base before restoring the scale, so a tiny logarithm does not
            // underflow before division by ln(2) or ln(10).
            double result = radix switch
            {
                2 => Value / Math.Log(2),
                10 => Value / Math.Log(10),
                _ => Value,
            };
            return Math.ScaleB(result, Exponent);
        }

        double input = value.AsDouble();
        if (input <= 0)
        {
            throw LogarithmDomainException();
        }

        return radix switch { 2 => Math.Log2(input), 10 => Math.Log10(input), _ => Math.Log(input) };
    }

    private static (double Value, int Exponent) LogarithmParts(MathValue value) =>
        MathValue.TryToRational(value, out var numerator, out var denominator) ?
            LogFractionParts(numerator, denominator) : (EvaluateLogarithm(value), 0);

    private static double LogFraction(BigInteger numerator, BigInteger denominator)
    {
        var (Value, Exponent) = LogFractionParts(numerator, denominator);
        return Math.ScaleB(Value, Exponent);
    }

    // Returns ln(numerator/denominator) approximately as Value * 2^Exponent.
    // The exponent belongs to the logarithm's result, not to its original input.
    private static (double Value, int Exponent) LogFractionParts(BigInteger numerator, BigInteger denominator)
    {
        if (numerator.Sign <= 0)
        {
            throw LogarithmDomainException();
        }

        // n/d = 1 + (n-d)/d. Subtract before conversion so a small difference from
        // one survives; |n-d|*2 <= d means the offset lies between -0.5 and 0.5.
        var difference = numerator - denominator;
        if (BigInteger.Abs(difference) * 2 <= denominator)
        {
            int differenceExponent = (int)(BigInteger.Abs(difference).GetBitLength() - denominator.GetBitLength());
            // Below 2^-54, log(1+x)/x differs from one by less than double precision.
            // Keep the exponent separate so logn can divide even underflow-sized logs.
            if (!difference.IsZero && differenceExponent < -54)
            {
                return (ScaleFractionAsDouble(difference, denominator, differenceExponent), differenceExponent);
            }

            return (SpecialFunctions.Log1p(MathValue.DivideIntegersAsDouble(difference, denominator)), 0);
        }

        double projected = MathValue.DivideIntegersAsDouble(numerator, denominator);
        if (double.IsNormal(projected))
        {
            return (Math.Log(projected), 0);
        }

        int exponent = (int)(numerator.GetBitLength() - denominator.GetBitLength());
        double mantissa = ScaleFractionAsDouble(numerator, denominator, exponent);
        // n/d = mantissa * 2^exponent, hence ln(n/d) = ln(mantissa) + exponent*ln(2).
        return (Math.Log(mantissa) + exponent * Math.Log(2), 0);
    }

    private static ExpressionException LogarithmDomainException() => new(new EvaluationDiagnostic(
        EvaluationError.UndefinedMathResult, "Logarithm is undefined for non-positive values."));

    // Computes (numerator/denominator) * 2^-exponent, shifting exact integers before
    // conversion so the unscaled fraction need not fit in Double's range.
    private static double ScaleFractionAsDouble(BigInteger numerator, BigInteger denominator, int exponent) =>
        exponent >= 0 ?
            MathValue.DivideIntegersAsDouble(numerator, denominator << exponent) :
            MathValue.DivideIntegersAsDouble(numerator << -exponent, denominator);

    private static double EvaluateHypot(MathValue left, MathValue right)
    {
        if (!MathValue.TryToRational(left, out var leftNumerator, out var leftDenominator) ||
            !MathValue.TryToRational(right, out var rightNumerator, out var rightDenominator))
        {
            return double.Hypot(left.AsDouble(), right.AsDouble());
        }

        leftNumerator = BigInteger.Abs(leftNumerator);
        rightNumerator = BigInteger.Abs(rightNumerator);
        if (leftNumerator.IsZero || rightNumerator.IsZero)
        {
            return leftNumerator.IsZero ?
                MathValue.DivideIntegersAsDouble(rightNumerator, rightDenominator) :
                MathValue.DivideIntegersAsDouble(leftNumerator, leftDenominator);
        }

        // Use the same binary scale for both inputs, bringing the larger close to one.
        // hypot(x,y) = 2^e * hypot(x/2^e,y/2^e); only the final rescaling can overflow.
        int exponent = (int)Math.Max(leftNumerator.GetBitLength() - leftDenominator.GetBitLength(),
            rightNumerator.GetBitLength() - rightDenominator.GetBitLength());
        return Math.ScaleB(double.Hypot(
            ScaleFractionAsDouble(leftNumerator, leftDenominator, exponent),
            ScaleFractionAsDouble(rightNumerator, rightDenominator, exponent)), exponent);
    }

    /// <summary>
    /// Projects an angle into the range of a single revolution based on the current angle
    /// unit.
    /// </summary>
    /// <param name="value">The angle value to project.</param>
    /// <returns>The projected angle value within a single revolution.</returns>
    private double ProjectAngle(MathValue value)
    {
        if (_options.AngleUnit == AngleUnit.Radians ||
            !MathValue.TryToRational(value, out var numerator, out var denominator))
        {
            return value.AsDouble();
        }

        // Work in numerator units: one turn of n/d is 360*d (or 400*d).
        // Removing whole turns here preserves offsets hidden by converting a huge angle.
        var revolution = denominator * (_options.AngleUnit == AngleUnit.Degrees ? 360 : 400);
        var remainder = numerator % revolution;
        // Beyond half a turn, shift by a WHOLE turn to choose the equivalent angle
        // closer to zero (270 degrees becomes -90). Doubling avoids integer division.
        if (BigInteger.Abs(remainder) * 2 > revolution)
        {
            remainder -= remainder.Sign * revolution;
        }

        return MathValue.DivideIntegersAsDouble(remainder, denominator);
    }

    private MathValue EvaluateRoot(MathValue value, MathValue degree)
    {
        if (degree.IsZero)
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.UndefinedMathResult, "Root degree cannot be zero."));
        }

        bool hasFraction = MathValue.TryToRational(value, out var numerator, out var denominator);
        bool isNegative = hasFraction ? numerator.Sign < 0 : value.DoubleValue < 0;
        bool hasIntegerDegree = degree.TryGetInteger(out var integerDegree);

        if (isNegative && (!hasIntegerDegree || integerDegree.IsEven))
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.UndefinedMathResult, "A negative number requires an odd integer root degree."));
        }

        if (hasFraction && hasIntegerDegree && integerDegree > 0 && integerDegree <= int.MaxValue)
        {
            int exactDegree = (int)integerDegree;
            if (numerator.TryGetExactRoot(exactDegree, out var numeratorRoot) &&
                denominator.TryGetExactRoot(exactDegree, out var denominatorRoot))
            {
                return ExactFunctionFraction(numeratorRoot, denominatorRoot, value);
            }
        }

        double projectedValue = value.AsDouble();
        double magnitude = isNegative ? -projectedValue : projectedValue;
        double root = hasFraction && !numerator.IsZero && !double.IsNormal(magnitude) ?
            ApproximateFractionRoot(BigInteger.Abs(numerator), denominator, degree) :
            ApproximateRoot(magnitude, degree.AsDouble());

        return new MathValue(isNegative ? -root : root);
    }

    private static double ApproximateRoot(double value, double degree) => degree switch
    {
        2 => Math.Sqrt(value),
        3 => Math.Cbrt(value),
        _ => Math.Pow(value, 1.0 / degree)
    };

    private static double ApproximateFractionRoot(BigInteger numerator, BigInteger denominator, MathValue degree)
    {
        // Keep the magnitude in a separate exponent before converting the fraction to Double.
        int exponent = (int)(numerator.GetBitLength() - denominator.GetBitLength());
        double mantissa = ScaleFractionAsDouble(numerator, denominator, exponent);
        double approximateDegree = degree.AsDouble();

        if (degree.TryGetInteger(out var integerDegree) && integerDegree >= int.MinValue && integerDegree <= int.MaxValue)
        {
            // Split the exponent so underflow or overflow occurs only at the final root's scale.
            int divisor = (int)integerDegree;
            int rootExponent = exponent / divisor;
            int remainder = exponent % divisor;
            double mantissaRoot = ApproximateRoot(mantissa, approximateDegree);
            double fractionalScale = Math.Pow(2, (double)remainder / divisor);
            return Math.ScaleB(mantissaRoot * fractionalScale, rootExponent);
        }

        double logarithm = exponent + Math.Log2(mantissa);
        return Math.Pow(2, logarithm / approximateDegree);
    }

    private MathValue RoundFraction(BigInteger numerator, BigInteger denominator, int places, MathValue input)
    {
        if (denominator.IsOne || numerator.IsZero)
        {
            return ExactFunctionFraction(numerator, BigInteger.One, input);
        }
        var scale = BigInteger.Pow(10, places);
        if (scale.GetBitLength() > CalculationLimits.MaxIntermediateBits ||
            (!numerator.IsZero &&
             BigInteger.Abs(numerator).GetBitLength() + scale.GetBitLength() - 1 > CalculationLimits.MaxIntermediateBits))
        {
            throw new CalculationLimitException();
        }
        var scaled = numerator * scale;
        if (BigInteger.Abs(scaled).GetBitLength() > CalculationLimits.MaxIntermediateBits)
        {
            throw new CalculationLimitException();
        }
        var integral = BigInteger.DivRem(scaled, denominator, out var remainder);
        if (BigInteger.Abs(remainder) >= (denominator + 1) / 2)
        {
            integral += numerator.Sign;
        }
        return ExactFunctionFraction(integral, scale, input);
    }

    // 100 revolutions (800 quarter-turns) provides ample range for practical angular
    // calculations while keeping error accumulation from IEEE 754 float representation
    // well within CardinalEpsilon (1e-12).
    private const double MaxSnappingRevolutions = 100.0;
    private const double CardinalEpsilon = 1e-12;

    /// <summary>
    /// Normalizes an angle into multiples of 45 degrees (pi/4 radians, 50 gradians)
    /// across all supported angle units within a bounded revolution ceiling.
    /// Used by all trigonometric functions to ensure exact cardinal values and detect
    /// asymptotes.
    /// </summary>
    private bool TryGetQuarterTurn(double v, out long step45)
    {
        if (!double.IsFinite(v))
        {
            step45 = 0;
            return false;
        }

        if (_options.AngleUnit == AngleUnit.Radians)
        {
            // Radians: heuristic snap for near-zero canonical multiples of pi (within
            // 100 revs).
            if (Math.Abs(v) <= MaxSnappingRevolutions * (2.0 * Math.PI))
            {
                double reduced = Math.IEEERemainder(v, 2.0 * Math.PI);
                double k = reduced / (Math.PI / 4.0);
                double roundK = Math.Round(k);

                if (Math.Abs(k - roundK) < CardinalEpsilon)
                {
                    step45 = (long)roundK;
                    return true;
                }
            }

            step45 = 0;
            return false;
        }

        // Degrees & Gradians: exact integer reduction valid up to 2^53 (9e15).
        var (revSize, stepSize) = _options.AngleUnit switch
        {
            AngleUnit.Degrees => (360.0, 45.0),
            _ => (400.0, 50.0),
        };

        if (Math.Abs(v) <= 9_007_199_254_740_992.0) // 2^53
        {
            double reduced = Math.IEEERemainder(v, revSize);
            double k = reduced / stepSize;
            double roundK = Math.Round(k);

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
        if (TryGetQuarterTurn(v, out long step45) && (step45 % 2 == 0))
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
            AngleUnit.Degrees => double.SinPi(Math.IEEERemainder(v, 360.0) / 180.0),
            AngleUnit.Gradians => double.SinPi(Math.IEEERemainder(v, 400.0) / 200.0),
            _ => Math.Sin(v),
        };
    }

    private double EvaluateCos(double v)
    {
        if (TryGetQuarterTurn(v, out long step45) && (step45 % 2 == 0))
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
            AngleUnit.Degrees => double.CosPi(Math.IEEERemainder(v, 360.0) / 180.0),
            AngleUnit.Gradians => double.CosPi(Math.IEEERemainder(v, 400.0) / 200.0),
            _ => Math.Cos(v),
        };
    }

    private double EvaluateTan(double v)
    {
        if (TryGetQuarterTurn(v, out long step45))
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

        double result = _options.AngleUnit switch
        {
            AngleUnit.Degrees => double.TanPi(Math.IEEERemainder(v, 360.0) / 180.0),
            AngleUnit.Gradians => double.TanPi(Math.IEEERemainder(v, 400.0) / 200.0),
            _ => Math.Tan(v),
        };

        if (!double.IsFinite(result))
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.UndefinedMathResult, "Tangent is undefined at odd multiples of 90 degrees (pi/2)."));
        }

        return result;
    }

    private double EvaluateCot(double v)
    {
        if (TryGetQuarterTurn(v, out long step45))
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

        double sin = EvaluateSin(v);
        if (sin == 0.0)
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.UndefinedMathResult, "Cotangent is undefined where sine is zero."));
        }

        double result = EvaluateCos(v) / sin;
        if (!double.IsFinite(result))
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.UndefinedMathResult, "Cotangent is undefined where sine is zero."));
        }

        return result;
    }

    private double EvaluateSec(double v)
    {
        double cos = EvaluateCos(v);
        if (cos == 0.0)
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.UndefinedMathResult, "Secant is undefined where cosine is zero."));
        }

        double result = 1.0 / cos;
        if (!double.IsFinite(result))
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.UndefinedMathResult, "Secant is undefined where cosine is zero."));
        }

        return result;
    }

    private double EvaluateCsc(double v)
    {
        double sin = EvaluateSin(v);
        if (sin == 0.0)
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.UndefinedMathResult, "Cosecant is undefined where sine is zero."));
        }

        double result = 1.0 / sin;
        if (!double.IsFinite(result))
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.UndefinedMathResult, "Cosecant is undefined where sine is zero."));
        }

        return result;
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

    private static MathValue ToIntegralMathValue(double roundedValue)
    {
        // When rounding or truncating an IEEE 754 double, preserve integer closure if the
        // resulting number is finite and within supported integer bit limits. Non-finite
        // values (NaN, infinities) remain Double.
        if (double.IsFinite(roundedValue))
        {
            return new MathValue((BigInteger)roundedValue).WithAccuracy(CalculationAccuracy.Unverified);
        }

        return new MathValue(roundedValue);
    }

    private MathValue EvaluateFactorial(MathValue argument)
    {
        if (!argument.TryGetInteger(out var factorialArgument))
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

        int n = (int)factorialArgument;
        BigInteger f = 1;
        for (int i = 2; i <= n; i++)
        {
            f = MathValue.Product(f, i);
            ValidateValue(new MathValue(f));
        }

        return new MathValue(f);
    }
}
