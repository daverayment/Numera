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

        // Exploit symmetry C(n, k) == C(n, n - k).
        if (k > n - k)
        {
            k = n - k;
        }

        // Bound the loop iteration count.
        if (k > CalculationLimits.MaxFactorialArgument)
        {
            throw new CalculationLimitException();
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

        // Bound the loop iteration count.
        if (k > CalculationLimits.MaxFactorialArgument)
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

        // Bound input bit length to protect against CPU exhaustion on large integers
        // before entering expensive Miller-Rabin modular exponentiations.
        if (n.GetBitLength() > CalculationLimits.MaxPrimalityBits)
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

    /// <summary>
    /// Represents a prime factor and its exponent in a canonical prime factorization.
    /// Internal record struct used to compute arithmetic number-theoretic functions
    /// without prematurely allocating structured public objects.
    /// </summary>
    internal readonly record struct PrimeFactor(BigInteger Prime, int Exponent);

    /// <summary>
    /// Computes the prime factorization of a positive integer n &gt;= 1.
    /// Applies trial division up to small primes and Pollard's rho algorithm for
    /// composite factors, rejecting inputs exceeding <see
    /// cref="CalculationLimits.MaxFactorizationBits"/> to prevent DoS.
    /// </summary>
    internal static List<PrimeFactor> Factorize(BigInteger n)
    {
        if (n <= BigInteger.Zero)
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.ArgumentOutOfRange, "Factorization requires a positive integer argument."));
        }

        if (n == BigInteger.One)
        {
            return [];
        }

        if (n.GetBitLength() > CalculationLimits.MaxFactorizationBits)
        {
            throw new CalculationLimitException();
        }

        var primeCounts = new Dictionary<BigInteger, int>();

        // Remove factors of 2.
        int exp2 = 0;
        while (n.IsEven)
        {
            exp2++;
            n >>= 1;
        }
        if (exp2 > 0)
        {
            primeCounts[2] = exp2;
        }

        // Trial division against small odd primes.
        var oddSmallPrimes = SmallPrimes[1..];
        foreach (int p in oddSmallPrimes)
        {
            if (n == BigInteger.One)
            {
                break;
            }
            if (n < (long)p * p)
            {
                break;
            }

            if (n % p == 0)
            {
                int exp = 0;
                while (n % p == 0)
                {
                    exp++;
                    n /= p;
                }
                primeCounts[p] = exp;
            }
        }

        if (n > BigInteger.One)
        {
            FactorizeCompositeRecursive(n, primeCounts);
        }

        var result = new List<PrimeFactor>(primeCounts.Count);
        foreach (var kvp in primeCounts.OrderBy(k => k.Key))
        {
            result.Add(new PrimeFactor(kvp.Key, kvp.Value));
        }

        return result;
    }

    private static void FactorizeCompositeRecursive(BigInteger n, Dictionary<BigInteger, int> factors)
    {
        if (n == BigInteger.One)
        {
            return;
        }

        if (IsPrime(n))
        {
            factors[n] = factors.GetValueOrDefault(n) + 1;
            return;
        }

        // Fast O(1) quadratic residue check + Newton check.
        if (n.TryGetExactSquareRoot(out var sqrt))
        {
            FactorizeCompositeRecursive(sqrt, factors);
            FactorizeCompositeRecursive(sqrt, factors);
            return;
        }

        var divisor = PollardRho(n);
        FactorizeCompositeRecursive(divisor, factors);
        FactorizeCompositeRecursive(n / divisor, factors);
    }

    private static BigInteger PollardRho(BigInteger n)
    {
        if (n.IsEven)
        {
            return 2;
        }

        // Try a few small constants for the polynomial f(x) = x^2 + c mod n to avoid cycles.
        for (BigInteger c = 1; c <= 10; c++)
        {
            // Vary the starting point to avoid duplicate cycles.
            BigInteger x = c + 1;
            BigInteger y = c + 1;
            BigInteger d = 1;

            int steps = 0;
            const int maxSteps = 100_000; // Limit steps to avoid infinite loops on pathological cases.

            while (d == 1 && steps < maxSteps)
            {
                x = (x * x + c) % n;
                y = (y * y + c) % n;
                y = (y * y + c) % n;
                d = BigInteger.GreatestCommonDivisor(BigInteger.Abs(x - y), n);
                steps++;
            }

            // Non-trivial factor found.
            if (d > 1 && d < n)
            {
                return d;
            }
        }

        // If Pollard's rho fails within the step limit, fail safely.
        throw new CalculationLimitException(new EvaluationDiagnostic(
            EvaluationError.LimitExceeded, "Factorization exceeded computation step limit."));
    }

    /// <summary>
    /// Computes the divisor count function d(n) (also known as tau(n) or sigma_0(n)).
    /// Named DivCount rather than Tau to avoid syntactic and lexical conflicts with the
    /// circle constant tau = 2*pi, especially when supporting implicit multiplication.
    /// Returns the number of positive divisors of n for n &gt;= 1.
    /// </summary>
    public static BigInteger DivCount(BigInteger n)
    {
        if (n <= BigInteger.Zero)
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.ArgumentOutOfRange, "divcount argument must be a positive integer."));
        }

        var factors = Factorize(n);
        BigInteger count = BigInteger.One;
        foreach (var factor in factors)
        {
            count *= (factor.Exponent + 1);

            if (count.GetBitLength() > CalculationLimits.MaxIntegerBits)
            {
                throw new CalculationLimitException();
            }
        }

        return count;
    }

    /// <summary>
    /// Computes the divisor sum function sigma(n) = sigma_1(n), the sum of all positive divisors of n.
    /// </summary>
    public static BigInteger Sigma(BigInteger n) => Sigma(n, 1);

    /// <summary>
    /// Computes the generalized divisor sum function sigma_k(n) = sum_{d|n} d^k.
    /// Supports k = 0 (yielding divcount(n)) and integer k &gt;= 0.
    /// </summary>
    public static BigInteger Sigma(BigInteger n, int k)
    {
        if (n <= BigInteger.Zero)
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.ArgumentOutOfRange, "sigma argument n must be a positive integer."));
        }

        if (k < 0)
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.ArgumentOutOfRange, "sigma exponent k must be a non-negative integer."));
        }

        if (k > CalculationLimits.MaxExponentMagnitude)
        {
            throw new CalculationLimitException();
        }

        if (k == 0)
        {
            return DivCount(n);
        }

        var factors = Factorize(n);
        BigInteger total = BigInteger.One;

        foreach (var factor in factors)
        {
            // Sum = 1 + p^k + p^(2k) + ... + p^(a*k)
            BigInteger pToK = BigInteger.Pow(factor.Prime, k);
            if (pToK.GetBitLength() > CalculationLimits.MaxIntegerBits)
            {
                throw new CalculationLimitException();
            }

            BigInteger termSum = BigInteger.One;
            BigInteger currentPower = BigInteger.One;
            for (int e = 1; e <= factor.Exponent; e++)
            {
                currentPower *= pToK;
                if (currentPower.GetBitLength() > CalculationLimits.MaxIntegerBits)
                {
                    throw new CalculationLimitException();
                }
                termSum += currentPower;
            }

            total *= termSum;
            if (total.GetBitLength() > CalculationLimits.MaxIntegerBits)
            {
                throw new CalculationLimitException();
            }
        }

        return total;
    }

    /// <summary>
    /// Computes Euler's totient function phi(n), counting the positive integers up to n
    /// that are coprime to n.
    /// </summary>
    public static BigInteger Totient(BigInteger n)
    {
        if (n <= BigInteger.Zero)
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.ArgumentOutOfRange, "totient argument must be a positive integer."));
        }

        var factors = Factorize(n);
        BigInteger result = n;

        foreach (var factor in factors)
        {
            result = (result / factor.Prime) * (factor.Prime - 1);
        }

        return result;
    }

    /// <summary>
    /// Counts the number of set bits (population count) of a non-negative integer.
    /// </summary>
    /// <param name="n">The integer whose set bits to count.</param>
    /// <returns>The population count as a <see cref="BigInteger"/>.</returns>
    /// <exception cref="ExpressionException">Thrown if <paramref name="n"/> is negative.
    /// </exception>
    public static BigInteger PopCount(BigInteger n)
    {
        if (n < BigInteger.Zero)
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.ArgumentOutOfRange, "popcount requires a non-negative integer."));
        }

        int byteCount = n.GetByteCount();

        // Use stack allocation when possible to avoid heap allocs.
        Span<byte> buffer = byteCount <= 256 ? stackalloc byte[byteCount] : new byte[byteCount];
        n.TryWriteBytes(buffer, out int bytesWritten);

        long count = 0;
        for (int i = 0; i < bytesWritten; i++)
        {
            count += BitOperations.PopCount(buffer[i]);
        }

        return new BigInteger(count);
    }
}
