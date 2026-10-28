namespace Numera.Tests;

/// <summary>
/// Shared, reusable engines for tests that only differ by one common option. Sharing
/// instances mirrors how consumers are expected to use <see cref="CalculatorEngine"/>
/// (build once, reuse). Tests needing unusual options (limits, parser flags) should
/// construct their own engine inline instead.
/// </summary>
internal static class TestEngines
{
    public static CalculatorEngine DegreesEngine { get; } =
        new(new CalculatorOptions { AngleUnit = AngleUnit.Degrees });

    public static CalculatorEngine GradiansEngine { get; } =
        new(new CalculatorOptions { AngleUnit = AngleUnit.Gradians });

    public static CalculatorEngine RawEngine { get; } =
        new(new CalculatorOptions { Normalization = null });
}
