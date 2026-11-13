# Calculation examples

A practical starting collection of Numera calculations: everyday arithmetic,
scientific functions, sessions, and cases where exact arithmetic matters.
Examples cover the main feature families; the [syntax reference](syntax.md)
lists every function and alias.

Unless stated otherwise, examples use default settings: decimal points,
radians, and a numeric display with **24 significant digits** and culture-aware
digit grouping. Your application may configure different input conventions,
angle units, or display settings. Scientific notation is used when a number
requires more than 24 integer digits or 24 fractional decimal places.

## Reading the answers

Use **calculation exactness** to guide a user's understanding of the answer, separately
from an indicator that its **display is rounded**:

- **Exact** means the finite value is known or asserted to be exact. A displayed decimal
  can still be rounded; an optional fraction view shows the exact answer.
- **Unverified** means Numera has not established exactness. This includes
  ordinary floating-point functions and calculations involving approximate inputs.
  A neat-looking answer such as `1` does not change that status.

The application can expose an exact-fraction toggle when available. Fractions
are reduced slash fractions in plain text, optionally stacked in rich rendering.
They do not replace the familiar numeric display by default.

| Calculation | Default numeric display | Exact alternate | Display rounded? |
| --- | --- | --- | --- |
| `1/3` | `0.333333333333333333333333` | `1/3` | Yes |
| `1/8` | `0.125` | `1/8` | No |
| `2^100` | `1.2676506002282294014967e30` | `1267650600228229401496703205376` | Yes |
| `1e-400` | `1e-400` | `1/10^400`, written out as a slash fraction | No |

All four calculations are Exact, regardless of whether their displays round.
In contrast, `sqrt(2)` is Unverified and has no exact-fraction alternate. `ln(1)` is
also Unverified despite displaying `0` without rounding, so your UI should not turn
the absence of a rounded-display indicator into an exactness claim.
The [exactness guide](exactness.md) shows UI labels, path interpretation, and caller
assertions with examples and counterexamples.

Fixed, unrounded displays also preserve bounded decimal places: `194.40 - 100`
displays `94.40`, and `100.00` keeps its two decimal places. Non-whole values
preserve scales up to four places; whole values preserve up to two. Larger scales
trim trailing zeros, so `194.40000 - 100` displays `94.4`. These are presentation
hints, not precision guarantees, and do not override the significant-digit budget
or scientific notation.

## Everyday arithmetic and exact fractions

The answer column below gives the exact mathematical value, not necessarily
the default display text.

| Try this | Exact answer | Demonstrates |
| --- | --- | --- |
| `2 + 3 * 4` | `14` | Multiplication before addition |
| `(2 + 3) * 4` | `20` | Explicit grouping |
| `2^3^2` | `512` | Powers group from the right: `2^(3^2)` |
| `-2^2` | `-4` | A leading minus applies after the power |
| `(-2)^2` | `4` | Parentheses make the negative number the base |
| `0.1 + 0.2` | `0.3` | Finite decimal literals are exact |
| `0.3 / 0.1` | `3` | Exact decimal division |
| `3033554.11 - 3033359.71` | `194.40` | Subtraction without losing the cents |
| `(1/49)*49` | `1` | An intermediate fraction stays exact |
| `1/3 + 0.5` | `5/6` | Decimal input does not force a rounding boundary |
| `(2^53 + 1) - 2^53` | `1` | Large consecutive integers remain distinct |
| `(10^20 + 0.125) - 10^20` | `0.125` | A small amount survives addition to a large one |

Integers, finite decimal literals, and fractions participate in exact arithmetic,
subject to resource limits. Ordinary Double values supplied by an application,
and most scientific function results, introduce an Unverified boundary.

Storage follows the calculation's domain: `1/2` and `1/3` are both Rational,
while `0.5` is Decimal. Exact Decimal arithmetic remains Decimal even when
`0.5 + 0.5` produces a whole number. This does not change the numeric/fraction
views above; applications can use `ExactFraction` without inspecting storage.

## Percentages, tax, discounts, and pasted input

| Try this | Exact answer | Meaning |
| --- | --- | --- |
| `200 + 15%` | `230` | Add a 15% tax or markup |
| `200 - 15%` | `170` | Apply a 15% discount |
| `15% of 200` | `30` | Calculate the tax or discount amount |
| `200 * 15%` | `30` | Multiply by the percentage as a rate |
| `200 / (1 + 15%)` | `4000/23` | Recover the pre-tax amount from a tax-inclusive total |
| `2(3 + 4)` | `14` | Implicit multiplication |
| `(2 + 3)(4 + 1)` | `25` | Adjacent groups multiply |
| `= 2 × (3 + 4) =` | `14` | Launcher decoration and pasted multiplication |
| `5 − 2` | `3` | A pasted mathematical minus |
| `5! × 2` | `240` | Postfix factorial and a typographic operator |
| `1'000'000 + 1` | `1000001` | Apostrophe digit grouping |

Percentage markup follows the **written operand shape**, not a hidden unit system.
For example, `200 + 15%` is a markup, while adding a variable whose value happens
to be `0.15` is ordinary addition. Use explicit parentheses and multiplication
when you want to make a rate calculation independent of that shorthand.

An application configured for German input and output can accept
`1.000,50 + 0,25` and display `1.000,75`. In comma-decimal cultures, semicolons
separate function arguments. See [normalization](normalization.md) for options.

## Previous answers and stored variables

These examples require an application that carries the evaluation context
from one calculation to the next. A standalone stateless call does not remember
earlier answers or assignments.

| Enter in order | Answer |
| --- | --- |
| `5` | `5` |
| `ans + 1` | `6` |
| `ans * 2` | `12` |

`ans` is the captured previous answer, not an instruction to execute its original
calculation again.

| Enter in a new session, in order | Answer |
| --- | --- |
| `x = 3` | `3` |
| `a = x/3` | `1` |
| `x = 6` | `6` |
| `a * 3` | `3` |

Changing `x` does not retroactively change `a`. Similarly, storing `part = 1/3`
and then calculating `part * 3` gives exactly `1`. Applications can deliberately
recalculate saved formulas, but Numera does not silently replay them.
See [variables and context](variables.md).

## Roots, rounding, and useful helpers

| Try this | Exact answer |
| --- | --- |
| `sqrt(144)` | `12` |
| `cbrt(-125)` | `-5` |
| `root(81,4)` | `3` |
| `abs(-12.5)` | `12.5` |
| `sign(-12.5)` | `-1` |
| `floor(-7/3)` | `-3` |
| `ceil(-7/3)` | `-2` |
| `trunc(-7/3)` | `-2` |
| `frac(-7/3)` | `-1/3` |
| `round(2.5)` | `3` |
| `round(-2.5)` | `-3` |
| `round(2.55,1)` | `2.6` |
| `mod(17,5)` | `2` |
| `clamp(25,0,10)` | `10` |
| `lerp(10,20,0.25)` | `12.5` |

Recognized perfect integer roots are exact; `sqrt(2)` uses floating point.
Typed `round(...)` rounds midpoint ties **away from zero**. Display formatting
instead rounds midpoint ties **to even**, without changing the stored value.
For example, an application displaying two significant digits shows exact `1.25`
as `1.2`, while its fraction alternate remains `5/4`.

## Scientific functions and angle modes

These answers are **Unverified**, including those that display as whole numbers.
“About” gives a useful reference value, not a certified error bound.

| Try this in radians | Answer |
| --- | --- |
| `sin(pi/2)` | About `1` |
| `sqrt(2)` | About `1.414213562373095` |
| `ln(e)` | About `1` |
| `log(1000)` | About `3` |
| `log2(1024)` | About `10` |
| `exp(1)` | About `2.718281828459045` |
| `expm1(1)` | About `1.718281828459045` |
| `log1p(1)` | About `0.6931471805599453` |
| `hypot(3,4)` | About `5` |
| `sinh(0)`, `tanh(0)`, `arsinh(0)` | `0` |
| `cosh(0)` | `1` |
| `deg2grad(90)` | About `100` |

`pi`, `tau`, `e`, and `phi` are approximate constants, not exact symbolic values.
Using them carries Unverified status through later calculations.

With **degree mode explicitly configured**, `sin(30)` and `cos(60)` give about
`0.5`, `tan(45)` about `1`, and `arcsin(1)` about `90`. With **gradian mode**,
`sin(100)` gives about `1`. Typing an angle does not automatically change the mode.

**Current limitation:** tiny-argument checks of `expm1(1e-20)` and `log1p(1e-20)`
on the tested Windows/.NET 10 runtime did not retain the expected tiny positive
value. Do not assume those names guarantee cancellation-free or arbitrary-precision
evaluation. The examples above use moderate arguments.

## Lists, statistics, and counting

| Try this | Answer | Exactness |
| --- | --- | --- |
| `min(7,2,9)` | `2` | Exact |
| `max(7,2,9)` | `9` | Exact |
| `avg(1/3,2/3)` | `0.5` | Exact |
| `sum(1/3,1/3,1/3)` | `1` | Exact |
| `mul(2,3,4)` | `24` | Exact |
| `median(1,9,3,5)` | `4` | Exact |
| `variance(1,2,3)` | `1` | Exact |
| `stdev(1,2,3)` | `1` | Unverified |
| `gcd(24,36,60)` | `12` | Exact |
| `lcm(4,6)` | `12` | Exact |
| `factorial(6)` | `720` | Exact |
| `comb(52,5)` | `2598960` | Exact |
| `perm(10,3)` | `720` | Exact |
| `isprime(97)` | `1` | Exact |
| `divcount(12)` | `6` | Exact |
| `sigma(12)` | `28` | Exact |
| `sigma(12,2)` | `210` | Exact |
| `totient(12)` | `4` | Exact |

Recommendation: use `totient(n)` for Euler's totient; its `phi(n)` alias shares a name with the
golden-ratio constant. Write `phi * (n)` when you mean multiplication by that constant.

Variance is **sample variance**, divided by `n-1`. `comb(52,5)` counts unordered
five-card hands; `perm(10,3)` counts ordered selections without replacement.
`sigma(12,2)` sums squared divisors. For sufficiently large inputs, primality
and factorization-dependent functions may use probable-prime reasoning and return
Unverified results rather than claiming a proof.

## Specialist and probability functions

These functions use floating-point approximations and are Unverified.

| Try this | Reference answer |
| --- | --- |
| `erf(0)` | `0` |
| `erfc(10)` | About `2.088487583762545 × 10^-45` |
| `ncdf(0)` | `0.5` |
| `sinc(0)` | `1` |
| `gamma(5)` | About `24` |
| `beta(2,3)` | About `1/12` |
| `zeta(2)` | About `1.644934066848226` |

`ncdf` is the standard normal cumulative distribution. `sinc(x)` uses
`sin(x)/x`, with the continuous value `1` at zero. These are numerical functions,
not exact symbolic evaluations of the special functions.

## Programmer calculations and logical tests

Radix literals and these bitwise examples produce exact integers.

| Try this | Answer |
| --- | --- |
| `0xFF + 1` | `256` |
| `0b101010` | `42` |
| `0o755` | `493` |
| `1 << 8` | `256` |
| `255 >> 4` | `15` |
| `band(0b1100,0b1010)` | `8` |
| `bor(0b1100,0b1010)` | `14` |
| `bxor(0b1100,0b1010)` | `6` |
| `bnot(5)` | `-6` |
| `popcount(0b101101)` | `4` |

An application can offer hexadecimal, binary, and octal alternate displays
for integral results.

Comparisons and logical operators use `1` for true and `0` for false:

| Try this | Exact answer |
| --- | --- |
| `0.1 + 0.2 == 0.3` | `1` |
| `1 < 2 < 3` | `1` |
| `1 < 3 < 2` | `0` |
| `true xor false` | `1` |
| `not 0` | `1` |
| `0 and (1/0)` | `0` |
| `1 or (1/0)` | `1` |
| `1 > 2 < (1/0)` | `0` |

The last three skip the division because the answer is already determined.
Short-circuiting skips unnecessary **evaluation**, not syntax validation.

## Random values and application-defined functions

- `rand()` draws a floating-point number in `[0,1)`; its exactness is Unverified.
- `randi(6) + 1` draws an exact integer from `1` through `6`.
- In a session, `r = rand()` captures one draw. `r - r` gives `0` without drawing
  again, but remains Unverified because it used an Unverified input.
- Two explicitly written `rand()` calls execute twice. Reusing a completed result
  does not execute its expression again.

These random functions are not a source of cryptographic secrets.
Applications can register additional named functions, such as a unit conversion
or business calculation. Such names are not built-ins unless the host registers
them; their exactness depends on their *returned* values and conservatively propagates
Unverified evaluated arguments.
See [custom functions](custom-functions.md).

## Where exact arithmetic helps compared with Double-based evaluators

Numera can outperform a binary floating-point scalar evaluator **in retained
numerical precision**, especially for decimals, fractions, and large integers.
This is not a speed benchmark or a claim that Numera is a better tool for every task.

The baseline below is ordinary **.NET binary64 (`double`) arithmetic** with normally
parsed decimal values, verified separately from Numera. A user interface may round these baseline values to
clean-looking text; that does not restore lost information.

| Calculation | Numera | Illustrative .NET Double baseline |
| --- | --- | --- |
| `0.1 + 0.2` | Exactly `0.3` | `0.30000000000000004` |
| `0.3 / 0.1` | Exactly `3` | `2.9999999999999996` |
| `3033554.11 - 3033359.71` | Exactly `194.40` | `194.39999999990687` |
| `(1/49)*49` | Exactly `1` | `0.9999999999999999` |
| `(2^53 + 1) - 2^53` | Exactly `1` | `0` |
| `(10^20 + 0.125) - 10^20` | Exactly `0.125` | `0` |
| `1e-400` | Exact, positive, nonzero | `.NET Double.Parse` produces `0` |
| `sqrt(2^2000)` | Exact `2^1000`; display `1.07150860718626732094843e301` | Power overflows; its square root is positive infinity |

The last example has a finite mathematical answer within Double's magnitude range,
but the overflowing intermediate prevents this straightforward Double calculation
from reaching it. Numera retains the large integer and recognizes its exact square root.

For another stress test, try
`((2^2000+1)/(2^2000-1)-1)*(2^2000-1)`: Numera gives exactly `2`,
even though the fraction is too close to `1` for ordinary binary64 to retain
the difference.

**A fair control:** `(1/3)*3` displays `1` in ordinary Double arithmetic too.
Numera's advantage is that the intermediate `1/3` remains exact and can be
inspected as a fraction, not that every calculation has a visibly different answer.

## Errors and practical limits

| Try this | What happens |
| --- | --- |
| `1/0` | Division-by-zero diagnostic |
| `1/(1/0)` | The inner division-by-zero diagnostic is preserved |
| `sqrt(-1)` | Undefined in Numera's real-number domain |
| `log(0)` | Undefined logarithm diagnostic |
| `2.5 << 1` | Integer-required diagnostic |
| `10^5000` | Calculation-limit diagnostic under default settings |

Exact does not mean unlimited. Defaults bound input length to 4,096 UTF-16 code
units, nesting to 128, stored integer/fraction components to 16,384 bits,
arithmetic intermediates to 32,768 bits, and integer exponent magnitude to 10,000.
Specialized functions have additional limits; applications can lower the defaults.
Exceeding an exact calculation limit produces a diagnostic, not a silent Double fallback.
Ordinary floating-point functions can still produce non-finite results.

An application's rounded display is not a lossless save format. Numera also
provides lossless invariant value expressions and serialization; serialization
additionally preserves representation and exactness. Transfer text and a user's
input-length limit are separate concerns.
See [safety and limits](safety-and-limits.md) and [architecture](architecture.md).

## Verification and extending this collection

The accompanying `CalculationExamplesTests` check the exact-answer examples,
selected default displays, scientific reference values, session behavior,
angle/culture settings, error cases, and the .NET Double baseline.
Approximate reference checks are regression checks, not certified error bounds.
Random outputs and host-specific functions intentionally have no fixed expected answer.
