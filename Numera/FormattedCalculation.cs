namespace Numera;

/// <summary>
/// Represents a structured, presentation-ready calculation result formatted for user interfaces,
/// desktop launchers, and clipboard copying.
/// </summary>
/// <param name="Value">The raw calculated <see cref="MathValue"/>.</param>
/// <param name="PrimaryDisplay">The primary human-facing formatted representation (using the specified or current culture).</param>
/// <param name="MachineReadable">The culture-invariant, copy-pasteable canonical string representation.</param>
/// <param name="HexRepresentation">The prefixed hexadecimal string representation (e.g. "0x44C"), or <c>null</c> if the value is not an integer.</param>
/// <param name="BinaryRepresentation">The prefixed binary string representation (e.g. "0b10001001100"), or <c>null</c> if the value is not an integer.</param>
/// <param name="OctalRepresentation">The prefixed octal string representation (e.g. "0o2114"), or <c>null</c> if the value is not an integer.</param>
public sealed record FormattedCalculation(
    MathValue Value,
    string PrimaryDisplay,
    string MachineReadable,
    string? HexRepresentation = null,
    string? BinaryRepresentation = null,
    string? OctalRepresentation = null);
