# Numera

A lightweight mathematical expression evaluator for **.NET 10**, written in portable C# and with no third-party dependencies.

Numera dynamically scales between integer, decimal, and double arithmetic for each operation, with a focus on exact integer arithmetic and preserving representable base-10 values. It is designed for applications that need expression evaluation without a full symbolic math library.

## Why? The Precision Problem
Relying solely on double-precision floating-point arithmetic may lead to surprising evaluation results:

1. **Decimal Artifacts:** Base-10 fractions such as `0.3` and `0.1` aren't exactly representable in binary. Standard double-based engines evaluate `0.3 / 0.1` as `2.9999999999999996` rather than `3`. Financial calculations like `3033554.11 - 3033359.71` can likewise expose small binary rounding differences instead of the exact decimal value; so you would see `194.3999999999` instead of `194.40`.

2. **Loss of integer precision:** In double arithmetic, large gaps can exist between the values that can be accurately represented by the type. For example all the integers from `10^16 - 5` to `10^16 + 50` silently round to `10^16`. The issue becomes more pronounced with larger integers, such as `2^64`, which is represented as `18,446,744,073,709,600,000` instead of the exact value of `18,446,744,073,709,551,616`.


## How it Works: A Hybrid Approach

Instead of forcing all calculations into a single numeric type, Numera evaluates expressions using recursive descent where every node returns a polymorphic `NumberValue`. Each operation returns a `NumberValue`, which stores its result as an integer, decimal, or double:

* **BigInteger** for exact integer arithmetic within a 16,384-bit safety ceiling. Preserves large integers and whole-number quotients, such as `(10^50) / (10^49)`.
* **Decimal** for base-10 arithmetic with roughly 28-29 significant digits and up to 28 decimal places. It preserves representable decimal fractions, making it useful for financial expressions. Parsing and arithmetic can round to fit its precision; conversion or arithmetic overflow falls back to double.
* **Double** for functions such as `sqrt` and `sin`, powers outside the exact integer path, and decimal overflow fallback. It provides roughly 15-17 significant decimal digits and a wide range of magnitudes. Results may be approximate, `NaN`, or infinite.

Integer arithmetic is exact within the supported limits. Decimal and double calculations have finite precision, so their accuracy depends on the expression and its intermediate results. Numera reports the result's numeric type; the accuracy of approximate results is unverified.

## Getting Started

```csharp
using System.Globalization;
using Numera;

// Basic evaluation.
NumberValue result = Calculator.Evaluate("3033554.11 - 3033359.71");

// This calculation is exactly representable in decimal.
Console.WriteLine(result.Type); // Output: Decimal
Console.WriteLine(result.DecVal.ToString(CultureInfo.InvariantCulture)); // Output: 194.40

// Report expected evaluation failures using stable diagnostic codes.
if (Calculator.TryEvaluate("1 / 0", out var value, out var diagnostic))
{
	Console.WriteLine(value.AsDouble());
}
else
{
	Console.WriteLine($"Error: {diagnostic.Code}"); // Output: Error: DivisionByZero
}

```

`Evaluate` throws for evaluation failures; `TryEvaluate` returns false with a diagnostic for expected failures. Invalid API arguments, such as a null expression or invalid options, throw exceptions from either method. `diagnostic.Code` provides a stable category for programmatic decisions or localisation, while `diagnostic.Message` provides a human-readable explanation.

To read a result in its stored representation, inspect `Type` and use `IntVal`, `DecVal`, or `DblVal`. `AsDouble()` is convenient when you need a double, with possible precision loss when converting an integer or decimal.

`TryEvaluate` returns true when evaluation completes, including results such as `NaN` from `sqrt(-1)` or infinity from `exp(1000)`. Applications that require finite results should check these values before using them.

## Supported Syntax
Numera implements a standard Recursive Descent parser with the following capabilities:

- **Basic Mathematical Operators:** `+`, `-`, `*`, `/`, `%`, and right-associative `^` (exponentiation), unary `+` and unary `-`. (An example of right-associative exponentiation is `2^3^2` which evaluates as `2^(3^2) = 512`.)

- **Grouping:** Parentheses `()` and brackets `[]` for grouping expressions.

- **Equality and Logical Operators:** `==`, `~=`, `&&`, and `||`. Zero is false; other values, including `NaN`, are treated as true. AND binds more tightly than OR. Short-circuit evaluation validates skipped branches for syntax, function and constant names, and argument counts without executing their operations or functions. Equality chains are evaluated left to right: `a == b == c` compares the zero-or-one result of `a == b` with `c`.

- **Constants:** `pi` (or `π`) and `e`.

- **Functions:**
	- **Trigonometry (Radians):** sin, cos, tan, arcsin, arccos, arctan, cot, sec, csc, arccot, arcsec, arccsc.
  - **Hyperbolic:** sinh, cosh, tanh, arsinh, arcosh, artanh, coth, sech, csch, arcoth, arsech, arcsch.
	- **Angle Conversion:** rad(degrees) converts degrees to radians; deg(radians) converts radians to degrees; grad(radians) converts radians to gradians (200 gradians = π radians).
  - **Logarithms/Roots:** log (base 10), log2, log10, ln (natural), logn(value, base), sqrt, root(value, root).
  - **General:** min, max, abs, round, ceil, floor, sign/sgn, factorial.
  - **Random:** rand() returns a double in [0,1); randi(n) returns an integer in [0,n).

Function and constant names are case-insensitive. `min` and `max` accept one or more arguments. `round` rounds to a whole number, with midpoint ties away from zero. `factorial` accepts nonnegative whole numbers within its limit; invalid domain arguments return `NaN`. `randi(n)` requires a positive whole-number bound no greater than `long.MaxValue`; invalid bounds return `NaN`.

Unary signs bind less tightly than powers: `-2^2` means `-(2^2)`, while `2^-3` permits a negative exponent.

Numera requires `.` as the decimal point and `,` as the argument separator, regardless of the current culture. It does not support localised input, digit-grouping separators, implicit multiplication, postfix factorial, scientific notation, or base-prefixed literals. The caller is responsible for any transformations to support those features.

## Computational Safety and Limits
Numera limits expression size and computational work to reduce resource-exhaustion risk when evaluating user input. Applications should also control request volume and overall resource usage.

- Input length is limited to 4,096 UTF-16 code units, including whitespace.
- Parser recursion depth is limited to 128, counted at unary-expression parsing. Grouping, function arguments, unary signs, and powers contribute to this depth.
- Integers are limited to 16,384 bits (≈ 4,932 decimal digits).
- The maximum factorial argument is 1,000.
- Whole-number exponents are limited to an absolute magnitude of 10,000. Integer powers also have size checks. Fractional exponents use `Math.Pow` without an exponent-magnitude limit.
- Callers can set lower input-length and parser-depth limits using `EvaluationOptions`; the values above are the maximum allowed settings.

Exceeding a ceiling produces `CalculationLimitException`, or a `LimitExceeded` diagnostic through `TryEvaluate`.

## Development

With the .NET 10 SDK installed, open `Numera.slnx` in a compatible Visual Studio version, or run from the repository root:

- `dotnet build Numera.slnx`
- `dotnet test Numera.Tests/Numera.Tests.csproj`
- `dotnet pack Numera/Numera.csproj -c Release -o artifacts`

## License

Licensed under the MIT license. See [LICENSE](LICENSE) for more details.
