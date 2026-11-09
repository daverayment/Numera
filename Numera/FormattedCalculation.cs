namespace Numera;

/// <summary>
/// Represents a structured, presentation-ready calculation result formatted for user
/// interfaces, desktop launchers, and clipboard copying.
/// </summary>
/// <param name="Value">The raw calculated <see cref="MathValue"/>.</param>
/// <param name="PrimaryDisplay">The primary human-facing formatted representation (using
/// the specified or current culture).</param>
/// <param name="MachineReadable">The lossless, culture-invariant numeric expression,
/// preserving decimal scale where representable, independently of display rounding.</param>
/// <param name="HexRepresentation">The prefixed hexadecimal string representation (e.g.
/// "0x44C"), or <c>null</c> if the value is not an integer.</param>
/// <param name="BinaryRepresentation">The prefixed binary string representation (e.g.
/// "0b10001001100"), or <c>null</c> if the value is not an integer.</param>
/// <param name="OctalRepresentation">The prefixed octal string representation (e.g.
/// "0o2114"), or <c>null</c> if the value is not an integer.</param>
/// <param name="AssignedVariables">The variables assigned by the expression, if any.
/// </param>
/// <param name="IsDisplayRounded">Whether the numeric display rounds the stored value.
/// This does not establish the accuracy of the calculation.</param>
/// <param name="FractionRepresentation">The reduced invariant exact fraction, or
/// <c>null</c> when calculation accuracy is unverified.</param>
public sealed record FormattedCalculation(
    MathValue Value,
    string PrimaryDisplay,
    string MachineReadable,
    string? HexRepresentation = null,
    string? BinaryRepresentation = null,
    string? OctalRepresentation = null,
    VariableCollection? AssignedVariables = null,
    bool IsDisplayRounded = false,
    string? FractionRepresentation = null)
{
    /// <summary>
    /// Gets a formatted summary of assigned variables.
    /// </summary>
    public string? AssignmentSummary => AssignedVariables?.ToString();

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
}
