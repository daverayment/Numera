# Syntax and Functions Reference

Numera implements an unambiguous mathematical grammar parsed using recursive descent, operating over real scalar numbers.

---

## Literals & Numbers

- **Integer & Decimal Literals:** Standard decimal representations such as `42`, `3.14159`, `0.005`.
- **Scientific Notation:** e.g., `1e6`, `2.5e-3`, `1.23E+10`.
- **Radix Literals (Exact Arbitrary-Precision Integers):**
  - Hexadecimal: `0x...` (e.g. `0xFF`, `0xDEADBEEF`)
  - Binary: `0b...` (e.g. `0b10101011`)
  - Octal: `0o...` (e.g. `0o755`)

---

## Constants

Numera provides transcendental constants (case-insensitive):

| Constant | Aliases | Description | Underlying Type |
| :--- | :--- | :--- | :--- |
| `pi` | `π`, `Π` | Ratio of circle circumference to diameter ($\approx 3.141592653589793$) | `double` |
| `tau` | `τ`, `Τ` | Full circle angle constant in radians, $2\pi$ ($\approx 6.283185307179586$) | `double` |
| `e` | | Base of the natural logarithm, Euler's number ($\approx 2.718281828459045$) | `double` |

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
- **Chained Comparisons:** Chained comparisons evaluate conjunctively and short-circuit:
  - `1 < x <= 10` evaluates as `1 < x and x <= 10`.
  - `a == b == c` verifies that all three are equal (`a == b and b == c`), rather than folding into `(a == b) == c`.
- **Truthiness:** `0` evaluates to false. Any non-zero real number (including `NaN` and `Infinity`) evaluates to true. Comparisons return `1` for true and `0` for false.
- **Grouping:** Parentheses `()` group subexpressions. Square brackets `[]` are not supported.

---

## Built-In Functions

Function names are case-insensitive.

### Trigonometric Functions
Trigonometric routines operate in radians by default, or in degrees/gradians when configured via `EvaluationOptions.AngleUnit`. In degree mode, cardinal angles (multiples of 90°/180°) evaluate to exact results without floating-point drift.

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
| `sqrt(x)` | Square root. Evaluates exact `BigInteger` for perfect integer squares, exact `decimal` where exact, double otherwise |
| `cbrt(x)` | Cube root |
| `root(x, n)` | $n$-th root of $x$ |
| `ln(x)` | Natural logarithm (base $e$) |
| `log(x)` | Common logarithm (base 10) |
| `log10(x)` | Base-10 logarithm |
| `log2(x)` | Base-2 logarithm |
| `logn(x, base)` | Logarithm of $x$ to an arbitrary base |
| `log1p(x)` | $\ln(1 + x)$, accurate for small $|x|$ |
| `expm1(x)` | $e^x - 1$, accurate for small $|x|$ |

### Combinatorics & Number Theory
*(These functions require exact integer arguments and evaluate via arbitrary-precision integers)*

| Function | Aliases | Description |
| :--- | :--- | :--- |
| `factorial(n)` | | Factorial $n!$ (bounded by `CalculationLimits.MaxFactorialArgument = 1000`) |
| `gcd(...values)` | | Greatest common divisor across one or more integers (variadic) |
| `lcm(...values)` | | Least common multiple across one or more integers (variadic) |
| `comb(n, k)` | `nCr`, `choose`, `binomial` | Binomial coefficient $\binom{n}{k}$ |
| `perm(n, k)` | `nPr` | Permutations $P(n, k) = \frac{n!}{(n-k)!}$ |
| `isprime(n)` | | Primality test (1 if prime, 0 otherwise; bounded by `MaxPrimalityBits = 2048`) |
| `divcount(n)` | `divisorcount` | Number of positive divisors $d(n)$ |
| `sigma(n[, k])` | `divsum` | Divisor sum $\sigma(n)$ or sum of $k$-th powers $\sigma_k(n)$ |
| `totient(n)` | `phi`, `eulerphi` | Euler's totient function $\phi(n)$ |

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
