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
All arguments passed to custom functions are evaluated **eagerly** before invoking the callback delegate. Short-circuiting or conditional argument evaluation (such as an `if(condition, thenValue, elseValue)` construct) cannot prevent side-effects or limit checks in unselected branches.

### 2. No AST Leakage
Callbacks receive evaluated `IReadOnlyList<MathValue>` arguments and return a `MathValue`. Syntax tree nodes, token spans, and parser internal states are intentionally kept internal and are never exposed to custom implementations.

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
- If a custom function throws `CalculationLimitException` (or produces an integer exceeding `CalculationLimits.MaxIntegerBits`), the ceiling violation is enforced immediately.
- Any other uncaught exception thrown by a callback is wrapped into an `ExpressionException` with `EvaluationError.EvaluationFailed` preserving the original exception message and source location span.

### 6. Expression Formatting
Custom function calls format cleanly across all supported expression rendering styles:
- **Typographic & ASCII**: Formatted with standard comma-separated arguments, e.g. `myFunc(21)`.
- **MathJax / LaTeX**: Multi-character custom function identifiers and names containing underscores are automatically escaped and typeset using `\operatorname{...}`, e.g. `custom_fn(10)` becomes `\operatorname{custom\_fn}(10)`.

