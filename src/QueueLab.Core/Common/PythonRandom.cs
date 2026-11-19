using System.Numerics;
using System.Security.Cryptography;

namespace QueueLab.Core.Common;

/// <summary>
/// A bit-exact port of CPython's <c>random.Random</c> (Mersenne Twister MT19937, CPython integer seeding,
/// 53-bit <c>random()</c> and <c>expovariate()</c>). Given the same seed it produces exactly the same
/// numbers as the original Python backend, so seeded simulations reproduce identical customers.
/// </summary>
public sealed class PythonRandom
{
    private const int N = 624;
    private const int M = 397;

    private readonly uint[] _mt = new uint[N];
    private int _index = N + 1;

    /// <summary>Seed from OS entropy (equivalent of <c>random.Random(None)</c>).</summary>
    public PythonRandom()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        uint[] key = new uint[8];
        for (int i = 0; i < key.Length; i++)
        {
            key[i] = BitConverter.ToUInt32(bytes, i * 4);
        }

        InitByArray(key);
    }

    /// <summary>Seed with an integer (equivalent of <c>random.Random(seed)</c>); the sign is ignored like in CPython.</summary>
    public PythonRandom(BigInteger seed)
    {
        BigInteger n = BigInteger.Abs(seed);
        List<uint> key = new List<uint>();

        if (n.IsZero)
        {
            key.Add(0u);
        }
        else
        {
            BigInteger mask = new BigInteger(uint.MaxValue);
            while (n > BigInteger.Zero)
            {
                key.Add((uint)(n & mask));
                n >>= 32;
            }
        }

        InitByArray(key.ToArray());
    }

    public PythonRandom(long seed) : this(new BigInteger(seed))
    {
    }

    /// <summary>Creates a generator from an optional seed (<c>null</c> means a random seed).</summary>
    public static PythonRandom FromSeed(BigInteger? seed)
    {
        return seed.HasValue ? new PythonRandom(seed.Value) : new PythonRandom();
    }

    private void InitGenrand(uint s)
    {
        _mt[0] = s;
        for (int i = 1; i < N; i++)
        {
            uint prev = _mt[i - 1];
            _mt[i] = unchecked(1812433253u * (prev ^ (prev >> 30)) + (uint)i);
        }

        _index = N;
    }

    private void InitByArray(uint[] key)
    {
        InitGenrand(19650218u);

        int i = 1;
        int j = 0;
        int k = Math.Max(N, key.Length);

        for (; k > 0; k--)
        {
            uint prev = _mt[i - 1];
            _mt[i] = unchecked((_mt[i] ^ ((prev ^ (prev >> 30)) * 1664525u)) + key[j] + (uint)j);
            i++;
            j++;
            if (i >= N)
            {
                _mt[0] = _mt[N - 1];
                i = 1;
            }

            if (j >= key.Length)
            {
                j = 0;
            }
        }

        for (k = N - 1; k > 0; k--)
        {
            uint prev = _mt[i - 1];
            _mt[i] = unchecked((_mt[i] ^ ((prev ^ (prev >> 30)) * 1566083941u)) - (uint)i);
            i++;
            if (i >= N)
            {
                _mt[0] = _mt[N - 1];
                i = 1;
            }
        }

        _mt[0] = 0x80000000u;
        _index = N;
    }

    private uint NextUInt32()
    {
        if (_index >= N)
        {
            for (int kk = 0; kk < N; kk++)
            {
                uint y = (_mt[kk] & 0x80000000u) | (_mt[(kk + 1) % N] & 0x7fffffffu);
                uint v = _mt[(kk + M) % N] ^ (y >> 1);
                if ((y & 1u) != 0u)
                {
                    v ^= 0x9908b0dfu;
                }

                _mt[kk] = v;
            }

            _index = 0;
        }

        uint r = _mt[_index];
        _index++;

        r ^= r >> 11;
        r ^= (r << 7) & 0x9d2c5680u;
        r ^= (r << 15) & 0xefc60000u;
        r ^= r >> 18;
        return r;
    }

    /// <summary>Equivalent of <c>random.random()</c>: a float in [0, 1) with 53 bits of randomness.</summary>
    public double NextDouble()
    {
        uint a = NextUInt32() >> 5;
        uint b = NextUInt32() >> 6;
        return (a * 67108864.0 + b) * (1.0 / 9007199254740992.0);
    }

    /// <summary>Equivalent of <c>random.expovariate(lambd)</c>.</summary>
    public double ExpoVariate(double lambda)
    {
        return -Math.Log(1.0 - NextDouble()) / lambda;
    }
}
