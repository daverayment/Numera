# Syntax and Functions Reference

Numera's grammar is deliberately small and unambiguous: a recursive-descent parser over real scalar numbers. This page lists every literal, operator and built-in function.

Examples use decimal-point syntax. Human input follows the configured culture's decimal and argument separators; ambiguous grouping/argument commas are rejected rather than guessed. Use semicolons for explicit arguments, such as `pow(2;100)`, or space after commas in comma-list cultures, such as `pow(2, 100)`. See [Expression Normalization](normalization.md) for localized syntax and raw invariant mode.

> [!TIP]
> Looking for variables and assignment (`x = 5`, `ans`)? See [Variables and Context](variables.md).

---

## Literals & Numbers

- **Integer & Decimal Literals:** Standard decimal representations such as `42`, `3.14159`, `0.005`.
- **Scientific Notation:** e.g., `1e6`, `2.5e-3`, `1.23E+10`.

  > [!NOTE]
  > Finite literals are parsed exactly. A decimal point selects Decimal when exactly representable, even for `1.00`; fractional scientific notation such as `1e-2` also selects Decimal. Integers such as `42` and `1e6` stay Integer. Values beyond Decimal capacity remain exact as Rational or Integer. Integer division such as `1/2` produces Rational, not Decimal, unless it divides evenly. Reduced integer/fraction components are bounded by the configured numeric limits; exceeding them reports `LimitExceeded`, not a silent floating-point fallback.
- **Radix Literals (Exact Arbitrary-Precision Integers):**
  - Hexadecimal: `0x...` (e.g. `0xFF`, `0xDEADBEEF`)
  - Binary: `0b...` (e.g. `0b10101011`)
  - Octal: `0o...` (e.g. `0o755`)

For large decimal numbers, write the digits together (`1000`) or use the
[grouping forms for your culture](normalization.md#decimal-and-grouping-rules).
Underscores are for names, not numeric separators, so `1_000` is not accepted.

To multiply by an underscore-prefixed variable such as `_x`, write `2 _x` or
`2 * _x`, not `2_x`. See [Variables and Context](variables.md) for naming rules.

---

## Constants

Numera provides these constants (case-insensitive). The irrational constants
are Decimal approximations with Unverified exactness, not exact symbolic values.
Use that status for UI guidance rather than the number's appearance: `floor(pi)`
still has Unverified exactness even though it displays `3`. See the
[exactness guide](exactness.md) for interpreting a calculation path.

| Constant | Aliases | Description | Underlying Type |
| :--- | :--- | :--- | :--- |
| `pi` | `π`, `Π` | Ratio of circle circumference to diameter ($\approx 3.141592653589793$) | `decimal` |
| `tau` | `τ`, `Τ` | Full circle angle constant in radians, $2\pi$ ($\approx 6.283185307179586$) | `decimal` |
| `e` | | Base of the natural logarithm, Euler's number ($\approx 2.718281828459045$) | `decimal` |
| `phi` | `φ`, `ϕ`, `Φ` | Golden ratio ($\approx 1.618033988749895$) | `decimal` |
| `true`, `false` | | Exact truth values `1` and `0` | `decimal` |

`phi`, `φ`, `ϕ`, and `Φ` all denote the golden-ratio constant. With implicit
multiplication enabled, `phi(12)` means `phi * 12`, just like `φ(12)`.
When implicit multiplication is disabled, write the `*` explicitly. Use
`totient(12)` or `eulerphi(12)` for Euler's totient function.

### Constant Discovery

Use `Constants` to populate autocomplete, constant pickers, or tooltips without
evaluating expressions:

```csharp
foreach (ConstantInfo info in Constants.Canonical)
{
	Console.WriteLine($"{info.Name}: {info.Description} ({info.Value.Exactness})");
}

if (Constants.TryResolve("pi", out MathValue value))
{
	Console.WriteLine(value.DecimalValue); // The evaluator's stored Decimal approximation.
}
```

- `AllNames` and `All` include the canonical names and Greek aliases; `CanonicalNames`
  and `Canonical` omit alias entries. Each collection is sorted ordinally ignoring case
  and is read-only, including the nested `ConstantInfo.Aliases` collections.
- `TryGetInfo(name, out info)` returns the invocation name, canonical name, `ConstantKind`,
  stored `MathValue`, description, aliases, and alias flag. Both phi glyphs have their
  own alias entry; casing variants such as `PI` and `Π` are not enumerated separately.
- `TryResolve(name, out value)` returns the same representation, precision, and exactness
  used by evaluation. The four irrational constants remain Unverified; `true` and `false`
  remain Exact. Decimal storage is not itself a guarantee of mathematical exactness.
- Lookups follow the parser's culture-independent, case-insensitive rules and supported
  Greek spellings. Unknown, empty, whitespace-padded, or expression inputs return false;
  null names throw `ArgumentNullException`. On failure, metadata is null and the value
  is default, not a successfully resolved zero.

This catalog describes built-in expression constants, not variables, custom functions,
or the static properties on `MathValue`. In particular, `MathValue.NaN` and infinity
properties do not introduce corresponding recognized constant names.

---

## Operators & Precedence

Operators are grouped in the following order, from highest to lowest precedence:

| Precedence | Operators | Associativity | Description | Example |
| :---: | :--- | :---: | :--- | :--- |
| **1** | `^` | Right | Exponentiation | `2^3^2` = `2^(3^2)` = `512` |
| **2** | `+`, `-`, `!`, `not`, `~` | Right (Unary) | Unary positive, negation, logical NOT, bitwise NOT | `-10^4` = `-(10^4)` = `-10000` |
| **3** | Implicit multiplication | Left | Multiplication written without `*` | `2(3 + 4)` = `14` |
| **4** | `*`, `/`, `%` | Left | Multiplication, division, remainder | `10 % 3` = `1` |
| **5** | `+`, `-` | Left | Addition, subtraction | `5 - 3 + 2` = `4` |
| **6** | `<<`, `>>` | Left | Bitwise shifts (integers only) | `1 << 8` = `256` |
| **7** | `<`, `<=`, `>`, `>=`, `==`, `!=`, `~=` | Chained / Non-assoc | Comparisons and equality | `1 < x <= 10` |
| **8** | `&&`, `and` | Left | Logical conjunction (short-circuiting) | `x > 0 && y > 0` |
| **9** | `xor` | Left | Logical exclusive OR | `a xor b` |
| **10** | `\|\|`, `or` | Left | Logical disjunction (short-circuiting) | `x == 0 \|\| y == 0` |

> [!IMPORTANT]
> Calculators differ in how they read multiplication written without `*`.
> Numera reads `1/2x` as `1/(2*x)`. The same rule makes `6/2(1+2)` mean
> `6/(2*(1+2))`, giving `1`.
> When sharing a calculation, make the grouping explicit: `(6/2)*(1+2)` gives `9`,
> while `6/(2*(1+2))` gives `1`. See [implicit multiplication](implicit-rules.md)
> for more examples.

`%` also works as a postfix percentage: `15%` is `0.15`, and `200 + 15%`
is a markup giving `230`. A following `+` or `-` stays additive, regardless of
spacing: `7 % -3` means `7% - 3`, not remainder. For a signed remainder operand,
write `7 % (-3)` or `mod(7; -3)`. See [Percentages and modulo](percentages.md)
for the full rule and examples.

### Important Precedence & Grammar Notes

- **Unary Signs vs Exponentiation:** Following scientific convention (Python, Julia, MATLAB, ExprTK, Wolfram), exponentiation binds tighter than unary minus:
  - `-10^4` evaluates as `-(10^4) = -10000`.
  - Use parentheses when a negative base is intended: `(-10)^4 = 10000`.

  > [!WARNING]
  > This surprises people used to calculators that apply the sign first. If you want a negative base, always parenthesise it.
- **Chained Comparisons:** Chained comparisons evaluate conjunctively and short-circuit:
  - `1 < x <= 10` evaluates as `1 < x and x <= 10`.
  - `a == b == c` verifies that all three are equal (`a == b and b == c`), rather than folding into `(a == b) == c`.
- **Grouping:** Parentheses `()` group subexpressions, including [assignments](variables.md#assigning-variables). Square brackets `[]` are not supported.

### Logical and bitwise operations

Logical operators treat `0` as false and every other value as true, including
NaN and Infinity. Their answers, like comparison results, are always `0` or `1`.
Both `5` and `3` are true, so `5 xor 3` gives `0`: exactly one of them would
need to be true for the answer to be `1`.

For individual bits, use the [bitwise functions](#bitwise-functions) instead.
`bxor(5; 3)` gives `6`. Similarly, `6 and 3` gives `1`, while `band(6; 3)`
gives `2`. Single `&` and `|` symbols are not supported; use `and`/`or` for
logic and `band`/`bor` for bits.

`and` and `or` (also written `&&` and `||`) skip the right operand when the
left already determines the answer. For example, `0 and (x = 5)` returns `0`
without assigning `x`.

---

## Built-In Functions

Function names are case-insensitive.

### Trigonometric Functions
Trigonometric routines operate in radians by default, or in degrees/gradians when configured via `CalculatorOptions.AngleUnit` on a `CalculatorEngine`.

In degree and gradian modes, some special-angle results are exact: for example,
`sin(30)` in degrees and `sin(100/3)` in gradians return `1/2`, while `tan(45)` in
degrees returns `1`. Other results may be approximate; use the result's exactness
metadata rather than its displayed appearance. Undefined inputs, such as `tan(90)`
in degrees, report an error.

Radians use the numeric argument, not a symbolic identity. For example, `sin(pi)`
can have a small nonzero residual, while `tan(pi/2)` can return a large finite
approximation instead of a pole diagnostic.

Converting angles to radians with `rad` or `grad2rad` also produces a
finite-precision number, not a symbolic multiple of pi. For example,
`sin(rad(30))` in radian mode remains approximate even when it displays `0.5`,
unlike the exact `sin(30)` in degree mode. Exact symbolic pi support is a
[potential next step](potential-next-steps.md#exact-rational-multiples-of-pi).

`sin`, `cos`, `tan`, `cot`, `sec`, and `csc` return Unverified NaN for NaN or
positive/negative Infinity arguments in every angle mode.

| Function | Aliases | Description |
| :--- | :--- | :--- |
| `sin(x)` | | Sine |
| `cos(x)` | | Cosine |
| `tan(x)` | | Tangent |
| `arcsin(x)` | `asin` | Inverse sine |
| `arccos(x)` | `acos` | Inverse cosine |
| `arctan(x)` | `atan` | Inverse tangent |
| `atan2(y, x)` | | Two-argument quadrant-aware inverse tangent |
| `cot(x)` | | Cotangent |
| `sec(x)` | | Secant |
| `csc(x)` | | Cosecant |
| `arccot(x)` | `acot` | Inverse cotangent |
| `arcsec(x)` | `asec` | Inverse secant |
| `arccsc(x)` | `acsc` | Inverse cosecant |

### Hyperbolic Functions

| Function | Aliases | Description |
| :--- | :--- | :--- |
| `sinh(x)` | | Hyperbolic sine |
| `cosh(x)` | | Hyperbolic cosine |
| `tanh(x)` | | Hyperbolic tangent |
| `arsinh(x)` | `asinh` | Inverse hyperbolic sine |
| `arcosh(x)` | `acosh` | Inverse hyperbolic cosine |
| `artanh(x)` | `atanh` | Inverse hyperbolic tangent |
| `coth(x)` | | Hyperbolic cotangent; undefined at zero |
| `sech(x)` | | Hyperbolic secant |
| `csch(x)` | | Hyperbolic cosecant; undefined at zero |
| `arcoth(x)` | `acoth` | Inverse hyperbolic cotangent |
| `arsech(x)` | `asech` | Inverse hyperbolic secant |
| `arcsch(x)` | `acsch` | Inverse hyperbolic cosecant |

### Angle Conversion

| Function | Description |
| :--- | :--- |
| `rad(deg)` / `deg2rad(deg)` | Converts degrees to radians |
| `deg(rad)` / `rad2deg(rad)` | Converts radians to degrees |
| `grad(rad)` / `rad2grad(rad)` | Converts radians to gradians |
| `grad2rad(grad)` | Converts gradians to radians |
| `deg2grad(deg)` | Converts degrees to gradians |
| `grad2deg(grad)` | Converts gradians to degrees |

### Logarithms, Powers & Roots

| Function | Description |
| :--- | :--- |
| `sqrt(x)` | Square root. Recognized perfect integer squares return exact `BigInteger` roots; other inputs generally use Double |
| `cbrt(x)` | Cube root |
| `root(x, n)` | $n$-th root of $x$ |
| `pow(x, y)` | Raises $x$ to $y$, equivalent to `x^y` |
| `ln(x)` | Natural logarithm (base $e$) |
| `log(x)` | Common logarithm (base 10) |
| `log10(x)` | Base-10 logarithm |
| `log2(x)` | Base-2 logarithm |
| `logn(x, base)` | Logarithm of $x$ to an arbitrary base |
| `log1p(x)` | $\ln(1 + x)$, producing a Double with Unverified exactness |
| `expm1(x)` | $e^x - 1$, producing a Double with Unverified exactness |

#### Rational powers

Write a fractional exponent as `p/q` when you mean that fraction exactly.
Numera recognizes perfect rational roots, so `8^(1/3)` gives Exact `2` and
`(16/81)^(3/4)` gives Exact `8/27`. Other real results, such as `2^(1/3)`,
are Double approximations with Unverified exactness.

A negative base has a real rational power when the reduced denominator is odd:
`(-8)^(1/3)` is `-2`, `(-8)^(2/3)` is `4`, and `(-8)^(-1/3)` is `-1/2`.
An even denominator, as in `(-8)^(1/2)`, reports `UndefinedMathResult`.
Use parentheses around a negative base: `-8^(2/3)` negates the power of `8`,
while `(-8)^(2/3)` raises the negative number itself.

Finite decimal exponents mean their exact decimal fractions: `(-32)^0.2`
is `-2` because `0.2` is `1/5`. A finite decimal resembling `1/3` is not a
third. Likewise, a Double exponent supplied by an application is not matched
to a nearby fraction. Use `1/3`, `cbrt(x)`, or `root(x; 3)` for a cube root.
Stored fractional exponents work the same way through variables and `ans`.

Recognized rational results inherit their inputs' exactness; Unverified inputs
remain Unverified. Supplied Double bases stay on the approximate path.

Unverified describes what the application can claim, not how many digits are wrong.
For example, `ln(1)` displays `0` without rounding but is still Unverified, while
the recognized root `sqrt(4)` is Exact. Keep exactness and display indicators separate.

### Combinatorics & Number Theory
*(These functions require exact integer arguments and evaluate via arbitrary-precision integers)*

| Function | Aliases | Description |
| :--- | :--- | :--- |
| `factorial(n)` | | Factorial $n!$ (bounded by `CalculationLimits.MaxFactorialArgument = 1000`) |
| `gcd(...values)` | | Greatest common divisor across one or more integers (variadic) |
| `lcm(...values)` | | Least common multiple across one or more integers (variadic) |
| `comb(n, k)` | `nCr`, `choose`, `binomial` | Binomial coefficient $\binom{n}{k}$ |
| `perm(n, k)` | `nPr` | Permutations $P(n, k) = \frac{n!}{(n-k)!}$ |
| `isprime(n)` | | Primality test (deterministic through unsigned 64-bit inputs; larger positive results are probable-prime results with Unverified exactness; bounded by `MaxPrimalityBits = 2048`) |
| `divcount(n)` | `divisorcount` | Number of positive divisors $d(n)$ |
| `sigma(n[, k])` | `divsum` | Divisor sum $\sigma(n)$ or sum of $k$-th powers $\sigma_k(n)$ |
| `totient(n)` | `eulerphi` | Euler's totient function $\phi(n)$ |

### Special & Probability Functions

| Function | Description |
| :--- | :--- |
| `erf(x)` | Error function, accurate across double precision via Cody Chebyshev rational approximations |
| `erfc(x)` | Complementary error function $1 - \text{erf}(x)$, avoiding catastrophic tail cancellation |
| `ncdf(x)` | Standard normal cumulative distribution function $\Phi(x) = \frac{1}{2} \text{erfc}(-x/\sqrt{2})$ |
| `sinc(x)` | Unnormalized cardinal sine $\sin(x)/x$, with $\text{sinc}(0) = 1$ |
| `gamma(x)` | Euler gamma function $\Gamma(x)$ |
| `lgamma(x)` | Natural logarithm of the absolute value of the gamma function $\ln|\Gamma(x)|$ |
| `beta(a, b)` | Euler beta function $B(a, b) = \frac{\Gamma(a)\Gamma(b)}{\Gamma(a+b)}$ |
| `zeta(s)` | Riemann zeta function $\zeta(s)$ |

Gamma, log-Gamma, Beta, and Zeta evaluate using Double arithmetic and return
**Unverified** approximations, even when a particular result is a whole number.
Their output range is Double's: overflow produces signed infinity, and very small
results may be subnormal or round to signed zero. Use `lgamma` for the logarithmic
magnitude of Gamma values too large to return directly.

Poles and invalid real-domain arguments report `UndefinedMathResult`. Negative
infinity is invalid for `gamma`, `lgamma`, and either `beta` argument; positive-infinite
Beta arguments produce NaN. NaN arguments propagate.

### General Math & Rounding

| Function | Description |
| :--- | :--- |
| `abs(x)` | Absolute value |
| `sign(x)` / `sgn(x)` | Signum (-1 for negative, 0 for zero, 1 for positive); NaN returns Unverified `0` |
| `min(...values)` | Minimum value across one or more arguments (variadic) |
| `max(...values)` | Maximum value across one or more arguments (variadic) |
| `avg(...values)` | Arithmetic mean across one or more arguments (variadic) |
| `sum(...values)` | Sum across one or more arguments (variadic) |
| `mul(...values)` | Product across one or more arguments (variadic) |
| `mod(a, b)` | Remainder of $a / b$; an explicit alternative to [`%`](percentages.md#percentage-or-modulo), especially with signed operands |
| `clamp(val, min, max)` | Clamps value between lower and upper bounds |
| `hypot(x, y)` | Euclidean distance $\sqrt{x^2 + y^2}$, avoiding intermediate overflow |
| `lerp(a, b, t)` | Linear interpolation $a + t(b - a)$ |
| `floor(x)` | Greatest integer less than or equal to $x$ |
| `ceil(x)` | Least integer greater than or equal to $x$ |
| `trunc(x)` | Truncates fractional part toward zero |
| `frac(x)` | Fractional part $x - \text{trunc}(x)$ |
| `round(x[, places])` | Midpoint-away-from-zero rounding to nearest integer or specified decimal places (0–28) |

### Bitwise Functions
*(Require exact integer arguments)*

| Function | Description |
| :--- | :--- |
| `shl(val, count)` | Bitwise left shift (also `<<`) |
| `shr(val, count)` | Bitwise right shift (also `>>`) |
| `band(a, b)` | Bitwise AND |
| `bor(a, b)` | Bitwise OR |
| `bxor(a, b)` | Bitwise XOR |
| `bnot(a)` | Bitwise NOT (equivalent to prefix operator `~`) |

### Pseudo-Random Functions

For a six-sided die, use `randbetween(1, 6)`: both endpoints are included.
The `randi` forms exclude the upper endpoint, so `randi(1, 7)` gives the same
range, while `randi(6)` draws from `0` through `5`.

| Function | Description |
| :--- | :--- |
| `randbetween(min, max)` | Draws an integer from `min` through `max`, including both; requires `min <= max` |
| `rand()` | Returns a pseudo-random floating-point value in the half-open interval $[0.0, 1.0)$ |
| `randi(n)` | Returns an integer pseudo-random value in the half-open range $[0, n)$ (requires integer $n > 0$) |
| `randi(min, max)` | Draws an integer from `min` up to, but not including, `max`; requires `min < max` |

Integer bounds must fit the signed 64-bit range, from `-9223372036854775808`
through `9223372036854775807`. Negative bounds are allowed. The integer draws are
Exact; `rand()` returns an Unverified Double. These functions are not suitable for
cryptographic secrets.
