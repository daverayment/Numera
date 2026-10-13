<p align="center">
  <img src="assets/FxGear128x128.png" alt="Numera logo" width="128" height="128" />
</p>

# Numera

A lightweight mathematical expression evaluator for **.NET 10**, written in portable C# and with no third-party dependencies.

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
Expressions like `sqrt(-1)` or `exp(1000)` evaluate successfully to `double.NaN` and `double.PositiveInfinity`:

```csharp
if (!result.IsFinite)
{
    // Distinguish undefined operations (NaN) from overflow (Infinity).
    string message = result.IsNaN ? "Undefined" : "Overflow";
    Console.WriteLine(message);
}
```

### 4. Error Diagnostics
When evaluation fails (e.g. syntax errors, unknown functions, or division by zero), `TryEvaluate` returns `false` with a structured `EvaluationDiagnostic`:

```csharp
if (!Calculator.TryEvaluate(input, out var result, out var diagnostic))
{
    // Use diagnostic.Code for programmatic decisions or localisation:
    Console.WriteLine($"Error ({diagnostic.Code}): {diagnostic.Message}");
}
```
*(Use `Calculator.Evaluate(input)` if you prefer exceptions instead of boolean status returns.)*

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

- **Equality and Logical Operators:** `==`, `~=`, `&&`, and `||`. Zero is false; other values, including `NaN`, are treated as true. AND binds more tightly than OR. Short-circuit evaluation validates skipped branches for syntax and identifier validity without evaluating their expressions. Equality chains are evaluated left to right: `a == b == c` compares the zero-or-one result of `a == b` with `c`.

- **Constants:** `pi` (or `π`) and `e`.

- **Functions:**
  - **Trigonometry (Radians):** `sin`, `cos`, `tan`, `arcsin`, `arccos`, `arctan`, `cot`, `sec`, `csc`, `arccot`, `arcsec`, `arccsc`.
  - **Hyperbolic:** `sinh`, `cosh`, `tanh`, `arsinh`, `arcosh`, `artanh`, `coth`, `sech`, `csch`, `arcoth`, `arsech`, `arcsch`.
  - **Angle Conversion:** `rad(degrees)` converts degrees to radians; `deg(radians)` converts radians to degrees; `grad(radians)` converts radians to gradians (200 gradians = π radians).
  - **Logarithms/Roots:** `log` (base 10), `log2`, `log10`, `ln` (natural), `logn(value, base)`, `sqrt`, `root(value, root)`.
  - **General:** `min`, `max`, `abs`, `round`, `ceil`, `floor`, `sign`/`sgn`, `factorial`.
  - **Random:** `rand()` returns a double in [0,1); `randi(n)` returns an integer in [0,n).

- **Function behaviour and conventions:**
  - Function and constant names are case-insensitive.
  - `min` and `max` are variadic (accepting one or more arguments).
  - `round` rounds to the nearest whole number (midpoint away from zero).
  - Invalid domain arguments (such as negative factorials or invalid `randi` bounds) evaluate to `NaN`.

Unary signs bind less tightly than powers: `-2^2` means `-(2^2)`, while `2^-3` permits a negative exponent.

Numera input requires `.` as the decimal point and `,` as the argument separator, regardless of the current culture. It does not support localised input, digit-grouping separators, implicit multiplication, postfix factorial, scientific notation, or base-prefixed literals. The caller is responsible for any transformations to support those features.

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
