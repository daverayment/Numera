# Exactness: what should my app tell the user?

An answer can be exact even when its decimal display is rounded. An answer can also
look perfectly tidy without Numera having established that it is exact.

`MathValue.Exactness` and `EvaluationResult.Exactness` help your application tell these
cases apart. Their type is `CalculationExactness`:

- **Exact:** the finite value is known, or asserted by the caller, to be exact relative
  to the supplied inputs. It may still need a rounded display.
- **Unverified:** exactness has not been established. This does not mean the answer is
  wrong, or quantify how far it might be from the mathematical answer.

The examples below use an invariant-culture engine so their input conventions are
consistent:

```csharp
using System.Globalization;
using Numera;

var calculator = CalculatorEngine.ForCulture(CultureInfo.InvariantCulture);
```

## "I want a useful indicator beside the answer"

Use exactness to explain the calculation, and `IsDisplayRounded` to explain its current
presentation. They answer different questions:

| Calculation | Exactness | Display rounded? | What the UI can explain |
| --- | --- | --- | --- |
| `1/3` | Exact | Yes | The decimal is rounded; the exact answer is available as `1/3`. |
| `1/8` | Exact | No | The displayed `0.125` expresses the exact answer. |
| `sqrt(4)` | Exact | No | Numera recognized an exact root. |
| `ln(1)` | Unverified | No | The floating-point path displays `0`; exactness was not established. |
| `floor(pi)` | Unverified | No | A whole-number answer does not erase the approximate input. |
| `sqrt(2)` | Unverified | Yes | The calculation is approximate, and the display also rounds its stored value. |

For example, a calculator UI could distinguish these cases rather than using one
ambiguous "approximate" indicator:

```csharp
var result = calculator.Evaluate("1/3");
var formatted = result.FormatResult();

if (!result.IsFinite)
{
	Console.WriteLine("The result is not a finite number.");
}
else if (result.Exactness == CalculationExactness.Unverified)
{
	Console.WriteLine($"{formatted.PrimaryDisplay} (exactness not established)");
}
else if (formatted.IsDisplayRounded)
{
	Console.WriteLine($"{formatted.PrimaryDisplay} (rounded display; exact fraction available)");
}
else
{
	Console.WriteLine(formatted.PrimaryDisplay);
}
```

For a fraction button, use `result.HasExactFraction` or the presence of
`formatted.FractionRepresentation`. For `1/3` the button can show `1/3`; for `ln(1)`
it is unavailable, even though its decimal display needs no rounding.

**Do not treat `IsDisplayRounded == false`, an Integer storage type, or a short answer
as a guarantee of calculation exactness.** Increasing display precision does not improve
an approximate calculation, and Unverified does not tell you how many digits are reliable.

## "I want to understand the path this value has taken"

Exactness is a conservative signal that travels with captured values, variables, `ans`,
and result arithmetic. Exact operations on established-exact inputs retain Exact.
An Unverified input keeps reached arithmetic and function results Unverified, even when
the final number looks exact:

```csharp
var first = calculator.Evaluate("x=pi");
var whole = calculator.Evaluate("floor(x)", first.Context);
var zero = calculator.Evaluate("ans-ans", whole.Context);

Console.WriteLine(whole.Exactness); // Unverified, despite displaying 3
Console.WriteLine(zero.Exactness);  // Unverified, despite displaying 0
```

This helps explain why an answer does not qualify for an exactness claim. It is not a
complete provenance trail: the status does not identify which input or function caused
it, count approximate operations, or estimate accumulated error. For expression and
dependency inspection, use the result's context and variable metadata. A complete audit
trail needs application-owned records; the flag does not reconstruct one.

Propagation concerns evaluated inputs, not merely text that appears in an expression.
A short-circuited branch that never runs does not introduce its own uncertainty.
The library does not symbolically prove that uncertainty cancels: `pi-pi` remains
Unverified even though the mathematical answer is zero.

## "My app supplies numbers or custom functions"

Constructing an Integer, Decimal, or fraction asserts that the supplied value is exact.
It does not certify calculations performed before that value reached Numera:

```csharp
var decimalTenth = new MathValue(0.1m); // Exact: supplied decimal 1/10
var binaryTenth = new MathValue(0.1);   // Unverified: not exactly 1/10
var binaryHalf = new MathValue(0.5);    // Unverified, though 1/2 is exactly representable

var imported = new MathValue((decimal)Math.Sqrt(2));
// Exact describes this supplied decimal, not the earlier square-root calculation.
var labelled = imported.WithExactness(CalculationExactness.Unverified);
```

Use `WithExactness(Unverified)` when an imported value or custom-function result is an
approximation. `WithExactness(Exact)` is a **trusted caller assertion**, not a verification
operation. It neither re-evaluates an expression nor recovers lost digits. For example,
marking `binaryTenth` Exact exposes `3602879701896397/36028797018963968`, its stored
binary fraction, **not** `1/10`.

NaN and positive/negative infinity remain Unverified. Attempting to mark them Exact
throws `ArgumentException`; serialized data claiming they are Exact is rejected with
`FormatException`. Their exactness is not defined by the finite-value contract.

Callbacks receive evaluated values. An Unverified argument conservatively keeps the
function result Unverified even if the callback returns a newly constructed exact
Integer. See [custom functions](custom-functions.md) for registration and evaluation rules.

## "I want to keep this information in history or transfer it"

Keep the `EvaluationResult` or `MathValue` when continuing calculations; keep a
`FormattedCalculation` when caching its prepared presentations. Its `Value.Exactness`
retains the same status, while `IsDisplayRounded` describes that particular display.

`MathValue.Serialize()` / `Deserialize()` preserve the `Exactness` field alongside the
value and storage metadata. Machine-readable expressions preserve the stored number,
not the history behind it: re-evaluating the text of an Unverified `1/3` constructs an
Exact fraction. Use serialization rather than re-evaluation when that distinction matters.

The pre-v1 rename from Accuracy to Exactness also changes the serialized field name;
old payloads are not a supported compatibility format. The flag is trusted metadata,
not a tamper-proof certificate when crossing an application trust boundary.