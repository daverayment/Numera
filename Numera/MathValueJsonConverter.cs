using System.Text.Json;
using System.Text.Json.Serialization;

namespace Numera;

/// <summary>
/// Serializes a MathValue using its canonical, precision-preserving JSON object format.
/// Registered automatically on <see cref="MathValue"/>.
/// </summary>
/// <remarks>
/// Preserves representation, exactness, and decimal display scale without reading
/// mismatched storage getters. Wire field names and numeric enum encodings are fixed,
/// independent of serializer naming policies. Reads use the hard numeric limits and a
/// maximum of 25,000 UTF-8 bytes per value, excluding surrounding whitespace.
/// Invalid value data throws JsonException; calculation-limit violations retain
/// CalculationLimitException. Whole results, options, and sessions require separate
/// application-defined serialization contracts.
/// </remarks>
public sealed class MathValueJsonConverter : JsonConverter<MathValue>
{
    /// <inheritdoc />
    public override MathValue Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        try
        {
            return MathValue.ReadJson(ref reader);
        }
        catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException or DivideByZeroException)
        {
            throw new JsonException("Invalid MathValue JSON.", exception);
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, MathValue value, JsonSerializerOptions options) =>
        value.WriteJson(writer);
}
