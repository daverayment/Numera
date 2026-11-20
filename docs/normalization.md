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
> `x =` reports `InvalidSyntax` at the `=`. Trimming decoration does not supply missing
> operands or complete an assignment. See [Variables and Context](variables.md).

### 2. Typographic & Unicode Sanitization
Converts copied Unicode mathematical symbols, dashes, and Fortran/Python power operators to standard ASCII equivalents:
- Multiplication: `×`, `•`, `·`, `∗` $\to$ `*`
- Division: `÷`, `∕`, `⁄` $\to$ `/`
- Minus & Dashes: Math minus `−`, en-dash `–`, em-dash `—`, horizontal bar `―` $\to$ `-`
- Exponentiation: `2 ** 3` $\to$ `2 ^ 3`

### 3. Culture & Digit Grouping Normalization
Uses `NumberDecimalSeparator`, `NumberGroupSeparator`, `NumberGroupSizes`, and `TextInfo.ListSeparator` from `CalculatorOptions.Culture`. A null culture passed directly to `ExpressionNormalizer` means invariant culture, not current culture.

#### Decimal and grouping rules

Write large numbers as `1000` or use the grouping forms below. Underscores are for
variable and function names, not digit grouping, so `1_000` is not accepted.

- Only the configured decimal separator is accepted. German `1,25` and leading `,5` become `1.25` and `.5`; invariant-looking decimal points are not a fallback.
- Grouping is validated across the complete integer part before any separators are removed, working right-to-left through the culture's group sizes. This includes Indian grouping (`12,34,567`) and custom group sizes ending in zero (no further separators).
- A grouped integer cannot have leading zeroes. Thus German `1.234` means `1234`, but `0.123` is rejected rather than silently becoming `123`. Use `0,123` for that decimal value.
- If culture data uses the same decimal and grouping separator, numbers that admit both interpretations are rejected too; Numera does not silently prefer decimals. Omit grouping or use raw invariant input to avoid such collisions.
- Straight/curly apostrophes and ordinary, non-breaking, narrow non-breaking, and thin spaces remain portable grouping forms. They must obey the configured group sizes. Do not mix native punctuation, apostrophe, and space grouping families within one number.
- Grouping is not allowed in fractional parts, exponents, or radix literals. For example, `1.2.3`, `1 23 456` in en-US, and `0x10 000` are invalid. Numeric literals use ASCII digits `0` through `9`; identifier digits are not normalized as numbers.

Spaces between digits are interpreted as grouping, not multiplication. Write
`2 * 3` for separate factors; `2 3` is invalid digit grouping.

#### Argument separators and ambiguity

The culture's list separator separates function arguments. **Semicolon is also an explicit alternative in every culture.** A direct semicolon selects semicolon style for that entire call, including numbers before it. Other argument separators cannot be mixed into that call; commas may still occur as valid decimal or grouping punctuation.

In a comma-separated call, a comma that could also form valid digit grouping is rejected as ambiguous. Numera does not use function arity, later malformed groups, or evaluation results to guess what was intended. To resolve this, use semicolons, remove digit grouping, or put a space after an argument comma so it cannot group digits.

Parentheses after operator keywords (`not`, `and`, `or`, `xor`, `of`) group an
expression, so en-US `not (1,000 == 1000)` contains a grouped number rather than
function arguments. Nested function calls follow the same argument-separator rules
as other calls.

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

Each nested call selects its own separator style. Ordinary grouping parentheses also isolate grouped literals. An identifier followed by `(` is treated as a potential call during normalization, without resolving functions or variables. For a variable multiplied by a grouped literal, write `x * (1,234)` in en-US rather than the call-like `x(1,234)`.

The culture pass leaves square-bracket contents unchanged, and their semicolons do not select a surrounding call's separator style. Brackets remain unsupported by the expression parser; their punctuation is reserved for future matrix syntax.

#### Diagnostics

Malformed or ambiguous localized numbers fail with `InvalidNumberFormat`; invalid or mixed argument-separator styles fail with `InvalidSyntax`. Messages explain the ambiguity or suggest the expected separator. Positions and lengths refer to the **original input**, even after earlier normalization passes changed its offsets. `TryNormalize`, `TryParse`, and `TryEvaluate` report these diagnostics; throwing APIs raise `ExpressionException`. Rejection happens before evaluation, so custom functions are not invoked.

### 4. Division Colon
Rewrites a European ratio colon to `/` only when the left side is a number (decimal, scientific such as `1e5` or `1e-5`, or radix-prefixed such as `0xFF`) or a closing `)`, and the right side is an optionally signed number or starts with `(`. The sign must touch the number, so `6 : - 2` is left unchanged. Controlled by `ReplaceDivisionColon`.
- Operands follow the configured culture: German `1.000.000 : 2,5` becomes `1000000 / 2.5`. Configured number and argument separators take precedence over division colons.
- `6 : 2` $\to$ `6 / 2`, `16:9` $\to$ `16/9`, `log10(100):2` $\to$ `log10(100)/2`
- Identifiers are never guessed at, so launcher-style text is preserved: `status:200`, `pi:2`, `item1:200`. Parenthesise an identifier to opt in: `(pi):2` $\to$ `(pi)/2`.
- Colons inside square brackets are left alone. Brackets are reserved for future syntax and are an error in expressions today.

---

## Percentages

For help with `%`, see [Percentages and modulo](percentages.md): when it means
a percentage or a remainder, how to write signed operands, and examples of
markups and discounts.

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

- `new CalculatorOptions()` and `CalculatorOptions.ForCurrentCulture()` capture current culture with all passes enabled.
- `new CalculatorEngine()` and `CalculatorEngine.ForCurrentCulture()` create an engine capturing current culture. Existing options and engines do not follow later culture changes.
- `Calculator` resolves current culture per call; its results retain the culture used for evaluation.
- For machine-authored expressions, including `MachineReadable` output, use raw invariant parsing as shown below. Merely choosing invariant culture still enables human-input grouping validation and comma-ambiguity checks.
- An engine is bound to one culture. Applications serving users with different cultures should keep one engine per culture.
- Set `Normalization = null` for raw mode, which passes text to the parser untouched. This suits serialized or machine-generated expressions.
- Angle units are never changed implicitly. Opt in explicitly with `with { AngleUnit = AngleUnit.Degrees }`.
- Variables and the previous result (`ans`) are per-call state and are passed in an `EvaluationContext`, not in the options.

```csharp
var machineEngine = new CalculatorEngine(
	CalculatorOptions.ForMachineInput());

// Commas are always argument separators in raw mode, never digit grouping.
var machineResult = machineEngine.Evaluate("pow(2,100)");
```

`ForMachineInput()` selects invariant culture and `Normalization = null`, leaving other language and safety settings at their defaults. Its default result displays are invariant too; use a formatting culture override when a localized display is needed. Re-evaluating machine text preserves the stored finite number, not storage or exactness metadata; use value serialization when those distinctions matter.

Raw mode does not accept localized numbers, semicolon arguments, or launcher/typographic rewrites. `ExpressionNormalizer` remains public for callers that only want text normalization and its diagnostics. Its output is invariant parser input: do not normalize it a second time.
