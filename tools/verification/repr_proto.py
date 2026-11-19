"""Mirror of the C# PyFloat.Repr: takes .NET-style shortest round-trip text and re-renders it like Python repr()."""
import math, random

def net_r(x):
    """Simulate .NET Core 3.0+ double.ToString("R"): shortest digits, E-notation when exp>=15 or exp<=-5."""
    r = repr(x)
    neg = r.startswith('-')
    if neg: r = r[1:]
    if 'e' in r:
        m, e = r.split('e'); e = int(e)
    else:
        m, e = r, 0
    ip, _, fp = m.partition('.')
    if fp == '0': fp = ''
    digits = (ip + fp).lstrip('0')
    decpt = len(ip) + e - (len(ip + fp) - len((ip + fp).lstrip('0')))
    digits = digits.rstrip('0') or '0'
    sci = decpt - 1
    if x == 0: out = '0'
    elif sci >= 15 or sci <= -5:
        mant = digits[0] + ('.' + digits[1:] if len(digits) > 1 else '')
        out = f"{mant}E{'+' if sci >= 0 else '-'}{abs(sci):02d}"
    else:
        if decpt <= 0: out = '0.' + '0' * (-decpt) + digits
        elif decpt >= len(digits): out = digits + '0' * (decpt - len(digits))
        else: out = digits[:decpt] + '.' + digits[decpt:]
    return ('-' if neg else '') + out

def py_repr_from_net(s):
    neg = s.startswith('-')
    if neg: s = s[1:]
    exp = 0
    if 'E' in s or 'e' in s:
        s = s.replace('e', 'E'); s, e = s.split('E'); exp = int(e)
    ip, _, fp = s.partition('.')
    allds = ip + fp
    decpt = len(ip) + exp
    stripped = allds.lstrip('0')
    decpt -= len(allds) - len(stripped)
    digits = stripped.rstrip('0')
    if digits == '':
        return '-0.0' if neg else '0.0'
    n = len(digits)
    if -4 < decpt <= 16:
        if decpt <= 0: out = '0.' + '0' * (-decpt) + digits
        elif decpt >= n: out = digits + '0' * (decpt - n) + '.0'
        else: out = digits[:decpt] + '.' + digits[decpt:]
    else:
        e = decpt - 1
        mant = digits[0] + ('.' + digits[1:] if n > 1 else '')
        out = f"{mant}e{'+' if e >= 0 else '-'}{abs(e):02d}"
    return ('-' if neg else '') + out

if __name__ == "__main__":
    rng = random.Random(11)
    vals = [0.0, -0.0, 1.0, 100.0, 0.5, 1e15, 1e16, 1.5e16, 123456789012345678.0, 1e-4, 1e-5, 1.5e-5, 5e-324, 1.7976931348623157e308,
            0.1, 0.30000000000000004, 2.0**53, 1e22, 1e21, 123.456, -42.0, -0.00001234]
    for _ in range(200000):
        vals.append(rng.uniform(-1, 1) * 10 ** rng.randint(-30, 30))
    for _ in range(50000):
        vals.append(float(rng.randint(-10**18, 10**18)))
    bad = 0
    for v in vals:
        got = py_repr_from_net(net_r(v))
        if got != repr(v):
            bad += 1
            if bad < 8: print("MISMATCH", repr(v), net_r(v), got)
    print(f"repr: {len(vals)} values, mismatches: {bad}")
