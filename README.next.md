<p align="center">
  <img src="assets/FxGear128x128.png" alt="Numera logo" width="128" height="128" />
</p>

# Numera

A calculator for **.NET 10** applications where people type expressions and expect useful answers.

Let's start with one calculation, then turn it into a small calculator app.


## "I just want to calculate something"

Install the Numera package via NuGet:

```bash
dotnet add package Numera
```

Now evaluate your first expression:

```csharp
using Numera;

var result = Calculator.Evaluate("2 + 2");
Console.WriteLine(result);
```

That's all you need. You don't have to configure a calculator or decide how to store its numbers.

Try replacing the expression:

| Try this | You should see |
| --- | --- |
| `2 + 3 * 4` | `14` |
| `(2 + 3) * 4` | `20` |
| `200 + 15%` | `230` |
| `sqrt(144)` | `12` |
| `5!` | `120` |

The result also works in a message: `$"Your answer is {result}"`. If you are writing a script or adding a calculation to an existing app, you can stop here.

## "I want to work with user input in my app"

People make typos, and an expression is often incomplete while they are typing. Instead of throwing an exception for each unsuccessful calculation, let's give them a chance to try again:

```csharp
while (true)
{
	Console.Write("> ");
	string? input = Console.ReadLine();
	if (string.IsNullOrWhiteSpace(input))
	{
		break;
	}

	if (Calculator.TryEvaluate(input, out var result, out var diagnostic))
	{
		Console.WriteLine(result);
	}
	else
	{
		Console.WriteLine($"Couldn't calculate: {diagnostic.Message}");
	}
}
```

Run it and enter a few expressions. A blank line finishes the session:

```text
> 2 + 2
4
> 200 + 15%
230
> 2 +
Couldn't calculate: Unexpected end of input.
```

The same `TryEvaluate` call fits into a textbox event handler. If you want to underline the problem or supply your own error messages, [diagnostics](https://github.com/daverayment/Numera/blob/main/docs/safety-and-limits.md#error-handling-patterns) give you more than the message shown here, including exactly where the error occurred.

## "I want to keep calculating with the previous answer"

Suppose someone is working out a restaurant bill. They want to remember the subtotal, calculate a tip, then add it to the bill without retyping either number.

Give your calculator some memory with an `EvaluationContext`:

```csharp
var context = EvaluationContext.Empty;

var result = Calculator.Evaluate("subtotal = 120", context);
Console.WriteLine(result); // 120
context = result.Context;

result = Calculator.Evaluate("15% of subtotal", context);
Console.WriteLine(result); // 18
context = result.Context;

result = Calculator.Evaluate("subtotal + ans", context);
Console.WriteLine(result); // 138
context = result.Context;
```

`subtotal` remembers the named value; `ans` means the previous answer, which was `18`. Try starting with `subtotal = 200` instead: the next two answers become `30` and `230`.

The important step is saving `result.Context` after each successful calculation. To give the input loop above the same memory, pass `context` as the last argument to `TryEvaluate` and update it only on success. Keep a separate context for each user's calculator.

You can keep using `Calculator` for these kinds of application use cases. The [variables guide](https://github.com/daverayment/Numera/blob/main/docs/variables.md) goes further into named values and sessions. You only need to create a custom `CalculatorEngine` when you want to go beyond the defaults.

## "My app needs different calculator defaults"

Let's say you're building a school calculator. You want angles in degrees, and six significant digits are enough for its normal display.

Create a calculator with those defaults and reuse it:

```csharp
var calculator = new CalculatorEngine(new CalculatorOptions
{
	AngleUnit = AngleUnit.Degrees,
	ResultFormat = new ResultFormatOptions
	{
		SignificantDigits = 6
	}
});

Console.WriteLine(calculator.Evaluate("sin(90)")); // 1
Console.WriteLine(calculator.Evaluate("1/3"));     // 0.333333
```

Try `cos(180)` and `tan(45)` too: you should see `-1` and `1`. Without the customization to use degrees, angles are interpreted in radians.

These examples show a decimal point. Your calculator follows the current culture, so you might see `0,333333` instead. Enter decimals using that same convention. For example, with German culture, try `1,5 + 2,5` to get `4,0`.

An engine keeps the culture it was created with; its results keep their display settings too. If your app needs to select a particular user's culture explicitly, the [culture guide](https://github.com/daverayment/Numera/blob/main/docs/normalization.md#quick-example) shows how.

Use `result.FormatResult()` to retain those settings, especially for saved history.
`calculator.FormatResult(result)` instead selects that engine's policy, while
`Calculator.FormatResult(result)` uses library display defaults and current culture at
the formatting call. Both engine and facade also accept raw `MathValue` values.

You can pass the same contexts to `calculator.Evaluate` and `calculator.TryEvaluate` that you used with `Calculator`. Changing settings doesn't require a different way of managing sessions.

If you need a second configuration, `calculator.WithAngleUnit(...)` and `calculator.WithOptions(...)` return new engines without changing this one. You can also base reusable options on other options using record `with` expressions. The [configuration guide](https://github.com/daverayment/Numera/blob/main/docs/architecture.md#immutable-configuration) explains both workflows and how complete replacement differs from changing selected settings.

## "I'd like a button that shows the exact fraction"

Six digits make a third easier to display, but sometimes the person using your app wants to see `1/3` instead. Let's add a toggle without changing the calculation.

Using the configured `calculator` from above:

```csharp
var result = calculator.Evaluate("1/3");
var formatted = result.FormatResult();
bool showFraction = true;
string? display = showFraction
	? formatted.FractionRepresentation
	: formatted.PrimaryDisplay;

if (display is not null)
{
	Console.WriteLine(display);
}
else
{
	Console.WriteLine("An exact fraction is not available.");
}
```

With `showFraction` set to `true`, this prints `1/3`. Set it to `false` and you'll see `0.333333`. In a UI, enable the fraction toggle when `formatted.FractionRepresentation` is present. `result.HasExactFraction` also tells you whether an exact fraction is available.

The primary display and machine-readable text are always present. Alternatives are null when unavailable, such as hexadecimal for a third. All text is prepared before `FormatResult()` returns, so switching views does not trigger more formatting or exceptions. Long representations remain available: your app can check `.Length` before showing them. Significant digits are not a character budget; the [sizing guide](https://github.com/daverayment/Numera/blob/main/docs/architecture.md#representation-lengths) explains the extra space for signs, separators, exponents, and leading zeros.

Try a few different answers:

| Expression | Numeric view | Exact fraction available? |
| --- | --- | --- |
| `1/3` | `0.333333` | Yes: `1/3` |
| `1/8` | `0.125` | Yes: `1/8` |
| `sqrt(2)` | `1.41421` | No: this is a floating-point approximation |

For `1/3`, `result.Exactness` is `Exact` and only the decimal display is rounded, so your UI can explain that the exact answer is available behind the fraction button. In contrast, `ln(1)` displays `0` without rounding but remains `Unverified`: a clean-looking answer does not establish exactness. `formatted.IsDisplayRounded` describes the presentation, not the calculation. [The exactness guide](https://github.com/daverayment/Numera/blob/main/docs/exactness.md) shows how to label these cases and use the status to understand a value's calculation path, without mistaking it for an error estimate or full history.

The shortened display hasn't replaced the stored answer. Still using the same `result`:

```csharp
Console.WriteLine(result * 3); // 1, not 0.999999
```

Use the result itself for further calculations, rather than feeding its display text back into the calculator.

## Where would you like to go next?

You don't need to learn the rest of the API to use the examples above. When your app needs more:

- **"I need a number for another .NET API."** Both results and values offer `ToDouble()`, `ToDecimal()`, `ToBigInteger()`, and explicit numeric casts; `AsDouble()` remains an alias. [Conversion contracts](https://github.com/daverayment/Numera/blob/main/docs/architecture.md#inspection--conversion) explain rounding, range limits, and why integer conversions never truncate.
- **"I need to compare answers or history content."** Result equality is numeric; use `EvaluationResultComparers.Content` for metadata-aware comparison. [Result comparison](https://github.com/daverayment/Numera/blob/main/docs/architecture.md#result-equality-and-content-comparison) explains the contract.
- **"I want my own functions, such as a tax or unit conversion."** [Register a custom function](https://github.com/daverayment/Numera/blob/main/docs/custom-functions.md) implemented in your application code.
- **"I need to copy or save an answer without display rounding."** [Machine-readable expressions and serialization](https://github.com/daverayment/Numera/blob/main/docs/architecture.md#display-vs-machine-readable-text-and-culture) serve different purposes from the user-facing display.
- **"I want to show the formula, not just its answer."** [Expression rendering](https://github.com/daverayment/Numera/blob/main/docs/architecture.md#on-demand-abstract-syntax-tree-ast--formatting) can produce typography or LaTeX.
- **"What else can people type?"** Browse the [calculation examples](docs/examples.md) or the [syntax and functions reference](https://github.com/daverayment/Numera/blob/main/docs/syntax.md).

Before exposing a calculator to untrusted input, review the [limits and diagnostics guide](https://github.com/daverayment/Numera/blob/main/docs/safety-and-limits.md). Numera's exact arithmetic is bounded, and its limits do not impose execution-time guarantees on your custom functions.

## License

[MIT](LICENSE).
