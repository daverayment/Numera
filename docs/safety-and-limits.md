# Safety Limits & Error Diagnostics

If you evaluate expressions typed by someone else, you need to expect the awkward ones: deeply nested brackets, enormous integers, and calculations that would run for hours. Numera guards against these with strict, configurable limits, and reports every failure as a structured diagnostic rather than a crash.

---

## Computational Safety Ceilings

Numera defines hard safety bounds via `CalculationLimits`:

| Safety Bound | Constant | Default Limit | Purpose |
| :--- | :--- | :---: | :--- |
| **Input Length** | `MaxInputLength` | `4,096` UTF-16 units | Prevents excessive memory use during parsing |
| **Recursion Depth** | `MaxNestingDepth` | `128` frames | Prevents thread call-stack overflow from nested expressions or unary chains |
| **Integer Size** | `MaxIntegerBits` | `16,384` bits (up to 4,933 decimal digits) | Prevents memory exhaustion from giant integer allocations |
| **Factorial Argument** | `MaxFactorialArgument` | `1,000` | Prevents CPU and memory exhaustion in `factorial(n)` |
| **Exponent Magnitude** | `MaxExponentMagnitude` | `10,000` | Limits integer power calculations |
| **Primality Testing** | `MaxPrimalityBits` | `2,048` bits | Limits Miller-Rabin primality testing to prevent CPU exhaustion on huge primes |
| **Factorization Bound** | `MaxFactorizationBits` | `128` bits | Prevents denial of service on arbitrary-precision prime factorization |

### Customizing Limits
Limits are long-lived configuration, so set them on a `CalculatorOptions` and create a `CalculatorEngine` once. The engine validates the options at construction and can be reused across calls and threads. The static `Calculator` facade always uses the defaults.

```csharp
var engine = new CalculatorEngine(new CalculatorOptions
{
	MaxInputLength = 512,       // Stricter than default 4096
	MaxNestingDepth = 32,       // Stricter than default 128
	MaxIntegerBits = 4096,      // Stricter than default 16384
	MaxExponentMagnitude = 500, // Stricter than default 10000
	ResultFormat = new ResultFormatOptions
	{
		SignificantDigits = 10
	}
});

var result = engine.Evaluate("1 + 2");
```

> [!NOTE]
> Custom limits cannot exceed the hard ceilings defined in `CalculationLimits`. Out-of-range values throw `ArgumentOutOfRangeException` when the engine is created.

Calculation limits (`CalculatorOptions`) and formatting presentation options (`ResultFormatOptions`) are cleanly separated immutable configurations. Defaults are:

| Control | Default |
| :--- | ---: |
| Integer / reduced rational numerator / reduced denominator bits | 16,384 each |
| Arithmetic intermediate bits | 32,768 |
| Absolute integer exponent | 10,000 |
| Maximum requested significant display digits | 1,000 |
| Default significant display digits | 24 |

Fractions are cross-cancelled and reduced before unnecessary growth. Intermediate limits still apply: an oversized intermediate can be rejected even if a later expression would cancel it. Literal coefficients and powers of ten are bounded before allocation; callback-returned values and captured variable values are checked against the receiving engine's limits.

Calculation limit violations throw `CalculationLimitException`; exact calculations never silently fall back to Double and formatted output is never silently truncated. Standalone `MathValue` operators use default numeric limits.

There is no character-output ceiling. `SignificantDigits` controls primary-display precision, not total width, and exact fractions, machine-readable text, and radix alternatives are not shortened to match it. `FormatResult()` prepares a complete immutable snapshot; null alternatives indicate mathematical unavailability, not rejected length. Callers decide what fits their UI using the returned strings' `.Length`. Invalid options still throw, as do numerical work-limit violations; character and UTF-8 span formatting still return `false` if the supplied buffer is too small. See [representation lengths](architecture.md#representation-lengths) for overhead formulas and bounds derived from the numeric limits.

These size limits are not execution-time guarantees and cannot constrain work performed inside caller callbacks. Existing specialized number-theory limits remain in force.

Completed-result arithmetic checks the captured operands and the final value against the selected options' integer and rational-component limits. Operands are checked before arithmetic, even when cancellation would produce a small result. Binary composition selects the left result's options when available, otherwise the right result's options, otherwise the defaults. Unary composition uses the operand's options or the defaults.

Composition also applies the selected structural AST limits to its metadata, without evaluating that metadata. This prevents repeated shared-expression composition from creating an exponentially large inspection workload. Metadata is not required to resolve functions against the receiving engine: its captured values are already authoritative.

---

## Error Handling Patterns

Numera offers two equivalent error handling styles:

### Pattern A: Structured Try Pattern (Recommended)

> [!TIP]
> Prefer this pattern for user-typed input. Bad input is normal there, so it shouldn't cost you an exception.

`Calculator.TryEvaluate` returns `false` with an `EvaluationDiagnostic` record without throwing exceptions:

```csharp
if (!Calculator.TryEvaluate(input, out var result, out var diagnostic))
{
	Console.WriteLine($"Error Code: {diagnostic.Code}");
	Console.WriteLine($"Message:    {diagnostic.Message}");
	Console.WriteLine($"Location:   Index {diagnostic.Position}, Length {diagnostic.Length}");
}
```

### Pattern B: Exception Handling
`Calculator.Evaluate` throws typed exceptions inheriting from `NumeraException`:

- `ExpressionException`: Thrown on syntax errors, undefined mathematical operations, or invalid types.
- `CalculationLimitException`: Thrown when an expression violates a `CalculationLimits` ceiling.

Both exception classes provide the structured `.Diagnostic` record:

```csharp
try
{
	var result = Calculator.Evaluate(input);
}
catch (NumeraException ex)
{
	Console.WriteLine($"Failed ({ex.Diagnostic.Code}): {ex.Message}");
}
```

> [!NOTE]
> Programmer misuse, such as passing `null` or invalid arguments to library methods, throws standard .NET exceptions such as `ArgumentNullException`, not `NumeraException`.

---

## Diagnostic Error Codes

When evaluation fails, `EvaluationDiagnostic.Code` reports a stable, strongly typed category from the `EvaluationError` enum:

- `InvalidSyntax`: The expression violates grammar rules (e.g. malformed syntax, unbalanced parentheses, unexpected characters).
- `UnknownFunction`: An identifier was called as a function but is not recognized by the evaluator.
- `UnknownConstant`: An identifier was referenced as a constant but is not recognized by the evaluator.
- `InvalidArgumentCount`: A function was invoked with fewer or more arguments than permitted by its arity rules.
- `DivisionByZero`: The expression attempts a division or modulo operation by zero.
- `LimitExceeded`: Evaluation exceeded a configured safety ceiling (e.g. max input length, nesting depth, or bit size).
- `NumericOverflow`: A numerical operation exceeded the representable bounds of the arithmetic type.
- `IntegerRequired`: An operation requires integer operands, but received a non-integer or fractional value (e.g. bit shifts, combinatorics).
- `ArgumentOutOfRange`: An argument value is outside the permitted numerical range for the operation (e.g. negative base with fractional power).
- `UndefinedMathResult`: A mathematical function has no defined real-valued result for the given argument (e.g. `sqrt(-1)`).
- `AmbiguousLogicalNegation`: A logical negation was applied directly to a comparison without explicit parentheses while `RequireExplicitLogicalNegation` is enabled.
- `InvalidNumberFormat`: Reserved for human-formatted numbers that normalization rejects as ambiguous. Normalization does not currently reject any input, so this is not produced today; malformed numbers surface as positioned `InvalidSyntax` parse diagnostics.
