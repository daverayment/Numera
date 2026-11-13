# Potential Next Steps

These are optional post-v1 investigations, not requirements for the initial release.
Prioritize release readiness over expanding the numerical model.

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
  or the existing tolerance-based cardinal-angle checks.
- Consider the corresponding exact rational-angle path for degrees and gradians.
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
