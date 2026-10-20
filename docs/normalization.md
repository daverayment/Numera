# Expression Normalization

Desktop calculators, app launchers (e.g. PowerToys Run, Raycast, Alfred), search bars, and copied text often receive human-formatted mathematical expressions. These inputs frequently contain colloquial idioms such as:

- Leading or trailing equals signs (`= 2 + 2 =`)
- Typographic operators pasted from articles or Word documents (`×`, `÷`, `−`, `·`)
- Culture-specific decimal commas and digit grouping separators (`1.000,50` vs `1,000.50` vs `1'000.50` vs `1 000 000`)
- Percentage idioms (`15% of 200`, `100 + 20%`, `80 - 15%`)
- Postfix factorial notation (`5!`)
- Implicit multiplication (`2pi`, `2(3+4)`, `(1+2)(3+4)`)

Numera's core evaluator (`Calculator.Evaluate` and `Calculator.TryEvaluate`) intentionally requires a strict, unambiguous, culture-invariant grammar with standard ASCII operators. 

The standalone **`ExpressionNormalizer`** bridges this gap, allowing applications to preprocess human expressions into canonical Numera syntax before evaluation.

### Why Normalization is Separate from Parsing

A core design principle of Numera is keeping the evaluator and AST strict, deterministic, and free of colloquial heuristics:

1. **Deterministic Core:** The AST and mathematical parser treat symbols consistently (for instance, `%` is strictly binary remainder in standard programming and scientific computing). Embedding colloquial human rules (like commercial markup `+ 20%` or cultural decimal commas) directly into the parser would introduce contextual ambiguity and unpredictability into the engine.
2. **Opt-in Human Preprocessing:** Desktop launchers and calculators need lenient, human-centric input handling; script runners and automated systems require strict mathematical grammar. Separating the two via `ExpressionNormalizer` gives callers explicit control over preprocessing passes without compromising core evaluation integrity.
3. **Canonical Traceability:** Applications can inspect or display the exact transformed expression (e.g. showing users how `= 100 + 10% + 20% =` became `((100 + (abs(100) * (10 / 100))) + (abs(...) * (20 / 100)))`) before evaluation, ensuring total transparency into how inputs are interpreted.

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

// 1. Normalize the human input to canonical Numera grammar
if (ExpressionNormalizer.TryNormalize(rawInput, out var normalized, out var errorMessage, options))
{
	Console.WriteLine($"Original:   {rawInput}");
	Console.WriteLine($"Normalized: {normalized}");
	// Prints: 1000.50 * factorial(2 + 3) + (15 / 100) * 200

	// 2. Evaluate the canonical expression
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

### 4. Percentage Idioms
Disambiguates postfix percentage from the binary modulo operator (`%`) and models commercial desktop calculator semantics:
- **Prepositional Percentage:** `15% of 200` $\to$ `(15 / 100) * 200`
- **Additive Markup & Discount:** `A + B%` expands to `(A + (abs(A) * (B / 100)))`. Using Numera's built-in `abs()` function guarantees the percentage is computed from the magnitude of the base term, ensuring intuitive behavior across both positive and negative values:
  - Positive base: `100 + 20%` $\to$ `(100 + (abs(100) * (20 / 100)))` ($= 120$)
  - Negative base: `-100 + 10%` $\to$ `(-100 + (abs(-100) * (10 / 100)))` ($= -90$)
  - Negative discount: `-100 - 10%` $\to$ `(-100 - (abs(-100) * (10 / 100)))` ($= -110$)
- **Compound Multiplicative Base:** In `50 * 2 + 10%`, multiplication binds tighter than addition, so the markup captures the entire product `50 * 2` rather than merely the trailing factor `2`: `(50 * 2 + (abs(50 * 2) * (10 / 100)))` ($= 110$).
- **Chained Cumulative Markup:** When markups/discounts are chained, the entire preceding parenthesized subexpression becomes term $A$ for the next percentage step, compounding cumulatively. For instance, in `10 + 10% + 10%`:
  - Pass 1 expands `10 + 10%` $\to$ `(10 + (abs(10) * (10 / 100)))` ($= 11$)
  - Pass 2 substitutes the whole prior term into the next markup: `((10 + (abs(10) * (10 / 100))) + (abs((10 + (abs(10) * (10 / 100)))) * (10 / 100)))` ($= 12.1$)
- **Standalone Rate Arithmetic:** When both operands are percentage rates (e.g. `10% + 20%` or `50% - 20%`), they are not treated as base markup; instead, they are expanded to pure algebraic rates `(10 / 100) + (20 / 100)` ($= 0.30$).
- **Modulo Disambiguation:** Binary modulo expressions such as `10 % 3` and `10%3` are kept untouched as remainder operations whenever the following non-whitespace token begins an operand (digit, identifier, or opening parenthesis). Whitespace before `%` (e.g. `100 + 15 %`) is accepted as postfix percent when no operand follows.

### 5. Postfix Factorials
Rewrites postfix factorial notation to Numera's built-in `factorial(...)` function:
- `5!` $\to$ `factorial(5)`
- `(2 + 3)!` $\to$ `factorial(2 + 3)`
- Does not affect inequality operator `!=` (`x != y` is preserved).

### 6. Implicit Multiplication
Inserts explicit multiplication operators `*` between adjacent mathematical terms while safeguarding scientific notation and radix literals:
- Number adjacent to parenthesis: `2(3 + 4)` $\to$ `2 * (3 + 4)`
- Parenthesis adjacent to parenthesis: `(1 + 2)(3 + 4)` $\to$ `(1 + 2) * (3 + 4)`
- Number adjacent to variable/constant: `2pi` $\to$ `2 * pi`, `3sqrt(16)` $\to$ `3 * sqrt(16)`
- **Preserved Formats:** Scientific notation (`1e5`, `2.5e-3`) and radix prefixes (`0x100`, `0b101`, `0o77`) are never split.

---

## Configuring Options

Every transformation pass can be enabled or disabled via `NormalizationOptions`:

```csharp
var options = new NormalizationOptions
{
	Culture = CultureInfo.CurrentCulture,
	ExpandPercentages = true,
	ExpandImplicitMultiplication = true,
	ConvertPostfixFactorials = true,
	ReplaceUnicodeOperators = true,
	TrimLauncherEquals = true
};
```

Using `NormalizationOptions.Default` enables all transformations with invariant culture.
