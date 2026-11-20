namespace Numera;

/// <summary>
/// An immutable snapshot of an answer's prepared presentations.
/// Reading its properties does not perform formatting.
/// </summary>
/// <remarks>
/// Use <see cref="EvaluationResult.FormatResult"/> or the engine or facade formatting
/// methods to create a snapshot. The primary display and machine-readable text are
/// always present. Null alternatives mean the display is unavailable for this value.
/// Text is not withheld because of its length; callers decide what fits their display.
/// </remarks>
public sealed record FormattedResult
{
    /// <summary>
    /// Gets the calculated value.
    /// </summary>
    public MathValue Value { get; }

    /// <summary>
    /// Gets the prepared primary human-readable display, without performing formatting.
    /// </summary>
    public string PrimaryDisplay { get; }

    /// <summary>
    /// Gets invariant text preserving the stored numerical value.
    /// Non-finite tokens are diagnostic text, not re-evaluable expressions.
    /// </summary>
    public string MachineReadable { get; }

    /// <summary>
    /// Gets hexadecimal display text, or null if the value is not integral and finite.
    /// </summary>
    public string? HexDisplay { get; }

    /// <summary>
    /// Gets binary display text, or null if the value is not integral and finite.
    /// </summary>
    public string? BinaryDisplay { get; }

    /// <summary>
    /// Gets octal display text, or null if the value is not integral and finite.
    /// </summary>
    public string? OctalDisplay { get; }

    /// <summary>
    /// Gets variables assigned by the calculation, if any.
    /// </summary>
    public VariableCollection? AssignedVariables { get; }

    /// <summary>
    /// Gets whether the primary display rounded the stored value.
    /// Use this for a rounded-display indicator, not to infer calculation exactness:
    /// 1/3 is Exact with a rounded display; ln(1) is Unverified without display rounding.
    /// Inspect <see cref="MathValue.Exactness"/> for a separate exactness indicator.
    /// </summary>
    public bool IsDisplayRounded { get; }

    /// <summary>
    /// Gets exact fraction display text, or null if an exact fraction is unavailable.
    /// </summary>
    public string? ExactFractionDisplay { get; }

    /// <summary>
    /// Gets whether an exact fraction display is available, for example to enable a UI toggle.
    /// </summary>
    public bool HasExactFraction => ExactFractionDisplay is not null;

    internal FormattedResult(
        MathValue value,
        string primaryDisplay,
        string machineReadable,
        string? hexDisplay = null,
        string? binaryDisplay = null,
        string? octalDisplay = null,
        VariableCollection? assignedVariables = null,
        bool isDisplayRounded = false,
        string? exactFractionDisplay = null)
    {
        ArgumentNullException.ThrowIfNull(primaryDisplay);
        ArgumentNullException.ThrowIfNull(machineReadable);

        Value = value;
        PrimaryDisplay = primaryDisplay;
        MachineReadable = machineReadable;
        HexDisplay = hexDisplay;
        BinaryDisplay = binaryDisplay;
        OctalDisplay = octalDisplay;
        AssignedVariables = assignedVariables;
        IsDisplayRounded = isDisplayRounded;
        ExactFractionDisplay = exactFractionDisplay;
    }

    /// <inheritdoc/>
    override public string ToString() => PrimaryDisplay;
}
