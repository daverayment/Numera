<p align="center">
  <img src="assets/FxGear128x128.png" alt="Numera logo" width="128" height="128" />
</p>

<h1 align="center">Numera</h1>

Numera is a calculator library designed for easy integration into apps. It accepts familiar mathematical input, starts with practical human-first defaults, and lets you adapt its behaviour and presentation to your application. When an input is incomplete or cannot be evaluated, diagnostics help pinpoint the issue and explain what needs attention.

## Why Numera?
The highlights:
- Exact integer, decimal, and fractional arithmetic where supported, with floating-point approximations for other calculations.
- Human-friendly, culture-aware input parsing.
- Helpful diagnostics for user input, and numeric or exact-fraction displays for answers.
- More than 60 built-in functions, from min/max to statistics and numeric analysis, plus your own custom functions.
- Start with the static `Calculator` class; create a reusable `CalculatorEngine` when your app needs different defaults.
- Named variables and a previous answer for calculations that build on each other.

# Quickstart
Let's start with one calculation, then turn it into a small calculator app.

First, install Numera in your .NET project via NuGet:

```bash
dotnet add package Numera
```

## 1. "I just want to calculate something"
Evaluate your first expression:

```csharp
using Numera;

var result = Calculator.Evaluate("2 + 2");
Console.WriteLine(result);
```

Try swapping in one of these:

Expression | Answer
--- | ---
`0.1 + 0.2` | `0.3`
`2 + 3 * 4` | `14`
`(2 + 3) * 4` | `20`
`200 + 15%` | `230`
`sqrt(144)` | `12`
`5!` | `120`
`6 (1/3)` | `2`

Numera preserves exact integer, decimal, and fractional arithmetic where supported, so `0.1 + 0.2` gives exactly `0.3`. Some functions, such as `sqrt(2)`, produce floating-point approximations instead.

For scripts and one-off calculations, there's nothing else to set up.

## 2. "I want to work with user input in my app"

User input can contain typos, and an expression is often incomplete while being composed. Instead of throwing an exception for each unsuccessful calculation, let's give users a chance to try again in this next example:

```csharp
using Numera;

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
> [!NOTE]
> Each main quickstart example can replace `Program.cs` in a console app. Shorter snippets are labelled as adaptations or continuations of an earlier example.

The key here is the `TryEvaluate` call; it returns a value that indicates whether the evaluation succeeded or not. Run the example and enter a few expressions. A blank line ends the session:

```text
> 2 + 2
4
> 200 + 15%
230
> 2 +
Couldn't calculate: Unexpected end of input.
```

The same `TryEvaluate` call suits a textbox event handler well. When it fails, `diagnostic.IsIncomplete` helps you avoid showing an error while the user is still composing an expression. The [diagnostics guide](https://github.com/daverayment/Numera/blob/main/docs/safety-and-limits.md#error-handling-patterns) also shows how to underline where a problem is in the expression or to supply your own messages.

## 3. "I want to keep calculating with the previous answer"

Suppose you're working out a restaurant bill. Give your calculator a context so it can remember named variables and the last answer at each step:

```csharp
using Numera;

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

In the final expression here, `subtotal` remembers the named value, and `ans` means the previous answer, which was `18`. Try starting with `subtotal = 200` instead: the next two answers become `30` and `230`.

The important step is saving `result.Context` after each successful calculation. To adapt the input loop in [the second example](#2-i-want-to-work-with-user-input-in-my-app), declare `var context = EvaluationContext.Empty;` before the loop, then replace its `if`/`else` block with this:

```csharp
if (Calculator.TryEvaluate(input, out var result, out var diagnostic, context))
{
    Console.WriteLine(result);
    context = result.Context;
}
else
{
    Console.WriteLine($"Couldn't calculate: {diagnostic.Message}");
}
```

An unsuccessful calculation leaves your saved context unchanged. Keep a separate context for each user's calculator.

You can keep using `Calculator` for these kinds of application use cases. The [variables guide](https://github.com/daverayment/Numera/blob/main/docs/variables.md) further describes named values and sessions. You only need to create a custom `CalculatorEngine` when you want to go beyond the defaults.

## 4. "My app needs different calculator defaults"

Let's say you're building a classroom calculator. You want angles in degrees instead of the radians default, and six significant digits are enough for its normal display.

We can create a reusable calculator with those options like this:

```csharp
using Numera;

var options = new CalculatorOptions
{
	AngleUnit = AngleUnit.Degrees,
	ResultFormat = new ResultFormatOptions
	{
		SignificantDigits = 6
	}
};
var calculator = new CalculatorEngine(options);

Console.WriteLine(calculator.Evaluate("sin(90)")); // 1
Console.WriteLine(calculator.Evaluate("1/3"));     // 0.333333
```

Reuse the calculator for later calculations. It keeps the options and culture it was created with, and its results retain those display settings. On a system with a comma decimal separator, the last answer displays as `0,333333` instead.

For an explicit culture choice, see the [culture guide](https://github.com/daverayment/Numera/blob/main/docs/normalization.md#quick-example). The [configuration guide](https://github.com/daverayment/Numera/blob/main/docs/architecture.md#immutable-configuration) covers creating another configuration without changing this calculator.

## 5. "I'd like a button that shows the exact fraction"

Sometimes the person using your app may want to see `1/3` instead of `0.333333`. Let's add a toggle for that without changing the calculation.

We'll use the same configuration option to choose six significant digits for the normal display again:

```csharp
using Numera;

var options = new CalculatorOptions
{
    ResultFormat = new ResultFormatOptions
    {
        SignificantDigits = 6
    }
};
var calculator = new CalculatorEngine(options);

var result = calculator.Evaluate("1/3");
var formatted = result.FormatResult();
bool showFraction = true;

if (showFraction && formatted.FractionRepresentation is not null)
{
    Console.WriteLine(formatted.FractionRepresentation);
}
else
{
    Console.WriteLine(formatted.PrimaryDisplay);
}
```

With `showFraction` set to `true`, this prints `1/3`. Set it to `false` and you'll see the decimal representation. In a UI, enable the fraction toggle when `formatted.FractionRepresentation` is present. `result.HasExactFraction` also tells you whether an exact fraction is available.

Try a few different answers:

Expression | Numeric view | Exact fraction available?
--- | --- | ---
`1/3` | `0.333333` | Yes: `1/3`
`1/8` | `0.125` | Yes: `1/8`
`sqrt(2)` | `1.41421` | No: this is a floating-point approximation

For `1/3`, the calculation is exact; only its numeric display is rounded. `formatted.IsDisplayRounded` describes that display rounding, while `result.Exactness` describes calculation exactness. An unrounded display alone does not establish exactness. See the [exactness guide](https://github.com/daverayment/Numera/blob/main/docs/exactness.md) for UI labelling examples.

The shortened display hasn't replaced the stored answer. Still using the same `result`:

```csharp
Console.WriteLine(result * 3); // 1, not 0.999999
```

Use the result itself for further calculations, rather than feeding its display text back into the calculator.

## Optional recipe: "I want my own functions"

Your app can expose its own calculations alongside the built-in functions. For example, let people convert kilometres to miles with `miles(...)`:

```csharp
using Numera;

var options = new CalculatorOptions
{
    CustomFunctions =
    [
        CustomFunction.Create("miles", (MathValue kilometres) => kilometres / 1.609344m)
    ]
};
var calculator = new CalculatorEngine(options);

Console.WriteLine(calculator.Evaluate("miles(5)"));
```

The callback receives calculated values and returns a value; you don't need to work with the expression tree. The [custom-functions guide](https://github.com/daverayment/Numera/blob/main/docs/custom-functions.md) covers functions with more arguments and how Numera handles callback errors.

## Where would you like to go next?

You don't need to learn the rest of the API to use the examples above. When your app needs more:

- **"I need a number for another .NET API."** Use `ToDouble()`, `ToDecimal()`, or `ToBigInteger()`. [Conversion contracts](https://github.com/daverayment/Numera/blob/main/docs/architecture.md#inspection--conversion) explain rounding and range limits.
- **"I need to compare answers or history content."** Result equality is numeric; use `EvaluationResultComparers.Content` for metadata-aware comparison. [Result comparison](https://github.com/daverayment/Numera/blob/main/docs/architecture.md#result-equality-and-content-comparison) explains the contract.
- **"I want function suggestions as people type."** Use `calculator.SearchFunctions(...)` to find built-in and custom functions, with metadata for an autocomplete menu. See [discovery and autocomplete](https://github.com/daverayment/Numera/blob/main/docs/custom-functions.md#discovery--autocomplete).
- **"I need to copy or save an answer without display rounding."** [Machine-readable expressions and serialization](https://github.com/daverayment/Numera/blob/main/docs/architecture.md#display-vs-machine-readable-text-and-culture) serve different purposes from the user-facing display.
- **"I want to show the formula, not just its answer."** [Expression rendering](https://github.com/daverayment/Numera/blob/main/docs/architecture.md#on-demand-abstract-syntax-tree-ast--formatting) can produce typography or LaTeX / MathJAX.
- **"I need other answer formats or have limited display space."** The [presentation guide](https://github.com/daverayment/Numera/blob/main/docs/architecture.md#representation-lengths) covers exact-fraction and radix alternatives, prepared formatting snapshots, and representation lengths.
- **"What else can people type?"** Browse the [calculation examples](docs/examples.md) or the [syntax and functions reference](https://github.com/daverayment/Numera/blob/main/docs/syntax.md).

Before exposing a calculator to untrusted input, review the [limits and diagnostics guide](https://github.com/daverayment/Numera/blob/main/docs/safety-and-limits.md). Numera's exact arithmetic is bounded, and its limits do not impose execution-time guarantees on your custom functions.

## Building from Source

With the .NET 10 or later SDK installed, run these commands from the repository root:

```
dotnet build
dotnet test
```

## License

[MIT](LICENSE).
