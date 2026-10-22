# Expression Normalization

Desktop calculators, app launchers (e.g. PowerToys Run, Raycast, Alfred), search bars, and copied text often receive human-formatted mathematical expressions. These inputs frequently contain colloquial idioms such as:

- Leading or trailing equals signs (`= 2 + 2 =`)
- Typographic operators pasted from articles or Word documents (`×`, `÷`, `−`, `·`)
- Culture-specific decimal commas and digit grouping separators (`1.000,50` vs `1,000.50` vs `1'000.50` vs `1 000 000`)

Numera's evaluator (`Calculator.Evaluate` and `Calculator.TryEvaluate`) requires a culture-invariant grammar with standard ASCII operators. The standalone **`ExpressionNormalizer`** bridges this gap, allowing applications to clean human expressions into that grammar before parsing.

Percentage idioms (`15% of 200`, `100 + 20%`), postfix factorials (`5!`) and implicit multiplication (`2pi`, `2(3+4)`) are **not** normalizer concerns: they are part of the Numera grammar and are handled by the parser. See [Percentages](#percentages) below.

### Why Normalization is Separate from Parsing

The normalizer only rewrites text that the grammar has no way to express, so it never changes meaning:

1. **Locale is not grammar.** A decimal comma, a Swiss apostrophe or a thousands space cannot be interpreted without knowing the user's culture, which the parser deliberately does not know.
2. **Pasted decoration is not grammar.** Launcher `=` signs and typographic operators (`×`, `−`) are artefacts of where the text came from.
3. **Source spans stay honest.** Everything that carries mathematical meaning (percentages, factorials, implicit products) is parsed directly from the original text, so diagnostics and AST source spans never point into a rewritten string.

---

## Quick Example

```csharp
using Numera;
using System.Globalization;

// Configure options (or use NormalizationOptions.Default)
var options = new NormalizationOptions
{
	// Enable culture-specific separator disambiguation
	Culture = new CultureInfo("de-DE") 
};

string rawInput = "= 1.000,50 × (2 + 3)! + 15% of 200 =";

// 1. Clean the human input into culture-invariant Numera text
if (ExpressionNormalizer.TryNormalize(rawInput, out var normalized, out var errorMessage, options))
{
	Console.WriteLine($"Original:   {rawInput}");
	Console.WriteLine($"Normalized: {normalized}");
	// Prints: 1000.50 * (2 + 3)! + 15% of 200

	// 2. Evaluate. The parser handles the factorial and percentage syntax
	var result = Calculator.Evaluate(normalized);
	Console.WriteLine($"Result:     {result}");
}
else
{
	Console.WriteLine($"Normalization error: {errorMessage}");
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

### 2. Typographic & Unicode Sanitization
Converts copied Unicode mathematical symbols, dashes, and Fortran/Python power operators to standard ASCII equivalents:
- Multiplication: `×`, `•`, `·`, `∗` $\to$ `*`
- Division: `÷`, `∕`, `⁄` $\to$ `/`
- Division Colon: Flanked European ratio colon `6 : 2` $\to$ `6 / 2`
- Minus & Dashes: Math minus `−`, en-dash `–`, em-dash `—`, horizontal bar `―` $\to$ `-`
- Exponentiation: `2 ** 3` $\to$ `2 ^ 3`

### 3. Culture & Digit Grouping Normalization
Disambiguates decimal points, digit separators, and argument delimiters based on the specified `CultureInfo`:
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

Each normalization pass can be enabled or disabled via `NormalizationOptions`:

```csharp
var options = new NormalizationOptions
{
	Culture = CultureInfo.CurrentCulture,
	ReplaceUnicodeOperators = true,
	TrimLauncherEquals = true
};
```

Using `NormalizationOptions.Default` enables all passes with invariant culture.
