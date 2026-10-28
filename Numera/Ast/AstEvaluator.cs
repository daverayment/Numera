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
    private List<KeyValuePair<string, MathValue>>? _assignments;

    public IReadOnlyList<KeyValuePair<string, MathValue>>? Assignments => _assignments;

    public AstEvaluator(CalculatorOptions options, EvaluationContext? context = null)
	{
		_options = options;
		_context = context ?? EvaluationContext.Empty;
	}

	public MathValue Visit(NumberNode node) => node.Value;

	public MathValue Visit(IdentifierNode node)
	{
		// Constant resolution produces a high-precision decimal (up to 28 digits)
		// for direct inspection and linear decimal/integer arithmetic.
		if (ConstantCatalog.TryResolve(node.Name, out _, out decimal value))
		{
			return new MathValue(value);
		}

        // Check recent assignments.
        if (_assignments is not null)
        {
            for (int i = _assignments.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_assignments[i].Key, node.Name, Identifier.Comparison))
                {
                    return _assignments[i].Value;
                }
            }
        }

        // Check the evaluation context variables, including previous result "ans".
        if (_context.TryResolveVariable(node.Name, out var variableValue))
		{
			return variableValue;
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
			return node.Op switch
			{
				UnaryOperator.Negate => -value,
				UnaryOperator.Positive => value,
				UnaryOperator.LogicalNot => MathValue.LogicalNot(value),
				UnaryOperator.BitwiseNot => MathValue.BitwiseNot(value),
				UnaryOperator.Factorial => EvaluateFactorial(value),
				UnaryOperator.Percent => value / new MathValue(100m),
				_ => throw new ExpressionException(new EvaluationDiagnostic(
					EvaluationError.InvalidSyntax, "Unknown unary operator.")),
			};
		}
		catch (Exception ex) when (ex is ExpressionException or DivideByZeroException or OverflowException)
		{
			throw Enrich(ex, node.Span);
		}
	}

	public MathValue Visit(BinaryNode node)
	{
		// Fast path with no spine allocation. Power nodes must take it too, or the loop below
		// would recurse into this method forever; they are not flattened because they count
		// towards nesting height (see BinaryNode.Height).
		if (node.Op == BinaryOperator.Power || node.Left is not BinaryNode { Op: not BinaryOperator.Power })
		{
			return Apply(node, node.Left.Accept(this));
		}

		// Walk the non-power left spine iteratively so long flat chains (e.g. thousands of additive
		// terms) cannot overflow the call stack. This is the same rule as BinaryNode.Height.
		var spine = new Stack<BinaryNode>();
		ExpressionNode current = node;
		while (current is BinaryNode { Op: not BinaryOperator.Power } bin)
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
			return Guard(node.Span, () => MathValue.LogicalOr(left, new MathValue(1m)));
		}

		if (node.Op == BinaryOperator.LogicalAnd && left.IsZero)
		{
			return Guard(node.Span, () => MathValue.LogicalAnd(left, new MathValue(0m)));
		}

		var right = node.Right.Accept(this);
		bool markup = PercentRules.IsMarkup(node);
		var one = new MathValue(1m);
		return Guard(node.Span, () => node.Op switch
		{
			BinaryOperator.Add when markup => left * (one + right),
			BinaryOperator.Subtract when markup => left * (one - right),
			BinaryOperator.Of => left * right,
			BinaryOperator.Add => left + right,
			BinaryOperator.Subtract => left - right,
			BinaryOperator.Multiply => left * right,
			BinaryOperator.Divide => left / right,
			BinaryOperator.Modulo => left % right,
			BinaryOperator.Power => MathValue.Pow(left, right),
			BinaryOperator.ShiftLeft => MathValue.ShiftLeft(left, right),
			BinaryOperator.ShiftRight => MathValue.ShiftRight(left, right),
			BinaryOperator.LogicalAnd => MathValue.LogicalAnd(left, right),
			BinaryOperator.LogicalOr => MathValue.LogicalOr(left, right),
			BinaryOperator.LogicalXor => MathValue.LogicalXor(left, right),
			_ => throw new ExpressionException(new EvaluationDiagnostic(EvaluationError.InvalidSyntax, "Unknown binary operator.")),
		});
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
		for (int i = 0; i < node.Operators.Count; i++)
		{
			// Later operands are skipped once a comparison fails, avoiding needless work
            // and errors.
			var right = node.Operands[i + 1].Accept(this);
			if (!EvaluateComparison(node.Operators[i], left, right))
			{
				return new MathValue(0m);
			}

			left = right;
		}

		return new MathValue(1m);
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

		try
		{
			return EvaluateFunction(node.FunctionName, args);
		}
		catch (Exception ex) when (ex is ExpressionException or DivideByZeroException or OverflowException)
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
        // the list lazily.
        (_assignments ??= []).Add(
            new KeyValuePair<string, MathValue>(node.VariableName, value));
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
		catch (Exception ex) when (ex is ExpressionException or DivideByZeroException or OverflowException)
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
			DivideByZeroException => new EvaluationDiagnostic(
                EvaluationError.DivisionByZero, "Division by zero."),
			_ => new EvaluationDiagnostic(
                EvaluationError.NumericOverflow, "Numeric overflow."),
		};

		if (diagnostic.Position is null && span.HasValue)
		{
			diagnostic = diagnostic with { Position = span.Start, Length = span.Length };
		}

		return ex is ExpressionException exception && ReferenceEquals(diagnostic, exception.Diagnostic)
			? ex
			: new ExpressionException(diagnostic, ex);
	}

	private static bool EvaluateComparison(ComparisonOperator op, MathValue left, MathValue right)
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
            // Math-based functions use double approximations. Branches that can operate
            // on integer or decimal values use the original argument instead.
            double v = args[0].AsDouble();
            switch (lower)
            {
                case "ceil":
                    return args[0].Type switch
                    {
                        MathValue.DataType.Integer => args[0],
                        MathValue.DataType.Decimal => new MathValue((BigInteger)Math.Ceiling(args[0].DecVal)),
                        _ => ToIntegralMathValue(Math.Ceiling(v)),
                    };

                case "floor":
                    return args[0].Type switch
                    {
                        MathValue.DataType.Integer => args[0],
                        MathValue.DataType.Decimal => new MathValue((BigInteger)Math.Floor(args[0].DecVal)),
                        _ => ToIntegralMathValue(Math.Floor(v)),
                    };

                case "trunc":
                    return args[0].Type switch
                    {
                        MathValue.DataType.Integer => args[0],
                        MathValue.DataType.Decimal => new MathValue((BigInteger)decimal.Truncate(args[0].DecVal)),
                        _ => ToIntegralMathValue(Math.Truncate(v)),
                    };

                case "frac":
                    return args[0].Type switch
                    {
                        MathValue.DataType.Integer => new MathValue(0m),
                        MathValue.DataType.Decimal => new MathValue(args[0].DecVal - decimal.Truncate(args[0].DecVal)),
                        _ => new MathValue(v - Math.Truncate(v)),
                    };

                case "cbrt":
                    if (args[0].Type is MathValue.DataType.Integer && args[0].IntVal.TryGetExactRoot(3, out var exactCbrt))
                    {
                        return new MathValue(exactCbrt);
                    }

                    return new MathValue(Math.Cbrt(v));

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
                    if (args[0].Type is MathValue.DataType.Integer)
                    {
                        return new MathValue(BigInteger.Abs(args[0].IntVal));
                    }

                    if (args[0].Type is MathValue.DataType.Decimal)
                    {
                        return new MathValue(Math.Abs(args[0].DecVal));
                    }

                    return new MathValue(Math.Abs(v));

                case "log":
                case "log10":
                    if (v <= 0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "Logarithm is undefined for non-positive values."));
                    }
                    return new MathValue(Math.Log10(v));

                case "log2":
                    if (v <= 0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "Logarithm is undefined for non-positive values."));
                    }
                    return new MathValue(Math.Log2(v));

                case "ln":
                    if (v <= 0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "Natural logarithm is undefined for non-positive values."));
                    }
                    return new MathValue(Math.Log(v));

                case "log1p":
                    if (v <= -1.0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "log1p argument must be strictly greater than -1."));
                    }
                    return new MathValue(double.LogP1(v));

                case "sqrt":
                    if (args[0].Type is MathValue.DataType.Integer)
                    {
                        if (args[0].IntVal < BigInteger.Zero)
                        {
                            throw new ExpressionException(new EvaluationDiagnostic(
                                EvaluationError.UndefinedMathResult, "Square root of a negative number has no real solution."));
                        }

                        if (args[0].IntVal.TryGetExactSquareRoot(out var exactIntRoot))
                        {
                            return new MathValue(exactIntRoot);
                        }
                    }

                    if (v < 0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "Square root of a negative number has no real solution."));
                    }
                    return new MathValue(Math.Sqrt(v));

                case "factorial":
                    return EvaluateFactorial(args[0]);

                case "sign":
                case "sgn":
                    if (args[0].Type is MathValue.DataType.Integer)
                    {
                        return new MathValue((BigInteger)args[0].IntVal.Sign);
                    }

                    if (args[0].Type is MathValue.DataType.Decimal)
                    {
                        return new MathValue((decimal)Math.Sign(args[0].DecVal));
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
                    if (args[0].Type is MathValue.DataType.Integer)
                    {
                        return args[0];
                    }

                    if (args[0].Type is MathValue.DataType.Decimal)
                    {
                        return new MathValue((BigInteger)Math.Round(args[0].DecVal, MidpointRounding.AwayFromZero));
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
                    return new MathValue(NumberTheory.IsPrime(primeArg) ? BigInteger.One : BigInteger.Zero);

                case "divcount" or "divisorcount":
                    if (!args[0].TryGetInteger(out var divCountArg))
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.IntegerRequired, $"{lower} requires an integer argument."));
                    }
                    return new MathValue(NumberTheory.DivCount(divCountArg));

                case "sigma" or "divsum":
                    if (!args[0].TryGetInteger(out var sigmaArg))
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.IntegerRequired, $"{lower} requires an integer argument."));
                    }
                    return new MathValue(NumberTheory.Sigma(sigmaArg));

                case "totient" or "phi" or "eulerphi":
                    if (!args[0].TryGetInteger(out var totientArg))
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.IntegerRequired, $"{lower} requires an integer argument."));
                    }
                    return new MathValue(NumberTheory.Totient(totientArg));

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
                    return MathValue.Pow(args[0], args[1]);

                case "logn":
                    var logVal = args[0].AsDouble();
                    var logBase = args[1].AsDouble();
                    if (logVal <= 0.0 || logBase <= 0.0 || logBase == 1.0)
                    {
                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "logn requires positive arguments and base cannot be 1."));
                    }
                    return new MathValue(Math.Log(logVal, logBase));

                case "atan2":
                    var y = args[0].AsDouble();
                    var x = args[1].AsDouble();
                    return new MathValue(EvaluateArcTan2(y, x));

                case "hypot":
                    return new MathValue(Math.Sqrt(args[0].AsDouble() * args[0].AsDouble() + args[1].AsDouble() * args[1].AsDouble()));

                case "mod":
                    return args[0] % args[1];

                case "round":
                    return EvaluateRound(args[0], args[1]);

                case "root" or "nthroot":
                    // If base and degree are both integers and degree is a positive int,
                    // check for an exact integer root before falling back to double approximation.
                    if (args[0].Type is MathValue.DataType.Integer &&
                        args[1].TryGetInteger(out var rootDegInt) &&
                        rootDegInt > BigInteger.Zero &&
                        rootDegInt <= int.MaxValue &&
                        args[0].IntVal.TryGetExactRoot((int)rootDegInt, out var exactNthRoot))
                    {
                        return new MathValue(exactNthRoot);
                    }

                    double baseVal = args[0].AsDouble();
                    double rootVal = args[1].AsDouble();
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
                            return new MathValue(-Math.Pow(-baseVal, 1.0 / rootVal));
                        }

                        throw new ExpressionException(new EvaluationDiagnostic(
                            EvaluationError.UndefinedMathResult, "Even root of a negative number has no real solution."));
                    }

                    return new MathValue(Math.Pow(baseVal, 1.0 / rootVal));

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
                    if (sigmaK < 0 || sigmaK > CalculationLimits.MaxExponentMagnitude)
                    {
                        if (sigmaK < 0)
                        {
                            throw new ExpressionException(new EvaluationDiagnostic(
                                EvaluationError.ArgumentOutOfRange, $"{lower} exponent k must be a non-negative integer."));
                        }
                        throw new CalculationLimitException();
                    }
                    return new MathValue(NumberTheory.Sigma(sigmaN, (int)sigmaK));

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
                "lerp" => args[0] + (args[2] * (args[1] - args[0])), // a + t * (b - a)
                _ => throw new ExpressionException(new EvaluationDiagnostic(
                    EvaluationError.UnknownFunction, $"Unknown function '{name}'.")),
            };
        }

        throw new ExpressionException(new EvaluationDiagnostic(
            EvaluationError.InvalidArgumentCount, $"Invalid argument count for '{name}'."));
    }

    private static MathValue EvaluateGcd(List<MathValue> args)
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

    private static MathValue EvaluateLcm(List<MathValue> args)
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
        }

        return new MathValue(BigInteger.Abs(result));
    }

    private static MathValue EvaluateMinMax(List<MathValue> args, bool findMinimum)
    {
        // Compare without converting every argument to double, and return the selected
        // operand unchanged so its numeric representation is preserved.
        var result = args[0];
        for (var i = 1; i < args.Count; i++)
        {
            var comparison = MathValue.Compare(result, args[i]);
            if (findMinimum ? comparison > 0 : comparison < 0)
            {
                result = args[i];
            }
        }

        return result;
    }

    private static MathValue EvaluateAvg(List<MathValue> args)
    {
        var sum = args[0];
        for (int i = 1; i < args.Count; i++)
        {
            sum += args[i];
        }

        return sum / new MathValue((BigInteger)args.Count);
    }

    private static MathValue EvaluateMedian(List<MathValue> args)
    {
        var sorted = new List<MathValue>(args);
        sorted.Sort(MathValue.Compare);

        int count = sorted.Count;
        if (count % 2 == 1)
        {
            return sorted[count / 2];
        }

        var mid1 = sorted[(count / 2) - 1];
        var mid2 = sorted[count / 2];
        return (mid1 + mid2) / new MathValue(2m);
    }

    private static MathValue EvaluateVariance(List<MathValue> args)
    {
        // Sample variance requires n >= 2: s^2 = (1 / (n - 1)) * sum((x_i - mean)^2)
        var mean = EvaluateAvg(args);
        var sumOfSquares = new MathValue(0m);

        for (int i = 0; i < args.Count; i++)
        {
            var diff = args[i] - mean;
            sumOfSquares += diff * diff;
        }

        return sumOfSquares / new MathValue((BigInteger)(args.Count - 1));
    }

    private static MathValue EvaluateStDev(List<MathValue> args)
    {
        var variance = EvaluateVariance(args);
        return new MathValue(Math.Sqrt(variance.AsDouble()));
    }

    private static MathValue EvaluateSum(List<MathValue> args)
    {
        var sum = args[0];
        for (int i = 1; i < args.Count; i++)
        {
            sum += args[i];
        }

        return sum;
    }

    private static MathValue EvaluateMul(List<MathValue> args)
    {
        var product = args[0];
        for (int i = 1; i < args.Count; i++)
        {
            product *= args[i];
        }

        return product;
    }

    private static MathValue EvaluateClamp(MathValue val, MathValue min, MathValue max)
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

    private static MathValue EvaluateRound(MathValue value, MathValue placesArg)
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

        if (value.Type is MathValue.DataType.Integer)
        {
            return value;
        }

        if (places == 0)
        {
            if (value.Type is MathValue.DataType.Decimal)
            {
                return new MathValue((BigInteger)Math.Round(value.DecVal, MidpointRounding.AwayFromZero));
            }

            return ToIntegralMathValue(Math.Round(value.AsDouble(), MidpointRounding.AwayFromZero));
        }

        if (value.Type is MathValue.DataType.Decimal)
        {
            return new MathValue(Math.Round(value.DecVal, places, MidpointRounding.AwayFromZero));
        }

        if (places <= 15)
        {
            return new MathValue(Math.Round(value.AsDouble(), places, MidpointRounding.AwayFromZero));
        }

        // Double values beyond 15 fractional decimal digits exceed double.Round's
        // argument ceiling. We preserve full IEEE 754 value without crashing.
        return value;
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
        // resulting number is finite and within supported integer bit limits. Non-finite values
        // (NaN, infinities) remain Double.
        if (double.IsFinite(roundedValue))
        {
            return new MathValue((BigInteger)roundedValue);
        }

        return new MathValue(roundedValue);
    }

    private static MathValue EvaluateFactorial(MathValue argument)
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
            f *= i;
        }

        return new MathValue(f);
    }
}
