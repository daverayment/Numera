<p align="center">
  <img src="assets/FxGear128x128.png" alt="Numera logo" width="128" height="128" />
</p>

# Numera

A lightweight mathematical expression evaluator for **.NET 10**, written in portable C# with no third-party dependencies. Designed for desktop launchers, calculator apps, and systems that require trustworthy evaluation, deep introspection, and direct support for user-authored inputs.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)

## Why Numera
Most expression evaluators force all calculations into standard 64-bit binary floats (`double`). While this is fast, it sacrifices accuracy and range. `0.3 / 0.1` becomes `2.9999999999999996`; financial subtractions accumulate errors; and numbers past $2^{53}$ silently drop digits. These compromises are often hidden from the user.

Numera instead takes a hybrid approach:
* **No Floating-Point Surprises:** `0.3 / 0.1` is `3`. `3033554.11 - 3033359.71` is exactly `194.40`, not `194.3999999999`.
* **Exact Integers and Fractions:** `2^100` preserves all 31 digits internally, and `(1/3)*3` stays exactly `1` without replaying old expressions. Default display is rounded independently of storage.
* **Understands Human Desktop Input:** Copes with Swiss thousands (`1'000'000`), European comma decimals (`1.000,50`), percentages (`100 + 15%`, `15% of 200`), postfix factorials (`5!`), copied Unicode operators (`×`, `÷`, `−`), and more.
* **Clean Angle Displays:** In degrees mode, special cases give clean numeric results such as `sin(180) = 0`, `cos(90) = 0`, and `tan(45) = 1`. Floating-point function results still carry Unverified exactness, not a symbolic exactness guarantee.
* **LaTeX and Typographic Output:** Formats expressions directly into MathJax (`\frac{a}{b}`) or rich typography (`2 × π × √(16)`) ideal for tooltips.
* **Fast and AOT-Ready:** Pure recursive-descent evaluation with zero AST allocations on the fast path.

---

## Examples
| What You Enter | What Happens | Result |
| :--- | :--- | :--- |
| `3033554.11 - 3033359.71` | Exact 128-bit decimal arithmetic | `194.40` |
| `2^100` | Exact arbitrary-precision integer, rounded display | `1.2676506002282294014967e30` |
| `1/3 + 0.5` | Exact Rational, optional fraction view | `0.8333333333333333` or `5/6` |
| `1'000'000.50 + 15% of 200` | Swiss grouping + prepositional percent | `1000030.50` |
| `200 + 15%` | Consumer additive percentage markup | `230` |
| `5! × (2pi)` | Postfix factorial + implicit multiply + Unicode | `753.9822368615503` |
| `sqrt(144) + sqrt(2^2000)` | Integer square root (even past `double.MaxValue`!) | `12 + 10715086...` (exact) |
| `gcd(24, 36, 60) + lcm(4, 6)` | Built-in discrete number theory | `24` |
| `0b10101011 - 0xAA` | Binary and Hexadecimal literals | `1` |

---

## Quickstart

Install the package via NuGet:
```bash
dotnet add package Numera
```

Evaluate an expression in a few lines of code:

```csharp
using Numera;
using System.Globalization;

Console.Write("Enter an expression: ");
string input = Console.ReadLine() ?? "";

// Human input is normalized automatically using the current culture.
if (!Calculator.TryEvaluate(input, out var result, out var diagnostic))
{
    Console.WriteLine($"Error at position {diagnostic.Position}: {diagnostic.Code} - {diagnostic.Message}");
    return;
}

// Check for valid numbers before using the result
if (!result.IsFinite)
{
    Console.WriteLine("Result is not a finite number.");
    return;
}

// Use the readable result display; exact values remain available separately
var card = result.FormatResult();
Console.WriteLine($"Result: {card.PrimaryDisplay} ({result.Type}) | Hex: {card.HexRepresentation}");
```

The examples below use decimal-point cultures. Enter decimal and argument separators according to your current culture, or create an engine with an explicit culture.

Try entering:
* `3033554.11 - 3033359.71` *(Exact decimal arithmetic: `194.40`)*
* `2^100` *(Exact arbitrary-precision integer with a readable scientific display)*
* `101 + pi` *(Double fallback for transcendental constants)*
* `max(10, min(20, 7, 9))` *(Variadic functions and nesting)*
* `1 / 0` or `sqrt(-1)` *(Clean error diagnostics and non-finite number handling)*
* `0b10101011 - 0xAA` *(Support for multiple integer literal formats, such as binary and hexadecimal)*

## Working with Results

When `TryEvaluate` succeeds, it returns an `EvaluationResult` containing a `MathValue`. You have a few clean ways to work with it:

Result equality and ordering compare numeric values, ignoring context and metadata.
Use `EvaluationResultComparers.Content` for stored values, ordered variables and
assignments, expressions, and options. See [result comparison](docs/architecture.md#result-equality-and-content-comparison)
for examples and the distinction from history-entry identity or persistence.

### Culture and Formatting Policy

`Calculator` uses `CultureInfo.CurrentCulture` at each call. `new CalculatorEngine()` and `new CalculatorOptions()` capture the current culture when created; `CalculatorEngine.Default` and `CalculatorOptions.Default` also return fresh instances capturing the culture at access time. Reuse an engine to keep its culture and configuration stable.

Use record `with` expressions to derive reusable options, or fluent `engine.WithCulture(...)`, `WithAngleUnit(...)`, and `WithOptions(...)` to create a differently configured engine without changing the original. `WithOptions(transform)` starts from existing settings; `WithOptions(options)` replaces the whole configuration. Required nulls and null transform results are rejected, not replaced with defaults. See [immutable configuration](docs/architecture.md#immutable-configuration) for examples and the intentionally optional null settings.

Results retain their originating settings. Use `result.PrimaryDisplay` for text or `result.FormatResult()` for text, rounding information, and alternatives. Explicit options and culture override independently; omitted or null arguments retain the result's settings:

```csharp
var formatted = result.FormatResult(culture: CultureInfo.InvariantCulture);
```

Choose the formatting entry point according to whose defaults should apply:

| Call | Default formatting policy |
| --- | --- |
| `result.FormatResult()` | The result's captured settings; recommended for saved calculator history. |
| `engine.FormatResult(result)` | The receiving engine's settings. |
| `Calculator.FormatResult(result)` | Library display defaults and current culture at call time. |

Both engine and facade methods also accept raw `MathValue` values. Explicit options
and culture override independently; null selects the defaults of the chosen entry point.
The result overloads preserve assignment metadata; raw-value overloads have none.

`FormattedCalculation` is an immutable, fully prepared snapshot: reading properties or switching views does not perform more formatting. `PrimaryDisplay` and `MachineReadable` are always present; null alternatives mean that view is unavailable for the value. Text has no character ceiling, so even long exact fractions and radix strings remain accessible. The default is still 24 significant digits, not 24 characters. Callers can check `.Length` to decide what fits their UI; see [representation lengths](docs/architecture.md#representation-lengths) for sizing guidance. Invalid options and numerical safety violations still throw when formatting, not when reading the snapshot.

For deterministic machine-authored input, explicitly use `CalculatorEngine.ForCulture(CultureInfo.InvariantCulture)`. `MachineReadable` and serialization remain invariant. Matching input and display cultures avoids separator-related misinterpretation, but rounded display text does not preserve the original value. Evaluate machine-readable expressions with an invariant-culture engine. Manually constructed results without options use current culture when formatted.

### Exact Fractions, Exactness, and Display

```csharp
var thirds = Calculator.Evaluate("1/3");
var card = thirds.FormatResult();
Console.WriteLine(card.PrimaryDisplay);         // 0.333333333333333333333333
Console.WriteLine(card.FractionRepresentation); // 1/3
Console.WriteLine(card.IsDisplayRounded);       // True
Console.WriteLine(thirds.Exactness);            // Exact
Console.WriteLine((thirds * 3).Value);           // 1, without re-evaluation
```

`FormatResult().PrimaryDisplay` uses at most 24 significant digits and fixed notation for rounded decimal exponents -24 through 23 inclusive. Scientific displays default to compact lowercase e-notation, such as `1e-400`. Configure `CalculatorOptions.ResultFormat` to change precision, notation, or style; set `ExponentMarker = ExponentMarker.Uppercase` for `1E-400`. Marker casing affects only compact numeric displays, not expression identifiers or machine-readable output. Typographic Unicode and MathJax remain explicit result-style choices; expression formatting still defaults to typographic Unicode. Representation-native `ToString` formatting remains separate: Integer and Decimal retain their .NET formatting behavior, while Rational uses a numeric general format by default.

Fixed, unrounded displays preserve a bounded decimal-scale hint from literals and exact arithmetic: `194.40 - 100` displays `94.40`. Non-whole values preserve scales up to four decimal places; whole values preserve up to two. Larger scales normalize away trailing zeros. Padding never exceeds the significant-digit budget and is omitted for scientific or rounded displays. The hint survives captured values and serialization without changing the exact value, fraction alternate, equality, or exactness.

Default `EvaluationResult.ToString()`, interpolation, and span formatting use that same numeric display policy. For example, `Calculator.Evaluate("194.40-100").ToString()` returns `"94.40"`. Explicit format strings still use the underlying value's native formatting; `result.Value.ToString(...)` and lossless value expressions remain separate from the user-facing display.

Use `result.Exactness` (`CalculationExactness.Exact` or `Unverified`) to guide your UI: an Exact `1/3` can show a rounded-display indicator and an exact-fraction button, while Unverified `ln(1)` should not acquire an exactness claim merely because it displays `0` without rounding. The status also helps recognize a calculation path that encountered an unverified input or operation; it is not an error estimate or a complete provenance trail. [The exactness guide](docs/exactness.md) gives UI, propagation, and imported-value examples.

`HasExactFraction` allows a fraction toggle for known or caller-asserted Exact finite results, including terminating decimals and integers. Fractions use reduced slash notation, never Unicode fraction characters. Caller-asserted Exact Doubles expose their stored binary fraction, not a corrected decimal value; ordinary Doubles remain Unverified. NaN and infinities cannot be marked Exact.

Finite decimal literals and Integer/Decimal/Rational arithmetic on Exact inputs retain Exact within configured limits. Double mixing and ordinary approximate function results propagate Unverified. Representation alone does not establish exactness.

Representation follows the arithmetic domain rather than whether a fraction happens to terminate: `1/2` and `1/3` are Rational, while `10/2` is Integer. Decimal literals such as `0.5` and `1.00` are Decimal when exactly representable. Integer/Decimal arithmetic stays Decimal even for integral answers (`0.5 + 0.5`), while arithmetic involving Rational stays Rational until its denominator reduces to one. If a Decimal-domain answer cannot fit Decimal exactly, its value is retained as Rational or Integer, never rounded to fit.

Use representation-independent fraction access when storage details are irrelevant:

```csharp
var result = Calculator.Evaluate("1/3 + 2/3");
if (result.ExactFraction is (var numerator, var denominator))
{
    Console.WriteLine($"{numerator}/{denominator}"); // 1/1
}
var fraction = result.GetExactFraction(); // Throws when known/asserted exact fraction availability is absent
```

Both `MathValue` and `EvaluationResult` expose `ExactFraction`, `GetExactFraction()`, and `TryGetExactFraction(...)`. Storage getters (`IntegerValue`, `DecimalValue`, `DoubleValue`, and Rational-only `Numerator`/`Denominator`) throw `InvalidOperationException` for a mismatched storage type rather than converting or returning zero. The forwarded getters on `EvaluationResult` and `Variable` follow the same rule. None of these convenience APIs infer exactness from a clean numeric display.

Both types also provide `ToDecimal()`, `ToDouble()` (with `AsDouble()` as an alias),
`ToBigInteger()`, and explicit casts to `decimal`, `double`, `BigInteger`, `int`, and
`long`. Integer conversions require a finite whole number and never truncate;
`TryGetInteger(...)` tests that requirement without throwing. Decimal and Double
conversions can round. These operations do not change the stored value or exactness.
See [conversion contracts](docs/architecture.md#inspection--conversion) for range and
rounding behavior.

`MachineReadable` and `MathValue.ToInvariantExpression()` preserve the numeric value separately from the rounded display, retaining decimal scale where an invariant decimal literal can express it (`194.40 - 100` yields `"94.40"`). They never apply the display's significant-digit rounding or trailing-zero limits. Exact slash fractions are used when needed; representation, exactness, and all display metadata are not guaranteed to survive re-evaluation. `MathValue.Serialize()` / `Deserialize()` preserve that full metadata.

Standard System.Text.Json serialization of `MathValue` uses the same precision-preserving
schema automatically: `JsonSerializer.Serialize(result.Value)` and
`JsonSerializer.Deserialize<MathValue>(json)` also work inside application-owned history
records. The converter handles values, not entire results or engine configurations.
See [JSON value serialization](docs/architecture.md#json-value-serialization) for details.

### 1. Formatted Display (`IFormattable`)
`EvaluationResult` implements `IFormattable`, so string interpolation and standard format strings use the result's captured culture unless explicitly overridden. Raw `MathValue` formatting uses the usual .NET culture defaults:

```csharp
// Uses the result's captured culture:
Console.WriteLine($"Result: {result}");

// Supports standard numeric format specifiers:
Console.WriteLine($"{result:N2}");

// Or format explicitly with an invariant or specific culture:
string output = result.ToString(CultureInfo.InvariantCulture);
```
*(When formatting `double` values, Numera uses .NET's default round-trip formatting if no custom format is supplied.)*

### 2. Inspecting the Underlying Value
If your application needs the raw numeric value rather than formatted text, inspect `result.Kind` to read the active property:

```csharp
// Use a switch statement to route the exact underlying type safely:
switch (result.Kind)
{
    case MathValue.DataType.Integer:
        ProcessExactInteger(result.IntegerValue);
        break;
    case MathValue.DataType.Decimal:
        ProcessExactDecimal(result.DecimalValue);
        break;
    case MathValue.DataType.Rational:
        ProcessExactFraction(result.Numerator, result.Denominator);
        break;
    case MathValue.DataType.Double:
        ProcessFloatingPoint(result.DoubleValue);
        break;
}
```

*(If you just need a standard `double` and can tolerate potential precision loss on large integers or decimals, `result.AsDouble()` provides a convenient conversion.)*

### 3. Handling Non-Finite Numbers
Operations that overflow 64-bit float representation (such as `exp(1000)`) produce `double.PositiveInfinity` or `double.NegativeInfinity`:

```csharp
if (!result.IsFinite)
{
    // Distinguish overflow or non-finite float results:
    Console.WriteLine("Float overflow / non-finite value");
}
```

### 4. Error Diagnostics & Exceptions
When an expression cannot be evaluated (e.g. syntax errors, unknown functions, division by zero, invalid domains, or missing integer operands), Numera provides structured diagnostic details via either `TryEvaluate` or `Evaluate`:

#### Option A: The Try Pattern (Recommended)
`TryEvaluate` returns `false` with a structured `EvaluationDiagnostic` rather than throwing exceptions for evaluation failures:

```csharp
if (!Calculator.TryEvaluate(input, out var result, out var diagnostic))
{
    // diagnostic provides Code, Message, Position, and Length for UI highlighting or localization:
    Console.WriteLine($"Error at index {diagnostic.Position} ({diagnostic.Code}): {diagnostic.Message}");
}
```

#### Option B: The Exception Pattern
If you prefer traditional exception handling, `Calculator.Evaluate` throws domain exceptions inheriting from `NumeraException` (`ExpressionException` for syntax/math errors, and `CalculationLimitException` for safety limits).

Every `NumeraException` exposes the structured `Diagnostic` property, and wrapped math runtime failures preserve their underlying cause in `InnerException`:

```csharp
try
{
    var result = Calculator.Evaluate(input);
    Console.WriteLine($"Result: {result}");
}
catch (NumeraException ex)
{
    // One catch block handles syntax errors, resource limits, and math runtime failures:
    Console.WriteLine($"Error ({ex.Diagnostic.Code}): {ex.Message}");
}
```
*(Standard developer errors, such as passing a `null` expression or invalid `CalculatorOptions`, continue to throw `ArgumentNullException` and `ArgumentOutOfRangeException`.)*

### 5. Configuring Angle Modes (Degrees, Radians, Gradians)
By default, trigonometric functions operate in radians (`AngleUnit.Radians`). If your application or UI calculator operates in degrees or gradians, derive an engine with the desired angle setting:

```csharp
var engine = new CalculatorEngine().WithAngleUnit(AngleUnit.Degrees);

// Degree calculations use .NET IEEE 754 pi-scaled intrinsics for clean cardinal values:
var sin90 = engine.Evaluate("sin(90)");    // 1
var sin180 = engine.Evaluate("sin(180)");  // 0, not 1.22e-16
var cos90 = engine.Evaluate("cos(90)");    // 0
var tan45 = engine.Evaluate("tan(45)");    // 1

// Inverse trigonometric functions also return results in the configured unit:
var angle = engine.Evaluate("arcsin(1)");  // 90
```

These floating-point function results remain Unverified; clean cardinal displays do not establish calculation exactness.

Supported units are:
* `AngleUnit.Radians` (default)
* `AngleUnit.Degrees` (360° per revolution; exact cardinal values for multiples of 90°/180°)
* `AngleUnit.Gradians` (400 gon per revolution)

## Why Numera?

Most expression evaluators rely exclusively on 64-bit IEEE 754 floating-point (`double`) arithmetic. While fast, double-precision introduces surprising artifacts:

1. **Decimal rounding issues:** Fractions such as `0.1` and `0.3` cannot be represented with finite binary expansions. Standard engines evaluate `0.3 / 0.1` as `2.9999999999999996` rather than `3`. Financial subtraction like `3033554.11 - 3033359.71` yields `194.3999999999...` instead of `194.40`.
2. **Integer precision gaps:** Integers beyond $2^{53}$ ($\approx 9 \times 10^{15}$) silently lose precision. For example, $2^{64}$ rounds to `18,446,744,073,709,600,000` rather than the exact `18,446,744,073,709,551,616`.

Numera takes a **hybrid approach**, backing each `MathValue` with one of four representations:
- **`BigInteger`** for exact arbitrary-precision integer arithmetic up to 16,384 bits.
- **`decimal`** for exact base-10 financial arithmetic with 28–29 significant digits.
- **`Rational`** for reduced exact fractional arithmetic without premature rounding.
- **`double`** for trigonometric, transcendental, and hardware-accelerated floating-point operations.

Your application can inspect `result.Type` to distinguish provably exact answers from floating-point approximations.

---

## Normalizing Human Input

Numera normalizes human input automatically before passing culture-invariant syntax to its core parser. The static facade uses current culture; an engine uses its captured culture.

For interactive desktop applications, launchers, or search boxes, evaluate the original input directly. To select a particular culture:

```csharp
using Numera;
using System.Globalization;

var engine = CalculatorEngine.ForCulture(new CultureInfo("de-DE"));
string raw = "= 1.000,50 × (2 + 3)! + 15% of 200 =";

var result = engine.Evaluate(raw);
Console.WriteLine($"Result: {result}");
```

`ExpressionNormalizer` remains available for standalone text cleanup. Do not normalize cultured input separately and then pass the invariant output into a human-input engine: that would normalize it twice. See [Expression Normalization](docs/normalization.md) for per-pass settings and raw-input mode.

---

## On-Demand AST Parsing & Expression Formatting

Beyond direct string evaluation, Numera provides on-demand AST parsing and typographic expression formatting:

```csharp
using Numera;
using Numera.Ast;

// 1. Parse into an Abstract Syntax Tree on demand:
if (Calculator.TryParse("2 * pi * sqrt(16) / 2", out ExpressionNode? ast, out EvaluationDiagnostic? error))
{
    // The AST can be inspected, visited, or transformed via IExpressionVisitor<T>
}

// 2. Format expressions for desktop launchers, UI labels, or Markdown math:
string typographic = Calculator.FormatExpression("2 * pi * sqrt(16) / 2");
// -> "2 × π × √(16) ÷ 2"

string latex = Calculator.FormatExpression("2 * pi * sqrt(16) / 2", ExpressionFormatOptions.LaTeX);
// -> "\frac{2 \cdot \pi \cdot \sqrt{16}}{2}"
```

---

## State, Variables, and Caller-Controlled Recalculation

Numera supports interactive sessions, variable assignments, and expression dependency inspection using an immutable point-in-time model:

```csharp
var engine = new CalculatorEngine();
var context = EvaluationContext.Empty;

// 1. Evaluate with assignments:
var r1 = engine.Evaluate("bill = 120", context);
context = r1.Context;

var r2 = engine.Evaluate("tip = 15% of bill", context);
context = r2.Context;

// 2. Inspect dependencies and virtual 'ans':
var r3 = engine.Evaluate("total = bill + tip + ans", context);
Console.WriteLine(r3.Value); // 156

// Syntactic read dependencies (bill, tip, ans):
foreach (var dep in r3.ReferencedVariables)
{
    Console.WriteLine($"Referenced: {dep}");
}

// Stored canonical AST and definition text on assigned variables:
var totalVar = r3.Context.Variables["total"];
Console.WriteLine(totalVar.Definition); // "bill + tip + ans"
```

Because contexts are immutable snapshots and variables retain their canonical ASTs, callers can build reactive recalculation graphs, spreadsheets, or audit logs by replaying expressions through `engine.Evaluate(expr, context)`.

Numera itself never replays completed operands or variable definitions during arithmetic. Captured values are authoritative; stored ASTs serve formatting and dependency inspection. Recalculation is an explicit caller action.

---

## Documentation

For comprehensive guides and references, see the documentation in [`docs/`](docs/):

- **[Calculation Examples](docs/examples.md)**: End-user examples across the engine's feature families, exactness and display rounding, and qualified comparisons with Double-based arithmetic.
- **[Syntax & Functions Reference](docs/syntax.md)**: Full catalog of operators, precedence rules, constants, literals, and all 60+ built-in functions (trigonometric, hyperbolic, combinatorics, number theory, probability, bitwise).
- **[Expression Normalization Guide](docs/normalization.md)**: Detailed breakdown of the normalization pipeline, culture options, and percentage/factorial/implicit-multiplication rules.
- **[Exactness for Application UIs](docs/exactness.md)**: Indicators, fraction toggles, interpreting calculation paths, and caller-supplied values.
- **[Numerical Architecture & Type Model](docs/architecture.md)**: Canonical Integer/Decimal/Rational values, Double boundaries, exactness metadata, captured-value evaluation, and result formatting.
- **[Safety Limits & Error Diagnostics](docs/safety-and-limits.md)**: Resource ceilings (`CalculationLimits`), recursion depth, memory protections, exception hierarchy, and diagnostic codes.

---

## Scope: What Numera is Not (Yet)

Numera evaluates real scalar expressions with exact integers, finite decimals, and rational arithmetic where possible; it is intentionally scoped and is not:

- **A Computer Algebra System (CAS):** No general symbolic simplification, differentiation, or integration. Captured-value variables and host-defined functions are supported.
- **An Arbitrary-Precision Float Engine:** Exact rational arithmetic is supported, but transcendental functions generally use standard floating-point approximations.
- **A Matrix or Complex Library:** Operates strictly on real scalar numbers.

## Development

With the .NET 10 SDK installed, open `Numera.slnx` in a compatible Visual Studio version, or run from the repository root:

- `dotnet build Numera.slnx`
- `dotnet test Numera.Tests/Numera.Tests.csproj`
- `dotnet pack Numera/Numera.csproj -c Release -o artifacts`

## License

Licensed under the MIT license. See [LICENSE](LICENSE) for more details.
