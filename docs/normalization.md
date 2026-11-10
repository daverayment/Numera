# Expression Normalization

Desktop calculators, app launchers (e.g. PowerToys Run, Raycast, Alfred), search bars, and copied text often receive human-formatted mathematical expressions. These inputs frequently contain colloquial idioms such as:

- Leading or trailing equals signs (`= 2 + 2 =`)
- Typographic operators pasted from articles or Word documents (`×`, `÷`, `−`, `·`)
- Culture-specific decimal commas and digit grouping separators (`1.000,50` vs `1,000.50` vs `1'000.50` vs `1 000 000`)

Numera automatically normalizes human input into its parser's culture-invariant grammar. `Calculator.Evaluate` and `Calculator.TryEvaluate` use current culture at each call; an engine uses the culture captured when it was created. The standalone **`ExpressionNormalizer`** is available for applications that only need text cleanup.

Percentage idioms (`15% of 200`, `100 + 20%`), postfix factorials (`5!`) and implicit multiplication (`2pi`, `2(3+4)`) are **not** normalizer concerns: they are part of the Numera grammar and are handled by the parser. See [Percentages](#percentages) below.

### Why Normalization is Separate from Parsing

The normalizer only rewrites text that the grammar has no way to express, so it never changes meaning:

1. **Locale is not grammar.** A decimal comma, a Swiss apostrophe or a thousands space cannot be interpreted without knowing the user's culture, which the parser deliberately does not know.
2. **Pasted decoration is not grammar.** Launcher `=` signs and typographic operators (`×`, `−`) are artefacts of where the text came from.
3. **Source spans stay honest.** Diagnostics and AST source spans always refer to the original text you passed in, even when normalization rewrote or removed characters.

---

## Quick Example

```csharp
using Numera;
using System.Globalization;

// The static facade normalizes human input using current culture:
var result = Calculator.Evaluate("= 2 × 3! + 15% of 200 =");

// To select a particular culture, create an engine once and reuse it.
// Results retain that culture for display even if current culture changes.
var engine = new CalculatorEngine(CalculatorOptions.ForCulture(new CultureInfo("de-DE")));

if (engine.TryEvaluate("= 1.000,50 × (2 + 3)! + 15% of 200 =", out var value, out var diagnostic))
{
	Console.WriteLine(value.PrimaryDisplay);
}
else
{
	Console.WriteLine($"Error ({diagnostic.Code}): {diagnostic.Message}");
}
```

---

## Normalization Passes

When called, `ExpressionNormalizer` executes a configurable sequence of passes:

### 1. Launcher Equals Trimming
Strips leading and trailing equals characters without disturbing equality comparison operators:
- `= 2 + 2 =` $\to$ `2 + 2`
- `x == 10` $\to$ preserved as `x == 10`
- `x <= 5` $\to$ preserved as `x <= 5`

> [!NOTE]
> The trailing `=` is removed even from `x =`, so that evaluates as plain `x` rather than as an unfinished assignment. See [Variables and Context](variables.md).

### 2. Typographic & Unicode Sanitization
Converts copied Unicode mathematical symbols, dashes, and Fortran/Python power operators to standard ASCII equivalents:
- Multiplication: `×`, `•`, `·`, `∗` $\to$ `*`
- Division: `÷`, `∕`, `⁄` $\to$ `/`
- Minus & Dashes: Math minus `−`, en-dash `–`, em-dash `—`, horizontal bar `―` $\to$ `-`
- Exponentiation: `2 ** 3` $\to$ `2 ^ 3`

### 3. Division Colon
Rewrites a European ratio colon to `/` only when the left side is a number (decimal, scientific such as `1e5` or `1e-5`, or radix-prefixed such as `0xFF`) or a closing `)`, and the right side is an optionally signed number or starts with `(`. The sign must touch the number, so `6 : - 2` is left unchanged. Controlled by `ReplaceDivisionColon`.
- `6 : 2` $\to$ `6 / 2`, `16:9` $\to$ `16/9`, `log10(100):2` $\to$ `log10(100)/2`
- Identifiers are never guessed at, so launcher-style text is preserved: `status:200`, `pi:2`, `item1:200`. Parenthesise an identifier to opt in: `(pi):2` $\to$ `(pi)/2`.
- Colons inside square brackets are left alone. Brackets are reserved for future syntax and are an error in expressions today.

### 4. Culture & Digit Grouping Normalization
Disambiguates decimal points, digit separators, and argument delimiters based on `CalculatorOptions.Culture`:
- **Swiss & ISO Grouping:** Apostrophe thousands separators (`1'000'000.50`) and space or non-breaking space grouping (`1 000 000.50`) are stripped to standard format `1000000.50`.
- **European Cultures (Comma Decimal):** Period grouping (`1.000.000,50`) is stripped and the decimal comma is normalized to `1000000.50`. Semicolon argument separators inside parentheses (`max(1,5; 2,5)` or `round(2,5; 1)`) are normalized to standard commas (`max(1.5, 2.5)`). Semicolons inside square brackets (`[...]`) are intentionally preserved for future matrix row definitions.
- **US / Standard Culture:** Comma grouping (`1,000,000.50`) is stripped to `1000000.50`. Non-grouping commas inside functions (`max(1, 2)`) are preserved.

---

## Percentages

Percentages are part of the Numera grammar, so they are parsed from the original text and keep accurate source spans. The rules favour what a person expects from a calculator over strict programming-language semantics.

- **Postfix or modulo:** `%` is a percentage unless an operand (digit, identifier or `(`) follows it. `10 % 3` and `10%3` are modulo; `100 + 15 %`, `10% + 3` and `10% of 5` are percentages.
- **Standalone:** `B%` is `B / 100`, so `50%` is `0.5` and `200 * 15%` is `30`.
- **Markup and discount:** when the right operand of `+` or `-` is a percentage and the left is not, the result is `L × (1 ± r)`:
  - `200 + 15%` is `230`, `200 - 15%` is `170`.
  - The percentage applies to the whole left expression, so `50 * 2 + 10%` is `110` and `10 + 50 * 2 + 10%` is `121`.
  - Chains compound: `100 + 10% + 20%` is `132`.
  - Scaling, not magnitude, keeps negative bases and rates consistent: `-100 + 10%` is `-110`, `-100 - 10%` is `-90`, `100 + -10%` is `90`, `100 - -10%` is `110`.
- **Percent arithmetic:** when the left operand is itself a percentage, the arithmetic is plain: `10% + 20%` is `0.3` and `-10% + 20%` is `0.1`.
- **`of`:** `15% of 200` is `30`. `of` is a reserved operator, it is right-associative (`10% of 20% of 1000` is `20`), and only a percentage may appear on its left; `200 of 10%` is a syntax error.

### Known limitation

Whether an operand is a percentage is decided from the parsed shape, not by tracing units through arithmetic. In `10% * 2 + 5%` the left operand is a product, so it is marked up: `0.2 × 1.05 = 0.21`. This is deliberate: it keeps `200 * 15% + 10%` (a 10% tax on a fee) meaning `33`.

---

## Configuring Options

Normalization is configured through `CalculatorOptions`. `Culture` is shared by input parsing and result formatting, and `Normalization` holds the per-pass switches:

```csharp
var options = CalculatorOptions.ForCurrentCulture() with
{
	Normalization = new NormalizationOptions
	{
		ReplaceUnicodeOperators = true,
		ReplaceDivisionColon = true,
		TrimLauncherEquals = true
	}
};

var engine = new CalculatorEngine(options);
```

- `new CalculatorOptions()` and each access to `CalculatorOptions.Default` capture current culture with all passes enabled.
- `new CalculatorEngine()` and each access to `CalculatorEngine.Default` create an engine capturing current culture. `ForCurrentCulture()` also captures at call time. Existing options and engines do not follow later culture changes.
- `Calculator` resolves current culture per call; its results retain the culture used for evaluation.
- Explicitly select `CultureInfo.InvariantCulture` for machine-authored expressions, including `MachineReadable` output. Machine text and serialization never use the display culture.
- An engine is bound to one culture. Applications serving users with different cultures should keep one engine per culture.
- Set `Normalization = null` for raw mode, which passes text to the parser untouched. This suits serialized or machine-generated expressions.
- Angle units are never changed implicitly. Opt in explicitly with `with { AngleUnit = AngleUnit.Degrees }`.
- Variables and the previous result (`ans`) are per-call state and are passed in an `EvaluationContext`, not in the options.

Normalization itself never rejects a number; malformed ones such as `1.2.3,4` fail in the parser with a position in your original text.

`ExpressionNormalizer` remains public for callers that only want the text cleanup; it takes the culture as a parameter and reports failures as `EvaluationDiagnostic`.
