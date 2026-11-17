# Implicit multiplication

When implicit multiplication is enabled, you can leave out `*` in expressions
such as `2x` and `2(3 + 4)`. This is convenient shorthand, but take care when
mixing it with division: calculators do not all group these expressions alike.

## Precedence

Numera keeps an implicit product together before dividing. `1/2x` means
`1/(2*x)`. The same rule makes `6/2(1+2)` mean `6/(2*(1+2))`, giving `1`.
To divide first and then multiply, write `(1/2)*x` or `(6/2)*(1+2)`; the latter gives `9`.
Explicit parentheses make the intended grouping clear when sharing a calculation.

Implicit multiplication binds tighter than explicit multiplication, division,
and remainder, but looser than unary operators and exponents:

$$\text{Additive } (+, -) < \text{Explicit } (*, /, \%) < \mathbf{Implicit } (\text{juxtaposition}) < \text{Unary } (-, +, \sim) < \text{Power } (\hat{\ })$$

Powers and unary signs still bind more tightly: `2x^2` means `2 * (x^2)`,
while `-2x` means `(-2) * x`.

## Where multiplication can be omitted

The table shows which pairs of terms can be written next to each other.
Where whitespace is allowed, adding a space does not change the multiplication.

| Left Operand ($L$) | Right Operand ($R$) | Allowed with whitespace? | Example | Evaluates As |
| :--- | :--- | :---: | :--- | :--- |
| **Number** | Identifier / Constant | **Yes** | `2pi`, `2 pi`, `2x` | `2 * pi`, `2 * x` |
| **Number** | Function Call | **Yes** | `2sqrt(16)`, `2 sqrt(16)` | `2 * sqrt(16)` |
| **Number** | Parenthesized Group | **Yes** | `2(3)`, `2 (3)` | `2 * 3` |
| **Number** | Bare Number | **NO (Syntax Error)** | `1 000`, `2 3` | *Rejected* |
| **Identifier** | Identifier | **Yes** | `x y` | `x * y` |
| **Identifier** | Function Call | **Yes** | `x sqrt(y)`, `xsqrt(y)` | `x * sqrt(y)` |
| **Identifier** | Parenthesized Group | **Yes** (if defined variable) | `x(y)` | `x * y` |
| **Identifier** | Parenthesized Group | **NO** (if unknown) | `unknown(y)` | *UnknownFunction* |
| **Identifier** | Number | **NO (Single Token)** | `x2` | Identifier `x2` |
| **Parenthesized Group** | Parenthesized Group | **Yes** | `(1)(2)`, `(1) (2)` | `1 * 2` |
| **Parenthesized Group** | Identifier / Function | **Yes** | `(2)pi`, `(2)sqrt(16)` | `2 * pi`, `2 * 4` |
| **Parenthesized Group** | Number | **Yes** | `(2)3`, `(1+2) 3` | `2 * 3`, `3 * 3` |
| **Function Call** | Parenthesized Group | **Yes** | `sqrt(16)(2)` | `4 * 2` |
| **Function Call** | Function Call | **Yes** | `sqrt(16)cbrt(27)` | `4 * 3` |
| **Function Call** | Number | **Yes** | `sqrt(16)2`, `triple(3) 4` | `4 * 2`, `9 * 4` |

## Numbers, keywords, and names
1. **No Silent Digit Grouping:** Two bare numbers separated by spaces (`1 000`, `2 3`) can never form implicit multiplication.
2. **Keywords Blocked:** Reserved keywords (`and`, `or`, `xor`, `not`, `of`, `true`, `false`) can never start an implicit product (`2true` is rejected, and `2and 3` evaluates as logical `2 && 3`).
3. **Variable Call vs. Function Call:** In `foo(bar)`, if `foo` is registered as a function (built-in or custom), it is a function call. If `foo` is defined in `context.Variables`, it evaluates as `foo * (bar)`.
