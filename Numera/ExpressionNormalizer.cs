using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Numera;

/// <summary>
/// Preprocesses and normalizes human desktop calculator and launcher expressions into
/// standard, unambiguous Numera mathematical grammar.
/// </summary>
public static class ExpressionNormalizer
{
    /// <summary>
    /// Normalizes the specified input string according to the provided options.
    /// Throws <see cref="FormatException"/> if unresolvable ambiguities are encountered.
    /// </summary>
    /// <param name="input">The raw user expression.</param>
    /// <param name="options">Optional normalization options; defaults to <see
    /// cref="NormalizationOptions.Default"/>.</param>
    /// <returns>The normalized expression string ready for evaluation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="input"/> is
    /// null.</exception>
    /// <exception cref="FormatException">Thrown if input syntax contains conflicting or
    /// ambiguous delimiters.</exception>
    public static string Normalize(string input, NormalizationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        options ??= NormalizationOptions.Default;

        if (!TryNormalize(input, out var normalized, out var errorMessage, options))
        {
            throw new FormatException(errorMessage);
        }

        return normalized;
    }

    /// <summary>
    /// Safely attempts to normalize an expression. Returns false if the input contains
    /// unresolvable delimiter conflicts or ambiguous formatting.
    /// </summary>
    public static bool TryNormalize(
        string input,
        [NotNullWhen(true)] out string? normalized,
        NormalizationOptions? options = null) => TryNormalize(input, out normalized, out _, options);

    /// <summary>
    /// Safely attempts to normalize an expression, providing an error message on failure.
    /// </summary>
    public static bool TryNormalize(
        string input,
        [NotNullWhen(true)] out string? normalized,
        [NotNullWhen(false)] out string? errorMessage,
        NormalizationOptions? options = null)
    {
        if (input is null)
        {
            normalized = null;
            errorMessage = "Input expression cannot be null.";
            return false;
        }

        options ??= NormalizationOptions.Default;
        string current = input;

        // Pass 1: Launcher quirks (leading / trailing '=').
        if (options.TrimLauncherEquals)
        {
            current = TrimLauncherEquals(current);
        }

        // Pass 2: Unicode and typographic operator sanitization.
        if (options.ReplaceUnicodeOperators)
        {
            current = SanitizeUnicodeOperators(current);
        }

        // Pass 3: Culture & number formatting (decimal comma, digit grouping, argument
        // semicolons).
        if (!NormalizeCultureAndSeparators(current, options.Culture, out current, out errorMessage))
        {
            normalized = null;
            errorMessage = $"Culture/number formatting normalization failed: {errorMessage}";
            return false;
        }

        // Pass 4: Percentage idioms ('of', markup '+ X%', discount '- X%', postfix '%').
        if (options.ExpandPercentages)
        {
            if (!NormalizePercentages(current, out current, out errorMessage))
            {
                normalized = null;
                errorMessage = $"Percentage normalization failed: {errorMessage}";
                return false;
            }
        }

        // Pass 5: Postfix factorials ('5!' -> 'factorial(5)').
        if (options.ConvertPostfixFactorials)
        {
            current = NormalizePostfixFactorials(current);
        }

        // Pass 6: Implicit multiplication ('2pi', '2(3+4)', '(1+2)(3+4)').
        if (options.ExpandImplicitMultiplication)
        {
            current = NormalizeImplicitMultiplication(current);
        }

        normalized = current;
        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Trim leading and trailing characters that may be added by launchers or
    /// calculators, such as '='.
    /// </summary>
    private static string TrimLauncherEquals(string text)
    {
        var span = text.AsSpan().Trim();
        if (span.IsEmpty)
        {
            return string.Empty;
        }

        // Leading activation '=' (e.g. '= 2 + 2'), but NOT '=='.
        if (span.StartsWith("=") && !span.StartsWith("=="))
        {
            span = span[1..].TrimStart();
        }

        // Trailing calculator '=' (e.g. '2 + 2 ='), but NOT '==', '<=', '>=', '!=', '~='.
        if (span.EndsWith("=") && !span.EndsWith("==") && !span.EndsWith("<=") &&
            !span.EndsWith(">=") && !span.EndsWith("!=") && !span.EndsWith("~="))
        {
            span = span[..^1].TrimEnd();
        }

        return span.ToString();
    }

    private static string SanitizeUnicodeOperators(string text)
    {
        var sb = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            switch (c)
            {
                // Multiplication.
                case '×': // \u00D7
                case '•': // \u2022
                case '·': // \u00B7
                case '∗': // \u2217
                    sb.Append('*');
                    break;

                // Division.
                case '÷': // \u00F7
                case '∕': // \u2215
                case '⁄': // \u2044
                    sb.Append('/');
                    break;

                // Subtraction / Math Minus / Dashes.
                case '−': // \u2212 math minus
                case '–': // \u2013 en-dash
                case '—': // \u2014 em-dash
                case '―': // \u2015 horizontal bar
                    sb.Append('-');
                    break;

                // Python/Fortran '**' power.
                case '*' when i + 1 < text.Length && text[i + 1] == '*':
                    sb.Append('^');
                    i++;
                    break;

                // European colon ':' used as division (e.g. '6 : 2').
                case ':' when IsDivisionColon(text, i):
                    sb.Append('/');
                    break;

                default:
                    sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }

    private static bool IsDivisionColon(string text, int index)
    {
        // Colon is division if it's flanked by operands (typically numbers or parentheses)
        // rather than part of a symbol or label.
        int left = index - 1;
        while (left >= 0 && char.IsWhiteSpace(text[left])) left--;
        if (left < 0) return false;

        char prev = text[left];
        if (!char.IsLetterOrDigit(prev) && prev != ')' && prev != ']') return false;

        int right = index + 1;
        while (right < text.Length && char.IsWhiteSpace(text[right])) right++;
        if (right >= text.Length) return false;

        char next = text[right];
        return char.IsLetterOrDigit(next) || next == '(' || next == '[' || next == '+' || next == '-';
    }

    private static bool NormalizeCultureAndSeparators(
        string text,
        CultureInfo? culture,
        out string result,
        out string? errorMessage)
    {
        result = text;
        errorMessage = null;

        // If culture is null, check for standard Swiss apostrophe or ISO thin space digit grouping
        // e.g., 1'000'000.50
        bool usesCommaDecimal = culture is not null && culture.NumberFormat.NumberDecimalSeparator == ",";

        var sb = new StringBuilder(text.Length);
        int parenDepth = 0;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '(') parenDepth++;
            else if (c == ')') parenDepth = Math.Max(0, parenDepth - 1);

            // Swiss digit grouping: apostrophe between digits (e.g. 1'000'000 or 1’000’000).
            // Supports both straight apostrophe (U+0027) and typographic/curly apostrophe (U+2019).
            if ((c == '\'' || c == '\u2019') && i > 0 && i + 1 < text.Length && char.IsDigit(text[i - 1]) && char.IsDigit(text[i + 1]))
            {
                // Discard grouping separator
                continue;
            }

            // Non-breaking space, thin/narrow space, or regular space between groups of digits
            // e.g. 1 000 000 or 1\u00A0000\u00A0000 or 1\u202F000\u202F000 (SI standard)
            if ((c == ' ' || c == '\u00A0' || c == '\u202F') &&
                i > 0 && i + 1 < text.Length && char.IsDigit(text[i - 1]) && char.IsDigit(text[i + 1]))
            {
                // Peek ahead: check if this is a 3-digit group (e.g. " 000")
                if (IsDigitGroupingSpace(text, i))
                {
                    continue;
                }
            }

            // Semicolons inside parentheses '(...)' are always function argument separators
            // (e.g. Excel/European pasted formulas like max(1; 2) or round(2.5; 1)),
            // leaving square brackets '[...]' free for future matrix rows.
            if (c == ';' && parenDepth > 0)
            {
                sb.Append(',');
                continue;
            }

            if (usesCommaDecimal)
            {
                // In European culture:
                // Period '.' is thousands separator (e.g. 1.000.000,50)
                // Comma ',' is decimal separator (e.g. 1,5)
                if (c == '.' && i > 0 && i + 1 < text.Length && char.IsDigit(text[i - 1]) && char.IsDigit(text[i + 1]))
                {
                    // If it's followed by exactly 3 digits and then another '.' or ',' or boundary, it's grouping
                    if (IsThreeDigitGroup(text, i + 1))
                    {
                        continue; // Discard thousands period
                    }
                }

                if (c == ',')
                {
                    // Disambiguate: is ',' decimal or argument separator?
                    // Inside function calls, if argument separator is ';', ',' is decimal.
                    // If between digits with no spaces: '1,5' -> '1.5'
                    if (i > 0 && i + 1 < text.Length && char.IsDigit(text[i - 1]) && char.IsDigit(text[i + 1]))
                    {
                        sb.Append('.');
                        continue;
                    }
                }
            }
            else
            {
                // In standard/US culture:
                // Comma ',' between digits with 3 digits following is thousands grouping (e.g. 1,000,000)
                if (c == ',' && i > 0 && i + 1 < text.Length && char.IsDigit(text[i - 1]) && char.IsDigit(text[i + 1]))
                {
                    if (IsThreeDigitGroup(text, i + 1))
                    {
                        continue; // Discard thousands comma
                    }
                }
            }

            sb.Append(c);
        }

        result = sb.ToString();
        return true;
    }

    private static bool IsThreeDigitGroup(string text, int startIndex)
    {
        int count = 0;
        int idx = startIndex;
        while (idx < text.Length && char.IsDigit(text[idx]))
        {
            count++;
            idx++;
        }

        // Standard grouping has 3 digits
        return count == 3;
    }

    private static bool IsDigitGroupingSpace(string text, int spaceIndex)
    {
        // Check if characters before and after form digit groups
        int afterCount = 0;
        int idx = spaceIndex + 1;
        while (idx < text.Length && char.IsDigit(text[idx]))
        {
            afterCount++;
            idx++;
        }

        return afterCount == 3;
    }

    private static bool NormalizePercentages(string text, out string result, out string? errorMessage)
    {
        errorMessage = null;

        // Prepositional idioms ("X% of Y") represent fractional multiplication, e.g. "15% of 200" -> 0.15 * 200.
        // We normalize these first so that downstream postfix scanning only encounters mathematical operators.
        text = ReplacePrepositionalPercent(text);

        // Commercial desktop calculators treat '+' and '-' with a percentage as markup and discount
        // operations (e.g. "100 + 10%" -> 110, "100 - 10%" -> 90), expanding "A +/- B%" into
        // "A +/- (A * (B / 100))". Standalone or multiplicative occurrences ("50% * 2", "50%") simply
        // represent the pure algebraic ratio "(B / 100)".
        //
        // Each replacement modifies the string length, so we perform iterative slice-based replacements
        // and advance searchStart past the generated substitution. This guarantees that source indices
        // never desynchronize and synthesized expressions are not mistakenly re-normalized.
        int searchStart = 0;
        while (true)
        {
            int percentIndex = FindNextPostfixPercent(text, searchStart);
            if (percentIndex < 0)
            {
                break;
            }

            // Identify operand B immediately preceding '%'. This can be a number, identifier,
            // or a balanced parenthesized subexpression like "(2 + 3)%".
            int bEnd = percentIndex;
            int bStart = ScanOperandBackward(text, bEnd - 1);
            if (bStart < 0)
            {
                errorMessage = $"Invalid percentage syntax near index {percentIndex}.";
                result = text;
                return false;
            }

            string bOperand = text[bStart..bEnd].Trim();

            // Check if B is preceded by an additive operator. If the '+' or '-' is binary (not a unary sign),
            // this is an additive markup/discount idiom where the percentage applies to the preceding term A.
            int opIdx = bStart - 1;
            while (opIdx >= 0 && char.IsWhiteSpace(text[opIdx])) opIdx--;

            if (opIdx >= 0 && (text[opIdx] == '+' || text[opIdx] == '-') &&
                !IsUnarySign(text, opIdx))
            {
                char markupOp = text[opIdx];
                int aStart = ScanAdditiveExpressionBackward(text, opIdx);
                if (aStart >= 0)
                {
                    string aOperand = text[aStart..opIdx].Trim();

                    // If operand A is itself a standalone percentage (e.g. "10% + 20%" or "(10 / 100) + 20%"),
                    // do not perform markup: treat both as standalone percentage rates (yielding algebraic addition/subtraction).
                    if (!IsPercentageTerm(aOperand))
                    {
                        // Wrap markup in outer parentheses so that chained markups (e.g. "100 + 10% + 20%")
                        // treat the entire preceding markup as cohesive operand A in subsequent iterations.
                        // Use abs(A) so negative bases (e.g. "-100 + 10%") adjust markup relative to magnitude (-90).
                        string replacement = $"({aOperand} {markupOp} (abs({aOperand}) * ({bOperand} / 100)))";
                        text = string.Concat(text.AsSpan(0, aStart), replacement, text.AsSpan(percentIndex + 1));
                        searchStart = aStart + replacement.Length;
                        continue;
                    }
                }
            }

            // Standalone or rate percentage (e.g. "50%", "200 * 15%", or "10% + 20%"): rewrite directly to a ratio.
            string standaloneReplacement = $"({bOperand} / 100)";
            text = string.Concat(text.AsSpan(0, bStart), standaloneReplacement, text.AsSpan(percentIndex + 1));
            searchStart = bStart + standaloneReplacement.Length;
        }

        result = text;
        return true;
    }

    /// <summary>
    /// Determines whether the specified operand represents a standalone percentage rate rather than
    /// a base value to be marked up or discounted.
    /// </summary>
    private static bool IsPercentageTerm(string operand)
    {
        operand = operand.Trim();
        if (operand.EndsWith('%'))
        {
            return true;
        }

        // Checks for normalized standalone ratio "(... / 100)" while excluding compound expressions like "(100 + ...)".
        return operand.StartsWith('(') &&
               (operand.EndsWith("/ 100)") || operand.EndsWith("/100)")) &&
               !operand.Contains('+') &&
               !operand.Contains('-');
    }

    private static int FindNextPostfixPercent(string text, int startIndex)
    {
        for (int i = startIndex; i < text.Length; i++)
        {
            if (text[i] == '%' && IsPostfixPercent(text, i))
            {
                return i;
            }
        }

        return -1;
    }

    private static string ReplacePrepositionalPercent(string text)
    {
        // Matches "X% of Y" or "X % of Y"
        const string ofWord = "of";
        int idx = 0;
        var sb = new StringBuilder(text.Length);

        while (idx < text.Length)
        {
            int percentIdx = text.IndexOf('%', idx);
            if (percentIdx < 0)
            {
                sb.Append(text.AsSpan(idx));
                break;
            }

            // Check if followed by "of"
            int after = percentIdx + 1;
            while (after < text.Length && char.IsWhiteSpace(text[after])) after++;

            if (after + ofWord.Length <= text.Length &&
                text.AsSpan(after, ofWord.Length).Equals(ofWord, StringComparison.OrdinalIgnoreCase) &&
                (after + ofWord.Length == text.Length || char.IsWhiteSpace(text[after + ofWord.Length]) || text[after + ofWord.Length] == '('))
            {
                // Found "X% of"
                int bStart = ScanOperandBackward(text, percentIdx - 1);
                if (bStart >= 0 && bStart >= idx)
                {
                    sb.Append(text.AsSpan(idx, bStart - idx));
                    string bOperand = text[bStart..percentIdx].Trim();
                    sb.Append($"({bOperand} / 100) *");
                    // If there was a trailing space or delimiter after "of", keep a single space
                    idx = after + ofWord.Length;
                    if (idx < text.Length && char.IsWhiteSpace(text[idx]))
                    {
                        sb.Append(' ');
                        idx++;
                    }
                    continue;
                }
            }

            sb.Append(text.AsSpan(idx, percentIdx - idx + 1));
            idx = percentIdx + 1;
        }

        return sb.ToString();
    }

    private static bool IsPostfixPercent(string text, int percentIndex)
    {
        // Disambiguation between binary modulo ("10 % 3") and postfix percentage ("15%", "100 + 15 %"):
        // Binary modulo is an infix operator and strictly requires a following operand. Postfix percentage
        // has no right-hand operand, so what follows can only be an operator, a delimiter, a closing paren,
        // or the end of the input.
        int prevIdx = percentIndex - 1;
        while (prevIdx >= 0 && char.IsWhiteSpace(text[prevIdx])) prevIdx--;
        if (prevIdx < 0) return false;

        char prev = text[prevIdx];
        if (!char.IsLetterOrDigit(prev) && prev != ')') return false;

        int nextIdx = percentIndex + 1;
        while (nextIdx < text.Length && char.IsWhiteSpace(text[nextIdx])) nextIdx++;

        if (nextIdx < text.Length)
        {
            char next = text[nextIdx];
            if (char.IsDigit(next) || char.IsLetter(next) || next == '(')
            {
                // A following operand indicates an infix binary modulo operation (e.g. "10 % 3", "10%3").
                return false;
            }
        }

        return true;
    }

    private static bool IsUnarySign(string text, int opIndex)
    {
        // A '+' or '-' acts as a unary prefix sign (rather than a binary operator) when it has no
        // preceding operand expression—i.e. at the start of input or immediately after another operator or '('.
        int i = opIndex - 1;
        while (i >= 0 && char.IsWhiteSpace(text[i])) i--;
        if (i < 0) return true;

        char prev = text[i];
        return prev is '+' or '-' or '*' or '/' or '^' or '(' or ',' or '<' or '>' or '=';
    }

    /// <summary>
    /// Scans backward to identify the primary operand immediately preceding an operator or postfix notation.
    /// Operands are either simple tokens (identifiers, numbers) or balanced parenthesized expressions,
    /// optionally preceded by a function name (e.g. "sqrt(16)").
    /// </summary>
    private static int ScanOperandBackward(string text, int endIndex)
    {
        int i = endIndex;
        while (i >= 0 && char.IsWhiteSpace(text[i])) i--;
        if (i < 0) return -1;

        if (text[i] == ')')
        {
            // Balance parentheses backwards so composite groups like "(2 + 3)" are captured as a unit.
            int depth = 1;
            i--;
            while (i >= 0 && depth > 0)
            {
                if (text[i] == ')') depth++;
                else if (text[i] == '(') depth--;
                i--;
            }

            // Include preceding function identifiers (e.g. "sqrt" in "sqrt(16)") so function calls are intact.
            int fnEnd = i;
            while (i >= 0 && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i--;
            return i + 1;
        }

        // Simple token: scan backward across contiguous digits, decimal dots, or identifier characters.
        while (i >= 0 && (char.IsLetterOrDigit(text[i]) || text[i] == '.' || text[i] == '_'))
        {
            i--;
        }

        return i + 1;
    }

    /// <summary>
    /// Scans backward from an additive operator ('+' or '-') to capture the entire preceding term or
    /// cumulative subexpression A that the percentage modifies (for "A +/- B%" markup or discount).
    /// </summary>
    /// <remarks>
    /// In human desktop calculations like "50 * 2 + 10%", multiplication binds tighter than addition,
    /// so the user expects a 10% markup on the entire product (100 + 10 = 110), not merely the trailing factor "2".
    /// This scanner steps backward across all factors and tighter operators ('*', '/', '^'), stopping only
    /// at lower-precedence boundaries: unnested additive operators, comparisons, logical operators,
    /// function argument commas, or the enclosing subexpression / start of string.
    /// </remarks>
    private static int ScanAdditiveExpressionBackward(string text, int opIndex)
    {
        int i = opIndex - 1;
        while (i >= 0 && char.IsWhiteSpace(text[i])) i--;
        if (i < 0) return -1;

        int depth = 0;

        while (i >= 0)
        {
            char c = text[i];

            if (c == ')')
            {
                depth++;
                i--;
                continue;
            }

            if (c == '(')
            {
                if (depth > 0)
                {
                    depth--;
                    i--;
                    continue;
                }

                // Unnested '(': hit the enclosing subexpression or function call boundary (e.g. "min(10, 50 * 2 + 10%)").
                break;
            }

            // Inside nested parentheses, all contents belong to the operand subexpression.
            if (depth > 0)
            {
                i--;
                continue;
            }

            // Function argument boundary: commas separate sibling parameters, so term A cannot cross them.
            if (c == ',')
            {
                break;
            }

            // Comparison and logical boundaries: boolean/relational expressions bind looser than arithmetic.
            if (c is '=' or '<' or '>' or '&' or '|')
            {
                break;
            }

            // Preceding additive operator: an unnested binary '+' or '-' marks the start of the current term
            // (e.g. in "10 + 50 * 2 + 10%", term A is "50 * 2", not "10 + 50 * 2").
            // Unary signs (e.g. "-50 * 2") are retained so negative terms remain cohesive.
            if (c is '+' or '-' && !IsUnarySign(text, i))
            {
                break;
            }

            i--;
        }

        int start = i + 1;
        while (start < opIndex && char.IsWhiteSpace(text[start])) start++;
        return start;
    }

    /// <summary>
    /// Converts postfix factorial notation ('5!', '(2 + 3)!') into canonical prefix function calls ('factorial(5)').
    /// </summary>
    /// <remarks>
    /// In mathematics, factorial binds directly to its primary operand (a number, identifier, or parenthesized group)
    /// with higher precedence than multiplicative or additive operators. We iteratively replace from left to right,
    /// advancing searchStart past each replacement to avoid index desynchronization across variable-length substitutions.
    /// </remarks>
    private static string NormalizePostfixFactorials(string text)
    {
        int searchStart = 0;
        while (true)
        {
            int exclIdx = -1;
            for (int i = searchStart; i < text.Length; i++)
            {
                if (text[i] == '!')
                {
                    // Disambiguate against inequality comparison operator '!='.
                    if (i + 1 < text.Length && text[i + 1] == '=')
                    {
                        continue;
                    }

                    // A valid postfix factorial operator must be immediately preceded by an operand.
                    int prevIdx = i - 1;
                    while (prevIdx >= 0 && char.IsWhiteSpace(text[prevIdx])) prevIdx--;

                    if (prevIdx >= 0 && (char.IsLetterOrDigit(text[prevIdx]) || text[prevIdx] == ')'))
                    {
                        exclIdx = i;
                        break;
                    }
                }
            }

            if (exclIdx < 0)
            {
                break;
            }

            int pIdx = exclIdx - 1;
            while (pIdx >= 0 && char.IsWhiteSpace(text[pIdx])) pIdx--;

            int opStart = ScanOperandBackward(text, pIdx);
            if (opStart >= 0)
            {
                string operand = text[opStart..(pIdx + 1)].Trim();
                string replacement = $"factorial({operand})";
                text = string.Concat(text.AsSpan(0, opStart), replacement, text.AsSpan(exclIdx + 1));
                searchStart = opStart + replacement.Length;
            }
            else
            {
                searchStart = exclIdx + 1;
            }
        }

        return text;
    }

    private static string NormalizeImplicitMultiplication(string text)
    {
        var sb = new StringBuilder(text.Length * 2);

        for (int i = 0; i < text.Length; i++)
        {
            char current = text[i];
            sb.Append(current);

            if (i + 1 >= text.Length) break;
            char next = text[i + 1];

            // 1. ')' followed by '(' -> ') * ('
            if (current == ')' && next == '(')
            {
                sb.Append(" * ");
                continue;
            }

            // 2. ')' followed by digit or letter -> ') * 2' or ') * pi'
            if (current == ')' && (char.IsDigit(next) || char.IsLetter(next)))
            {
                sb.Append(" * ");
                continue;
            }

            // 3. Digit followed by '(' -> '2 * ('
            if (char.IsDigit(current) && next == '(')
            {
                sb.Append(" * ");
                continue;
            }

            // 4. Digit followed by letter (e.g. '2pi', '3sqrt(16)', '2x')
            // CRITICAL: Must not break scientific notation (e.g. '1e5', '2.5e-3', '0x100', '0b101')
            if (char.IsDigit(current) && char.IsLetter(next))
            {
                if (IsScientificNotationExponent(text, i + 1))
                {
                    continue;
                }

                if (IsHexOrBinaryLiteralPrefix(text, i))
                {
                    continue;
                }

                sb.Append(" * ");
                continue;
            }
        }

        return sb.ToString();
    }

    private static bool IsScientificNotationExponent(string text, int letterIndex)
    {
        char c = text[letterIndex];
        if (c != 'e' && c != 'E') return false;

        // In scientific notation, 'e' is followed by digits or optional '+' / '-' then digits
        int idx = letterIndex + 1;
        if (idx < text.Length && (text[idx] == '+' || text[idx] == '-'))
        {
            idx++;
        }

        return idx < text.Length && char.IsDigit(text[idx]);
    }

    private static bool IsHexOrBinaryLiteralPrefix(string text, int digitIndex)
    {
        // Check if digit is '0' and next is 'x', 'X', 'b', 'B', 'o', 'O'
        if (text[digitIndex] == '0' && digitIndex + 1 < text.Length)
        {
            char next = text[digitIndex + 1];
            if (next is 'x' or 'X' or 'b' or 'B' or 'o' or 'O')
            {
                return true;
            }
        }

        return false;
    }
}
