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
    public static BigInteger Gcd(BigInteger a, BigInteger b)
    {
        ValidateInteger(a);
        ValidateInteger(b);

        // Euclidean remainders and quotients cannot exceed the input magnitudes.
        ValidateIntermediate(a);
        ValidateIntermediate(b);

        return BigInteger.GreatestCommonDivisor(BigInteger.Abs(a), BigInteger.Abs(b));
    }

    /// <summary>
    /// Computes the least common multiple of two integers.
    /// Returns 0 if either argument is 0; always non-negative.
    /// </summary>
    public static BigInteger Lcm(BigInteger a, BigInteger b)
    {
        ValidateInteger(a);
        ValidateInteger(b);
        var absA = BigInteger.Abs(a);
        var absB = BigInteger.Abs(b);

        if (absA.IsZero || absB.IsZero)
        {
            return BigInteger.Zero;
        }

        var gcd = Gcd(absA, absB);
        var result = MathValue.Product(absA / gcd, absB);
        ValidateInteger(result);

        return result;
    }

    /// <summary>
    /// Computes the number of combinations n choose k: C(n, k) = n! / (k! * (n - k)!).
    /// </summary>
    public static BigInteger Comb(BigInteger n, BigInteger k)
    {
        ValidateInteger(n);
        ValidateInteger(k);
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
        var complement = BoundedAdd(n, -k);
        if (k > complement)
        {
            k = complement;
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
            result = MathValue.Product(result, BoundedAdd(n, -(i - 1))) / i;
            ValidateInteger(result);
        }

        return result;
    }

    /// <summary>
    /// Computes the number of permutations: P(n, k) = n! / (n - k)!.
    /// </summary>
    public static BigInteger Perm(BigInteger n, BigInteger k)
    {
        ValidateInteger(n);
        ValidateInteger(k);
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
            result = MathValue.Product(result, BoundedAdd(n, -i));
            ValidateInteger(result);
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
    /// Tests primality deterministically through the unsigned 64-bit range. Above
    /// that range, true indicates a probable prime, not a proof. False establishes
    /// non-primality. Negative numbers, 0, and 1 return false.
    /// </summary>
    public static bool IsPrime(BigInteger n)
    {
        ValidateInteger(n);

        if (n <= BigInteger.One)
        {
            return false;
        }

        if (n == 2 || n == 3)
        {
            return true;
        }

        ValidateIntermediate(n);
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

        // More fixed bases strengthen probable-prime testing, but do not supply a proof.
        return MillerRabin(n, SmallPrimes);
    }

    private static bool MillerRabin(BigInteger n, ReadOnlySpan<int> bases)
    {
        // ModPow is opaque: every modular multiplication is bounded by (n - 1)^2.
        // Reserve the conservative upper bound before entering the implementation.
        if (2 * n.GetBitLength() > CalculationLimits.MaxIntermediateBits)
        {
            throw new CalculationLimitException();
        }
        BigInteger nMinusOne = BoundedAdd(n, -BigInteger.One);
        BigInteger d = nMinusOne;
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
            if (x.IsOne || x == nMinusOne)
            {
                continue;
            }

            bool composite = true;
            for (int r = 1; r < s; r++)
            {
                x = BigInteger.ModPow(x, 2, n);
                if (x == nMinusOne)
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
        ValidateInteger(n);
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

        ValidateIntermediate(n);
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

    private static void FactorizeCompositeRecursive(
        BigInteger n, Dictionary<BigInteger, int> factors)
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

        // The opaque square-root helper uses Newton sums and a candidate square.
        // Both fit within one bit beyond the input magnitude.
        if (n.GetBitLength() + 1 > CalculationLimits.MaxIntermediateBits)
        {
            throw new CalculationLimitException();
        }
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
        for (int c = 1; c <= 10; c++)
        {
            // Vary the starting point to avoid duplicate cycles.
            BigInteger x = BoundedAdd(c, BigInteger.One);
            BigInteger y = x;
            BigInteger d = 1;

            int steps = 0;
            const int maxSteps = 100_000; // Limit steps to avoid infinite loops on pathological cases.

            while (d == 1 && steps < maxSteps)
            {
                x = BoundedAdd(MathValue.Product(x, x), c) % n;
                y = BoundedAdd(MathValue.Product(y, y), c) % n;
                y = BoundedAdd(MathValue.Product(y, y), c) % n;
                d = Gcd(BoundedAdd(x, -y), n);
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
        ValidateInteger(n);
        if (n <= BigInteger.Zero)
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.ArgumentOutOfRange, "divcount argument must be a positive integer."));
        }

        var factors = Factorize(n);
        BigInteger count = BigInteger.One;
        foreach (var factor in factors)
        {
            count = MathValue.Product(count, factor.Exponent + 1);
            ValidateInteger(count);
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
        ValidateInteger(n);
        ValidateInteger(k);
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
            BigInteger pToK = BoundedPow(factor.Prime, k);
            ValidateInteger(pToK);

            BigInteger termSum = BigInteger.One;
            BigInteger currentPower = BigInteger.One;
            for (int e = 1; e <= factor.Exponent; e++)
            {
                currentPower = MathValue.Product(currentPower, pToK);
                ValidateInteger(currentPower);
                termSum = BoundedAdd(termSum, currentPower);
            }

            total = MathValue.Product(total, termSum);
            ValidateInteger(total);
        }

        return total;
    }

    /// <summary>
    /// Computes Euler's totient function phi(n), counting the positive integers up to n
    /// that are coprime to n.
    /// </summary>
    public static BigInteger Totient(BigInteger n)
    {
        ValidateInteger(n);
        if (n <= BigInteger.Zero)
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.ArgumentOutOfRange, "totient argument must be a positive integer."));
        }

        var factors = Factorize(n);
        BigInteger result = n;

        foreach (var factor in factors)
        {
            result = MathValue.Product(result / factor.Prime, BoundedAdd(factor.Prime, -1));
            ValidateInteger(result);
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
        ValidateInteger(n);
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
            ValidateIntermediate(count);
        }

        var result = new BigInteger(count);
        ValidateInteger(result);
        return result;
    }

    private static void ValidateInteger(BigInteger value)
    {
        if (BigInteger.Abs(value).GetBitLength() > CalculationLimits.MaxIntegerBits)
        {
            throw new CalculationLimitException();
        }
    }

    private static void ValidateIntermediate(BigInteger value)
    {
        if (BigInteger.Abs(value).GetBitLength() > CalculationLimits.MaxIntermediateBits)
        {
            throw new CalculationLimitException();
        }
    }

    private static BigInteger BoundedAdd(BigInteger a, BigInteger b)
    {
        ValidateIntermediate(a);
        ValidateIntermediate(b);
        var sum = a + b;
        ValidateIntermediate(sum);
        return sum;
    }

    private static BigInteger BoundedPow(BigInteger value, int exponent)
    {
        BigInteger result = BigInteger.One;
        while (exponent > 0)
        {
            if ((exponent & 1) != 0)
            {
                result = MathValue.Product(result, value);
            }
            exponent >>= 1;
            if (exponent > 0)
            {
                value = MathValue.Product(value, value);
            }
        }

        return result;
    }
}
