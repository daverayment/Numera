namespace Numera;

/// <summary>
/// Provides reusable numerical evaluation routines for special functions.
/// Implements Chebyshev rational approximations (Cody 1969 / SPECFUN / Cephes)
/// designed to be accurate across IEEE 754 double precision and adaptable for
/// extended-precision arithmetic.
/// </summary>
internal static class SpecialFunctions
{
    private static readonly double Sqrt2 = Math.Sqrt(2.0);

    // Cody 1969 rational Chebyshev approximation coefficients for erf/erfc.
    // Interval 1: [0, 0.46875] for erf(x) = x * P(x^2) / Q(x^2)
    // P and Q are stored from lowest degree (constant term) to highest degree:
    // P(z) = ErfA[0] + ErfA[1]*z + ErfA[2]*z^2 + ErfA[3]*z^3 + ErfA[4]*z^4
    // Q(z) = ErfB[0] + ErfB[1]*z + ErfB[2]*z^2 + ErfB[3]*z^3 + z^4
    private static ReadOnlySpan<double> ErfA =>
    [
        3.20937758913846947e03,
        3.77485237685302021e02,
        1.13864154151050156e02,
        3.16112374387056560e00,
        1.85777706184603153e-01
    ];

    private static ReadOnlySpan<double> ErfB =>
    [
        2.84423683343917062e03,
        1.28261652607737228e03,
        2.44024637934444173e02,
        2.36012909523441209e01,
        1.0
    ];

    // Interval 2: [0.46875, 4.0] for erfc(x) = exp(-x^2) * P(x) / Q(x)
    // P(x) = ErfcC[0] + ErfcC[1]*x + ... + ErfcC[8]*x^8
    // Q(x) = ErfcD[0] + ErfcD[1]*x + ... + ErfcD[7]*x^7 + x^8
    private static ReadOnlySpan<double> ErfcC =>
    [
        1.23033935479799725e03,
        2.05107837782607147e03,
        1.71204761263407058e03,
        8.81952221241769090e02,
        2.98635138197400131e02,
        6.61191906371416295e01,
        8.88314979438837594e00,
        5.64188496988670089e-01,
        2.15311535474403846e-08
    ];

    private static ReadOnlySpan<double> ErfcD =>
    [
        1.23033935480374942e03,
        3.43936767414372164e03,
        4.36261909014324716e03,
        3.29079923573345963e03,
        1.62138957456669019e03,
        5.37181101862009858e02,
        1.17693950891312499e02,
        1.57449261107098347e01,
        1.0
    ];

    // Interval 3: [4.0, inf) for erfc(x) = (exp(-x^2) / x) * (1/sqrt(pi) - (1/x^2) * P(1/x^2) / Q(1/x^2))
    // In terms of z = 1/x^2, Cody's loop evaluates:
    // P(z) = P[5] + P[4]*z + P[3]*z^2 + P[2]*z^3 + P[1]*z^4 + P[6]*z^5
    // Q(z) = Q[5] + Q[4]*z + Q[3]*z^2 + Q[2]*z^3 + Q[1]*z^4 + 1.0*z^5
    // Stored in ascending order of z (z^0 to z^5) for Horner evaluation:
    private static ReadOnlySpan<double> ErfcP =>
    [
        6.58749161529837803e-04,
        1.60837851487422766e-02,
        1.25781726111229246e-01,
        3.60344899949804439e-01,
        3.05326634961232344e-01,
        1.63153871373020978e-02
    ];

    private static ReadOnlySpan<double> ErfcQ =>
    [
        2.33520497626869185e-03,
        6.05183413124413191e-02,
        5.27905102951428412e-01,
        1.87295284992346047e+00,
        2.56852019228982242e+00,
        1.0
    ];

    private const double OneOverSqrtPi = 0.56418958354775628694807945156077;

    /// <summary>
    /// Evaluates a polynomial via Horner's scheme: coefficients[0] + coefficients[1]*x + ...
    /// </summary>
    internal static double EvaluatePolynomial(double x, ReadOnlySpan<double> coefficients)
    {
        var result = coefficients[^1];
        for (var i = coefficients.Length - 2; i >= 0; i--)
        {
            result = result * x + coefficients[i];
        }

        return result;
    }

    /// <summary>
    /// Evaluates the error function erf(x) across IEEE 754 double precision.
    /// </summary>
    public static double Erf(double x)
    {
        if (double.IsNaN(x))
        {
            return double.NaN;
        }

        if (double.IsInfinity(x))
        {
            return double.IsPositiveInfinity(x) ? 1.0 : -1.0;
        }

        var absX = Math.Abs(x);

        if (absX <= 0.46875)
        {
            if (absX < 1e-10)
            {
                // For very small x, erf(x) ≈ (2/sqrt(pi)) * x
                return 2.0 * OneOverSqrtPi * x;
            }

            var x2 = x * x;
            var num = EvaluatePolynomial(x2, ErfA);
            var den = EvaluatePolynomial(x2, ErfB);
            return x * (num / den);
        }

        // For |x| > 0.46875, compute erf from erfc to avoid cancellation
        var erfcVal = Erfc(absX);
        var result = 1.0 - erfcVal;
        return x < 0 ? -result : result;
    }

    /// <summary>
    /// Evaluates the complementary error function erfc(x) = 1 - erf(x) across IEEE 754 double precision.
    /// Preserves precision in the tail without catastrophic cancellation.
    /// </summary>
    public static double Erfc(double x)
    {
        if (double.IsNaN(x))
        {
            return double.NaN;
        }

        if (double.IsInfinity(x))
        {
            return double.IsPositiveInfinity(x) ? 0.0 : 2.0;
        }

        if (x < 0)
        {
            return 2.0 - Erfc(-x);
        }

        if (x <= 0.46875)
        {
            return 1.0 - Erf(x);
        }

        if (x <= 4.0)
        {
            var num = EvaluatePolynomial(x, ErfcC);
            var den = EvaluatePolynomial(x, ErfcD);
            return Math.Exp(-x * x) * (num / den);
        }

        // x > 4.0
        if (x >= 28.0)
        {
            // Underflows double precision (exp(-x^2) < 1e-340)
            return 0.0;
        }

        var invX2 = 1.0 / (x * x);
        var numLarge = EvaluatePolynomial(invX2, ErfcP);
        var denLarge = EvaluatePolynomial(invX2, ErfcQ);
        var factor = OneOverSqrtPi - invX2 * (numLarge / denLarge);
        return (Math.Exp(-x * x) / x) * factor;
    }

    /// <summary>
    /// Evaluates the standard normal cumulative distribution function (Gaussian CDF),
    /// Phi(x) = (1/2) * erfc(-x / sqrt(2)).
    /// </summary>
    public static double NormalCdf(double x)
    {
        if (double.IsNaN(x))
        {
            return double.NaN;
        }

        if (double.IsPositiveInfinity(x))
        {
            return 1.0;
        }

        if (double.IsNegativeInfinity(x))
        {
            return 0.0;
        }

        return 0.5 * Erfc(-x / Sqrt2);
    }

    /// <summary>
    /// Evaluates the unnormalized sinc function: sinc(x) = sin(x) / x, with sinc(0) = 1.
    /// </summary>
    public static double Sinc(double x)
    {
        if (double.IsNaN(x))
        {
            return double.NaN;
        }

        var absX = Math.Abs(x);

        if (absX < 1e-4)
        {
            // Taylor expansion: 1 - x^2/6 + x^4/120 to avoid IEEE 754 division instability near zero
            var x2 = x * x;
            return 1.0 - x2 * (1.0 / 6.0) + x2 * x2 * (1.0 / 120.0);
        }

        if (double.IsInfinity(x))
        {
            return 0.0;
        }

        return Math.Sin(x) / x;
    }
}
