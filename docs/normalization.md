# Expression Normalization

Desktop calculators, app launchers (e.g. PowerToys Run, Raycast, Alfred), search bars, and copied text often receive human-formatted mathematical expressions. These inputs frequently contain colloquial idioms such as:

- Leading or trailing equals signs (`= 2 + 2 =`)
- Typographic operators pasted from articles or Word documents (`×`, `÷`, `−`, `·`)
- Culture-specific decimal commas and digit grouping separators (`1.000,50` vs `1,000.50` vs `1'000.50` vs `1 000 000`)

Numera automatically normalizes human input into its parser's culture-invariant grammar. `Calculator.Evaluate` and `Calculator.TryEvaluate` use current culture at each call; an engine uses the culture captured when it was created. The standalone **`ExpressionNormalizer`** is available for applications that only need text cleanup.

Percentage idioms (`15% of 200`, `100 + 20%`), postfix factorials (`5!`) and implicit multiplication (`2pi`, `2(3+4)`) are **not** normalizer concerns: they are part of the Numera grammar and are handled by the parser. See [Percentages](#percentages) below.

### Why Normalization is Separate from Parsing

The normalizer accepts one configured localized syntax and rewrites it into invariant grammar. It does not detect cultures or try alternative interpretations:

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
- `x = 5 =` $\to$ `x = 5` (completed assignment with launcher decoration)
- `x =` $\to$ preserved as `x =` (incomplete assignment)
- `x == 10` $\to$ preserved as `x == 10`
- `x <= 5` $\to$ preserved as `x <= 5`

> [!NOTE]
> An equals after a lone identifier is an assignment operator, not launcher decoration.
> `x =` reports `InvalidSyntax` at that operator whether or not `x` already exists.
> Other eligible trailing equals are removed once, and the parser checks the remaining
> syntax: `x = 5 + =` still fails because its value expression is incomplete. Disabling
> `TrimLauncherEquals` (or using raw mode) disables decoration removal, not assignment
> validation. See [Variables and Context](variables.md).

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
Uses `NumberDecimalSeparator`, `NumberGroupSeparator`, `NumberGroupSizes`, and `TextInfo.ListSeparator` from `CalculatorOptions.Culture`. A null culture passed directly to `ExpressionNormalizer` means invariant culture, not current culture.

#### Decimal and grouping rules

- Only the configured decimal separator is accepted. German `1,25` and leading `,5` become `1.25` and `.5`; invariant-looking decimal points are not a fallback.
- Grouping is validated across the complete integer part before any separators are removed, working right-to-left through the culture's group sizes. This includes Indian grouping (`12,34,567`) and custom group sizes ending in zero (no further separators).
- A grouped integer cannot have leading zeroes. Thus German `1.234` means `1234`, but `0.123` is rejected rather than silently becoming `123`. Use `0,123` for that decimal value.
- If culture data uses the same decimal and grouping separator, numbers that admit both interpretations are rejected too; Numera does not silently prefer decimals. Omit grouping or use raw invariant input to avoid such collisions.
- Straight/curly apostrophes and ordinary, non-breaking, narrow non-breaking, and thin spaces remain portable grouping forms. They must obey the configured group sizes. Do not mix native punctuation, apostrophe, and space grouping families within one number.
- Grouping is not allowed in fractional parts, exponents, or radix literals. For example, `1.2.3`, `1 23 456` in en-US, and `0x10 000` are invalid. Numeric literals use ASCII digits `0` through `9`; identifier digits are not normalized as numbers.

#### Argument separators and ambiguity

The culture's list separator separates function arguments. **Semicolon is also an explicit alternative in every culture.** A direct semicolon selects semicolon style for that entire call, including numbers before it. Other argument separators cannot be mixed into that call; commas may still occur as valid decimal or grouping punctuation.

In a comma-separated call, a comma that could also form valid digit grouping is rejected as ambiguous. Numera does not use function arity, later malformed groups, or evaluation results to guess what was intended. To resolve this, use semicolons, remove digit grouping, or put a space after an argument comma so it cannot group digits.

| Culture | Input | Interpretation |
| :--- | :--- | :--- |
| en-US | `pow(2,100)` | Ambiguous: use `pow(2;100)` or `pow(2, 100)` |
| en-US | `max(1,234,567)` | Ambiguous: grouping or arguments |
| en-US | `max(1,234,56)` | Still ambiguous; the later group does not settle the earlier comma |
| en-US | `max(1,000+2,000,3,000+9,000)` | Ambiguous |
| en-US | `max(1,000+2,000;3,000+9,000)` | Two arguments; result `12000` |
| en-US | `max(1000+2000,3000+9000)` | Two ungrouped arguments; result `12000` |
| en-US | `max((1,234), 567)` | Grouped literal inside ordinary parentheses; result `1234` |
| de-DE | `max(1,234)` | One decimal argument: `1.234` |
| de-DE | `max(1,234;567)` | Two arguments: `1.234` and `567` |
| de-DE | `max(1,234,567)` or `max(1,234,567.890)` | Invalid repeated decimal separators |
| de-DE | `max(1, 2)` | Invalid separator: use `max(1; 2)` |

Each nested call selects its own separator style. Ordinary grouping parentheses also isolate grouped literals. This is a bounded structural scan, not a second expression parser: an identifier followed by `(` is treated conservatively as a potential call, without resolving functions or variables. For a variable multiplied by a grouped literal, write `x * (1,234)` in en-US rather than the call-like `x(1,234)`.

The culture pass leaves square-bracket contents unchanged, and their semicolons do not select a surrounding call's separator style. Brackets remain unsupported by the expression parser; their punctuation is reserved for future matrix syntax.

#### Diagnostics

Malformed or ambiguous localized numbers fail with `InvalidNumberFormat`; invalid or mixed argument-separator styles fail with `InvalidSyntax`. Messages explain the ambiguity or suggest the expected separator. Positions and lengths refer to the **original input**, even after earlier normalization passes changed its offsets. `TryNormalize`, `TryParse`, and `TryEvaluate` report these diagnostics; throwing APIs raise `ExpressionException`. Rejection happens before evaluation, so custom functions are not invoked.

This intentionally tightens older permissive behavior: foreign decimal syntax and compact ambiguous argument commas are no longer guessed or silently stripped.

---

## Percentages

Percentages are part of the Numera grammar, so they are parsed from the original text and keep accurate source spans. The rules favour what a person expects from a calculator over strict programming-language semantics.

- **Postfix or modulo:** `%` is a percentage unless an operand (digit, identifier or `(`) follows it. `10 % 3` and `10%3` are modulo; `100 + 15 %`, `10% + 3` and `10% of 5` are percentages.
- **Unary operands:** `+` and `-` after `%` remain additive operators, regardless of whitespace: `10 % -3` is `0.1 - 3`, not modulo. Use `10 % (-3)` or `mod(10; -3)` for a signed modulo operand. `10% ~3` is a syntax error; write `10 % (~3)` or `mod(10; ~3)` for modulo with a bitwise-complement operand.
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
- For machine-authored expressions, including `MachineReadable` output, use raw invariant parsing as shown below. Merely choosing invariant culture still enables human-input grouping validation and comma-ambiguity checks.
- An engine is bound to one culture. Applications serving users with different cultures should keep one engine per culture.
- Set `Normalization = null` for raw mode, which passes text to the parser untouched. This suits serialized or machine-generated expressions.
- Angle units are never changed implicitly. Opt in explicitly with `with { AngleUnit = AngleUnit.Degrees }`.
- Variables and the previous result (`ans`) are per-call state and are passed in an `EvaluationContext`, not in the options.

```csharp
var machineEngine = new CalculatorEngine(
	CalculatorOptions.ForCulture(CultureInfo.InvariantCulture) with { Normalization = null });

// Commas are always argument separators in raw mode, never digit grouping.
var machineResult = machineEngine.Evaluate("pow(2,100)");
```

Raw mode does not accept localized numbers, semicolon arguments, or launcher/typographic rewrites. `ExpressionNormalizer` remains public for callers that only want text normalization and its diagnostics. Its output is invariant parser input: do not normalize it a second time.
