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

The name `phi` is also a function alias for Euler's totient. Known functions own
their following parentheses: `phi(12)` (including `phi (12)`) means `totient(12)`,
which is `4`, not multiplication by the golden ratio. Write `phi * (12)` for
that product. With implicit multiplication enabled, `φ(12)`, `ϕ(12)`, and `Φ(12)`
are golden-ratio products: the Greek spellings are constant aliases only.

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

Evaluation follows standard mathematical and scientific computing precedence (highest to lowest):

| Precedence | Operators | Associativity | Description | Example |
| :---: | :--- | :---: | :--- | :--- |
| **1** | `^` | Right | Exponentiation | `2^3^2` = `2^(3^2)` = `512` |
| **2** | `+`, `-`, `!`, `not`, `~` | Right (Unary) | Unary positive, negation, logical NOT, bitwise NOT | `-10^4` = `-(10^4)` = `-10000` |
| **3** | `*`, `/`, `%` | Left | Multiplication, division, remainder | `10 % 3` = `1` |
| **4** | `+`, `-` | Left | Addition, subtraction | `5 - 3 + 2` = `4` |
| **5** | `<<`, `>>` | Left | Bitwise shifts (integers only) | `1 << 8` = `256` |
| **6** | `<`, `<=`, `>`, `>=`, `==`, `!=`, `~=` | Chained / Non-assoc | Comparisons and equality | `1 < x <= 10` |
| **7** | `&&`, `and` | Left | Logical conjunction (short-circuiting) | `x > 0 && y > 0` |
| **8** | `xor` | Left | Logical exclusive OR | `a xor b` |
| **9** | `\|\|`, `or` | Left | Logical disjunction (short-circuiting) | `x == 0 \|\| y == 0` |

### Important Precedence & Grammar Notes

- **Unary Signs vs Exponentiation:** Following scientific convention (Python, Julia, MATLAB, ExprTK, Wolfram), exponentiation binds tighter than unary minus:
  - `-10^4` evaluates as `-(10^4) = -10000`.
  - Use parentheses when a negative base is intended: `(-10)^4 = 10000`.

  > [!WARNING]
  > This surprises people used to calculators that apply the sign first. If you want a negative base, always parenthesise it.
- **Chained Comparisons:** Chained comparisons evaluate conjunctively and short-circuit:
  - `1 < x <= 10` evaluates as `1 < x and x <= 10`.
  - `a == b == c` verifies that all three are equal (`a == b and b == c`), rather than folding into `(a == b) == c`.
- **Truthiness:** `0` evaluates to false. Any non-zero real number (including `NaN` and `Infinity`) evaluates to true. Comparisons return `1` for true and `0` for false.
- **Grouping:** Parentheses `()` group subexpressions. Square brackets `[]` are not supported.

---

## Built-In Functions

Function names are case-insensitive.

### Trigonometric Functions
Trigonometric routines operate in radians by default, or in degrees/gradians when configured via `CalculatorOptions.AngleUnit` on a `CalculatorEngine`. Exact cardinal values after degree/gradian angle projection have special cases, without proximity-based snapping. Trigonometric results remain Unverified; a whole-number display is not an exactness guarantee.

Radians use the numeric argument, not a symbolic identity. For example, `sin(pi)`
can have a small nonzero residual, while `tan(pi/2)` can return a large finite
approximation instead of a pole diagnostic. The same applies to radians obtained
with `rad` or `grad2rad`. Small nonzero angles and values near poles are not
snapped to cardinal values. Finite-precision projection and evaluation still
apply in every angle mode. Exact symbolic pi support is a
[potential next step](potential-next-steps.md#exact-rational-multiples-of-pi).

`tan`, `cot`, `sec`, and `csc` propagate NaN inputs as Unverified NaN results,
like `sin` and `cos`, rather than reporting them as poles. Existing pole and
infinite-input diagnostics are unchanged.

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
| `coth(x)` | | Hyperbolic cotangent |
| `sech(x)` | | Hyperbolic secant |
| `csch(x)` | | Hyperbolic cosecant |
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
| `ln(x)` | Natural logarithm (base $e$) |
| `log(x)` | Common logarithm (base 10) |
| `log10(x)` | Base-10 logarithm |
| `log2(x)` | Base-2 logarithm |
| `logn(x, base)` | Logarithm of $x$ to an arbitrary base |
| `log1p(x)` | $\ln(1 + x)$, producing a Double with Unverified exactness |
| `expm1(x)` | $e^x - 1$, producing a Double with Unverified exactness |

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
| `totient(n)` | `phi`, `eulerphi` | Euler's totient function $\phi(n)$ |

Prefer `totient(n)` in examples and user guidance to avoid confusion with the
golden-ratio constant `phi`. The aliases remain supported.

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
Scaled Gamma evaluation and reflection avoid overflowing intermediate
factors when the final answer is representable. Negative Gamma uses direct reflection
while its positive reflected Gamma is finite, preserving ordinary-case accuracy;
otherwise it uses logarithmic reflection to retain subnormal results. Beta combines its Lanczos terms
instead of subtracting nearly equal log-Gamma values, including for highly unequal
arguments: `beta(1, 1e16)` is approximately `1e-16`, not `1`.

These routines do not extend Double's output range. Genuine overflow produces
signed infinity; very small results may be subnormal or round to signed zero.
`lgamma` can represent the logarithmic magnitude of Gamma values that are too large
to return directly. Existing pole/domain checks still report `UndefinedMathResult`;
Zeta's negative-even-integer zeros remain explicitly handled.

Negative infinity in `gamma`, `lgamma`, or either `beta` argument reports an
infinity-specific `UndefinedMathResult` diagnostic. Beta with positive-infinite
arguments retains its existing NaN convention; these changes do not redefine
infinite-input limits. NaN propagation still takes precedence over domain errors.

### General Math & Rounding

| Function | Description |
| :--- | :--- |
| `abs(x)` | Absolute value |
| `sign(x)` / `sgn(x)` | Signum (-1 for negative, 0 for zero, 1 for positive) |
| `min(...values)` | Minimum value across one or more arguments (variadic) |
| `max(...values)` | Maximum value across one or more arguments (variadic) |
| `avg(...values)` | Arithmetic mean across one or more arguments (variadic) |
| `sum(...values)` | Sum across one or more arguments (variadic) |
| `mul(...values)` | Product across one or more arguments (variadic) |
| `mod(a, b)` | Remainder of $a / b$ |
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
| `shl(val, count)` | Bitwise left shift |
| `shr(val, count)` | Bitwise right shift |
| `band(a, b)` | Bitwise AND |
| `bor(a, b)` | Bitwise OR |
| `bxor(a, b)` | Bitwise XOR |
| `bnot(a)` | Bitwise NOT (equivalent to prefix operator `~`) |

### Pseudo-Random Functions

| Function | Description |
| :--- | :--- |
| `rand()` | Returns a pseudo-random floating-point value in the half-open interval $[0.0, 1.0)$ |
| `randi(n)` | Returns an integer pseudo-random value in the half-open range $[0, n)$ (requires integer $n > 0$) |
