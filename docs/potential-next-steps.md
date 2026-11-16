# Potential Next Steps

These are optional post-v1 investigations, not requirements for the initial release.
Prioritize release readiness over expanding the numerical model.

## MathValue Default Formatting

Review the boundary between representation-native `MathValue.ToString()` and
calculator presentation. Integer, Decimal, and Double use native .NET formatting;
Rational general formatting uses the result formatter's default display policy.
Default native formatting does not consistently apply decimal display-scale metadata.

Consider whether default formatting should be uniform across representations while
keeping explicit numeric format strings representation-native. Account for character
and UTF-8 span formatting and interpolation. `EvaluationResult.ToString()` and
`FormatResult()` provide calculator presentation independently of this decision.

## Reproducible Random Evaluation

Consider whether consumers need a configurable random source or seed for `rand()`
and `randi()`, beyond deterministic testing within the library.

Before adding public configuration, define sequence ownership and lifetime: whether
successive evaluations share a sequence, how independent sessions remain isolated,
and what reproducibility means when evaluations run concurrently. Account for the
mutable state and thread-safety requirements of caller-supplied random sources
without weakening the shared-engine and immutable-configuration contracts.

Retain `Random.Shared` as the default. Parsing, formatting, and discovery must not
consume random values. If testing is the only requirement, consider an internal
execution-scoped injection point rather than expanding the public API.

## Double Result Presentation

Review whether default calculator presentation of Double values should use a
15-significant-digit ceiling or shortest-round-trip text instead of expanding the
stored binary value to the general result precision. Define whether explicitly
requesting more digits exposes the stored expansion or selects another policy.
Preserve round-trip machine-readable output, independent precision for exact
Integer/Decimal/Rational values, and Unverified indicators regardless of appearance.
Display rounding is not a guarantee of calculation accuracy.

## Inverse Trigonometric Special Values

Consider recognizing rational inverse-trigonometric inputs with proven rational
degree/gradian outputs, such as `asin(1/2) = 30` degrees. Define principal branches,
domain boundaries, and exactness propagation consistently across aliases and inverse
reciprocal functions. Do not infer special values from proximity or rounded Double
inputs. General correctly rounded transcendental evaluation and symbolic radicals
remain separate numerical-model investigations.

## Exact Rational Multiples of Pi

Investigate a bounded value representation for `q * pi`, where `q` is a reduced
rational coefficient. Preserve the symbolic value through arithmetic, variables,
and `ans`, rather than retaining input ASTs or replaying expressions. This is not a
proposal for a general computer algebra system.

Potential benefits:

- `sin(pi/6)` and `cos(pi/3)` can produce Exact Rational `1/2`.
- `tan(pi/4)` can produce Exact Integer `1`.
- `(pi/6)/pi` can produce Exact Rational `1/6`.
- Exact rational angle reduction can preserve small offsets across large revolutions.
- A saved `pi/6` can retain its symbolic meaning for subsequent calculations.

Keep the initial domain narrow:

- Support addition/subtraction of pi-multiples and multiplication/division by rationals.
- Divide nonzero pi-multiples to obtain an ordinary rational.
- Normalize a zero coefficient to ordinary zero.
- Treat operations outside the domain, such as `pi + 1` or `pi * pi`, as explicit
  approximation boundaries producing Unverified results.
- Recognize special-angle identities only from proven values, never numeric proximity
	or tolerance-based cardinal-angle checks.
- Leave irrational outputs such as `sin(pi/4)` approximate; exact radicals, general
  identity rewriting, and expression provenance/replay are out of scope.

Design work before implementation:

- Integrate with MathValue's existing rational-component storage and GCD reduction;
  a conceptual `PiMultiple(Rational coefficient)` need not introduce a separate
  public Rational API.
- Separate mathematical exactness from exact-fraction availability: symbolic pi is
  exact but has no exact fractional representation.
- Define arithmetic with Unverified inputs, scalar conversion, configured limits,
  equality, ordering, and hashing without equating pi to a rounded approximation.
- Keep familiar numeric display as an appropriately labeled approximation, with an
  optional exact symbolic view.
- Preserve the representation in JSON, captured values, and application history.
- Keep constants, aliases (including tau), evaluator behavior, and public inspection
  APIs consistent.
- Preserve domain errors, argument evaluation, and callback execution counts.

This is a cross-cutting numeric-model extension, not a prerequisite for v1.

### Current Trigonometric Behavior and Motivation

Radian functions evaluate the numeric argument without proximity-based snapping.
The current `pi` constant and angle-conversion helpers do not retain symbolic pi
provenance: `sin(pi)` can have a small nonzero residual, and `tan(pi/2)` or
`sec(rad(90))` can produce large finite values rather than pole diagnostics.
Likewise, `floor(tan(pi/4))` operates on the approximation, not the exact identity.
These results remain Unverified.

Direct degree/gradian functions recognize rational special values before numeric
projection and reduce rational quadrant residuals before approximation. Irrational
results still cross a Double approximation boundary.

A future rational-multiple-of-pi representation could support exact radian
identities and exact pole detection, including through variables and `ans`.
It must distinguish symbolic `pi/2` from a nearby numeric approximation, not
use proximity-based snapping.
