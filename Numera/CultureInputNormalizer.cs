using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Numera;

internal sealed class CultureInputNormalizer
{
    private sealed class Container(bool isCall)
    {
        internal bool IsCall { get; } = isCall;
        internal bool HasSemicolon { get; set; }
    }

    private readonly string _text;
    private readonly CultureInfo _culture;
    private readonly string _decimalSeparator;
    private readonly string _groupSeparator;
    private readonly string _argumentSeparator;
    private readonly int[] _groupSizes;
    private readonly Dictionary<int, Container> _containers = [];
    private readonly StringBuilder _output;
    private readonly List<int> _starts = [];
    private readonly List<int> _ends = [];
    private EvaluationDiagnostic? _diagnostic;

    internal CultureInputNormalizer(string text, CultureInfo? culture)
    {
        _text = text;
        _culture = culture ?? CultureInfo.InvariantCulture;
        _decimalSeparator = _culture.NumberFormat.NumberDecimalSeparator;
        _groupSeparator = _culture.NumberFormat.NumberGroupSeparator;
        _groupSizes = _culture.NumberFormat.NumberGroupSizes;
        _argumentSeparator = _culture.TextInfo.ListSeparator;
        _output = new StringBuilder(text.Length);
    }

    internal bool TryNormalize(
        [NotNullWhen(true)] out string? normalized,
        out SourceMap map,
        [NotNullWhen(false)] out EvaluationDiagnostic? diagnostic)
    {
        FindContainers();
        var stack = new Stack<Container>();
        stack.Push(new Container(false));
        int index = 0;

        while (index < _text.Length && _diagnostic is null)
        {
            var container = stack.Peek();
            char c = _text[index];
            if (c == '[')
            {
                int end = BracketEnd(index);
                Copy(index, end);
                index = end;
            }
            else if (c == '(')
            {
                stack.Push(_containers[index]);
                Copy(index, index + 1);
                index++;
            }
            else if (c == ')')
            {
                if (stack.Count > 1)
                {
                    stack.Pop();
                }
                Copy(index, index + 1);
                index++;
            }
            else if (Identifier.IsIdentifierStart(c))
            {
                int start = index++;
                while (index < _text.Length && Identifier.IsIdentifierPart(_text[index]))
                {
                    index++;
                }
                Copy(start, index);
            }
            else if (char.IsAsciiDigit(c) || StartsLeadingDecimal(index))
            {
                ReadNumber(ref index, container);
            }
            else if (char.IsDigit(c))
            {
                Fail(index, 1, "Use ASCII digits 0 through 9 in numeric literals.");
            }
            else if (container.IsCall && Matches(index, ArgumentSeparator(container)))
            {
                int length = ArgumentSeparator(container).Length;
                Emit(',', index, length);
                index += length;
            }
            else if (container.IsCall && container.HasSemicolon &&
                !string.IsNullOrWhiteSpace(_argumentSeparator) &&
                _argumentSeparator != _decimalSeparator && _argumentSeparator != _groupSeparator &&
                Matches(index, _argumentSeparator))
            {
                Fail(index, _argumentSeparator.Length,
                    "Use ';' between arguments; do not mix argument-separator styles.",
                    EvaluationError.InvalidSyntax);
            }
            else if (c is ',' or ';')
            {
                if (c == ';' && !container.IsCall)
                {
                    Copy(index, index + 1);
                    index++;
                    continue;
                }

                Fail(index, 1,
                    $"Use '{ArgumentSeparator(container)}' between function arguments; do not mix argument-separator styles.",
                    EvaluationError.InvalidSyntax);
            }
            else if (c == '.' || Matches(index, _decimalSeparator) || GroupAt(index) is not null)
            {
                if (char.IsWhiteSpace(c))
                {
                    Copy(index, index + 1);
                    index++;
                }
                else
                {
                    Fail(index, 1, NumberMessage());
                }
            }
            else
            {
                Copy(index, index + 1);
                index++;
            }
        }

        diagnostic = _diagnostic;
        if (diagnostic is not null)
        {
            normalized = null;
            map = SourceMap.Identity(_text.Length);
            return false;
        }

        normalized = _output.ToString();
        map = SourceMap.FromRanges(_starts, _ends, _text.Length);

        return true;
    }

    // Establish each call's explicit separator style before interpreting its numbers.
    private void FindContainers()
    {
        var stack = new Stack<Container>();

        for (int index = 0; index < _text.Length; index++)
        {
            if (_text[index] == '[')
            {
                index = BracketEnd(index) - 1;
            }
            else if (_text[index] == '(')
            {
                int previous = index - 1;
                while (previous >= 0 && char.IsWhiteSpace(_text[previous]))
                {
                    previous--;
                }

                int start = previous;
                while (start >= 0 && (char.IsLetterOrDigit(_text[start]) || _text[start] == '_'))
                {
                    start--;
                }

                var word = _text.AsSpan(start + 1, previous - start);
                if (ExpressionNormalizer.TryConsumeNumber(word, out int consumed))
                {
                    word = word[consumed..];
                }

                bool isCall = !word.IsEmpty && Identifier.IsIdentifierStart(word[0]) &&
                    !KeywordCatalog.IsOperatorKeyword(word);
                var container = new Container(isCall);
                _containers.Add(index, container);
                stack.Push(container);
            }
            else if (_text[index] == ')' && stack.Count > 0)
            {
                stack.Pop();
            }
            else if (_text[index] == ';' && stack.TryPeek(out var container) && container.IsCall)
            {
                container.HasSemicolon = true;
            }
        }
    }

    private int BracketEnd(int start)
    {
        int depth = 1;
        int index = start + 1;
        while (index < _text.Length && depth > 0)
        {
            if (_text[index] == '[')
            {
                depth++;
            }
            else if (_text[index] == ']')
            {
                depth--;
            }

            index++;
        }

        return index;
    }

    private string ArgumentSeparator(Container container)
    {
        if (container.HasSemicolon)
        {
            return ";";
        }

        return _argumentSeparator;
    }

    private bool StartsLeadingDecimal(int index)
    {
        if (!Matches(index, _decimalSeparator) ||
            index + _decimalSeparator.Length >= _text.Length ||
            !char.IsAsciiDigit(_text[index + _decimalSeparator.Length]))
        {
            return false;
        }

        int previous = index - 1;
        while (previous >= 0 && char.IsWhiteSpace(_text[previous]))
        {
            previous--;
        }

        return previous < 0 || 
            (!char.IsLetterOrDigit(_text[previous]) && _text[previous] != '_');
    }

    private void ReadNumber(ref int index, Container container)
    {
        if (index + 1 < _text.Length && _text[index] == '0' &&
            _text[index + 1] is 'x' or 'X' or 'b' or 'B' or 'o' or 'O')
        {
            ReadRadix(ref index, container);
            return;
        }

        int start = index;
        var groups = new List<int>();
        var separators = new List<(int Position, int Length)>();
        string? groupingFamily = null;
        while (true)
        {
            int digits = index;
            ReadDigits(ref index);
            groups.Add(index - digits);
            var grouping = GroupAt(index);
            if (Matches(index, _decimalSeparator) || grouping is null)
            {
                break;
            }

            var (separator, family) = grouping.Value;
            bool followsDigit = index + separator.Length < _text.Length &&
                char.IsAsciiDigit(_text[index + separator.Length]);
            if (container.IsCall && separator == ArgumentSeparator(container))
            {
                int right = index + separator.Length;
                while (right < _text.Length && char.IsAsciiDigit(_text[right]))
                {
                    right++;
                }
                if (followsDigit && separators.Count == 0 &&
                    CanFormGroupedPair(groups[0], right - index - separator.Length, _text[start]))
                {
                    Fail(index, separator.Length,
                        $"Ambiguous separator '{separator}': it could group digits or " + 
                        "separate arguments. Use ';' between arguments, or remove " + 
                        "digit grouping.");
                }
                break;
            }

            if (!followsDigit && char.IsWhiteSpace(separator[0]))
            {
                break;
            }

            if (!followsDigit || (groupingFamily is not null && groupingFamily != family))
            {
                if (!followsDigit && container.IsCall && separator == "," && container.HasSemicolon)
                {
                    Fail(index, separator.Length,
                        "Use ';' between arguments; do not mix comma and semicolon " +
                        "argument separators.",
                        EvaluationError.InvalidSyntax);
                    return;
                }

                Fail(index, separator.Length,
                    "Invalid or mixed digit grouping. " + NumberMessage());
                return;
            }

            groupingFamily = family;
            separators.Add((index, separator.Length));
            index += separator.Length;
        }

        if (_diagnostic is not null || !ValidateGroups(start, groups, separators))
        {
            return;
        }

        if (Matches(index, _decimalSeparator))
        {
            if (_decimalSeparator == _groupSeparator && separators.Count == 0)
            {
                int right = index + _decimalSeparator.Length;
                while (right < _text.Length && char.IsAsciiDigit(_text[right]))
                {
                    right++;
                }

                if (CanFormGroupedPair(groups[0], right - index - _decimalSeparator.Length, _text[start]))
                {
                    Fail(index, _decimalSeparator.Length,
                        "Ambiguous number: this culture uses the same separator for " +
                        "decimals and digit grouping. Omit grouping or use raw " + 
                        "invariant input.");

                    return;
                }
            }

            if (container.IsCall && _decimalSeparator == ArgumentSeparator(container))
            {
                Fail(index, _decimalSeparator.Length,
                    "Ambiguous decimal separator in an argument list. Use ';' between " +
                    "arguments.");
                return;
            }

            int next = index + _decimalSeparator.Length;
            if ((next >= _text.Length || !char.IsAsciiDigit(_text[next])) &&
                container.IsCall && _decimalSeparator == ",")
            {
                Fail(index, _decimalSeparator.Length,
                    "Use ';' between function arguments; a decimal comma must touch " + 
                    "its digits.",
                    EvaluationError.InvalidSyntax);
                return;
            }

            Emit('.', index, _decimalSeparator.Length);
            index = next;
            ReadDigits(ref index);
            if (Matches(index, _decimalSeparator))
            {
                Fail(index, _decimalSeparator.Length,
                    "A number cannot contain more than one decimal separator. " +
                    NumberMessage());
                return;
            }

            ValidateNoGrouping(index, container);
            if (_diagnostic is not null)
            {
                return;
            }
        }

        if (index < _text.Length && _text[index] is 'e' or 'E')
        {
            int end = index + 1;
            if (end < _text.Length && _text[end] is '+' or '-')
            {
                end++;
            }

            int digits = end;
            while (end < _text.Length && char.IsAsciiDigit(_text[end]))
            {
                end++;
            }

            if (end > digits)
            {
                Copy(index, end);
                index = end;
                ValidateNoGrouping(index, container);
            }
        }
    }

    private void ValidateNoGrouping(int index, Container container)
    {
        var grouping = GroupAt(index);
        if (grouping is { } separator &&
            !(container.IsCall && separator.Separator == ArgumentSeparator(container)) &&
            index + separator.Separator.Length < _text.Length &&
            char.IsAsciiDigit(_text[index + separator.Separator.Length]))
        {
            Fail(index, separator.Separator.Length,
                "Digit grouping is allowed only in the integer part of a number.");
        }
    }

    private bool CanFormGroupedPair(int leftDigits, int rightDigits, char first)
    {
        if (leftDigits == 0 || _groupSizes.Length == 0 || _groupSizes[0] == 0 ||
            first == '0' || rightDigits != _groupSizes[0])
        {
            return false;
        }

        int leftLimit = _groupSizes[Math.Min(1, _groupSizes.Length - 1)];
        return leftLimit == 0 || leftDigits <= leftLimit;
    }

    private bool ValidateGroups(int start, List<int> groups, List<(int Position, int Length)> separators)
    {
        if (separators.Count == 0)
        {
            return true;
        }

        int sizeIndex = 0;
        for (int group = groups.Count - 1; group > 0; group--)
        {
            if (_groupSizes.Length == 0 || _groupSizes[sizeIndex] == 0 || groups[group] != _groupSizes[sizeIndex])
            {
                var sep = separators[group - 1];
                Fail(sep.Position, sep.Length, "Invalid digit-group size. " + NumberMessage());
                return false;
            }

            sizeIndex = Math.Min(sizeIndex + 1, _groupSizes.Length - 1);
        }

        if (_text[start] == '0' || (_groupSizes[sizeIndex] != 0 && groups[0] > _groupSizes[sizeIndex]))
        {
            var sep = separators[0];
            Fail(sep.Position, sep.Length,
                "A grouped integer must have a correctly sized first group without leading zeroes. " + NumberMessage());
            return false;
        }

        return true;
    }

    private void ReadRadix(ref int index, Container container)
    {
        int start = index;
        int length = 2;
        if (ExpressionNormalizer.TryConsumeNumber(_text.AsSpan(index), out int consumed) && consumed > 2)
        {
            length = consumed;
        }

        index += length;
        Copy(start, index);
        var grouping = GroupAt(index);
        if (grouping is { } separator &&
            !(container.IsCall && separator.Separator == ArgumentSeparator(container)) &&
            index + separator.Separator.Length < _text.Length &&
            char.IsAsciiDigit(_text[index + separator.Separator.Length]))
        {
            Fail(index, separator.Separator.Length,
                "Digit grouping is not supported in radix literals. Remove the " +
                "grouping separator.");
        }
    }

    private (string Separator, string Family)? GroupAt(int index)
    {
        if (index >= _text.Length)
        {
            return null;
        }

        if (_text[index] is '\'' or '\u2019')
        {
            return (_text[index].ToString(), "apostrophe");
        }

        if (_text[index] is ' ' or '\u00A0' or '\u202F' or '\u2009')
        {
            return (_text[index].ToString(), "space");
        }

        if (_groupSeparator.Length > 0 && Matches(index, _groupSeparator))

        {
            return (_groupSeparator, "native");
        }

        return null;
    }

    private bool Matches(int index, string token) =>
        token.Length > 0 && _text.AsSpan(index).StartsWith(token, StringComparison.Ordinal);

    private void ReadDigits(ref int index)
    {
        int start = index;
        while (index < _text.Length && char.IsAsciiDigit(_text[index]))
        {
            index++;
        }

        Copy(start, index);
    }

    private void Copy(int start, int end)
    {
        for (int index = start; index < end; index++)
        {
            Emit(_text[index], index, 1);
        }
    }

    private void Emit(char character, int start, int length)
    {
        _output.Append(character);
        _starts.Add(start);
        _ends.Add(start + length);
    }

    private string NumberMessage()
    {
        string cultureName = _culture.Name;
        if (cultureName.Length == 0)
        {
            cultureName = "invariant";
        }

        return $"Use '{_decimalSeparator}' for decimals and '{_argumentSeparator}' " +
            $"between arguments for culture '{cultureName}'.";
    }

    private void Fail(int position, int length, string message, EvaluationError code = EvaluationError.InvalidNumberFormat)
    {
        _diagnostic = new EvaluationDiagnostic(code, message, position, length);
    }
}
