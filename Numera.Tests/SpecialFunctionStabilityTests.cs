using System.Globalization;

namespace Numera.Tests;

[TestClass]
public sealed class SpecialFunctionStabilityTests
{
    private static readonly CalculatorEngine Engine = new(
        CalculatorOptions.ForCulture(CultureInfo.InvariantCulture) with { Normalization = null });

    // References were evaluated at 400 decimal digits with binary64 inputs.
    [TestMethod]
    [DataRow("gamma(143)", 2.695364137888163e245)]
    [DataRow("gamma(150)", 3.80892263763057e260)]
    [DataRow("gamma(171.6)", 1.5858969096672565e308)]
    [DataRow("gamma(171.62)", 1.7576826789978127e308)]
    [DataRow("gamma(171.6243769563027)", 1.7976931348622299e308)]
    [DataRow("gamma(-170.5)", -3.3127395215386074e-308)]
    [DataRow("gamma(-170.6243769563027)", -1.890026233049676e-308)]
    [DataRow("gamma(-170.62437695630274)", -1.8900262330494694e-308)]
    [DataRow("gamma(1e-300)", 1e300)]
    [DataRow("gamma(1e-308)", 1e308)]
    [DataRow("gamma(-1e-308)", -1e308)]
    [DataRow("beta(75, 75)", 2.8727561925549378e-46)]
    [DataRow("beta(200, 200)", 9.713217247611181e-122)]
    [DataRow("beta(500, 500)", 1.479901599125611e-302)]
    [DataRow("beta(1e-300, 100)", 1e300)]
    [DataRow("beta(1e-200, 150)", 1e200)]
    [DataRow("beta(1e-300, 1e-300)", 2e300)]
    [DataRow("beta(1, 1e16)", 1e-16)]
    [DataRow("beta(1, 1e308)", 1e-308)]
    [DataRow("beta(-1e-308, 1.0000000000000004e-308)", -4.940656458412464e292)]
    [DataRow("beta(0.5, 1e300)", 1.772453850905516e-150)]
    [DataRow("beta(2.5, 1e100)", 1.329340388179137e-250)]
    [DataRow("beta(-0.5, 1e16)", -354490770.18110317)]
    [DataRow("beta(-2.5, 1e16)", -9.453087204829414e39)]
    [DataRow("beta(-0.5, -1000.25)", -112.15586198358181)]
    [DataRow("beta(-150.5, 175.25)", -3.746224263395586e29)]
    [DataRow("beta(-150.5, 75.25)", -7.169910805846543e-47)]
    [DataRow("beta(-75.25, -75.5)", -4.891290636583628e44)]
    [DataRow("beta(-0.5, 0.25)", 2.6220575542921196)]
    [DataRow("beta(-0.5, -0.25)", -3.594420704206777)]
    [DataRow("zeta(-171.5)", 4.739302330550545e172)]
    [DataRow("zeta(-200.5)", -2.320000663352899e215)]
    [DataRow("zeta(-250.5)", 1.3106450149434267e293)]
    [DataRow("zeta(-259)", 8.760156344622921e306)]
    [DataRow("zeta(-259.5)", 3.982766118112322e307)]
    [DataRow("zeta(-259.99999999999994)", 3.236722036492491e295)]
    [DataRow("zeta(-260.00000000000006)", -3.2367220364938613e295)]
    [DataRow("zeta(-0.5)", -0.20788622497735457)]
    [DataRow("zeta(-0.49999999999999994)", -0.2078862249773546)]
    [DataRow("zeta(-0.5000000000000001)", -0.20788622497735454)]
    [DataRow("zeta(-0.25)", -0.3204512642285773)]
    [DataRow("zeta(-1e-20)", -0.5)]
    [DataRow("zeta(-2.0000000000000004)", 1.3521831236215423e-17)]
    [DataRow("zeta(-1.9999999999999998)", -6.760915618107716e-18)]
    public void FiniteResultsMatchIndependentReferencesWithRelativeError(string expression, double expected)
    {
        var result = Engine.Evaluate(expression);
        Assert.AreEqual(MathValue.DataType.Double, result.Kind);
        Assert.AreEqual(CalculationExactness.Unverified, result.Exactness);
        Assert.IsTrue(double.IsFinite(result.DoubleValue));

        double relativeError = Math.Abs(result.DoubleValue / expected - 1.0);
        Assert.IsLessThanOrEqualTo(5e-13, relativeError,
            $"{expression}: expected {expected:R}, got {result.DoubleValue:R}; relative error {relativeError:R}.");
    }

    [TestMethod]
    [DataRow(-10.25, -6.780818043294673e-7, 12L)]
    [DataRow(-25.5, 3.99121704344051e-26, 8L)]
    [DataRow(-50.5, -1.449954393907748e-65, 8L)]
    [DataRow(-75.25, 6.072759163352232e-110, 8L)]
    [DataRow(-100.5, -3.3536908198076787e-159, 4L)]
    [DataRow(-125.5, 1.4880570743952872e-210, 4L)]
    [DataRow(-150.5, -4.478447658150641e-264, 4L)]
    [DataRow(-160.5, -5.255546447007829e-286, 8L)]
    [DataRow(-170.5, -3.3127395215386074e-308, 4L)]
    public void ModerateNegativeGammaMatchesReferencesWithinTightUlpBudgets(double argument, double expected, long maximumUlps)
    {
        double actual = SpecialFunctions.Gamma(argument);
        Assert.IsTrue(double.IsFinite(actual));
        Assert.AreEqual(Math.Sign(expected), Math.Sign(actual));

        long distance = Math.Abs(BitConverter.DoubleToInt64Bits(actual) - BitConverter.DoubleToInt64Bits(expected));
        Assert.IsLessThanOrEqualTo(maximumUlps, distance, $"gamma({argument:R}): {distance} ULPs, allowed {maximumUlps}.");
    }

    [TestMethod]
    [DataRow("gamma(-171.5)", 1.9316265431712e-310, 8L)]
    [DataRow("gamma(-175.5)", 2.1075e-319, 1L)]
    [DataRow("gamma(-176.5)", -1.196e-321, 1L)]
    [DataRow("gamma(-177.5)", double.Epsilon, 0L)]
    [DataRow("gamma(-180.00000000000003)", -1.7513719e-316, 1L)]
    [DataRow("gamma(-179.99999999999997)", 1.7513719e-316, 1L)]
    [DataRow("beta(530, 530)", 1.2465e-320, 1L)]
    [DataRow("beta(535, 535)", 1e-323, 0L)]
    public void SubnormalResultsAreNotPrematurelyFlushedToZero(string expression, double expected, long maximumUlps)
    {
        double actual = Engine.Evaluate(expression).DoubleValue;
        Assert.AreNotEqual(0.0, actual);
        Assert.AreEqual(Math.Sign(expected), Math.Sign(actual));
        Assert.IsTrue(double.IsFinite(actual));

        long distance = Math.Abs(BitConverter.DoubleToInt64Bits(actual) - BitConverter.DoubleToInt64Bits(expected));
        Assert.IsLessThanOrEqualTo(maximumUlps, distance, $"{expression}: {distance} ULPs, allowed {maximumUlps}.");
    }

    [TestMethod]
    [DataRow("gamma(171.625)", double.PositiveInfinity)]
    [DataRow("gamma(171.62437695630274)", double.PositiveInfinity)]
    [DataRow("gamma(172)", double.PositiveInfinity)]
    [DataRow("gamma(1e-320)", double.PositiveInfinity)]
    [DataRow("gamma(-1e-320)", double.NegativeInfinity)]
    [DataRow("beta(1e-320, 1)", double.PositiveInfinity)]
    [DataRow("beta(1e-320, 1e-320)", double.PositiveInfinity)]
    [DataRow("beta(-2.5, 1e308)", double.NegativeInfinity)]
    [DataRow("zeta(-260.5)", double.NegativeInfinity)]
    [DataRow("zeta(-400.5)", double.NegativeInfinity)]
    [DataRow("zeta(-500.5)", double.NegativeInfinity)]
    [DataRow("zeta(-262.5)", double.PositiveInfinity)]
    [DataRow("zeta(-9007199254740991)", double.PositiveInfinity)]
    public void GenuineOverflowRetainsItsSignInsteadOfProducingNaN(string expression, double expected)
    {
        Assert.AreEqual(expected, Engine.Evaluate(expression).DoubleValue);
    }

    [TestMethod]
    [DataRow("gamma(-178.5)", true)]
    [DataRow("gamma(-179.5)", false)]
    [DataRow("beta(-1100.5, 550.75)", true)]
    [DataRow("beta(550.75, -1100.5)", true)]
    [DataRow("beta(540, 540)", false)]
    [DataRow("beta(1e308, 1e308)", false)]
    public void GenuineUnderflowRetainsSignedZero(string expression, bool negative)
    {
        double result = Engine.Evaluate(expression).DoubleValue;
        Assert.AreEqual(0.0, result);
        Assert.AreEqual(negative, double.IsNegative(result));
    }

    [TestMethod]
    [DataRow(-2.0)]
    [DataRow(-200.0)]
    [DataRow(-500.0)]
    [DataRow(-260.0)]
    [DataRow(-9007199254740992.0)]
    [DataRow(-1e308)]
    public void NegativeEvenZetaArgumentsRemainTrivialZeros(double argument)
    {
        Assert.AreEqual(0.0, SpecialFunctions.Zeta(argument));
    }

    [TestMethod]
    public void BetaSymmetryAndRecurrenceHoldForUnequalAndNegativeArguments()
    {
        foreach (var (a, b) in new[] { (0.25, 1e16), (-0.5, 100.25), (-75.25, -75.5), (1e-300, 100.0) })
        {
            double value = SpecialFunctions.Beta(a, b);
            double swapped = SpecialFunctions.Beta(b, a);
            Assert.IsLessThanOrEqualTo(5e-13, Math.Abs(swapped / value - 1.0));
        }

        double recurrence = SpecialFunctions.Beta(0.25, 1e16) * 0.25 / (0.25 + 1e16);
        Assert.IsLessThanOrEqualTo(5e-13, Math.Abs(SpecialFunctions.Beta(1.25, 1e16) / recurrence - 1.0));
    }

    [TestMethod]
    [DataRow("lgamma(-exp(1000))")]
    [DataRow("beta(-exp(1000), 2)")]
    [DataRow("beta(2, -exp(1000))")]
    [DataRow("beta(-exp(1000), exp(1000))")]
    [DataRow("beta(exp(1000), -exp(1000))")]
    [DataRow("beta(-exp(1000), -exp(1000))")]
    public void NegativeInfinityHasSpecificDiagnosticsOnThrowingAndTrySurfaces(string expression)
    {
        Assert.IsFalse(Engine.TryEvaluate(expression, out _, out var diagnostic));
        Assert.IsNotNull(diagnostic);
        Assert.AreEqual(EvaluationError.UndefinedMathResult, diagnostic.Code);
        Assert.Contains("negative infinity", diagnostic.Message);

        var exception = Assert.ThrowsExactly<ExpressionException>(() => Engine.Evaluate(expression));
        Assert.AreEqual(diagnostic, exception.Diagnostic);
    }

    [TestMethod]
    public void NearZeroAndNonFiniteInputsPreserveDomainBehavior()
    {
        Assert.AreEqual(-0.5, SpecialFunctions.Zeta(-double.Epsilon), 1e-15);
        Assert.AreEqual(-0.5, SpecialFunctions.Zeta(double.Epsilon), 1e-15);
        Assert.AreEqual(-Math.Log(double.Epsilon), SpecialFunctions.LogGamma(double.Epsilon), 1e-12);
        Assert.IsTrue(double.IsNaN(SpecialFunctions.Zeta(double.NegativeInfinity)));
        Assert.IsTrue(double.IsNaN(SpecialFunctions.Beta(double.PositiveInfinity, 2.0)));
        Assert.IsTrue(double.IsNaN(SpecialFunctions.Beta(2.0, double.PositiveInfinity)));
        Assert.IsTrue(double.IsNaN(SpecialFunctions.Beta(double.NaN, double.NegativeInfinity)));
        Assert.IsTrue(double.IsNaN(SpecialFunctions.Beta(double.NegativeInfinity, double.NaN)));
        Assert.AreEqual(0.0, SpecialFunctions.Beta(-1.5, -0.5));
        Assert.ThrowsExactly<ExpressionException>(() => SpecialFunctions.Beta(double.NegativeInfinity, 2.0));
        Assert.ThrowsExactly<ExpressionException>(() => SpecialFunctions.Beta(-1.0, double.PositiveInfinity));
    }
}
