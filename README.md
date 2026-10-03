# Numera

A portable calculator expression engine targeting .NET 10.

## Evaluation

`Numera.Calculator.Evaluate(expression)` returns a `NumberValue`. Its `Type`
selects the meaningful property: `IntVal` (BigInteger), `DecVal` (decimal), or
`DblVal` (double). Results are not narrowed to decimal or formatted for display.
`AsDouble()` is an explicit, potentially lossy conversion.

`Calculator.TryEvaluate` returns a value and null diagnostic on success, or false
and an `EvaluationDiagnostic` on an expected expression failure. Invalid API
arguments still throw. Diagnostic codes support consumer localization; messages
are not localized. Syntax spans use UTF-16 offsets; arithmetic/limit failures
may have no source location.

## Syntax

- Invariant decimal point `.` and argument separator `,`; no digit grouping.
- Integer/decimal literals, `pi` / `π`, and `e`; case-insensitive functions.
- `+`, `-`, `*`, `/`, `%`, right-associative `^`, unary signs, `()` / `[]` grouping.
- Powers bind more tightly than unary minus: `-3^2` means `-(3^2)`.
- `==`, `~=`, `&&`, `||`; zero is false and nonzero is true.
- Skipped logical branches still validate syntax and function arity.
- `log` / `log10` are base 10; `ln` is natural; `logn(value,base)` selects a base.
- Trigonometric functions use radians. `rad`, `deg`, `grad` convert degrees to
  radians, radians to degrees, and radians to gradians respectively.
- `min` / `max` accept one or more operands and retain the selected value.
- `round` uses midpoint rounding away from zero; factorial takes nonnegative integers.
- `rand()` returns a double in [0,1); `randi(n)` returns an integer in [0,n).
- `Functions.TryGetSignature` exposes supported function arities.

Localized input, implicit multiplication, postfix factorial, scientific notation
and base-prefixed literals are not currently parser syntax. PowerToys implements
those transformations in its adapter. No variables/custom functions in this version.

## Numerical contract and limitations

Integer arithmetic is exact within safety limits. Integral integer quotients stay
integers; other division uses decimal when possible. Decimal has .NET's rounding
and range constraints: decimal storage does not prove mathematical exactness.
Overflowing mixed/decimal operations can fall back to double. Transcendental
functions and non-integer powers use double; accuracy beyond tested tolerances
is unverified. No arbitrary-precision floating point, rational arithmetic or
accuracy tracking is promised.

Integer/decimal comparison avoids narrowing. Comparison involving double uses
double conversion and may lose distinctions between large exact values.
NaN/infinity remain numeric results for consumers to handle. Extrema retain the
existing comparison ordering, including double.CompareTo's NaN ordering.
`sign(NaN)` returns zero for compatibility.

`EvaluationOptions` can tighten input length/nesting per call. `CalculationLimits`
lists hard integer-size, factorial and exponent ceilings. No timeout/cancellation
guarantee is provided.

## Development

Open `Numera.slnx` in Visual Studio, or run from this repository:

- `dotnet build tests/Numera.Tests/Numera.Tests.csproj`
- `dotnet vstest tests/Numera.Tests/bin/Debug/net10.0/Numera.Tests.dll`
- `dotnet pack src/Numera/Numera.csproj -c Release -o artifacts`

## License

Licensed under the MIT license. See [LICENSE.md](LICENSE.md) for more details.
