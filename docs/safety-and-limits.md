# Safety Limits & Error Diagnostics

When evaluating untrusted or dynamic expressions, applications must be protected against denial-of-service (DoS) vectors such as deeply nested expressions, massive integer memory allocations, and expensive algorithms.

Numera enforces strict, configurable limits and provides rich structured diagnostics on failure.

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

### Customizing Limits per Evaluation
Callers can configure stricter ceilings via `EvaluationOptions`:

```csharp
var options = new EvaluationOptions
{
	MaxInputLength = 512,  // Stricter than default 4096
	MaxNestingDepth = 32   // Stricter than default 128
};

var result = Calculator.Evaluate("1 + 2", options);
```

*(Note: Custom limits cannot exceed the hard ceilings defined in `CalculationLimits`.)*

---

## Error Handling Patterns

Numera offers two equivalent error handling styles:

### Pattern A: Structured Try Pattern (Recommended)
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

*(Programmer misuse, such as passing `null` or invalid arguments to library methods, throws standard .NET exceptions such as `ArgumentNullException`.)*

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
