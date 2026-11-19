"""Line-by-line mirror of the C# PythonRandom (MT19937 + CPython seeding + random() + expovariate)."""
import math, random

M32 = 0xFFFFFFFF

class PyRandom:
    def __init__(self, seed):
        self.mt = [0] * 624
        self.idx = 625
        # CPython: random.seed(int) -> abs(int) split into 32-bit little-endian words
        n = abs(int(seed))
        key = []
        if n == 0:
            key = [0]
        else:
            while n > 0:
                key.append(n & M32)
                n >>= 32
        self._init_by_array(key)

    def _init_genrand(self, s):
        mt = self.mt
        mt[0] = s & M32
        for i in range(1, 624):
            mt[i] = (1812433253 * (mt[i-1] ^ (mt[i-1] >> 30)) + i) & M32
        self.idx = 624

    def _init_by_array(self, key):
        mt = self.mt
        self._init_genrand(19650218)
        i, j = 1, 0
        k = 624 if 624 > len(key) else len(key)
        for _ in range(k):
            mt[i] = ((mt[i] ^ ((mt[i-1] ^ (mt[i-1] >> 30)) * 1664525)) + key[j] + j) & M32
            i += 1; j += 1
            if i >= 624:
                mt[0] = mt[623]; i = 1
            if j >= len(key):
                j = 0
        for _ in range(623):
            mt[i] = ((mt[i] ^ ((mt[i-1] ^ (mt[i-1] >> 30)) * 1566083941)) - i) & M32
            i += 1
            if i >= 624:
                mt[0] = mt[623]; i = 1
        mt[0] = 0x80000000
        self.idx = 624

    def _gen(self):
        mt = self.mt
        if self.idx >= 624:
            for kk in range(624):
                y = (mt[kk] & 0x80000000) | (mt[(kk + 1) % 624] & 0x7FFFFFFF)
                v = mt[(kk + 397) % 624] ^ (y >> 1)
                if y & 1:
                    v ^= 0x9908B0DF
                mt[kk] = v
            self.idx = 0
        y = mt[self.idx]; self.idx += 1
        y ^= (y >> 11)
        y ^= (y << 7) & 0x9D2C5680
        y ^= (y << 15) & 0xEFC60000
        y ^= (y >> 18)
        return y & M32

    def random(self):
        a = self._gen() >> 5
        b = self._gen() >> 6
        return (a * 67108864.0 + b) * (1.0 / 9007199254740992.0)

    def expovariate(self, lambd):
        return -math.log(1.0 - self.random()) / lambd


if __name__ == "__main__":
    seeds = [0, 1, 2, 42, 100, 123456789, 2**31, 2**32 - 1, 2**32, 2**32 + 5, 2**40 + 12345,
             2**64 + 7, 10**30, -1, -7, -42, 99999]
    bad = 0
    for s in seeds:
        a, b = PyRandom(s), random.Random(s)
        for _ in range(2000):   # crosses the 624-word regeneration boundary several times
            x, y = a.random(), b.random()
            if x != y:
                bad += 1; print("random mismatch", s, x, y); break
        a, b = PyRandom(s), random.Random(s)
        for lam in (0.5, 1.0, 3.7):
            for _ in range(500):
                if a.expovariate(lam) != b.expovariate(lam):
                    bad += 1; print("expo mismatch", s, lam); break
    print("seeds tested:", len(seeds), "mismatches:", bad)
