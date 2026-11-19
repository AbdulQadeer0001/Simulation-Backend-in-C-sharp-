"""Mirrors of the C# helpers: PyMath.Round (fast path + exact BigInteger fallback), NumpyMath.Mean, PyFloat repr."""
import math, random, struct
import numpy as np

def decompose(x):
    bits = struct.unpack('<q', struct.pack('<d', x))[0]
    sign = -1 if bits < 0 else 1
    exp = (bits >> 52) & 0x7FF
    frac = bits & ((1 << 52) - 1)
    if exp == 0:
        exp = 1                      # subnormal
    else:
        frac |= (1 << 52)
    return sign, frac, exp - 1075    # value = sign * frac * 2**e

def round_exact(x, n):
    """Exact round-half-even of the binary value to n decimals, like CPython's round(float, n)."""
    if math.isnan(x) or math.isinf(x):
        return x
    if x == 0:
        return x
    sign, mant, e = decompose(x)
    p10 = 10 ** n
    if e >= 0:
        q = (mant << e) * p10
    else:
        num = mant * p10
        den = 1 << (-e)
        q, r = divmod(num, den)
        twice = r * 2
        if twice > den or (twice == den and (q & 1) == 1):
            q += 1
    if q == 0:
        return -0.0 if sign < 0 else 0.0
    s = str(q).rjust(n + 1, '0')
    text = (s[:-n] + "." + s[-n:]) if n > 0 else s
    v = float(text)
    return -v if sign < 0 else v

def py_round(x, n):
    """Fast path (matches the C# code) with exact fallback near ties."""
    if math.isnan(x) or math.isinf(x) or x == 0:
        return x
    p10 = float(10 ** n)
    y = abs(x) * p10
    if y < 1e9:
        f = math.floor(y)
        frac = y - f
        if abs(frac - 0.5) > 1e-6:
            q = f + 1.0 if frac > 0.5 else f
            r = q / p10
            return -r if x < 0 else r      # negative zero preserved via sign handling below
    return round_exact(x, n)

def pairwise(a, lo, n):
    if n < 8:
        res = 0.0
        for i in range(n):
            res += a[lo + i]
        return res
    elif n <= 128:
        r = [a[lo + j] for j in range(8)]
        i = 8
        limit = n - (n % 8)
        while i < limit:
            for j in range(8):
                r[j] += a[lo + i + j]
            i += 8
        res = ((r[0] + r[1]) + (r[2] + r[3])) + ((r[4] + r[5]) + (r[6] + r[7]))
        while i < n:
            res += a[lo + i]
            i += 1
        return res
    else:
        n2 = n // 2
        n2 -= n2 % 8
        return pairwise(a, lo, n2) + pairwise(a, lo + n2, n - n2)

def np_mean(values):
    return pairwise(values, 0, len(values)) / len(values)

def test_round():
    rng = random.Random(7)
    bad = 0; total = 0
    cases = []
    for _ in range(300000):
        cases.append((rng.uniform(-50, 600) * rng.choice([1, 1e-3, 1e-6]), rng.choice([3, 6])))
    for _ in range(100000):
        cases.append((rng.expovariate(0.5), 6)); cases.append((rng.expovariate(1.0), 3))
    # exact ties and near ties
    for k in range(0, 4000):
        cases.append((k / 8.0, 2)); cases.append((k / 16.0, 3)); cases.append((k / 128.0, 6)); cases.append((-k / 16.0, 3))
        cases.append((k + 0.0005, 3)); cases.append((k + 0.0000005, 6)); cases.append((k + 0.4995, 3))
    cases += [(0.0, 3), (-0.0, 3), (1e-9, 6), (-1e-9, 6), (123456789.123456, 6), (5e15, 3), (1e300, 3), (2.5e-324, 6)]
    for x, n in cases:
        total += 1
        a, b = py_round(x, n), round(x, n)
        if not (a == b and math.copysign(1, a) == math.copysign(1, b)):
            bad += 1
            if bad < 8: print("ROUND MISMATCH", repr(x), n, repr(a), repr(b))
    print(f"round: {total} cases, mismatches: {bad}")

def test_mean():
    rng = random.Random(3)
    bad = 0; total = 0
    for n in [1, 2, 3, 5, 7, 8, 9, 15, 16, 17, 63, 100, 127, 128, 129, 200, 255, 256, 257, 1000, 5001]:
        for _ in range(300):
            vals = [round(rng.uniform(0, 250), 1) for _ in range(n)]
            total += 1
            if np_mean(vals) != float(np.mean(np.array(vals, dtype=np.float64))):
                bad += 1
    print(f"np.mean pairwise: {total} arrays, mismatches: {bad}")

if __name__ == "__main__":
    test_round()
    test_mean()
