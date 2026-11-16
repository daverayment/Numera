# Percentages and modulo

Use `15%` for a rate, `200 + 15%` for a markup, and `15% of 200` for the
percentage amount. The same `%` character can also mean remainder, as in
`10 % 3`. Numera chooses between them from what follows `%`, not from spacing.

## Percentage or modulo?

`%` means modulo when the next non-whitespace character starts an unsigned
number (a digit or `.`), an identifier other than an operator keyword, or a
parenthesized expression (`(`). Otherwise it is a postfix percentage. In
particular, `+` and `-` after `%` remain addition and subtraction, regardless of
whitespace. Put a signed modulo operand in parentheses, or use `mod` to make
the intention explicit.

| Enter | Read as | Answer |
| --- | --- | --- |
| `50%` | Fifty percent | `0.5` |
| `10 % 3` or `10%3` | Remainder of 10 divided by 3 | `1` |
| `7 % -3` | `7% - 3`, or `0.07 - 3` | `-2.93` |
| `5 % +3` | `5% + 3`, or `0.05 + 3` | `3.05` |
| `10 % -3` | `10% - 3`, or `0.1 - 3` | `-2.9` |
| `5 %(-3)` | Remainder of 5 divided by -3 | `2` |
| `10 % (-3)` or `mod(10; -3)` | Remainder of 10 divided by -3 | `1` |
| `10 % (+3)` | Remainder of 10 divided by +3 | `1` |
| `100 + 10% - 5` | Add a 10% markup, then subtract 5 | `105` |

For example, if you meant remainder rather than the subtraction in `7 % -3`,
write `7 % (-3)` or `mod(7; -3)`; both give `1`. For `5 % +3`, the remainder
spelling is `5 % (+3)` or `mod(5; +3)`, giving `2`.

The same parenthesis rule applies to bitwise complement: `10% ~3` is a syntax
error, while `10 % (~3)` and `mod(10; ~3)` give `2`.

These are expression grammar rules, not normalization rewrites. Disabling
[normalization](normalization.md) does not change them. The examples use decimal
points and semicolons between function arguments; localized number input follows
the engine's culture.

## Rates, markups, and discounts

A standalone `B%` means `B / 100`: `50%` is `0.5`, and `200 * 15%` is `30`.

When the right operand of `+` or `-` is a percentage and the left is not,
Numera applies that rate to the whole left operand. For a base `L` and rate `r`,
the result is `L * (1 + r)` or `L * (1 - r)`:

- `200 + 15%` is `230`; `200 - 15%` is `170`.
- `50 * 2 + 10%` is `110`; `10 + 50 * 2 + 10%` is `121`.
- Chains compound: `100 + 10% + 20%` is `132`.
- Negative bases scale too: `-100 + 10%` is `-110`, and `-100 - 10%` is `-90`.
- Negative rates reverse the adjustment: `100 + -10%` is `90`, and
  `100 - -10%` is `110`.

When the left operand is itself a percentage, addition and subtraction are
ordinary arithmetic: `10% + 20%` is `0.3`, and `-10% + 20%` is `0.1`.

Whether an operand is a percentage depends on its parsed shape, not on tracing
units through arithmetic. In `10% * 2 + 5%`, the left operand is a product,
so the result is a markup: `0.2 * 1.05 = 0.21`. This also lets
`200 * 15% + 10%` mean a fee with 10% tax, giving `33`. A variable containing
`0.15` is a number, not a written `15%`; use explicit multiplication when you
want a rate calculation independent of this shorthand.

## A percentage of an amount

`15% of 200` is `30`. The reserved operator `of` groups from the right:
`10% of 20% of 1000` means `10% of (20% of 1000)`, giving `20`.
Only a percentage may appear on its left; `200 of 10%` is a syntax error.

## Showing how an expression was read

Showing the formatted expression beside its answer helps users spot an
unintended interpretation. `FormatExpression` attaches a percentage sign to
its operand, but spaces `%` as a binary remainder operator and retains the
parentheses around signed remainder operands.

Parse once, then evaluate and format that same tree with the same engine and
context. This avoids interpreting the input again just to display it:

```csharp
using Numera;
using System.Globalization;

var engine = CalculatorEngine.ForCulture(CultureInfo.InvariantCulture);
if (engine.TryParse("7 % -3", out var expression, out var diagnostic) &&
	engine.TryEvaluate(expression, out var result, out diagnostic))
{
	string interpretation = engine.FormatExpression(expression, ExpressionFormatOptions.Typographic);
	var card = engine.FormatResult(result);
	Console.WriteLine($"{interpretation} = {card.PrimaryDisplay}");
}
else
{
	Console.WriteLine($"Error: {diagnostic.Code} - {diagnostic.Message}");
}
```

The typographic expression reads as seven percent minus three, with the answer
`-2.93`. For `5 %(-3)`, it shows a spaced remainder operator and a parenthesized
negative operand, with the answer `2`. This is a presentation choice for the
host, not a warning or a change to evaluation.

See the [calculation examples](examples.md#percentages-tax-discounts-and-pasted-input)
for everyday uses, and the [syntax reference](syntax.md#operators--precedence)
for other operators.
