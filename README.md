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

When `TryEvaluate` succeeds, it returns a `NumberValue`. You have a few clean ways to work with it:

### 1. Formatted Display (`IFormattable`)
`NumberValue` implements `IFormattable`, so string interpolation and standard format strings work naturally with the current culture:

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
    case NumberValue.DataType.Integer:
        ProcessExactInteger(result.IntVal);
        break;
    case NumberValue.DataType.Decimal:
        ProcessExactDecimal(result.DecVal);
        break;
    case NumberValue.DataType.Double:
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

## Under the Hood: Why Three Numeric Types?
Notice in the examples above that Numera returns distinct **Integer**, **Decimal**, and **Double** representations rather than forcing all results into a single type.

Most expression evaluators rely exclusively on 64-bit IEEE 754 double-precision floating-point (**double**) arithmetic for all calculations. While this is fast and convenient, it can produce surprising behaviour and imprecision:

1. **Decimal Artifacts:** Base-10 fractions such as `0.3` and `0.1` aren't exactly representable in binary. Standard double-based engines evaluate `0.3 / 0.1` as `2.9999999999999996` rather than `3`. Financial calculations like `3033554.11 - 3033359.71` can likewise expose small binary rounding differences instead of the exact decimal value; so you would see `194.3999999999` instead of `194.40`.

2. **Loss of integer precision:** In double arithmetic, large gaps can exist between the values that can be accurately represented by the type. For example all the integers from `10^16 - 5` to `10^16 + 50` silently round to `10^16`. The issue becomes more pronounced with larger integers, such as `2^64`, which is represented as `18,446,744,073,709,600,000` instead of the exact value of `18,446,744,073,709,551,616`.

## How Numera is Different: A Hybrid Approach

Instead of forcing all calculations into a single numeric type, each operation in Numera produces a `NumberValue` backed by one of three types:

* **BigInteger** for exact integer arithmetic within a 16,384-bit safety ceiling. Preserves large integers and whole-number quotients, such as `(10^50) / (10^49)`.
* **Decimal** for base-10 arithmetic with roughly 28-29 significant digits and up to 28 decimal places. It preserves representable decimal fractions, making it useful for financial expressions. Parsing and arithmetic can round to fit its precision; conversion or arithmetic overflow falls back to double.
* **Double** for functions such as `sqrt` and `sin`, powers outside the exact integer path, and decimal overflow fallback. It provides roughly 15-17 significant decimal digits and a wide range of magnitudes. Results may be approximate, `NaN`, or infinite.

By exposing `result.Type`, Numera lets your application distinguish between provably exact integer results, high-precision decimal outputs, and floating-point approximations.

## Supported Syntax
Numera implements a standard Recursive Descent parser with the following capabilities:

- **Basic Mathematical Operators:** `+`, `-`, `*`, `/`, `%`, and right-associative `^` (exponentiation), unary `+` and unary `-`. (An example of right-associative exponentiation is `2^3^2` which evaluates as `2^(3^2) = 512`.)

- **Grouping:** Parentheses `()` and brackets `[]` for grouping expressions. Matching brackets/parentheses are required; unbalanced grouping is a syntax error.

- **Relational and Equality Operators:**
  - Comparisons: `<`, `<=`, `>`, `>=`, `==`, `!=`, and `~=`.
  - Results evaluate to `1` (true) or `0` (false) via exact comparison.
  - Chained comparisons evaluate conjunctively and short-circuit:
    - `1 < x <= 10` evaluates as `1 < x and x <= 10`.
    - `a == b == c` checks that all three values are equal (`a == b and b == c`), rather than folding into `(a == b) == c`. (Explicit parentheses like `(a == b) == c` can be used if comparing the Boolean outcome).

- **Logical Operators:**
  - Disjunction: `||` or `or` (short-circuiting).
  - Conjunction: `&&` or `and` (short-circuiting).
  - Exclusive OR: `xor`.
  - Unary negation: `!` or `not`.
  - Truthiness: Zero (`0`) is false; all non-zero numbers (including `NaN` and infinity) are true.
  - Short-circuiting skips evaluating right-hand branches when the outcome is already known, while still checking syntax.

- **Operator Precedence (highest to lowest):**
  1. Exponentiation (`^`, right-associative)
  2. Unary negation, signs, and bitwise NOT (`!`, `not`, `~`, `+`, `-`)
  3. Multiplication, division, remainder (`*`, `/`, `%`)
  4. Addition and subtraction (`+`, `-`)
  5. Bitwise shift (`<<`, `>>`)
  6. Chained comparison and equality (`<`, `<=`, `>`, `>=`, `==`, `!=`, `~=`)
  7. `&&`, `and`
  8. `xor`
  9. `||`, `or`

  > **Note on Unary Negation vs Exponentiation:**  
  > Numera follows standard mathematical and scientific computing convention (e.g. Python, Julia, MATLAB, ExprTK, Wolfram) where exponentiation binds tighter than unary minus:
  > - `-10^4` evaluates as `-(10^4) = -10000`.
  > - Use parentheses when a negative base is intended: `(-10)^4 = 10000`.  
  > *(Note that software like Microsoft Excel and Windows Calculator invert this rule by treating leading signs as higher precedence, evaluating `-10^4` as `10000`.)*

- **Constants:** `pi` (or `π`, `Π`) and `e`.

- **Functions:**
  - **Trigonometry:** `sin`, `cos`, `tan`, `arcsin`/`asin`, `arccos`/`acos`, `arctan`/`atan`, `atan2(y, x)`, `cot`, `sec`, `csc`, `arccot`, `arcsec`, `arccsc` (operates in radians by default, or degrees/gradians via `EvaluationOptions.AngleUnit`).
  - **Hyperbolic:** `sinh`, `cosh`, `tanh`, `arsinh`/`asinh`, `arcosh`/`acosh`, `artanh`/`atanh`, `coth`, `sech`, `csch`, `arcoth`, `arsech`, `arcsch`.
  - **Angle Conversion:** `rad(degrees)` or `deg2rad(degrees)` converts degrees to radians; `deg(radians)` or `rad2deg(radians)` converts radians to degrees; `grad(gradians)` or `grad2rad(gradians)` converts gradians to radians; plus explicit cross-conversions `deg2grad`, `rad2grad`, and `grad2deg`.
  - **Logarithms/Roots:** `log` (base 10), `log2`, `log10`, `ln` (natural), `log1p` (ln(1 + x)), `expm1` (e^x - 1), `logn(value, base)`, `sqrt`, `cbrt`, `root(value, root)`.
  - **Special & Probability:** `erf(x)` (error function), `erfc(x)` (complementary error function), `ncdf(x)` (standard normal cumulative distribution), `sinc(x)` (cardinal sine: sin(x)/x).
  - **General:** `min`, `max`, `avg`, `sum`, `mul`, `mod(a, b)`, `clamp(val, min, max)`, `abs`, `round(val[, places])`, `ceil`, `floor`, `trunc`, `frac`, `sign`/`sgn`, `factorial`, `hypot(x, y)`, `lerp(a, b, t)`.
  - **Bitwise:** Operator `~` (unary bitwise NOT); functions `shl(val, count)`, `shr(val, count)`, `band(a, b)`, `bor(a, b)`, `bxor(a, b)`, `bnot(a)`. Require exact integer operands and evaluate via arbitrary-precision integers.
  - **Random:** `rand()` returns a double in [0,1); `randi(n)` returns an integer in [0,n).

- **Literal Formats:**
  - Standard decimal and integer numbers.
  - Scientific notation: e.g. `1e6`, `2.5e-3`, `1.23E+10`.
  - Radix-prefixed exact integers: `0x...` (hexadecimal), `0b...` (binary), `0o...` (octal).

- **Function behaviour and conventions:**
  - Function and constant names are case-insensitive.
  - `min`, `max`, `avg`, `sum`, and `mul` are variadic (accepting one or more arguments).
  - `round` rounds midpoint away from zero to the nearest whole number (`round(val)`), or to the specified number of decimal places (`round(val, places)` for 0–28 places).
  - Invalid or undefined calculations fail evaluation with specific diagnostic codes:
    - **Mathematically undefined results** (e.g. `sqrt(-1)` or `log(0)`) report `UndefinedMathResult`.
    - **Fractions where whole numbers are required** (e.g. `1.5 << 2` or `factorial(2.5)`) report `IntegerRequired`.
    - **Out-of-range inputs** (e.g. `factorial(-5)` or negative shift counts like `1 << -1`) report `ArgumentOutOfRange`.

Unary signs bind less tightly than powers: `-2^2` means `-(2^2)`, while `2^-3` permits a negative exponent.

Numera input requires `.` as the decimal point and `,` as the argument separator, regardless of the current culture. It does not support localised input, digit-grouping separators, implicit multiplication, or postfix factorial. The caller is responsible for any transformations to support those features.

## Computational Safety and Limits
To safeguard against resource exhaustion when evaluating untrusted user input, Numera enforces sensible computational ceilings by default:

- Input length is limited to 4,096 UTF-16 code units, including whitespace.
- Parser recursion depth is limited to 128, counted at unary-expression parsing. Grouping, function arguments, unary signs, and powers contribute to this depth.
- Integers are limited to 16,384 bits (≈ 4,932 decimal digits).
- The maximum factorial argument is 1,000.
- Whole-number exponents are limited to an absolute magnitude of 10,000. Integer powers also have size checks. Fractional exponents use `Math.Pow` without an exponent-magnitude limit.
- Callers can set lower input-length and parser-depth limits using `EvaluationOptions`; the values above are the maximum allowed settings.

Exceeding a ceiling produces `CalculationLimitException`, or a `LimitExceeded` diagnostic through `TryEvaluate`.

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
