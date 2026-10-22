using System.Numerics;

namespace Numera;

internal static class BigIntegerExtensions
{
    /// <summary>
    /// Computes the integer square root floor(sqrt(n)) of a non-negative BigInteger.
    /// </summary>
    /// <remarks>Uses Newton's method with an initial guess derived from the bit length.
    /// Returns 0 for n = 0 and 1 for small values (n = 1..3).</remarks>
    /// <param name="n">The non-negative BigInteger to find the integer square root of.
    /// </param>
    /// <returns>The largest integer r such that r * r &lt;= n.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if n is negative.</exception>
    public static BigInteger Sqrt(this BigInteger n)
    {
        if (n < BigInteger.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(n),
                "Cannot compute the square root of a negative number.");
        }

        if (n.IsZero)
        {
            return BigInteger.Zero;
        }

        if (n <= 3)
        {
            return BigInteger.One;
        }

        // Initial guess x0 = 2^(ceil(bitLength(n) / 2)), guaranteed to be >= floor(sqrt(n)).
        int bitLength = (int)n.GetBitLength();
        var x = BigInteger.One << ((bitLength + 1) / 2);

        // Use Newton's method to converge to the integer square root.
        while (true)
        {
            var y = (x + n / x) >> 1;
            if (y >= x)
            {
                return x;
            }

            x = y;
        }
    }

    public static bool TryGetExactSquareRoot(this BigInteger n, out BigInteger root)
    {
        if (n < 0)
        {
            root = BigInteger.Zero;
            return false;
        }

        // Fast quadratic residue filter modulo 64:
        // A perfect square modulo 64 can only end in 0, 1, 4, 9, 16, 17, 25, 33, 36, 41,
        // 49, or 57. We set a 64-bit mask of the non-residues to eliminate 52 of 64 non-
        // residues in a single bitwise shift and mask operation.
        // Bits set:
        //     (1UL << 0) | (1UL << 1) | (1UL << 4) | (1UL << 9) | (1UL << 16) |
        //     (1UL << 17) | (1UL << 25) | (1UL << 33) | (1UL << 36) | (1UL << 41) |
        //     (1UL << 49) | (1UL << 57)
        //     = 0x0202021202030213UL.
        const ulong squareResiduesMod64 = 0x0202021202030213UL;
        int mod64 = (int)(n & 63);
        if (((squareResiduesMod64 >> mod64) & 1) == 0)
        {
            root = BigInteger.Zero;
            return false;
        }

        var candidate = n.Sqrt();
        if ((candidate * candidate) == n)
        {
            root = candidate;
            return true;
        }

        root = BigInteger.Zero;
        return false;
    }

    /// <summary>
    /// Attempts to compute the exact integer k-th root of a <see cref="BigInteger"/>.
    /// Supports even and odd positive integer degrees. Odd roots are supported for negative numbers.
    /// Returns true if an exact integer root exists; otherwise false.
    /// </summary>
    public static bool TryGetExactRoot(this BigInteger n, int degree, out BigInteger root)
    {
        if (degree <= 0)
        {
            root = BigInteger.Zero;
            return false;
        }

        if (degree == 1)
        {
            root = n;
            return true;
        }

        if (n.IsZero)
        {
            root = BigInteger.Zero;
            return true;
        }

        if (n == BigInteger.One)
        {
            root = BigInteger.One;
            return true;
        }

        if (degree == 2)
        {
            return n.TryGetExactSquareRoot(out root);
        }

        if (n < BigInteger.Zero)
        {
            // Even roots of negative numbers have no real solution.
            if ((degree & 1) == 0)
            {
                root = BigInteger.Zero;
                return false;
            }

            // Odd roots of negative numbers: root(-n, degree) = -root(n, degree).
            if (TryGetExactRoot(-n, degree, out var posRoot))
            {
                root = -posRoot;
                return true;
            }

            root = BigInteger.Zero;
            return false;
        }

        // Degree greater than or equal to bit length of n (for n >= 2):
        // Since 2^degree > n when degree >= bitLength, the only possible integer root is 1,
        // which was already handled above.
        long bitLength = n.GetBitLength();
        if (degree >= bitLength)
        {
            root = BigInteger.Zero;
            return false;
        }

        // Compute integer k-th root floor(n^(1/degree)) via integer Newton-Raphson.
        // Initial estimate x0: 2^(ceil(bitLength / degree)).
        int initialBits = (int)((bitLength + degree - 1) / degree);
        var x = BigInteger.One << initialBits;
        int kMinus1 = degree - 1;

        while (true)
        {
            // x_{next} = ((k - 1) * x + n / x^(k - 1)) / k
            var xPow = BigInteger.Pow(x, kMinus1);
            var y = (kMinus1 * x + n / xPow) / degree;
            if (y >= x)
            {
                break;
            }

            x = y;
        }

        // x is now floor(n^(1/degree)) (or within 1 due to integer division).
        // Check if x^degree == n, or test adjacent candidates if needed.
        if (BigInteger.Pow(x, degree) == n)
        {
            root = x;
            return true;
        }

        var xPlusOne = x + 1;
        if (BigInteger.Pow(xPlusOne, degree) == n)
        {
            root = xPlusOne;
            return true;
        }

        root = BigInteger.Zero;
        return false;
    }
}
