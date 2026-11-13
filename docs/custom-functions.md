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
	.SetItem("answer", () => new MathValue(42), "A zero-argument function")
	.SetItem("double", (MathValue x) => x * new MathValue(2m))
	.SetItem("addThree", (MathValue a, MathValue b, MathValue c) => a + b + c);

var engine = new CalculatorEngine(new CalculatorOptions { CustomFunctions = functions });
var answer = engine.Evaluate("answer()"); // 42
```

`SetItem` supports zero, one, two, or three arguments and variadic handlers. It returns
a new collection; replacing a case-insensitively matching name preserves its registration
position without changing the original collection. Zero-argument callbacks run when
evaluated, not when registered, and must be called with empty parentheses.

## Discovery & Autocomplete

`engine.GetFunctions()` lists the built-in and custom functions available to that
engine, alphabetically by name. `engine.SearchFunctions(query)` searches that same
set using the built-in discovery ranking: exact names and aliases, prefixes,
substrings, categories, then keywords. Both methods omit built-in alias entries
unless `includeAliases: true` is supplied. Empty or whitespace-only searches return
the selected functions alphabetically. Name matching and ordering are ordinal and
case-insensitive. Discovery never invokes callbacks.

```csharp
var suggestions = engine.SearchFunctions("dou");
var customOnly = engine.GetFunctions()
	.Where(f => f.Category == FunctionCategory.Custom);
var builtInOnly = engine.GetFunctions()
	.Where(f => f.Category != FunctionCategory.Custom);
```

Both methods return `FunctionInfo` metadata. Custom entries use
`FunctionCategory.Custom`, their registered name as their canonical name, and their
declared signature. Descriptions can be null; aliases and keywords are empty
collections. Searching for `"custom"` also matches the custom category.

The static `Functions` API describes built-in functions only, independently of
engine configuration.

---

## Design Characteristics & Limitations

### 1. Eager Argument Evaluation
All arguments passed to a reached custom function are evaluated **once, in source order**, before invoking the callback delegate. Logical and comparison short-circuiting can skip an entire function occurrence, but functions themselves have no lazy argument branches.

Two written calls execute twice. Reusing one completed `EvaluationResult`, variable, or `ans` uses its captured value and never invokes its stored expression again.

### 2. No AST Leakage
Callbacks receive evaluated `IReadOnlyList<MathValue>` arguments and return a `MathValue`. Syntax tree nodes, token spans, and parser internal states are intentionally kept internal and are never exposed to custom implementations.

Arguments can be Rational. Inspect `Kind` for representation access, or explicitly use `ToDouble()` / `ToDecimal()` when a scalar is required. Exact arithmetic preserves fractions across callbacks.

Values carry `Exactness` (`CalculationExactness.Exact` or `Unverified`) so the host can label answers and recognize unverified calculation paths. For example, an exact fraction callback returning `1/3` can enable a fraction toggle even when its decimal display rounds; a callback returning a Double `0.5` remains Unverified even though its display needs no rounding.

Integer, Decimal, and fraction construction assert exact supplied values, not the exactness of work done before constructing them. If your callback converts an approximate calculation to Decimal, mark the result with `WithExactness(CalculationExactness.Unverified)`. `WithExactness(Exact)` is a trusted caller assertion: it does not verify earlier work, recover digits, or turn a Double `0.1` into `1/10`. NaN and infinities cannot be marked Exact. Unverified arguments conservatively keep function results Unverified even when a callback returns a newly constructed Integer. See [the exactness guide](exactness.md#my-app-supplies-numbers-or-custom-functions) for examples and counterexamples.

### 3. Arity & Case-Insensitive Matching
- Function identifiers are matched ordinally and case-insensitively, consistent with all Numera identifiers.
- Fixed-argument factories (`Create` with 0, 1, 2, or 3 arguments) and variadic factories (`CreateVariadic`) configure parameter signatures verified during AST validation. Calls with insufficient or excessive arguments fail with `EvaluationError.InvalidArgumentCount`.
- Like built-in functions, custom functions require parentheses (`myFunc(x)`). Referencing a function without parentheses throws `EvaluationError.InvalidSyntax`.

### 4. Reserved Names & No Shadowing
To prevent subtle mathematical ambiguities and maintain consistency:
- Custom function names cannot shadow built-in functions in `FunctionCatalog` (e.g. `sin`, `sqrt`, `hypot`).
- Custom function names cannot shadow reserved constants (e.g. `pi`, `e`, `tau`). Use `Constants.AllNames` or `Constants.TryResolve` to discover these names; see [constant discovery](syntax.md#constant-discovery).
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
