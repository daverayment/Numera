# Safety Limits & Error Diagnostics

If you evaluate expressions typed by someone else, you need to expect the awkward ones: deeply nested brackets, enormous integers, and calculations that would run for hours. Numera guards against these with strict, configurable limits, and reports every failure as a structured diagnostic rather than a crash.

---

## Computational Safety Ceilings

Numera defines hard safety bounds via `CalculationLimits`:

| Safety Bound | Constant | Default Limit | Purpose |
| :--- | :--- | :---: | :--- |
| **Input Length** | `MaxInputLength` | `4,096` UTF-16 units | Prevents excessive memory use during parsing |
| **Recursion Depth** | `MaxNestingDepth` | `128` frames | Prevents thread call-stack overflow from nested expressions or unary chains |
| **Integer Size** | `MaxIntegerBits` | `16,384` bits ($\approx 4,932$ digits) | Prevents memory exhaustion from giant integer allocations |
| **Factorial Argument** | `MaxFactorialArgument` | `1,000` | Prevents CPU and memory exhaustion in `factorial(n)` |
| **Exponent Magnitude** | `MaxExponentMagnitude` | `10,000` | Limits integer power calculations |
| **Primality Testing** | `MaxPrimalityBits` | `2,048` bits | Limits Miller-Rabin primality testing to prevent CPU exhaustion on huge primes |
| **Factorization Bound** | `MaxFactorizationBits` | `128` bits | Prevents denial of service on arbitrary-precision prime factorization |

### Customizing Limits
Limits are long-lived configuration, so set them on a `CalculatorOptions` and create a `CalculatorEngine` once. The engine validates the options at construction and can be reused across calls and threads. The static `Calculator` facade always uses the defaults.

```csharp
var engine = new CalculatorEngine(new CalculatorOptions
{
	MaxInputLength = 512,  // Stricter than default 4096
	MaxNestingDepth = 32   // Stricter than default 128
});

var result = engine.Evaluate("1 + 2");
```

> [!NOTE]
> Custom limits cannot exceed the hard ceilings defined in `CalculationLimits`. Out-of-range values throw `ArgumentOutOfRangeException` when the engine is created.

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
