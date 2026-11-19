import sys, glob

def check(path):
    t = open(path, encoding="utf-8").read()
    i, n = 0, len(t)
    stack = []
    line = 1
    errs = []
    pairs = {')': '(', ']': '[', '}': '{'}
    while i < n:
        c = t[i]
        if c == '\n': line += 1
        if t.startswith('//', i):
            while i < n and t[i] != '\n': i += 1
            continue
        if t.startswith('/*', i):
            j = t.index('*/', i + 2)
            line += t.count('\n', i, j); i = j + 2; continue
        if t.startswith('@"', i):
            i += 2
            while i < n:
                if t[i] == '"':
                    if i + 1 < n and t[i+1] == '"': i += 2; continue
                    break
                if t[i] == '\n': line += 1
                i += 1
            i += 1; continue
        if t.startswith('$"', i) or t.startswith('$@"', i):
            errs.append(f"line {line}: interpolated string (not expected)")
        if c == '"':
            i += 1
            while i < n and t[i] != '"':
                if t[i] == '\\': i += 1
                if t[i] == '\n': errs.append(f"line {line}: newline in string")
                i += 1
            i += 1; continue
        if c == "'":
            i += 1
            if t[i] == '\\':
                i += 2
                while t[i] != "'": i += 1   # \u0000 style
            else:
                i += 1
            if t[i] != "'": errs.append(f"line {line}: bad char literal")
            i += 1; continue
        if c in '([{': stack.append((c, line))
        elif c in ')]}':
            if not stack or stack[-1][0] != pairs[c]:
                errs.append(f"line {line}: unmatched {c} (stack top {stack[-1] if stack else None})")
            else:
                stack.pop()
        i += 1
    for s in stack: errs.append(f"line {s[1]}: unclosed {s[0]}")
    return errs

bad = 0
for f in sorted(glob.glob(sys.argv[1] + "/**/*.cs", recursive=True)):
    e = check(f)
    if e:
        bad += 1
        print(f, e[:4])
print("files with problems:", bad)
