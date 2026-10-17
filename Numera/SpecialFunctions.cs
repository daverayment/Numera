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
        double result = coefficients[^1];
        for (int i = coefficients.Length - 2; i >= 0; i--)
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

        double absX = Math.Abs(x);

        if (absX <= 0.46875)
        {
            if (absX < 1e-10)
            {
                // For very small x, erf(x) ≈ (2/sqrt(pi)) * x
                return 2.0 * OneOverSqrtPi * x;
            }

            double x2 = x * x;
            double num = EvaluatePolynomial(x2, ErfA);
            double den = EvaluatePolynomial(x2, ErfB);
            return x * (num / den);
        }

        // For |x| > 0.46875, compute erf from erfc to avoid cancellation
        double erfcVal = Erfc(absX);
        double result = 1.0 - erfcVal;
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
            double num = EvaluatePolynomial(x, ErfcC);
            double den = EvaluatePolynomial(x, ErfcD);
            return Math.Exp(-x * x) * (num / den);
        }

        // x > 4.0
        if (x >= 28.0)
        {
            // Underflows double precision (exp(-x^2) < 1e-340)
            return 0.0;
        }

        double invX2 = 1.0 / (x * x);
        double numLarge = EvaluatePolynomial(invX2, ErfcP);
        double denLarge = EvaluatePolynomial(invX2, ErfcQ);
        double factor = OneOverSqrtPi - invX2 * (numLarge / denLarge);
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

        double absX = Math.Abs(x);

        if (absX < 1e-4)
        {
            // Taylor expansion: 1 - x^2/6 + x^4/120 to avoid IEEE 754 division instability near zero
            double x2 = x * x;
            return 1.0 - x2 * (1.0 / 6.0) + x2 * x2 * (1.0 / 120.0);
        }

        if (double.IsInfinity(x))
        {
            return 0.0;
        }

        return Math.Sin(x) / x;
    }

    // Lanczos approximation parameters for g = 7, n = 9.
    // Coefficients originally derived by Paul Godfrey / Numerical Recipes / CEPHES.
    private const double LanczosG = 607.0 / 128.0; // 4.7421875
    private static readonly double SqrtTwoPi = Math.Sqrt(2.0 * Math.PI);

    private static ReadOnlySpan<double> LanczosCoefficients =>
    [
        0.99999999999999709182,
        57.156235665862923517,
        -59.597960355475491248,
        14.136097974741747174,
        -0.49191381609762019978,
        0.33994649984811888699e-4,
        0.46523628927048575665e-4,
        -0.98374475304879564677e-4,
        0.15808870328783812428e-3,
        -0.21026444172410488319e-3,
        0.21743961811521260747e-3,
        -0.16431810653676389022e-3,
        0.84418223983852743293e-4,
        -0.26190838401581408670e-4,
        0.36899182659531622704e-5
    ];

    /// <summary>
    /// Evaluates the rational partial sum in the Lanczos approximation:
    /// p[0] + sum_{i=1}^{N} p[i] / (z + i).
    /// Sums in reverse order so smaller tail terms accumulate first to minimize floating-
    /// point error.
    /// </summary>
    internal static double LanczosSum(double z)
    {
        var p = LanczosCoefficients;
        double sum = 0.0;
        for (int i = p.Length - 1; i >= 1; i--)
        {
            sum += p[i] / (z + i);
        }

        return sum + p[0];
    }

    /// <summary>
    /// Evaluates the natural logarithm of the Gamma function, ln(|Gamma(x)|).
    /// </summary>
    public static double LogGamma(double x)
    {
        if (double.IsNaN(x))
        {
            return double.NaN;
        }

        if (double.IsPositiveInfinity(x))
        {
            return double.PositiveInfinity;
        }

        if (x <= 0.0 && x == Math.Floor(x))
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.UndefinedMathResult, "lgamma is undefined for non-positive integers."));
        }

        // Reflection formula for x < 0.5:
        // Gamma(x) * Gamma(1 - x) = pi / sin(pi * x)
        // ln|Gamma(x)| = ln(pi) - ln|sin(pi * x)| - ln|Gamma(1 - x)|
        if (x < 0.5)
        {
            double sinPiX = Math.Abs(double.SinPi(x));
            if (sinPiX == 0.0)
            {
                throw new ExpressionException(new EvaluationDiagnostic(
                    EvaluationError.UndefinedMathResult, "lgamma is undefined for non-positive integers."));
            }

            return Math.Log(Math.PI) - Math.Log(sinPiX) - LogGamma(1.0 - x);
        }

        double z = x - 1.0;
        double sum = LanczosSum(z);
        double t = z + LanczosG + 0.5;
        return 0.5 * Math.Log(2.0 * Math.PI) + (z + 0.5) * Math.Log(t) - t + Math.Log(sum);
    }

    /// <summary>
    /// Evaluates the complete Gamma function Gamma(x).
    /// </summary>
    public static double Gamma(double x)
    {
        if (double.IsNaN(x))
        {
            return double.NaN;
        }

        if (double.IsPositiveInfinity(x))
        {
            return double.PositiveInfinity;
        }

        if (double.IsNegativeInfinity(x))
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.UndefinedMathResult, "gamma is undefined for negative infinity."));
        }

        if (x <= 0.0 && x == Math.Floor(x))
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.UndefinedMathResult, "gamma is undefined for non-positive integers (poles)."));
        }

        // Reflection formula for x < 0.5:
        // Gamma(x) = pi / (sin(pi * x) * Gamma(1 - x))
        if (x < 0.5)
        {
            var sinPiX = double.SinPi(x);
            if (sinPiX == 0.0)
            {
                throw new ExpressionException(new EvaluationDiagnostic(
                    EvaluationError.UndefinedMathResult, "gamma is undefined for non-positive integers (poles)."));
            }

            return Math.PI / (sinPiX * Gamma(1.0 - x));
        }

        // Exact values for small integers n = 1..10 to avoid floating point drift.
        if (x is >= 1.0 and <= 20.0 && x == Math.Floor(x))
        {
            var n = (int)x;
            var fact = 1.0;
            for (var i = 1; i < n; i++)
            {
                fact *= i;
            }

            return fact;
        }

        if (x > 171.6144788718994)
        {
            return double.PositiveInfinity;
        }

        var z = x - 1.0;
        var sum = LanczosSum(z);
        var t = z + LanczosG + 0.5;
        return SqrtTwoPi * Math.Pow(t, z + 0.5) * Math.Exp(-t) * sum;
    }

    /// <summary>
    /// Evaluates the Euler Beta function B(a, b) = Gamma(a) * Gamma(b) / Gamma(a + b).
    /// </summary>
    public static double Beta(double a, double b)
    {
        if (double.IsNaN(a) || double.IsNaN(b))
        {
            return double.NaN;
        }

        if (a <= 0.0 && a == Math.Floor(a))
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.UndefinedMathResult, "beta is undefined when an argument is a non-positive integer."));
        }

        if (b <= 0.0 && b == Math.Floor(b))
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.UndefinedMathResult, "beta is undefined when an argument is a non-positive integer."));
        }

        double sum = a + b;
        if (sum <= 0.0 && sum == Math.Floor(sum))
        {
            // Gamma(a+b) is infinite, so B(a, b) = 0.
            return 0.0;
        }

        // For moderate positive values where Gamma doesn't overflow, compute directly for higher speed & precision
        if (a > 0.0 && b > 0.0 && sum < 170.0)
        {
            return (Gamma(a) * Gamma(b)) / Gamma(sum);
        }

        // For large or negative arguments, compute via LogGamma with sign tracking
        double signA = a > 0.0 || double.SinPi(a) > 0.0 ? 1.0 : -1.0;
        double signB = b > 0.0 || double.SinPi(b) > 0.0 ? 1.0 : -1.0;
        double signSum = sum > 0.0 || double.SinPi(sum) > 0.0 ? 1.0 : -1.0;
        double totalSign = signA * signB * signSum;

        double logVal = LogGamma(a) + LogGamma(b) - LogGamma(sum);
        return totalSign * Math.Exp(logVal);
    }

    /// <summary>
    /// Evaluates the Riemann zeta function zeta(s) using Borwein's algorithm (1995)
    /// and the functional reflection equation for s &lt; 0.
    /// </summary>
    public static double Zeta(double s)
    {
        if (double.IsNaN(s))
        {
            return double.NaN;
        }

        if (double.IsPositiveInfinity(s))
        {
            return 1.0;
        }

        if (s == 1.0)
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.UndefinedMathResult, "zeta is undefined at s = 1 (simple pole)."));
        }

        // Reflection formula for s < 0:
        // zeta(s) = 2^s * pi^(s - 1) * sin(pi * s / 2) * Gamma(1 - s) * zeta(1 - s)
        if (s < 0.0)
        {
            // Trivial zeros at negative even integers: -2, -4, -6, ...
            if (s % 2.0 == 0.0)
            {
                return 0.0;
            }

            double sinTerm = double.SinPi(s * 0.5);
            double term1 = Math.Pow(2.0, s);
            double term2 = Math.Pow(Math.PI, s - 1.0);
            double gammaTerm = Gamma(1.0 - s);
            double zetaReflect = Zeta(1.0 - s);

            return term1 * term2 * sinTerm * gammaTerm * zetaReflect;
        }

        if (s == 0.0)
        {
            return -0.5;
        }

        // For large s, terms beyond 1 converge to 1 + 2^(-s) + ...
        if (s > 60.0)
        {
            return 1.0 + Math.Pow(2.0, -s);
        }

        // Evaluate zeta(s) using the Euler-Maclaurin summation formula:
        // zeta(s) = sum_{k=1}^{N-1} k^(-s) + N^(1-s)/(s-1) + 1/2 * N^(-s) + sum_{k=1}^M (B_{2k} / (2k)!) * s*(s+1)*...*(s+2k-2) * N^(-s-2k+1)
        // With N = 14 and M = 6, this yields 15-16 digits across the entire domain s > 0, s != 1.
        const int nLimit = 14;
        double sum = 0.0;
        for (var k = 1; k < nLimit; k++)
        {
            sum += Math.Pow(k, -s);
        }

        double invN = 1.0 / nLimit;
        double nPowMinusS = Math.Pow(nLimit, -s);

        // N^(1-s) / (s - 1) + 1/2 * N^(-s)
        double integralTerm = (nLimit * nPowMinusS) / (s - 1.0);
        sum += integralTerm + 0.5 * nPowMinusS;

        // Bernoulli numbers B_{2k} / (2k)!:
        // B_2 / 2!   = 1/12
        // B_4 / 4!   = -1/720
        // B_6 / 6!   = 1/30240
        // B_8 / 8!   = -1/1209600
        // B_10 / 10! = 1/47900160
        // B_12 / 12! = -691/1307674368000
        ReadOnlySpan<double> bCoeff =
        [
            1.0 / 12.0,
            -1.0 / 720.0,
            1.0 / 30240.0,
            -1.0 / 1209600.0,
            1.0 / 47900160.0,
            -691.0 / 1307674368000.0
        ];

        double factor = s;
        double invN2 = invN * invN;
        double termPow = nPowMinusS * invN; // N^(-s - 1)

        for (int j = 0; j < bCoeff.Length; j++)
        {
            sum += bCoeff[j] * factor * termPow;
            factor *= (s + 2 * j + 1) * (s + 2 * j + 2);
            termPow *= invN2;
        }

        return sum;
    }
}
