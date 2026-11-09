# Custom Functions

Numera allows callers to define domain-specific custom functions and register them on `CalculatorOptions.CustomFunctions`. Custom functions participate in parsing, static arity validation, and evaluation without exposing AST internals or requiring a dependency injection framework.

---

## Defining & Registering Functions

Custom functions are registered as immutable configuration on `CalculatorOptions` via `CustomFunction` instances.

### Collection Expressions & Fluent Builders

You can initialize `CustomFunctions` using C# collection expressions or fluent chaining:

```csharp
using Numera;

var options = new CalculatorOptions
{
	CustomFunctions =
	[
		CustomFunction.Create("double", (MathValue x) => x * new MathValue(2m)),
		CustomFunction.Create("hypot", (MathValue a, MathValue b) =>
			new MathValue(Math.Sqrt((a * a + b * b).AsDouble()))),
		CustomFunction.CreateVariadic("sumAll", minArgs: 1, args =>
		{
			var sum = args[0];
			for (int i = 1; i < args.Count; i++)
			{
				sum += args[i];
			}
			return sum;
		})
	]
};

var engine = new CalculatorEngine(options);
var result = engine.Evaluate("double(21) + sumAll(1, 2, 3)"); // 42 + 6 = 48
```

Fluent chained registration is also supported:

```csharp
var functions = CustomFunctionCollection.Empty
	.Add("double", (MathValue x) => x * new MathValue(2m))
	.Add("addThree", (MathValue a, MathValue b, MathValue c) => a + b + c);

var engine = new CalculatorEngine(new CalculatorOptions { CustomFunctions = functions });
```

---

## Design Characteristics & Limitations

### 1. Eager Argument Evaluation
All arguments passed to a reached custom function are evaluated **once, in source order**, before invoking the callback delegate. Logical and comparison short-circuiting can skip an entire function occurrence, but functions themselves have no lazy argument branches.

Two written calls execute twice. Reusing one completed `EvaluationResult`, variable, or `ans` uses its captured value and never invokes its stored expression again.

### 2. No AST Leakage
Callbacks receive evaluated `IReadOnlyList<MathValue>` arguments and return a `MathValue`. Syntax tree nodes, token spans, and parser internal states are intentionally kept internal and are never exposed to custom implementations.

Arguments can be Rational. Inspect `Kind` for representation access, or explicitly use `ToDouble()` / `ToDecimal()` when a scalar is required. Exact arithmetic preserves fractions across callbacks.

Values carry `Accuracy` (`Exact` or `Unverified`). Integer, Decimal, and fraction construction describe exact supplied values; ordinary Double construction is Unverified. `WithAccuracy` is a trusted caller assertion, not an independent numerical proof. Unverified inputs conservatively propagate through function evaluation.

### 3. Arity & Case-Insensitive Matching
- Function identifiers are matched ordinally and case-insensitively, consistent with all Numera identifiers.
- Fixed-argument factories (`Create` with 0, 1, 2, or 3 arguments) and variadic factories (`CreateVariadic`) configure parameter signatures verified during AST validation. Calls with insufficient or excessive arguments fail with `EvaluationError.InvalidArgumentCount`.
- Like built-in functions, custom functions require parentheses (`myFunc(x)`). Referencing a function without parentheses throws `EvaluationError.InvalidSyntax`.

### 4. Reserved Names & No Shadowing
To prevent subtle mathematical ambiguities and maintain consistency:
- Custom function names cannot shadow built-in functions in `FunctionCatalog` (e.g. `sin`, `sqrt`, `hypot`).
- Custom function names cannot shadow reserved constants in `ConstantCatalog` (e.g. `pi`, `e`, `tau`).
- Custom function names cannot shadow reserved keywords (e.g. `and`, `or`, `not`, `xor`).
- Custom functions cannot be assigned to as variables (e.g. `myFunc = 10` throws `EvaluationError.InvalidSyntax`).
Conflicts are validated at engine creation and throw `ArgumentException`.

### 5. Exception Handling & Safety Limits
- If a custom function throws `DivideByZeroException`, it is mapped to `EvaluationError.DivisionByZero`.
- If a custom function throws `CalculationLimitException` or returns a value exceeding the engine's configured integer or rational component limits, the ceiling violation is enforced immediately.
- Any other uncaught exception thrown by a callback is wrapped into an `ExpressionException` with `EvaluationError.EvaluationFailed` preserving the original exception message and source location span.

### 6. Expression Formatting
Custom function calls format cleanly across all supported expression rendering styles:
- **Typographic & ASCII**: Formatted with standard comma-separated arguments, e.g. `myFunc(21)`.
- **MathJax / LaTeX**: Multi-character custom function identifiers and names containing underscores are automatically escaped and typeset using `\operatorname{...}`, e.g. `custom_fn(10)` becomes `\operatorname{custom\_fn}(10)`.
