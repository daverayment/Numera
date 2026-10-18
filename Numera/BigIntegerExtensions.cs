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
}
