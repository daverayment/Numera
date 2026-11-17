# Safety Limits & Error Diagnostics

If you evaluate expressions typed by someone else, you need to expect the awkward ones: deeply nested brackets, enormous integers, and calculations that would run for hours. Numera uses strict safety limits to reject work beyond its supported bounds and reports expected input and calculation failures as structured diagnostics.

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

The exponent-magnitude ceiling applies to integer exponents, not to the numerator
of a reduced fractional exponent. Exact rational results and their intermediate
calculations remain subject to the component and intermediate bit bounds.
See [rational powers](syntax.md#rational-powers) for the real-domain and exactness
rules.

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

Fractions are cross-cancelled and reduced before unnecessary growth. Intermediate limits apply: an oversized intermediate can be rejected even if a later expression would cancel it. Literal coefficients and powers of ten are bounded before allocation; callback-returned values and captured variable values are checked against the receiving engine's limits.

Calculation limit violations throw `CalculationLimitException`; exact calculations never silently fall back to Double and formatted output is never silently truncated. Standalone `MathValue` operators use default numeric limits.

There is no character-output ceiling. `SignificantDigits` controls primary-display precision, not total width, and exact fractions, machine-readable text, and radix alternatives are not shortened to match it. `FormatResult()` prepares a complete immutable snapshot; null alternatives indicate mathematical unavailability, not rejected length. Callers decide what fits their UI using the returned strings' `.Length`. Invalid options and numerical work-limit violations throw; character and UTF-8 span formatting return `false` if the supplied buffer is too small. See [representation lengths](architecture.md#representation-lengths) for overhead formulas and bounds derived from the numeric limits.

These size limits are not execution-time guarantees and cannot constrain work performed inside caller callbacks.

### Arithmetic on Completed Results

You can use an `EvaluationResult` in further arithmetic, such as `result * 2`.
Numera uses the value already calculated; it does not run the original expression
or its functions again.

The input values and the answer must all fit the integer and fraction-size limits.
Inputs are checked first, so multiplying an oversized value by zero does not bypass
a limit. These checks use the results' calculator options, falling back to library
defaults when no options are supplied.

Numera also limits the size and depth of the combined expression tree it keeps for
display and inspection. Repeatedly combining results cannot grow that tree without
bound, even when the numeric answer stays small. Keeping the tree does not mean
evaluating it again.

Combining two results also requires compatible variables and calculator options.
See [combining completed results](variables.md#combining-completed-results) for
how these are matched, how missing options are handled, and what happens on a
conflict.

### Identifying the Limit

Limit failures use `EvaluationError.LimitExceeded`. The diagnostic's `LimitKind`
identifies the safety guard, and `LimitValue` gives the ceiling it used: an engine
setting or a fixed safety bound. The message names the limit too, but use the
structured fields for UI decisions or localization rather than matching message text.

| Expression | `LimitKind` | `LimitValue` with default settings |
| :--- | :--- | ---: |
| `1001!` | `FactorialArgument` | 1,000 |
| `9999^9999` | `IntegerBits` | 16,384 bits |
| `sqrt(2^16000)` | `ExponentMagnitude` | 10,000 |

The first guard to reject the calculation determines the diagnostic. In the last
example, the inner power exceeds the exponent limit before the square root runs.
`LimitValue` is the allowed ceiling, not the size of the rejected result; Numera may
reject an operation before constructing that result.

`CalculationLimitKind` also distinguishes intermediate arithmetic size, AST node
count, primality and factorization budgets, formatting precision, and serialized
value lengths. Units follow the kind: bit limits count absolute-magnitude bits,
and serialized lengths distinguish UTF-16 code units from UTF-8 bytes.
`ExecutionStack` has no numeric ceiling, so its `LimitValue` is null. Both fields
can be null for caller-supplied limit exceptions without that metadata, and are
null on other built-in error categories. Source positions remain available where
the failure can be associated with the input.

### Manually Constructed Expression Trees

Evaluation validates the entire AST before executing any node or custom callback.
Variable references, assignment targets, and function names must satisfy
`Identifier.IsValidIdentifier`; malformed names such as `1x` or `a b` report
`InvalidSyntax`, with the offending node's source span when supplied. Expression
constant markings, reserved assignment names, function bindings, arity checks,
and structural limits are also enforced.

Formatting an existing AST tolerates unknown function names, including
display-only names that are not executable identifiers. Variable references and
assignment targets must have valid identifier syntax. Captured-result metadata
receives structural validation only; arithmetic uses the stored
values and does not execute that metadata.

---

## Error Handling Patterns

Numera offers two equivalent error handling styles:

### Pattern A: Structured Try Pattern (Recommended)

> [!TIP]
> Prefer this pattern for user-typed input. Check the returned boolean and use the diagnostic to explain a rejected expression.

`Calculator.TryEvaluate` returns `false` with an `EvaluationDiagnostic` for expected
normalization, parsing, and calculation failures:

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

`TryEvaluate` is not a blanket exception-containment boundary. Unexpected library
or runtime faults are outside its expected-failure contract. A launcher that needs
to isolate such faults should handle and log them at its own application boundary,
without treating a failed calculation as a usable result. Custom callback failures
have their own [documented handling](custom-functions.md#5-exception-handling--safety-limits).

---

## While the user is typing

An unfinished expression need not produce a visible error on every keystroke.
When parsing recognizes missing input, the diagnostic has `IsIncomplete = true`:
`2 +`, `sin(`, `(2 + 3`, `x =`, and `0x` are examples. A known function name
such as `sin` also needs more input to form a call. The same applies to registered
custom functions, without guessing that an unknown name such as `sqr` means `sqrt`.

A host can leave its result and MathJax preview hidden until evaluation succeeds,
and hold back incomplete-input errors while the user is typing:

```csharp
var engine = CalculatorEngine.ForCurrentCulture();
if (engine.TryParse(input, out var expression, out var diagnostic) &&
	engine.TryEvaluate(expression, out var result, out diagnostic))
{
	Console.WriteLine(result.PrimaryDisplay);
	Console.WriteLine(engine.FormatExpression(expression, ExpressionFormatOptions.LaTeX));
}
else if (!diagnostic.IsIncomplete)
{
	Console.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
}
```

The flag describes a failed parse, not a partial result. `2 + )`, `max()`,
unknown names, and calculation errors are not incomplete. A false flag does not
mean valid; use the success boolean for that. Parsing failures do not execute
callbacks or commit assignments, and diagnostic spans still point to the original
input. For `x =`, the span highlights the `=`, even though the missing value
belongs after it; hosts should use the flag rather than infer incompleteness from
the span.

This is recognition of unfinished syntax, not a test of every suffix that could
make an expression valid. Input still follows the configured normalization rules,
including launcher decoration. Malformed localized numbers such as `1,00` in
en-US remain `InvalidNumberFormat` with a false flag, even while the user may be
on the way to typing `1,000`.

## Diagnostic Error Codes

When evaluation fails, `EvaluationDiagnostic.Code` reports a stable, strongly typed category from the `EvaluationError` enum:

- `InvalidSyntax`: The expression violates grammar rules (e.g. malformed syntax, unbalanced parentheses, unexpected characters).
- `UnknownFunction`: An identifier was called as a function but is not recognized by the evaluator.
- `UnknownVariable`: A variable name is not defined in the supplied context or expression.
- `NoPreviousResult`: The expression uses `ans`, but the context has no previous result. Carry forward a successful result's context, or seed `PreviousResult` explicitly.
- `InvalidArgumentCount`: A function was invoked with fewer or more arguments than permitted by its arity rules.
- `DivisionByZero`: The expression attempts a division or modulo operation by zero.
- `LimitExceeded`: A safety guard rejected the operation. `LimitKind` and `LimitValue` identify the guard and its effective ceiling when known.
- `NumericOverflow`: A numerical operation exceeded the representable bounds of the arithmetic type.
- `IntegerRequired`: An operation requires integer operands, but received a non-integer or fractional value (e.g. bit shifts, combinatorics).
- `ArgumentOutOfRange`: An argument value is outside the permitted numerical range for the operation (e.g. `round(1; 29)`).
- `UndefinedMathResult`: A mathematical function has no defined real-valued result for the given argument (e.g. `sqrt(-1)` or `(-8)^(1/2)`).
- `AmbiguousLogicalNegation`: A logical negation was applied directly to a comparison without explicit parentheses while `RequireExplicitLogicalNegation` is enabled.
- `InvalidNumberFormat`: Normalization rejected malformed or ambiguous localized numbers, including invalid grouping, repeated decimal separators, non-ASCII numeric digits, or commas that could both group digits and separate arguments. Invalid/mixed argument separators use `InvalidSyntax`. Diagnostics point into the original input and occur before evaluation; see [Expression Normalization](normalization.md).
