### The Formal Grammar of Implicit Multiplication

Formal specification of juxtaposition (implicit) multiplication in Numera:

#### A. Precedence Hierarchy
Implicit multiplication sits in **Level 9**, binding tighter than explicit multiplication, division, and remainder, but looser than unary operators and exponents:

$$\text{Additive } (+, -) < \text{Explicit } (*, /, \%) < \mathbf{Implicit } (\text{juxtaposition}) < \text{Unary } (-, +, \sim) < \text{Power } (\hat{\ })$$

* **Consequence with division:** `1 / 2x` evaluates as `1 / (2x)` (because implicit multiplication binds tighter than `/`).
* **Consequence with powers:** `2x^2` evaluates as `2 * (x^2)` (because exponentiation binds tighter than implicit multiplication).
* **Consequence with signs:** `-2x` evaluates as `(-2) * x` (because unary minus binds tighter than implicit multiplication).

#### B. The Left/Right Juxtaposition Matrix
When the parser finishes evaluating an operand ($L$) and looks ahead at the next token ($R$), juxtaposition multiplication is formed if and only if the pair matches this matrix:

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

#### C. The Disambiguation Guarantees
1. **No Silent Digit Grouping:** Two bare numbers separated by spaces (`1 000`, `2 3`) can never form implicit multiplication.
2. **Keywords Blocked:** Reserved keywords (`and`, `or`, `xor`, `not`, `of`, `true`, `false`) can never start an implicit product (`2true` is rejected, and `2and 3` evaluates as logical `2 && 3`).
3. **Variable Call vs. Function Call:** In `foo(bar)`, if `foo` is registered as a function (built-in or custom), it is a function call. If `foo` is defined in `context.Variables`, it evaluates as `foo * (bar)`.
