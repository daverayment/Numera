using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Numera.Tests;

[TestClass]
public sealed class MathValueJsonConverterTests
{
    public sealed record HistoryEntry(string Expression, MathValue Value);

    [TestMethod]
    public void CanonicalPayloadRetainsTheExistingFieldNamesAndEncodings()
    {
        const string expected = "{\"Kind\":0,\"Exactness\":1,\"Value\":\"1\",\"Numerator\":null,\"Denominator\":null,\"DecimalDisplayScale\":0}";
        Assert.AreEqual(expected, JsonSerializer.Serialize(MathValue.One));
        Assert.AreEqual(expected, MathValue.One.Serialize());
        AssertStoredValue(MathValue.One, JsonSerializer.Deserialize<MathValue>(expected));
    }

    [TestMethod]
    public void DirectSerializationUsesTheExistingCanonicalObjectFormat()
    {
        foreach (var value in SampleValues())
        {
            string json = JsonSerializer.Serialize(value);
            Assert.AreEqual(value.Serialize(), json);
            using var document = JsonDocument.Parse(json);
            Assert.AreEqual(JsonValueKind.Object, document.RootElement.ValueKind);
            Assert.IsFalse(document.RootElement.TryGetProperty("IntegerValue", out _));
            Assert.IsFalse(document.RootElement.TryGetProperty("DecimalValue", out _));
            Assert.IsFalse(document.RootElement.TryGetProperty("DoubleValue", out _));
            AssertStoredValue(value, JsonSerializer.Deserialize<MathValue>(json));
            AssertStoredValue(value, MathValue.Deserialize(json));
            AssertStoredValue(value, JsonSerializer.Deserialize<MathValue>(value.Serialize()));
        }
    }

    [TestMethod]
    public void ValuesRoundTripInsideApplicationHistoryArraysAndDictionaries()
    {
        var values = SampleValues();
        var history = values.Select(value => new HistoryEntry("saved expression", value)).ToArray();
        string json = JsonSerializer.Serialize(history);
        var restored = JsonSerializer.Deserialize<HistoryEntry[]>(json)!;
        Assert.HasCount(history.Length, restored);
        for (int i = 0; i < history.Length; i++)
        {
            Assert.AreEqual(history[i].Expression, restored[i].Expression);
            AssertStoredValue(history[i].Value, restored[i].Value);
        }

        var dictionary = new Dictionary<string, MathValue>
        {
            ["zero"] = MathValue.Zero,
            ["third"] = MathValue.FromFraction(1, 3),
            ["nan"] = MathValue.NaN
        };
        var restoredDictionary = JsonSerializer.Deserialize<Dictionary<string, MathValue>>(
            JsonSerializer.Serialize(dictionary))!;
        foreach (var (name, value) in dictionary)
        {
            AssertStoredValue(value, restoredDictionary[name]);
        }

        var restoredArray = JsonSerializer.Deserialize<MathValue[]>(JsonSerializer.Serialize(values))!;
        Assert.HasCount(values.Length, restoredArray);
        for (int i = 0; i < values.Length; i++)
        {
            AssertStoredValue(values[i], restoredArray[i]);
        }
    }

    [TestMethod]
    public void SourceGeneratedSerializationSupportsValuesAndApplicationHistory()
    {
        foreach (var value in SampleValues())
        {
            string json = JsonSerializer.Serialize(value, MathValueTestJsonContext.Default.MathValue);
            Assert.AreEqual(value.Serialize(), json);
            AssertStoredValue(value, JsonSerializer.Deserialize(json, MathValueTestJsonContext.Default.MathValue));

            var history = new HistoryEntry("saved", value);
            string historyJson = JsonSerializer.Serialize(history, MathValueTestJsonContext.Default.HistoryEntry);
            var restored = JsonSerializer.Deserialize(historyJson, MathValueTestJsonContext.Default.HistoryEntry)!;
            Assert.AreEqual("saved", restored.Expression);
            AssertStoredValue(value, restored.Value);
        }
    }

    [TestMethod]
    public async Task StreamSerializationHandlesLargeValuesWithoutReplayingExpressions()
    {
        var component = BigInteger.One << (CalculationLimits.MaxIntegerBits - 1);
        var value = MathValue.FromFraction(component + 1, component);
        var history = new HistoryEntry(new string('x', 30000), value);
        using var stream = new MemoryStream();
        await JsonSerializer.SerializeAsync(stream, history, MathValueTestJsonContext.Default.HistoryEntry);
        Assert.IsGreaterThan(25000, stream.Length);
        stream.Position = 0;
        var restored = await JsonSerializer.DeserializeAsync(stream, MathValueTestJsonContext.Default.HistoryEntry);
        Assert.IsNotNull(restored);
        Assert.AreEqual(history.Expression, restored.Expression);
        AssertStoredValue(value, restored.Value);
    }

    [TestMethod]
    public void NullableValuesSupportNullWithoutTreatingNullAsNumericZero()
    {
        Assert.AreEqual("null", JsonSerializer.Serialize<MathValue?>(null));
        Assert.IsNull(JsonSerializer.Deserialize<MathValue?>("null"));
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<MathValue>("null"));
        var restored = JsonSerializer.Deserialize<MathValue?>(JsonSerializer.Serialize<MathValue?>(MathValue.One));
        Assert.IsTrue(restored.HasValue);
        AssertStoredValue(MathValue.One, restored.Value);
    }

    [TestMethod]
    public void SerializerPoliciesDoNotChangeTheCanonicalValueSchema()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            IgnoreReadOnlyProperties = true,
            Converters = { new JsonStringEnumConverter() }
        };
        var history = new HistoryEntry("1/3", MathValue.FromFraction(1, 3));
        string json = JsonSerializer.Serialize(history, options);
        using var document = JsonDocument.Parse(json);
        var payload = document.RootElement.GetProperty("value");
        Assert.AreEqual(history.Value.Serialize(), payload.GetRawText());
        Assert.AreEqual(JsonValueKind.Number, payload.GetProperty("Kind").ValueKind);
        Assert.IsFalse(payload.TryGetProperty("kind", out _));
        var restored = JsonSerializer.Deserialize<HistoryEntry>(json, options)!;
        AssertStoredValue(history.Value, restored.Value);

        options = new JsonSerializerOptions { WriteIndented = true };
        string indented = JsonSerializer.Serialize(history.Value, options);
        Assert.IsTrue(indented.Contains('\n'));
        AssertStoredValue(history.Value, JsonSerializer.Deserialize<MathValue>(indented, options));
    }

    [TestMethod]
    public void SerializationIsIndependentOfCurrentCultureAndRetainsDisplayScale()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            var value = new MathValue(194.40m);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            string json = JsonSerializer.Serialize(value);
            Assert.AreEqual(value.Serialize(), json);
            using var document = JsonDocument.Parse(json);
            Assert.AreEqual("194.40", document.RootElement.GetProperty("Value").GetString());
            Assert.AreEqual(2, document.RootElement.GetProperty("DecimalDisplayScale").GetInt32());
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var restored = JsonSerializer.Deserialize<MathValue>(json);
            AssertStoredValue(value, restored);
            Assert.AreEqual("194.40", Calculator.FormatResult(restored).PrimaryDisplay);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
    [DataRow("{")]
    [DataRow("{}")]
    [DataRow("42")]
    [DataRow("[]")]
    [DataRow("{\"Kind\":0,\"Value\":\"1\"}")]
    [DataRow("{\"Exactness\":1,\"Value\":\"1\"}")]
    [DataRow("{\"Kind\":0,\"Exactness\":1}")]
    [DataRow("{\"Kind\":0,\"Exactness\":1,\"Value\":null}")]
    [DataRow("{\"Kind\":0,\"Exactness\":1,\"Value\":\"01\"}")]
    [DataRow("{\"Kind\":100,\"Exactness\":1,\"Value\":\"1\"}")]
    [DataRow("{\"Kind\":0,\"Exactness\":100,\"Value\":\"1\"}")]
    [DataRow("{\"Kind\":0,\"Exactness\":1,\"Value\":\"1\",\"DecimalDisplayScale\":-1}")]
    [DataRow("{\"Kind\":0,\"Exactness\":1,\"Value\":\"1\",\"DecimalDisplayScale\":30}")]
    [DataRow("{\"Kind\":2,\"Exactness\":0,\"Value\":\"1\",\"DecimalDisplayScale\":1}")]
    [DataRow("{\"Kind\":2,\"Exactness\":1,\"Value\":\"NaN\"}")]
    [DataRow("{\"Kind\":2,\"Exactness\":1,\"Value\":\"Infinity\"}")]
    [DataRow("{\"Kind\":2,\"Exactness\":1,\"Value\":\"-Infinity\"}")]
    [DataRow("{\"Kind\":1,\"Exactness\":1,\"Value\":\"1e100\"}")]
    [DataRow("{\"Kind\":3,\"Exactness\":1,\"Value\":\"1/3\",\"Numerator\":\"1\"}")]
    [DataRow("{\"Kind\":3,\"Exactness\":1,\"Value\":\"1/0\",\"Numerator\":\"1\",\"Denominator\":\"0\"}")]
    [DataRow("{\"Kind\":3,\"Exactness\":1,\"Value\":\"2/6\",\"Numerator\":\"2\",\"Denominator\":\"6\"}")]
    public void InvalidPayloadsThrowJsonExceptionRatherThanProducingDefaultValues(string json)
    {
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<MathValue>(json));
    }

    [TestMethod]
    public void JsonValidationErrorsRetainTheOriginalCauseAndPropertyPath()
    {
        const string json = "{\"Expression\":\"saved\",\"Value\":{\"Kind\":0,\"Exactness\":100,\"Value\":\"1\"}}";
        var exception = Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<HistoryEntry>(json));
        Assert.IsInstanceOfType<ArgumentOutOfRangeException>(exception.InnerException);
        Assert.AreEqual("$.Value", exception.Path);
    }

    [TestMethod]
    public void ConverterEnforcesHardNumericAndPerValuePayloadLimits()
    {
        string oversizedInteger = (BigInteger.One << CalculationLimits.MaxIntegerBits).ToString(CultureInfo.InvariantCulture);
        string json = "{\"Kind\":0,\"Exactness\":1,\"Value\":\"" + oversizedInteger + "\"}";
        Assert.ThrowsExactly<CalculationLimitException>(() => JsonSerializer.Deserialize<MathValue>(json));
        Assert.ThrowsExactly<CalculationLimitException>(() => MathValue.Deserialize(json));
        Assert.ThrowsExactly<CalculationLimitException>(() => JsonSerializer.Deserialize<MathValue>("{" + new string(' ', 25000) + "}"));

        string bounded = MathValue.One.Serialize();
        bounded = bounded[..^1] + new string(' ', 25000 - bounded.Length) + "}";
        AssertStoredValue(MathValue.One, JsonSerializer.Deserialize<MathValue>(bounded));
        Assert.ThrowsExactly<CalculationLimitException>(() => JsonSerializer.Deserialize<MathValue>(bounded.Insert(1, " ")));

        string padded = new string(' ', 30000) + MathValue.One.Serialize();
        AssertStoredValue(MathValue.One, JsonSerializer.Deserialize<MathValue>(padded));
        Assert.ThrowsExactly<CalculationLimitException>(() => MathValue.Deserialize(new MathValue(256).Serialize(), maxIntegerBits: 8));
        AssertStoredValue(new MathValue(256), JsonSerializer.Deserialize<MathValue>(new MathValue(256).Serialize()));
    }

    private static MathValue[] SampleValues()
    {
        var component = BigInteger.One << (CalculationLimits.MaxIntegerBits - 1);
        return
        [
            default,
            MathValue.Zero,
            MathValue.One,
            new MathValue(-42),
            new MathValue(194.40m),
            new MathValue(decimal.MaxValue),
            new MathValue(0.1),
            new MathValue(-0.0),
            new MathValue(double.Epsilon),
            new MathValue(double.MaxValue),
            MathValue.NaN,
            MathValue.PositiveInfinity,
            MathValue.NegativeInfinity,
            MathValue.FromFraction(-123, 7),
            MathValue.FromFraction(1, 3).WithExactness(CalculationExactness.Unverified),
            MathValue.FromFraction(1, 3).WithDecimalDisplayScale(2),
            new MathValue(0.1).WithExactness(CalculationExactness.Exact),
            new MathValue((BigInteger.One << CalculationLimits.MaxIntegerBits) - 1),
            MathValue.FromFraction(component + 1, component)
        ];
    }

    private static void AssertStoredValue(MathValue expected, MathValue actual)
    {
        Assert.AreEqual(expected, actual);
        Assert.AreEqual(expected.Kind, actual.Kind);
        Assert.AreEqual(expected.Exactness, actual.Exactness);
        Assert.AreEqual(expected.DecimalDisplayScale, actual.DecimalDisplayScale);
        Assert.AreEqual(expected.Serialize(), actual.Serialize());
        if (expected.Kind == MathValue.DataType.Double && !expected.IsNaN)
        {
            Assert.AreEqual(BitConverter.DoubleToInt64Bits(expected.DoubleValue), BitConverter.DoubleToInt64Bits(actual.DoubleValue));
        }
    }
}

[JsonSerializable(typeof(MathValue))]
[JsonSerializable(typeof(MathValueJsonConverterTests.HistoryEntry))]
internal sealed partial class MathValueTestJsonContext : JsonSerializerContext;
