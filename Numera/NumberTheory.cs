using System.Numerics;

namespace Numera;

/// <summary>
/// Numerical routines for discrete combinatorics and number theory operations on
/// arbitrary-precision integers, enforcing limits and mathematical edge cases.
/// </summary>
internal static class NumberTheory
{
    /// <summary>
    /// Computes the greatest common divisor of two integers.
    /// Returns 0 when both are 0; always non-negative.
    /// </summary>
    public static BigInteger Gcd(BigInteger a, BigInteger b) =>
        BigInteger.GreatestCommonDivisor(BigInteger.Abs(a), BigInteger.Abs(b));

    /// <summary>
    /// Computes the least common multiple of two integers.
    /// Returns 0 if either argument is 0; always non-negative.
    /// </summary>
    public static BigInteger Lcm(BigInteger a, BigInteger b)
    {
        var absA = BigInteger.Abs(a);
        var absB = BigInteger.Abs(b);

        if (absA.IsZero || absB.IsZero)
        {
            return BigInteger.Zero;
        }

        var gcd = BigInteger.GreatestCommonDivisor(absA, absB);
        var result = (absA / gcd) * absB;

        if (result.GetBitLength() > CalculationLimits.MaxIntegerBits)
        {
            throw new CalculationLimitException();
        }

        return result;
    }

    /// <summary>
    /// Computes the number of combinations n choose k: C(n, k) = n! / (k! * (n - k)!).
    /// </summary>
    public static BigInteger Comb(BigInteger n, BigInteger k)
    {
        if (n < BigInteger.Zero || k < BigInteger.Zero)
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.ArgumentOutOfRange, "comb arguments must be non-negative integers."));
        }

        if (k > n)
        {
            return BigInteger.Zero;
        }

        if (k.IsZero || k == n)
        {
            return BigInteger.One;
        }

        // Bound n to avoid denial of service.
        if (n > CalculationLimits.MaxFactorialArgument)
        {
            throw new CalculationLimitException();
        }

        // Exploit symmetry C(n, k) == C(n, n - k).
        if (k > n - k)
        {
            k = n - k;
        }

        int kInt = (int)k;
        BigInteger result = BigInteger.One;
        for (int i = 1; i <= kInt; i++)
        {
            result = (result * (n - (i - 1))) / i;

            if (result.GetBitLength() > CalculationLimits.MaxIntegerBits)
            {
                throw new CalculationLimitException();
            }
        }

        return result;
    }

    /// <summary>
    /// Computes the number of permutations: P(n, k) = n! / (n - k)!.
    /// </summary>
    public static BigInteger Perm(BigInteger n, BigInteger k)
    {
        if (n < BigInteger.Zero || k < BigInteger.Zero)
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.ArgumentOutOfRange, "perm arguments must be non-negative integers."));
        }

        if (k > n)
        {
            return BigInteger.Zero;
        }

        if (k.IsZero)
        {
            return BigInteger.One;
        }

        // Bound n to avoid denial of service.
        if (n > CalculationLimits.MaxFactorialArgument)
        {
            throw new CalculationLimitException();
        }

        int kInt = (int)k;
        BigInteger result = BigInteger.One;
        for (int i = 0; i < kInt; i++)
        {
            result *= (n - i);

            if (result.GetBitLength() > CalculationLimits.MaxIntegerBits)
            {
                throw new CalculationLimitException();
            }
        }

        return result;
    }

    /// <summary>
    /// Prime bases under 100 used for trial division and Miller-Rabin testing.
    /// </summary>
    private static ReadOnlySpan<int> SmallPrimes =>
    [
        2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53, 59, 61, 67, 71, 73, 79, 83, 89, 97
    ];

    /// <summary>
    /// The first 12 prime bases [2..37], which provably provide deterministic
    /// Miller-Rabin testing for all 64-bit integers n &lt;= 2^64.
    /// </summary>
    private static ReadOnlySpan<int> DeterministicBases64 => SmallPrimes[..12];

    /// <summary>
    /// Determines whether an integer is prime. Returns true if n is prime, false
    /// otherwise. Negative numbers, 0, and 1 return false.
    /// </summary>
    public static bool IsPrime(BigInteger n)
    {
        if (n <= BigInteger.One)
        {
            return false;
        }

        if (n == 2 || n == 3)
        {
            return true;
        }

        if (n.IsEven || n % 3 == 0)
        {
            return false;
        }

        // Fast trial division against small primes up to 100 (skipping 2 and 3).
        var trialPrimes = SmallPrimes[2..];
        foreach (int p in trialPrimes)
        {
            if (n == p)
            {
                return true;
            }

            if (n % p == 0)
            {
                return false;
            }
        }

        if (n < 100 * 100)
        {
            return true;
        }

        // Bound input bit length to protect against CPU exhaustion on large integers.
        if (n.GetBitLength() > 2048)
        {
            throw new CalculationLimitException();
        }

        // Deterministic Miller-Rabin primality test.
        // For numbers up to 2^64, testing the first 12 prime bases [2..37] is provably
        // deterministic.
        if (n <= ulong.MaxValue)
        {
            return MillerRabin(n, DeterministicBases64);
        }

        // For larger BigIntegers, use all 25 prime bases which gives virtually zero error
        // probability.
        return MillerRabin(n, SmallPrimes);
    }

    private static bool MillerRabin(BigInteger n, ReadOnlySpan<int> bases)
    {
        BigInteger d = n - 1;
        int s = 0;
        while (d.IsEven)
        {
            d >>= 1;
            s++;
        }

        foreach (int a in bases)
        {
            if (n <= a)
            {
                break;
            }

            var x = BigInteger.ModPow(a, d, n);
            if (x.IsOne || x == n - 1)
            {
                continue;
            }

            bool composite = true;
            for (int r = 1; r < s; r++)
            {
                x = BigInteger.ModPow(x, 2, n);
                if (x == n - 1)
                {
                    composite = false;
                    break;
                }
            }

            if (composite)
            {
                return false;
            }
        }

        return true;
    }
}
