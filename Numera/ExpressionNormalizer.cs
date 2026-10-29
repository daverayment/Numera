using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Numera;

/// <summary>
/// Preprocesses and normalizes human desktop calculator and launcher expressions into
/// standard, unambiguous Numera mathematical grammar.
/// </summary>
/// <remarks>
/// Passes run in a fixed order: launcher equals, Unicode operators, division colons, then
/// culture and number formatting. Colons inside square brackets are left alone, so they
/// are never rewritten to division. Normalization changes character offsets, so positions
/// in later parse diagnostics and AST spans refer to the normalized text, not the
/// caller's original input. A source map is returned to translate normalized offsets back
/// to the original input.
/// </remarks>
public static class ExpressionNormalizer
{
    /// <summary>
    /// Normalizes the specified input string according to the provided options.
    /// Throws <see cref="ExpressionException"/> if unresolvable ambiguities are
    /// encountered.
    /// </summary>
    /// <param name="input">The raw user expression.</param>
    /// <param name="culture">The culture used to interpret decimal and grouping
    /// separators; null means invariant rules.</param>
    /// <param name="options">Optional normalization options; defaults to <see
    /// cref="NormalizationOptions.Default"/>.</param>
    /// <returns>The normalized expression string ready for evaluation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="input"/> is
    /// null.</exception>
    /// <exception cref="ExpressionException">Thrown if input syntax contains conflicting
    /// or ambiguous delimiters.</exception>
    public static string Normalize(
        string input,
        CultureInfo? culture = null,
        NormalizationOptions? options = null) =>
        Normalize(input, out _, culture, options);

    /// <summary>
    /// Attempts to normalize an input expression using the specified culture and
    /// normalization options.
    /// </summary>
    /// <param name="input">The expression text to normalize.</param>
    /// <param name="culture">The culture used for culture-sensitive normalization rules,
    /// or <see langword="null"/> to use the default behavior.</param>
    /// <param name="normalized">When the method returns <see langword="true"/>, contains
    /// the normalized expression; otherwise <see langword="null"/>.</param>
    /// <param name="diagnostic">When the method returns <see langword="false"/>, contains
    /// diagnostic information describing why normalization failed; otherwise
    /// <see langword="null"/>.</param>
    /// <param name="options">Optional settings that control normalization behavior. If
    /// <see langword="null"/>, default normalization options are used.</param>
    /// <returns><see langword="true"/> if normalization succeeds; otherwise,
    /// <see langword="false"/>.</returns>
    public static bool TryNormalize(
        string input,
        CultureInfo? culture,
        [NotNullWhen(true)] out string? normalized,
        [NotNullWhen(false)] out EvaluationDiagnostic? diagnostic,
        NormalizationOptions? options = null) =>
        TryNormalize(input, culture, out normalized, out diagnostic, out _, options);

    /// <summary>
    /// Normalizes the specified input string according to the provided options.
    /// Throws <see cref="ExpressionException"/> if unresolvable ambiguities are
    /// encountered.
    /// </summary>
    /// <param name="input">The raw user expression.</param>
    /// <param name="sourceMap">A map that translates positions in the normalized text
    /// back to the caller's original input.</param>
    /// <param name="culture">The culture used to interpret decimal and grouping
    /// separators; null means invariant rules.</param>
    /// <param name="options">Optional normalization options; defaults to <see
    /// cref="NormalizationOptions.Default"/>.</param>
    /// <returns>The normalized expression string ready for evaluation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="input"/> is
    /// null.</exception>
    /// <exception cref="ExpressionException">Thrown if input syntax contains conflicting
    /// or ambiguous delimiters.</exception>
    public static string Normalize(
        string input,
        out SourceMap sourceMap,
        CultureInfo? culture = null,
        NormalizationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!TryNormalize(input, culture, out var normalized, out var diagnostic, out sourceMap, options))
        {
            throw new ExpressionException(diagnostic);
        }

        return normalized;
    }

    /// <summary>
    /// Safely attempts to normalize an expression, reporting failures with the same
    /// <see cref="EvaluationDiagnostic"/> type the calculator uses. The only failure
    /// is null input; malformed numbers are not rejected here and surface as positioned
    /// parse diagnostics instead.
    /// </summary>
    /// <param name="input">The raw user expression. A null input is reported as a
    /// diagnostic rather than thrown.</param>
    /// <param name="culture">The culture used to interpret decimal and grouping
    /// separators; null means invariant rules.</param>
    /// <param name="normalized">On success, the normalized expression; otherwise
    /// <c>null</c>.</param>
    /// <param name="diagnostic">On failure, the structured error (
    /// <see cref="EvaluationError.InvalidSyntax"/> for null input); otherwise
    /// <c>null</c>.</param>
    /// <param name="sourceMap">On success, a map that translates positions in the
    /// normalized text back to the caller's original input; otherwise <c>null</c>.
    /// </param>
    /// <param name="options">Optional pass configuration; defaults to
    /// <see cref="NormalizationOptions.Default"/>.</param>
    /// <returns><c>true</c> if normalization succeeded; otherwise <c>false</c>.</returns>
    public static bool TryNormalize(
        string input,
        CultureInfo? culture,
        [NotNullWhen(true)] out string? normalized,
        [NotNullWhen(false)] out EvaluationDiagnostic? diagnostic,
        out SourceMap sourceMap,
        NormalizationOptions? options = null)
    {
        if (input is null)
        {
            normalized = null;
            sourceMap = SourceMap.Identity(0);
            diagnostic = new EvaluationDiagnostic(
                EvaluationError.InvalidSyntax, "Input expression cannot be null.");
            return false;
        }

        options ??= NormalizationOptions.Default;
        string current = input;

        // Maps are chained as each pass runs: every pass describes its output in terms of its own input,
        // and composing them yields output-to-original-input without re-deriving any offsets.
        sourceMap = SourceMap.Identity(input.Length);

        // Pass 1: Launcher quirks (leading / trailing '=').
        if (options.TrimLauncherEquals)
        {
            current = TrimLauncherEquals(current, out var passMap);
            sourceMap = passMap.Compose(sourceMap);
        }

        // Pass 2: Unicode and typographic operator sanitization.
        if (options.ReplaceUnicodeOperators)
        {
            current = SanitizeUnicodeOperators(current, out var passMap);
            sourceMap = passMap.Compose(sourceMap);
        }

        // Pass 3: European ratio colon as division.
        if (options.ReplaceDivisionColon)
        {
            current = ReplaceDivisionColons(current, out var passMap);
            sourceMap = passMap.Compose(sourceMap);
        }

        // Pass 4: Culture & number formatting (decimal comma, digit grouping, argument
        // semicolons).
        current = NormalizeCultureAndSeparators(current, culture, out var cultureMap);
        sourceMap = cultureMap.Compose(sourceMap);
        normalized = current;
        diagnostic = null;
        return true;
    }

    /// <summary>
    /// Trim leading and trailing characters that may be added by launchers or
    /// calculators, such as '='.
    /// </summary>
    private static string TrimLauncherEquals(string text, out SourceMap map)
    {
        var span = text.AsSpan().Trim();
        if (span.IsEmpty)
        {
            map = SourceMap.Slice(0, 0, text.Length);
            return string.Empty;
        }

        // Only leading removals move the start, so the offset is tracked here; trailing trims just shorten.
        int offset = text.Length - text.AsSpan().TrimStart().Length;

        // Leading activation '=' (e.g. '= 2 + 2'), but NOT '=='.
        if (span.StartsWith("=") && !span.StartsWith("=="))
        {
            var afterEquals = span[1..];
            span = afterEquals.TrimStart();
            offset += 1 + (afterEquals.Length - span.Length);
        }

        // Trailing calculator '=' (e.g. '2 + 2 ='), but NOT '==', '<=', '>=', '!=', '~='.
        if (span.EndsWith("=") && !span.EndsWith("==") && !span.EndsWith("<=") &&
            !span.EndsWith(">=") && !span.EndsWith("!=") && !span.EndsWith("~="))
        {
            span = span[..^1].TrimEnd();
        }

        map = SourceMap.Slice(offset, span.Length, text.Length);
        return span.ToString();
    }

    private static string SanitizeUnicodeOperators(string text, out SourceMap map)
    {
        var sb = new StringBuilder(text.Length);
        var starts = new List<int>(text.Length);
        var ends = new List<int>(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            int start = i;
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

                default:
                    sb.Append(c);
                    break;
            }

            // Every case appends exactly one character; for '**' the index was advanced, so the one '^'
            // covers both original stars.
            starts.Add(start);
            ends.Add(i + 1);
        }

        map = SourceMap.FromRanges(starts, ends, text.Length);
        return sb.ToString();
    }

    /// <summary>
    /// Rewrites European ratio colons to '/'. This is separate from the typographic pass because it
    /// depends on context, and it is deliberately conservative: anything that could be an identifier
    /// or launcher qualifier ("status:200", "pi:2") is left alone rather than guessed at.
    /// </summary>
    private static string ReplaceDivisionColons(string text, out SourceMap map)
    {
        // Rewrites a ':' to '/' in place, so every character keeps its offset and the identity map holds.
        map = SourceMap.Identity(text.Length);
        if (!text.Contains(':'))
        {
            return text;
        }

        // Brackets are reserved for future matrix syntax and are a syntax error today, so whatever is
        // inside them is left exactly as written for the parser to reject.
        var sb = new StringBuilder(text.Length);
        int bracketDepth = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '[') bracketDepth++;
            else if (c == ']') bracketDepth = Math.Max(0, bracketDepth - 1);

            sb.Append(c == ':' && bracketDepth == 0 && IsDivisionColon(text, i) ? '/' : c);
        }

        return sb.ToString();
    }

    private static bool IsDivisionColon(string text, int index)
    {
        int left = index - 1;
        while (left >= 0 && char.IsWhiteSpace(text[left])) left--;
        if (left < 0) return false;

        char prev = text[left];

        // A closing group is always a valid dividend: "(a+b):2", "log10(100):2".
        // Identifiers must be parenthesised to take part: "(pi):2".
        if (prev is ')')
        {
            return IsValidRightOperand(text.AsSpan(index + 1));
        }

        // The left term must be a complete numeric literal: identifier-like words such as "log10",
        // "item1", "x2" or "pi" are left alone.
        if (!EndsWithNumericLiteral(text.AsSpan(0, left + 1)))
        {
            return false;
        }

        return IsValidRightOperand(text.AsSpan(index + 1));
    }

    /// <summary>
    /// True when the span ends with a complete numeric literal. The whole trailing word is taken as one
    /// token and must be a single literal, which is what rejects identifiers such as "x2" or "win32".
    /// The walk deliberately stops at '+' and '-', so for "1e-5" only "5" is examined. That is safe
    /// because only the final operand next to the colon matters, and a trailing "5" is always a valid
    /// operand whatever precedes the sign.
    /// </summary>
    private static bool EndsWithNumericLiteral(ReadOnlySpan<char> span)
    {
        int start = span.Length;
        while (start > 0 && (IsWordChar(span[start - 1]) || span[start - 1] == '.'))
        {
            start--;
        }

        var token = span[start..];
        return TryConsumeNumber(token, out int consumed) && consumed == token.Length;
    }

    /// <summary>
    /// True when the divisor starts with an optionally signed complete numeric literal or an opening
    /// group. Uses the same literal grammar as the dividend, so the two sides cannot drift apart.
    /// </summary>
    private static bool IsValidRightOperand(ReadOnlySpan<char> span)
    {
        int i = 0;
        while (i < span.Length && char.IsWhiteSpace(span[i])) i++;
        if (i >= span.Length) return false;

        if (span[i] is '+' or '-')
        {
            i++;
            if (i >= span.Length) return false;
        }

        if (span[i] == '(')
        {
            return true;
        }

        // A literal that runs straight into letters ("2x", "2pi") is an implicit product, not a divisor.
        var rest = span[i..];
        return TryConsumeNumber(rest, out int consumed) &&
            (consumed == rest.Length || !IsWordChar(rest[consumed]));
    }

    /// <summary>
    /// Reads one numeric literal from the start of the span: 0x/0b/0o radix integers, or a decimal with an
    /// optional exponent. This is the single definition of "number" used for both sides of the colon.
    /// </summary>
    private static bool TryConsumeNumber(ReadOnlySpan<char> span, out int consumed)
    {
        consumed = 0;

        if (span.Length >= 3 && span[0] == '0' && IsRadixPrefix(span[1]))
        {
            char prefix = char.ToLowerInvariant(span[1]);
            int j = 2;
            while (j < span.Length && IsRadixDigit(prefix, span[j])) j++;

            if (j == 2) return false;
            consumed = j;
            return true;
        }

        int i = 0;
        bool hasDigits = false;
        while (i < span.Length && char.IsAsciiDigit(span[i]))
        {
            hasDigits = true;
            i++;
        }

        if (i < span.Length && span[i] == '.')
        {
            i++;
            while (i < span.Length && char.IsAsciiDigit(span[i]))
            {
                hasDigits = true;
                i++;
            }
        }

        if (!hasDigits) return false;

        // An exponent only counts if digits follow; otherwise the 'e' is left for the caller to reject.
        if (i < span.Length && (span[i] is 'e' or 'E'))
        {
            int j = i + 1;
            if (j < span.Length && (span[j] is '+' or '-')) j++;

            int digitsStart = j;
            while (j < span.Length && char.IsAsciiDigit(span[j])) j++;

            if (j > digitsStart) i = j;
        }

        consumed = i;
        return true;
    }

    private static bool IsRadixPrefix(char c) => char.ToLowerInvariant(c) is 'x' or 'b' or 'o';

    private static bool IsRadixDigit(char lowerPrefix, char c) => lowerPrefix switch
    {
        'x' => char.IsAsciiHexDigit(c),
        'b' => c is '0' or '1',
        _ => c is >= '0' and <= '7',
    };

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static string NormalizeCultureAndSeparators(
        string text,
        CultureInfo? culture,
        out SourceMap map)
    {
        // If culture is null, check for standard Swiss apostrophe or ISO thin space digit grouping
        // e.g., 1'000'000.50
        bool usesCommaDecimal = culture is not null && culture.NumberFormat.NumberDecimalSeparator == ",";

        var sb = new StringBuilder(text.Length);
        var starts = new List<int>(text.Length);
        var ends = new List<int>(text.Length);
        int parenDepth = 0;

        // Every kept or rewritten character maps 1:1 to the input character at the same index; deleted
        // grouping characters are simply never emitted, so they fall into the gaps between ranges.
        void Emit(char output, int index)
        {
            sb.Append(output);
            starts.Add(index);
            ends.Add(index + 1);
        }

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
                Emit(',', i);
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
                        Emit('.', i);
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

            Emit(c, i);
        }

        map = SourceMap.FromRanges(starts, ends, text.Length);
        return sb.ToString();
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
}
