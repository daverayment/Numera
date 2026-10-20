<p align="center">
  <img src="assets/FxGear128x128.png" alt="Numera logo" width="128" height="128" />
</p>

# Numera

A lightweight mathematical expression evaluator for **.NET 10**, written in portable C# with no third-party dependencies.

Numera dynamically balances exact integer, high-precision decimal, and standard floating-point arithmetic for each operation. It is designed for applications that need trustworthy expression evaluation without the weight of a full symbolic math engine.

## Quickstart - Write a Calculator in ~10 Lines of Code
```csharp
using Numera;

Console.Write("Enter an expression: ");
string? input = Console.ReadLine();

if (!Calculator.TryEvaluate(input ?? "", out var result, out var diagnostic))
{
    Console.WriteLine($"Error: {diagnostic.Code} - {diagnostic.Message}");
    return;
}

// Check for valid numbers before using the result.
if (!result.IsFinite)
{
    Console.WriteLine("Result is not a finite number.");
    return;
}

// Display the result directly via string interpolation.
Console.WriteLine($"Result: {result} ({result.Type})");
```

Try entering:
* `3033554.11 - 3033359.71` *(Exact decimal arithmetic: `194.40`)*
* `2^100` *(Arbitrary precision integer: no overflow or scientific notation)*
* `101 + pi` *(Double fallback for transcendental constants)*
* `max(10, min(20, 7, 9))` *(Variadic functions and nesting)*
* `1 / 0` or `sqrt(-1)` *(Clean error diagnostics and non-finite number handling)*
* `0b10101011 - 0xAA` *(Support for multiple integer literal formats, such as binary and hexadecimal)*

## Working with Results

When `TryEvaluate` succeeds, it returns a `MathValue`. You have a few clean ways to work with it:

### 1. Formatted Display (`IFormattable`)
`MathValue` implements `IFormattable`, so string interpolation and standard format strings work naturally with the current culture:

```csharp
// Interpolates directly using CurrentCulture:
Console.WriteLine($"Result: {result}");

// Supports standard numeric format specifiers:
Console.WriteLine($"{result:N2}");

// Or format explicitly with an invariant or specific culture:
string output = result.ToString(CultureInfo.InvariantCulture);
```
*(When formatting `double` values, Numera defaults to `"G17"` round-trip precision if no custom format is supplied.)*

### 2. Inspecting the Underlying Value
If your application needs the raw numeric value rather than formatted text, inspect `result.Type` to read the active property:

```csharp
// Use a switch statement to route the exact underlying type safely:
switch (result.Type)
{
    case MathValue.DataType.Integer:
        ProcessExactInteger(result.IntVal);
        break;
    case MathValue.DataType.Decimal:
        ProcessExactDecimal(result.DecVal);
        break;
    case MathValue.DataType.Double:
        ProcessFloatingPoint(result.DblVal);
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
*(Standard developer errors, such as passing a `null` expression or an invalid `EvaluationOptions`, continue to throw `ArgumentNullException` and `ArgumentOutOfRangeException`.)*

### 5. Configuring Angle Modes (Degrees, Radians, Gradians)
By default, trigonometric functions operate in radians (`AngleUnit.Radians`). If your application or UI calculator operates in degrees or gradians, configure `EvaluationOptions.AngleUnit`:

```csharp
var options = new EvaluationOptions { AngleUnit = AngleUnit.Degrees };

// Degree calculations use .NET IEEE 754 pi-scaled intrinsics, giving exact cardinal results:
var sin90 = Calculator.Evaluate("sin(90)", options);    // 1.0 (exact)
var sin180 = Calculator.Evaluate("sin(180)", options);  // 0.0 (exact, not 1.22e-16)
var cos90 = Calculator.Evaluate("cos(90)", options);    // 0.0 (exact)
var tan45 = Calculator.Evaluate("tan(45)", options);    // 1.0 (exact)

// Inverse trigonometric functions also return results in the configured unit:
var angle = Calculator.Evaluate("arcsin(1)", options);  // 90.0 (exact)
```

Supported units are:
* `AngleUnit.Radians` (default)
* `AngleUnit.Degrees` (360° per revolution; exact cardinal values for multiples of 90°/180°)
* `AngleUnit.Gradians` (400 gon per revolution)

## Why Numera?

Most expression evaluators rely exclusively on 64-bit IEEE 754 floating-point (`double`) arithmetic. While fast, double-precision introduces surprising artifacts:

1. **Decimal rounding issues:** Fractions such as `0.1` and `0.3` cannot be represented with finite binary expansions. Standard engines evaluate `0.3 / 0.1` as `2.9999999999999996` rather than `3`. Financial subtraction like `3033554.11 - 3033359.71` yields `194.3999999999...` instead of `194.40`.
2. **Integer precision gaps:** Integers beyond $2^{53}$ ($\approx 9 \times 10^{15}$) silently lose precision. For example, $2^{64}$ rounds to `18,446,744,073,709,600,000` rather than the exact `18,446,744,073,709,551,616`.

Numera takes a **hybrid approach**, backing each `MathValue` with one of three representations:
- **`BigInteger`** for exact arbitrary-precision integer arithmetic up to 16,384 bits.
- **`decimal`** for exact base-10 financial arithmetic with 28–29 significant digits.
- **`double`** for trigonometric, transcendental, and hardware-accelerated floating-point operations.

Your application can inspect `result.Type` to distinguish provably exact answers from floating-point approximations.

---

## Normalizing Human Input

Numera's core evaluator expects clean, culture-invariant syntax (`.` decimal points, `,` argument commas, standard ASCII operators). 

For interactive desktop applications, launchers (such as PowerToys Run or Raycast), or search boxes, use **`ExpressionNormalizer`** to preprocess human quirks—including leading/trailing equals, European decimal commas, digit grouping, percentage idioms (`15% of 200`, `100 + 20%`), postfix factorials (`5!`), and implicit multiplication (`2pi`, `2(3+4)`):

```csharp
using Numera;
using System.Globalization;

var options = new NormalizationOptions { Culture = new CultureInfo("de-DE") };
string raw = "= 1.000,50 × (2 + 3)! + 15% of 200 =";

if (ExpressionNormalizer.TryNormalize(raw, out var clean, options))
{
    // clean: "1000.50 * factorial(2 + 3) + (15 / 100) * 200"
    var result = Calculator.Evaluate(clean);
    Console.WriteLine($"Result: {result}");
}
```

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

## Documentation

For comprehensive guides and references, see the documentation in [`docs/`](docs/):

- **[Syntax & Functions Reference](docs/syntax.md)**: Full catalog of operators, precedence rules, constants, literals, and all 60+ built-in functions (trigonometric, hyperbolic, combinatorics, number theory, probability, bitwise).
- **[Expression Normalization Guide](docs/normalization.md)**: Detailed breakdown of the normalization pipeline, culture options, and percentage/factorial/implicit-multiplication rules.
- **[Numerical Architecture & Type Model](docs/architecture.md)**: How `MathValue` balances `BigInteger`, `decimal`, and `double`, type promotion rules, and exact cardinal angles.
- **[Safety Limits & Error Diagnostics](docs/safety-and-limits.md)**: Resource ceilings (`CalculationLimits`), recursion depth, memory protections, exception hierarchy, and diagnostic codes.

---

## Scope: What Numera is Not (Yet)

Numera is designed for evaluating expressions with exact integers and representable base-10 decimals where possible; it is intentionally scoped and is not:

- **A Computer Algebra System (CAS):** No symbolic simplification, differentiation, integration, variables, or user-defined functions.
- **An Arbitrary-Precision Float Engine:** No arbitrary-precision floating-point arithmetic or exact rational types.
- **A Matrix or Complex Library:** Operates strictly on real scalar numbers.

## Development

With the .NET 10 SDK installed, open `Numera.slnx` in a compatible Visual Studio version, or run from the repository root:

- `dotnet build Numera.slnx`
- `dotnet test Numera.Tests/Numera.Tests.csproj`
- `dotnet pack Numera/Numera.csproj -c Release -o artifacts`

## License

Licensed under the MIT license. See [LICENSE](LICENSE) for more details.
