using Numera.Ast;

namespace Numera;

/// <summary>
/// Maps positions and spans in normalized text back to the caller's original input,
/// ensuring  diagnostics, tooltips, and AST spans align with the characters the user
/// typed.
/// </summary>
/// <remarks>
/// <para>
/// Each character in the normalized text records the half-open range <c>[start, end)</c>
/// it occupied in the original text. Tracking both bounds (not just a start) enables
/// N:1 contractions, such as <c>^</c> from <c>**</c>, to span the full original token
/// width.
/// </para>
/// <para>
/// Characters eliminated by normalization (such as trimmed launcher equals, grouping
/// separators, trimmed whitespace) have no individual entry; they sit in the gaps between
/// ranges and are automatically enclosed whenever a span encompasses the surviving
/// characters flanking them.
/// </para>
/// <para>
/// Ranges are strictly monotonic: each character's start is at least the previous
/// character's end. An unallocated (<c>null</c>) array pair denotes an identity mapping,
/// ensuring passes that leave text unchanged incur zero allocations.
/// </para>
/// </remarks>
public sealed class SourceMap
{
    private readonly int[]? _starts;
    private readonly int[]? _ends;
    private readonly int _originalLength;

    private SourceMap(int[]? starts, int[]? ends, int originalLength)
    {
        _starts = starts;
        _ends = ends;
        _originalLength = originalLength;
    }

    /// <summary>
    /// Gets an identity map for unchanged text, where every position and span maps
    /// directly to itself. Incurs zero heap allocations.
    /// </summary>
    public static SourceMap Identity(int length) => new(null, null, length);

    /// <summary>
    /// Builds a map from per-character original ranges. Falls back to the identity map
    /// when every character maps to itself, which keeps unchanged text allocation-free
    /// downstream.
    /// </summary>
    /// <param name="starts">Inclusive zero-based start offset in the original text for
    /// each normalized character.</param>
    /// <param name="ends">Exclusive zero-based end offset in the original text for each
    /// normalized character.
    /// </param>
    /// <param name="originalLength">Total length of the original text.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="starts"/> and
    /// <paramref name="ends"/> counts differ.</exception>
    public static SourceMap FromRanges(List<int> starts, List<int> ends, int originalLength)
    {
        if (starts.Count != ends.Count)
        {
            throw new ArgumentException("Start and end entries must pair up.", nameof(ends));
        }

        bool identity = starts.Count == originalLength;
        for (int i = 0; identity && i < starts.Count; i++)
        {
            identity = starts[i] == i && ends[i] == i + 1;
        }

        return identity ? Identity(originalLength) : new SourceMap([.. starts], [.. ends], originalLength);
    }

    /// <summary>
    /// Builds an optimized map for text that is one contiguous, unmodified slice of the
    /// original, such as the result of trimming equals signs or outer whitespace.
    /// Returns <see cref="Identity"/> when no characters were trimmed.
    /// </summary>
    /// <param name="offset">Starting character offset of the slice within the original
    /// text.</param>
    /// <param name="length">Length of the slice.</param>
    /// <param name="originalLength">Total length of the original text before slicing.
    /// </param>
    public static SourceMap Slice(int offset, int length, int originalLength)
    {
        if (offset == 0 && length == originalLength)
        {
            return Identity(originalLength);
        }

        var starts = new int[length];
        var ends = new int[length];
        for (int i = 0; i < length; i++)
        {
            starts[i] = offset + i;
            ends[i] = offset + i + 1;
        }

        return new SourceMap(starts, ends, originalLength);
    }

    /// <summary>
    /// Gets a value indicating whether this map leaves all positions and spans unchanged.
    /// </summary>
    public bool IsIdentity => _starts is null;

    /// <summary>
    /// Translates a position in normalized text to the matching position in the original
    /// input. Positions at or beyond the end map to the end of the original.
    /// </summary>
    /// <param name="normalizedPosition">The position in normalized text coordinates.</param>
    /// <returns>The corresponding position in the original text.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when
    /// <paramref name="normalizedPosition"/> is negative.</exception>
    public int MapPosition(int normalizedPosition)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(normalizedPosition);

        if (_starts is null)
        {
            return normalizedPosition;
        }

        if (normalizedPosition < _starts.Length)
        {
            return _starts[normalizedPosition];
        }

        // Exactly at EOF of the normalized expression:
        // Map to the end of the last surviving character, not past any trailing trimmed
        // whitespace or launcher '='.
        if (normalizedPosition == _starts.Length && _ends is { Length: > 0})
        {
            return _ends[^1];
        }

        return _originalLength;
    }

    /// <summary>
    /// Translates a span in normalized text to the encompassing span in the original
    /// input.
    /// </summary>
    /// <remarks>
    /// A zero-length span remains zero-length at the mapped start position. Any other
    /// span covers from the start of its first character's original range to the
    /// exclusive end of its last character's original range, absorbing any deleted
    /// characters located within it.
    /// </remarks>
    /// <param name="normalized">The source span in normalized text coordinates.</param>
    /// <returns>A <see cref="TextSpan"/> covering the corresponding range in the original
    /// text.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when
    /// <paramref name="normalized"/> has a negative start or length.</exception>
    public TextSpan MapSpan(TextSpan normalized)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(normalized.Start);
        ArgumentOutOfRangeException.ThrowIfNegative(normalized.Length);

        if (_starts is null || _ends is null)
        {
            return normalized;
        }

        int start = MapPosition(normalized.Start);
        if (normalized.Length == 0 || normalized.Start >= _starts.Length)
        {
            return new TextSpan(start, 0);
        }

        int last = Math.Min(normalized.Start + normalized.Length, _starts.Length) - 1;
        return new TextSpan(start, _ends[last] - start);
    }

    /// <summary>
    /// Chains two maps. <see langword="this"/> maps the final text to an intermediate
    /// text, and <paramref name="inner"/> maps that intermediate text to the original;
    /// the result maps the final text straight to the original.
    /// </summary>
    public SourceMap Compose(SourceMap inner)
    {
        ArgumentNullException.ThrowIfNull(inner);

        if (inner.IsIdentity)
        {
            return this;
        }

        if (_starts is null || _ends is null)
        {
            return inner;
        }

        var starts = new int[_starts.Length];
        var ends = new int[_starts.Length];
        for (int i = 0; i < starts.Length; i++)
        {
            var mapped = inner.MapSpan(new TextSpan(_starts[i], _ends[i] - _starts[i]));
            starts[i] = mapped.Start;
            ends[i] = mapped.Start + mapped.Length;
        }

        return new SourceMap(starts, ends, inner._originalLength);
    }
}
