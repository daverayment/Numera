namespace Numera;

/// <summary>
/// An immutable snapshot of an answer's prepared presentations.
/// Reading its properties does not perform formatting.
/// </summary>
/// <remarks>
/// The primary display and machine-readable text are always present. Null alternatives
/// mean the representation is unavailable for this value. Text is not withheld because
/// of its length; callers decide what fits their display.
/// </remarks>
public sealed record FormattedCalculation
{
    /// <summary>
    /// Gets the calculated value.
    /// </summary>
    public MathValue Value { get; }

    /// <summary>
    /// Gets the primary human-readable display.
    /// </summary>
    public string PrimaryDisplay { get; }

    /// <summary>
    /// Gets invariant text preserving the stored numerical value.
    /// Non-finite tokens are diagnostic text, not re-evaluable expressions.
    /// </summary>
    public string MachineReadable { get; }

    /// <summary>
    /// Gets hexadecimal text, or null if the value is not integral and finite.
    /// </summary>
    public string? HexRepresentation { get; }

    /// <summary>
    /// Gets binary text, or null if the value is not integral and finite.
    /// </summary>
    public string? BinaryRepresentation { get; }

    /// <summary>
    /// Gets octal text, or null if the value is not integral and finite.
    /// </summary>
    public string? OctalRepresentation { get; }

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
    /// Gets exact fraction text, or null if an exact fraction is unavailable.
    /// </summary>
    public string? FractionRepresentation { get; }

    /// <summary>
    /// Gets the prepared assignment summary, or null if absent.
    /// </summary>
    public string? AssignmentSummary { get; }

    /// <summary>
    /// Creates a snapshot from prepared representations. If an assignment summary is not
    /// supplied, prepares it using current culture. Numeric text is stored as supplied.
    /// </summary>
    /// <param name="value">The calculated value.</param>
    /// <param name="primaryDisplay">The primary display.</param>
    /// <param name="machineReadable">The invariant numerical expression.</param>
    /// <param name="hexRepresentation">The hexadecimal alternative, or null.</param>
    /// <param name="binaryRepresentation">The binary alternative, or null.</param>
    /// <param name="octalRepresentation">The octal alternative, or null.</param>
    /// <param name="assignedVariables">The assigned variables, if any.</param>
    /// <param name="isDisplayRounded">Whether the primary display rounded the value.</param>
    /// <param name="fractionRepresentation">The exact fraction alternative, or null.</param>
    /// <param name="assignmentSummary">The prepared assignment summary, or null.</param>
    /// <exception cref="ArgumentNullException">Required display text is null.</exception>
    public FormattedCalculation(
        MathValue value,
        string primaryDisplay,
        string machineReadable,
        string? hexRepresentation = null,
        string? binaryRepresentation = null,
        string? octalRepresentation = null,
        VariableCollection? assignedVariables = null,
        bool isDisplayRounded = false,
        string? fractionRepresentation = null,
        string? assignmentSummary = null)
    {
        ArgumentNullException.ThrowIfNull(primaryDisplay);
        ArgumentNullException.ThrowIfNull(machineReadable);

        Value = value;
        PrimaryDisplay = primaryDisplay;
        MachineReadable = machineReadable;
        HexRepresentation = hexRepresentation;
        BinaryRepresentation = binaryRepresentation;
        OctalRepresentation = octalRepresentation;
        AssignedVariables = assignedVariables;
        IsDisplayRounded = isDisplayRounded;
        FractionRepresentation = fractionRepresentation;
        AssignmentSummary = assignmentSummary ?? assignedVariables?.FormatSummary(null);
    }

    /// <summary>Deconstructs the original seven-field presentation surface.</summary>
    /// <param name="value">The calculated value.</param>
    /// <param name="primaryDisplay">The numeric display.</param>
    /// <param name="machineReadable">The invariant expression.</param>
    /// <param name="hexRepresentation">The hexadecimal alternate.</param>
    /// <param name="binaryRepresentation">The binary alternate.</param>
    /// <param name="octalRepresentation">The octal alternate.</param>
    /// <param name="assignedVariables">The assigned variables.</param>
    public void Deconstruct(
        out MathValue value,
        out string primaryDisplay,
        out string machineReadable,
        out string? hexRepresentation,
        out string? binaryRepresentation,
        out string? octalRepresentation,
        out VariableCollection? assignedVariables)
    {
        value = Value;
        primaryDisplay = PrimaryDisplay;
        machineReadable = MachineReadable;
        hexRepresentation = HexRepresentation;
        binaryRepresentation = BinaryRepresentation;
        octalRepresentation = OctalRepresentation;
        assignedVariables = AssignedVariables;
    }

    /// <inheritdoc/>
    override public string ToString() => PrimaryDisplay;
}
