using System.Globalization;
using System.Numerics;
using System.Text;

namespace Numera.Tests;

/// <summary>
/// Pins every behaviour that docs/architecture.md demonstrates in its formatting and culture
/// examples, so the documentation cannot drift from what the library actually does.
/// </summary>
[TestClass]
public sealed class FormattingGuideTests
{
    private static readonly CultureInfo German = new("de-DE");

    private static CalculatorEngine GermanEngine() =>
        new(CalculatorOptions.ForCulture(German));

    [TestMethod]
    public void RadixExamples_MatchTheDocumentedOutput()
    {
        var value = Calculator.Evaluate("1100");

        Assert.AreEqual("0x44C", value.ToHexString());
        Assert.AreEqual("0b10001001100", value.ToBinaryString());
        Assert.AreEqual("0o2114", value.ToOctalString());
        Assert.AreEqual("44C", value.ToHexString(prefix: false));

        Assert.AreEqual("-0xFF", Calculator.Evaluate("-255").ToHexString());
        Assert.IsNull(Calculator.Evaluate("1.5").ToHexString());
    }

    [TestMethod]
    [DataRow(255, "0xFF")]
    [DataRow(-255, "-0xFF")]
    [DataRow(128, "0x80")]
    [DataRow(4095, "0xFFF")]
    [DataRow(16, "0x10")]
    public void HexString_HasNoLeadingZeroPaddingWhenTheTopBitIsSet(int input, string expected)
    {
        Assert.AreEqual(expected, new MathValue(new BigInteger(input)).ToHexString());
    }

    [TestMethod]
    public void TryFormat_UsingDefaultFormat_WritesIntoTheBuffer()
    {
        var value = Calculator.Evaluate("1100");
        Span<char> buffer = stackalloc char[64];

        Assert.IsTrue(value.TryFormat(buffer, out var written, default, CultureInfo.InvariantCulture));
        Assert.AreEqual("1,100", new string(buffer[..written]));
    }

    [TestMethod]
    public void TryFormat_ReturnsFalseWhenTheBufferIsTooSmall()
    {
        var value = new MathValue(new BigInteger(123456));

        Span<char> chars = stackalloc char[3];
        Assert.IsFalse(value.TryFormat(chars, out _, default, CultureInfo.InvariantCulture));

        Span<byte> utf8 = stackalloc byte[3];
        Assert.IsFalse(value.TryFormat(utf8, out _, default, CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void EngineWithCulture_ReadsAndDisplaysInThatCulture()
    {
        var engine = GermanEngine();

        var value = engine.Evaluate("1000 + 2,5");
        var shown = engine.FormatResult(value);

        Assert.AreEqual("1.002,5", shown.PrimaryDisplay);
        Assert.AreEqual("1002.5", shown.MachineReadable);
        Assert.IsNull(shown.HexRepresentation);
    }

    [TestMethod]
    public void EngineDisplayText_CanBeTypedBackIntoTheSameEngine()
    {
        var engine = GermanEngine();
        var shown = engine.FormatResult(engine.Evaluate("1000 + 2,5"));

        Assert.IsNotNull(shown.PrimaryDisplay);
        Assert.AreEqual(1002.5m, engine.Evaluate(shown.PrimaryDisplay).DecimalValue);
    }

    [TestMethod]
    public void DisplayText_IsGroupedByDefault()
    {
        var engine = GermanEngine();

        Assert.AreEqual("1.234.567", engine.FormatResult(engine.Evaluate("1234567")).PrimaryDisplay);
        Assert.AreEqual("1.234.567,5", engine.FormatResult(engine.Evaluate("1234567,5")).PrimaryDisplay);

        var unGrouped = engine.FormatResult(
            engine.Evaluate("1234567"),
            engine.Options.ResultFormat with { GroupDigits = false });
        Assert.AreEqual("1234567", unGrouped.PrimaryDisplay);
    }

    [TestMethod]
    public void RadixAndMachineReadableText_NeverDependOnCulture()
    {
        var engine = GermanEngine();
        var shown = engine.FormatResult(engine.Evaluate("1100"));

        Assert.AreEqual("1100", shown.MachineReadable);
        Assert.AreEqual("0x44C", shown.HexRepresentation);
        Assert.AreEqual("0b10001001100", shown.BinaryRepresentation);
        Assert.AreEqual("0o2114", shown.OctalRepresentation);
    }

    [TestMethod]
    [DataRow("de-DE", "1000 + 2,5", "1.002,5")]
    [DataRow("fr-FR", "1000 + 2,5", "1\u202f002,5")]
    [DataRow("en-US", "1000 + 2.5", "1,002.5")]
    public void StaticFacade_ReadsAndDisplaysUsingCurrentCulture(string culture, string input, string expected)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var result = Calculator.Evaluate(input);

            Assert.AreEqual(1002.5m, result.DecimalValue);
            Assert.AreEqual(culture, result.Culture?.Name);
            Assert.AreEqual(expected, result.PrimaryDisplay);
            Assert.AreEqual(expected, result.FormatResult().PrimaryDisplay);
            Assert.AreEqual("1002.5", result.MachineReadable);
            Assert.AreEqual(result.Value, Calculator.Evaluate(expected).Value);
            Assert.IsTrue(Calculator.TryEvaluate(input, out var attempted, out var diagnostic));
            Assert.IsNull(diagnostic);
            Assert.AreEqual(result.Value, attempted.Value);
            Assert.AreEqual(expected, attempted.PrimaryDisplay);
            Assert.AreEqual(result.Value, Calculator.Evaluate(Calculator.Parse(input)).Value);
            Assert.IsTrue(Calculator.TryParse(input, out var node, out diagnostic));
            Assert.IsNull(diagnostic);
            Assert.AreEqual(result.Value, Calculator.Evaluate(node).Value);
            Assert.AreEqual("1000 + 2.5", Calculator.FormatExpression(input, ExpressionFormatOptions.Ascii));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void ResultsAndExistingEnginesRetainCultureWhileFacadeFollowsCurrentCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = German;
            var engine = new CalculatorEngine();
            var result = Calculator.Evaluate("1000 + 2,5");
            Assert.AreEqual(2.5m, Calculator.Evaluate("max(1,5; 2,5)").DecimalValue);

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

            Assert.AreEqual("1.002,5", result.PrimaryDisplay);
            Assert.AreEqual("1.002,5", result.FormatResult().PrimaryDisplay);
            Assert.AreEqual("1.002,5", result.FormatResult(null, null).PrimaryDisplay);
            Assert.AreEqual("1.002,5", result.ToString());
            Assert.AreEqual("1.002,5", $"{result}");
            Assert.AreEqual("1.002,5", engine.Evaluate("1000 + 2,5").PrimaryDisplay);
            Assert.AreEqual(result.Value, engine.Evaluate(result.PrimaryDisplay).Value);
            Assert.AreEqual("1,002.5", Calculator.Evaluate("1000 + 2.5").PrimaryDisplay);
            Assert.AreEqual("1,002.5", result.FormatResult(culture: CultureInfo.InvariantCulture).PrimaryDisplay);
            Assert.AreEqual("1002.5", result.FormatResult().MachineReadable);
            Assert.AreEqual(result.Value, CalculatorEngine.ForCulture(CultureInfo.InvariantCulture)
                .Evaluate(result.MachineReadable).Value);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    [DataRow("en-US", "x(2+3)+ans/2.5")]
    [DataRow("de-DE", "x(2+3)+ans/2,5")]
    public void ExpressionFormattingWithContextMatchesEngineAndKeepsValuesSymbolic(string culture, string input)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var engine = new CalculatorEngine();
            var context = new EvaluationContext([new Variable("x", 7)], 11);
            ExpressionFormatOptions[] styles =
            [
                ExpressionFormatOptions.Ascii,
                ExpressionFormatOptions.Typographic,
                ExpressionFormatOptions.LaTeX
            ];
            foreach (var style in styles)
            {
                Assert.AreEqual(engine.FormatExpression(input, style, context),
                    Calculator.FormatExpression(input, style, context));
            }

            Assert.AreEqual("x * (2 + 3) + ans / 2.5",
                Calculator.FormatExpression(input, ExpressionFormatOptions.Ascii, context));
            Assert.AreEqual(engine.FormatExpression(input, context: context),
                Calculator.FormatExpression(input, context: context));
            Assert.AreEqual(engine.FormatExpression(input), Calculator.FormatExpression(input));
            Assert.AreEqual(Calculator.FormatExpression(input),
                Calculator.FormatExpression(input, options: null, context: null));
            Assert.AreEqual(new MathValue(7), context.Variables["x"].Value);
            Assert.AreEqual(new MathValue(11), context.PreviousResult);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
    public void ExpressionFormattingWithContextDoesNotEvaluateAssignmentsOrDivision()
    {
        var context = new EvaluationContext([new Variable("x", 7)], 11);
        var variables = context.Variables;
        const string input = "x=(ans+1)/0";
        const string expected = "x = (ans + 1) / 0";

        Assert.AreEqual(expected, Calculator.FormatExpression(input, ExpressionFormatOptions.Ascii, context));
        Assert.AreEqual(expected, CalculatorEngine.Default.FormatExpression(input, ExpressionFormatOptions.Ascii, context));
        Assert.AreSame(variables, context.Variables);
        Assert.AreEqual(new MathValue(7), context.Variables["x"].Value);
        Assert.AreEqual(new MathValue(11), context.PreviousResult);
    }

    [TestMethod]
    public void ExpressionFormattingWithContextDoesNotReplayCapturedCallbacks()
    {
        int calls = 0;
        var engine = new CalculatorEngine(new CalculatorOptions
        {
            CustomFunctions = CustomFunctionCollection.Empty.SetItem("tick", () => new MathValue(++calls))
        });
        var captured = engine.Evaluate("x=tick()");
        var expression = captured.Context.PreviousExpression;
        Assert.AreEqual(1, calls);

        Assert.AreEqual("x + ans", Calculator.FormatExpression("x+ans", ExpressionFormatOptions.Ascii, captured.Context));
        Assert.AreEqual(engine.FormatExpression("x+ans", context: captured.Context),
            Calculator.FormatExpression("x+ans", context: captured.Context));
        Assert.AreEqual("x = ans + 1", Calculator.FormatExpression("x=ans+1", context: captured.Context));
        Assert.AreEqual(1, calls);
        Assert.AreSame(expression, captured.Context.PreviousExpression);
        Assert.AreEqual(MathValue.One, captured.Context.Variables["x"].Value);
        Assert.AreEqual(MathValue.One, captured.Context.PreviousResult);
    }

    [TestMethod]
    public void ExplicitNumericFormatsAndSpansRetainResultCultureUnlessOverridden()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            var result = GermanEngine().Evaluate("1234,5");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

            Assert.AreEqual("1234,50", result.ToString("F2", null));
            Assert.AreEqual("1234,50", $"{result:F2}");
            Assert.AreEqual("1234.50", result.ToString("F2", CultureInfo.InvariantCulture));
            Span<char> chars = stackalloc char[32];
            Span<byte> bytes = stackalloc byte[32];
            foreach (string format in new[] { "", "F2" })
            {
                string expected = format.Length == 0 ? "1.234,5" : "1234,50";
                Assert.IsTrue(result.TryFormat(chars, out int charsWritten, format));
                Assert.AreEqual(expected, chars[..charsWritten].ToString());
                Assert.IsTrue(result.TryFormat(bytes, out int bytesWritten, format));
                Assert.AreEqual(expected, Encoding.UTF8.GetString(bytes[..bytesWritten]));

                string invariant = format.Length == 0 ? "1,234.5" : "1234.50";
                Assert.IsTrue(result.TryFormat(chars, out charsWritten, format, CultureInfo.InvariantCulture));
                Assert.AreEqual(invariant, chars[..charsWritten].ToString());
                Assert.IsTrue(result.TryFormat(bytes, out bytesWritten, format, CultureInfo.InvariantCulture));
                Assert.AreEqual(invariant, Encoding.UTF8.GetString(bytes[..bytesWritten]));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void ResultOverridesAndReceivingEnginePolicyAreExplicitAndIndependent()
    {
        var options = new ResultFormatOptions { SignificantDigits = 3, GroupDigits = false };
        var origin = new CalculatorEngine(new CalculatorOptions { Culture = German, ResultFormat = options });
        var result = origin.Evaluate("amount=12,345");
        var moreDigits = options with { SignificantDigits = 5 };

        Assert.AreEqual("12,3", result.FormatResult().PrimaryDisplay);
        Assert.AreEqual("12,3", result.FormatResult(culture: null).PrimaryDisplay);
        Assert.AreEqual("12,345", result.FormatResult(moreDigits).PrimaryDisplay);
        Assert.AreEqual("12.3", result.FormatResult(culture: CultureInfo.InvariantCulture).PrimaryDisplay);
        var overridden = result.FormatResult(moreDigits, CultureInfo.InvariantCulture);
        Assert.AreEqual("12.345", overridden.PrimaryDisplay);
        Assert.AreEqual("12.345", overridden.MachineReadable);
        Assert.AreEqual("2469/200", overridden.FractionRepresentation);
        Assert.AreSame(result.AssignedVariables, overridden.AssignedVariables);

        var receiving = new CalculatorEngine(new CalculatorOptions
        {
            Culture = CultureInfo.InvariantCulture,
            ResultFormat = options with { SignificantDigits = 4 }
        });
        Assert.AreEqual("12.34", receiving.FormatResult(result).PrimaryDisplay);
        Assert.AreEqual("12.34", receiving.FormatResult(result.Value).PrimaryDisplay);
        Assert.AreEqual("12.34", receiving.FormatResult(result, null, null).PrimaryDisplay);
        Assert.AreEqual("12.34", receiving.FormatResult(result.Value, null, null).PrimaryDisplay);
        Assert.AreEqual("12,345", receiving.FormatResult(result, moreDigits, German).PrimaryDisplay);
        Assert.AreEqual("12,345", receiving.FormatResult(result.Value, moreDigits, German).PrimaryDisplay);
        Assert.AreSame(result.AssignedVariables, receiving.FormatResult(result).AssignedVariables);
        Assert.AreEqual("12,3", result.PrimaryDisplay);
        Assert.AreSame(options, result.FormatOptions);

        var integer = origin.Evaluate("n=42");
        Assert.AreEqual("2A", integer.FormatResult(prefixRadixOutput: false).HexRepresentation);
        Assert.AreEqual("101010", receiving.FormatResult(integer, prefixRadixOutput: false).BinaryRepresentation);
        Assert.AreEqual("52", receiving.FormatResult(integer.Value, prefixRadixOutput: false).OctalRepresentation);
    }

    [TestMethod]
    public void ResultsWithoutCapturedOptionsUseHumanFacingDefaults()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = German;
            var result = new EvaluationResult(new MathValue(12.5m), EvaluationContext.Empty);

            Assert.IsNull(result.Options);
            Assert.AreEqual("12,5", result.PrimaryDisplay);
            Assert.AreEqual("12,5", result.FormatResult().PrimaryDisplay);
            Assert.AreEqual("12,50", result.ToString("F2", null));
            Assert.AreEqual("12.5", result.FormatResult(culture: CultureInfo.InvariantCulture).PrimaryDisplay);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void FacadeResultFormattingUsesFreshDefaultsWithoutChangingCapturedSettings()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            var origin = new CalculatorEngine(new CalculatorOptions
            {
                Culture = German,
                ResultFormat = new ResultFormatOptions { SignificantDigits = 3, GroupDigits = false }
            });
            var result = origin.Evaluate("amount=1234,5");
            var captured = result.FormatResult();
            var context = result.Context;
            var options = result.Options;
            (string Culture, string Expected)[] cases =
            [
                ("en-US", "1,234.5"),
                ("de-DE", "1.234,5")
            ];

            foreach (var (culture, expected) in cases)
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var formatted = Calculator.FormatResult(result);
                var raw = Calculator.FormatResult(result.Value);
                Assert.AreEqual(expected, formatted.PrimaryDisplay);
                Assert.AreEqual(expected, raw.PrimaryDisplay);
                Assert.AreEqual("1234.5", formatted.MachineReadable);
                Assert.AreEqual("1234.5", raw.MachineReadable);
                Assert.IsFalse(formatted.IsDisplayRounded);
                Assert.AreSame(result.AssignedVariables, formatted.AssignedVariables);
                Assert.IsNull(raw.AssignedVariables);
                Assert.AreEqual(CalculatorEngine.Default.FormatResult(result), formatted);
                Assert.AreEqual(CalculatorEngine.Default.FormatResult(result.Value), raw);
                Assert.AreEqual(formatted, Calculator.FormatResult(result, null, null));
                Assert.AreEqual(raw, Calculator.FormatResult(result.Value, null, null));
            }

            Assert.AreEqual("1230", captured.PrimaryDisplay);
            Assert.IsTrue(captured.IsDisplayRounded);
            Assert.AreEqual(captured, result.FormatResult());
            Assert.AreSame(context, result.Context);
            Assert.AreSame(options, result.Options);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
    public void FacadeResultFormattingOverridesOptionsAndCultureIndependently()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var origin = new CalculatorEngine(new CalculatorOptions
            {
                Culture = German,
                ResultFormat = new ResultFormatOptions { SignificantDigits = 3 }
            });
            var result = origin.Evaluate("amount=12,345");
            var custom = new ResultFormatOptions { SignificantDigits = 4, GroupDigits = false };
            (ResultFormatOptions? Options, CultureInfo? Culture, string Expected)[] cases =
            [
                (null, null, "12.345"),
                (custom, null, "12.34"),
                (null, German, "12,345"),
                (custom, German, "12,34")
            ];

            foreach (var (options, culture, expected) in cases)
            {
                var formatted = Calculator.FormatResult(result, options, culture);
                var raw = Calculator.FormatResult(result.Value, options, culture);
                Assert.AreEqual(expected, formatted.PrimaryDisplay);
                Assert.AreEqual(expected, raw.PrimaryDisplay);
                Assert.AreEqual("12.345", formatted.MachineReadable);
                Assert.AreEqual("2469/200", formatted.FractionRepresentation);
                Assert.AreEqual(formatted.FractionRepresentation, raw.FractionRepresentation);
                Assert.AreSame(result.AssignedVariables, formatted.AssignedVariables);
                Assert.IsNull(raw.AssignedVariables);
                Assert.AreEqual(CalculatorEngine.Default.FormatResult(result, options, culture), formatted);
                Assert.AreEqual(CalculatorEngine.Default.FormatResult(result.Value, options, culture), raw);
                Assert.AreEqual(result.Value.Serialize(), formatted.Value.Serialize());
                Assert.AreEqual(result.Value.Serialize(), raw.Value.Serialize());
            }

            Assert.AreEqual("12,3", result.PrimaryDisplay);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
    [DataRow(true, "0x2A", "0b101010", "0o52")]
    [DataRow(false, "2A", "101010", "52")]
    public void FacadeResultFormattingForwardsRadixPrefixChoice(bool prefixes, string hex, string binary, string octal)
    {
        var result = Calculator.Evaluate("n=42");
        var formatted = Calculator.FormatResult(result, prefixRadixOutput: prefixes);
        var raw = Calculator.FormatResult(result.Value, prefixRadixOutput: prefixes);

        foreach (var card in new[] { formatted, raw })
        {
            Assert.AreEqual(hex, card.HexRepresentation);
            Assert.AreEqual(binary, card.BinaryRepresentation);
            Assert.AreEqual(octal, card.OctalRepresentation);
            Assert.AreEqual("42", card.MachineReadable);
            Assert.AreEqual("42", card.FractionRepresentation);
        }

        Assert.AreSame(result.AssignedVariables, formatted.AssignedVariables);
        Assert.IsNull(raw.AssignedVariables);
    }

    [TestMethod]
    public void FacadeResultFormattingRejectsInvalidDisplayOptions()
    {
        var result = Calculator.Evaluate("42");
        var invalid = new ResultFormatOptions { SignificantDigits = 0 };
        var withResult = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Calculator.FormatResult(result, invalid));
        var withValue = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Calculator.FormatResult(result.Value, invalid));
        Assert.AreEqual("SignificantDigits", withResult.ParamName);
        Assert.AreEqual("SignificantDigits", withValue.ParamName);
    }

    [TestMethod]
    [DataRow("x=1000000", "1,000,000")]
    [DataRow("x=1/3", "0.333333333333")]
    [DataRow("x=1.00+0", "1.00")]
    [DataRow("x=exp(1)", "2.71828182846")]
    public void FormattingPreservesAssignmentsAcrossRepresentations(
        string expression, string expectedDisplay)
    {
        var engine = CalculatorEngine.ForCulture(CultureInfo.InvariantCulture,
            options => options with
            {
                ResultFormat = new ResultFormatOptions { SignificantDigits = 12 }
            });
        var result = engine.Evaluate(expression);
        string storedValue = result.Value.Serialize();
        var formatted = result.FormatResult();

        Assert.AreEqual(expectedDisplay, formatted.PrimaryDisplay);
        Assert.AreEqual(storedValue, result.Value.Serialize());
        Assert.AreSame(result.AssignedVariables, formatted.AssignedVariables);
        Assert.AreSame(result.AssignedVariables, engine.FormatResult(result).AssignedVariables);
        Assert.AreEqual(result.Value.Serialize(), formatted.AssignedVariables!["x"].Value.Serialize());
    }

    [TestMethod]
    public void FormattingPreservesAssignmentMetadataWithoutReplayingCallbacks()
    {
        int calls = 0;
        var engine = CalculatorEngine.ForCulture(CultureInfo.InvariantCulture,
            options => options with
            {
                ResultFormat = new ResultFormatOptions { SignificantDigits = 4 },
                CustomFunctions = CustomFunctionCollection.Empty.SetItem("tick", () => new MathValue(++calls))
            });
        var result = engine.Evaluate("sum((a=1000000), (b=1/3), (c=1.00+0), (d=tick()))");

        Assert.AreSame(result.AssignedVariables, result.FormatResult().AssignedVariables);
        Assert.AreSame(result.AssignedVariables, engine.FormatResult(result).AssignedVariables);
        Assert.AreEqual(1, calls);
        Assert.HasCount(4, result.AssignedVariables);
        Assert.AreEqual(MathValue.FromFraction(1, 3), result.AssignedVariables["b"].Value);
        Assert.AreEqual("tick()", result.AssignedVariables["d"].Definition);
    }

    [TestMethod]
    public void SnapshotConstructionPreservesSuppliedTextAndAssignmentMetadata()
    {
        VariableCollection assignments = [new Variable("x", MathValue.FromFraction(1, 3), "1/3")];
        var supplied = new FormattedCalculation(new MathValue(42), "supplied display", "42",
            assignedVariables: assignments);

        Assert.AreSame(assignments, supplied.AssignedVariables);
        Assert.AreEqual("supplied display", supplied.PrimaryDisplay);
        Assert.AreEqual("42", supplied.MachineReadable);
        Assert.AreEqual(MathValue.FromFraction(1, 3), supplied.AssignedVariables!["x"].Value);
        Assert.AreEqual("1/3", supplied.AssignedVariables["x"].Definition);
    }

    [TestMethod]
    public void DoubleResults_UseRoundTripPrecision()
    {
        var value = new MathValue(0.1 + 0.2);

        Assert.AreEqual(MathValue.DataType.Double, value.Kind);
        Assert.AreEqual("0.30000000000000004", value.ToString(CultureInfo.InvariantCulture));
    }
}
