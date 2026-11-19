"""Heuristic scanner for C# local-variable scoping errors (CS0128 duplicate in scope, CS0136 name reused in a
nested/enclosing scope). Not a full parser: lambdas parameters and pattern variables are ignored."""
import re, glob, sys, os

KW = {"return","throw","new","else","await","yield","case","goto","using","lock","if","while","do","try","finally","switch","break","continue","delete","in","is","as","out","ref","default","base","this","var_"}
TYPE = r'(?:var|(?:[A-Za-z_][\w.]*(?:<[^;=]*?>)?(?:\[,*\])*\??)|\([^;=]*?\)\??)'
DECL = re.compile(r'^(?:const\s+|static\s+|readonly\s+)*(' + TYPE + r')\s+([A-Za-z_]\w*)\s*(?:=(?!=)|$)')
FOR = re.compile(r'^for\s*\(\s*(' + TYPE + r')\s+([A-Za-z_]\w*)\s*=')
FOREACH = re.compile(r'^foreach\s*\(\s*(' + TYPE + r')\s+([A-Za-z_]\w*)\s+in\b')
FOREACH_TUP = re.compile(r'^foreach\s*\(\s*\(([^)]*)\)\s+in\b')
USING = re.compile(r'^using\s*\(\s*(' + TYPE + r')\s+([A-Za-z_]\w*)\s*=')
CATCH = re.compile(r'^catch\s*\(\s*[\w.]+\s+([A-Za-z_]\w*)\s*\)')

def clean(t):
    out = []; i = 0; n = len(t)
    while i < n:
        c = t[i]
        if t.startswith("//", i):
            while i < n and t[i] != "\n": i += 1
            continue
        if t.startswith("/*", i):
            j = t.index("*/", i + 2); out.append("\n" * t.count("\n", i, j)); i = j + 2; continue
        if t.startswith('@"', i):
            j = i + 2
            while j < n:
                if t[j] == '"':
                    if j + 1 < n and t[j+1] == '"': j += 2; continue
                    break
                j += 1
            out.append('""' + "\n" * t.count("\n", i, j)); i = j + 1; continue
        if c == '"':
            j = i + 1
            while t[j] != '"':
                if t[j] == "\\": j += 1
                j += 1
            out.append('""'); i = j + 1; continue
        if c == "'":
            j = i + 1
            if t[j] == "\\":
                j += 2
                while t[j] != "'": j += 1
            else: j += 1
            out.append("' '"); i = j + 1; continue
        out.append(c); i += 1
    return "".join(out)

class Scope:
    def __init__(self, kind, parent):
        self.kind, self.parent, self.names, self.desc = kind, parent, set(), set()

def scan(path):
    text = clean(open(path, encoding="utf-8").read())
    problems = []; line = 1
    root = Scope("type", None); cur = root
    pending = []          # names to attach to the next block
    frag = []; frag_line = 1
    def declare(name, ln):
        s = cur
        if s.kind == "type": return
        if name in s.names: problems.append((ln, name, "CS0128 duplicate in same scope")); return
        p = s.parent
        while p is not None:
            if name in p.names: problems.append((ln, name, "CS0136 conflicts with enclosing scope")); return
            p = p.parent
        if name in s.desc: problems.append((ln, name, "CS0136 conflicts with a nested scope declared earlier")); return
        s.names.add(name)
    def process(fragment, ln):
        nonlocal pending
        f = " ".join(fragment.split()).strip()
        if not f: return
        m = FOREACH_TUP.match(f)
        if m:
            for part in m.group(1).split(","):
                bits = part.split()
                if bits: pending.append((bits[-1], ln))
            return
        for rx in (FOREACH, FOR, USING):
            m = rx.match(f)
            if m: pending.append((m.group(2), ln)); return
        m = CATCH.match(f)
        if m: pending.append((m.group(1), ln)); return
        if re.match(r'^(if|while|switch|lock|else|do|try|finally|return|throw)\b', f): return
        m = DECL.match(f)
        if m and m.group(1) not in KW and m.group(2) not in KW:
            declare(m.group(2), ln)
    buf = []; start_line = 1
    i = 0; n = len(text)
    header = []     # text since last boundary, for deciding scope kind & params
    while i < n:
        c = text[i]
        if c == "\n": line += 1
        if c in ";{}":
            frag_txt = "".join(buf); buf = []
            if c == ";":
                process(frag_txt, start_line)
            elif c == "{":
                h = " ".join(frag_txt.split())
                is_type = bool(re.search(r'\b(class|struct|enum|interface|namespace|record)\b', h)) and "=>" not in h
                # object / collection initialisers and array initialisers: treat as block scope but no params
                new = Scope("type" if is_type else "block", cur)
                if not is_type:
                    # method parameters
                    m = re.search(r'\(([^()]*)\)\s*(?:where [^{]*)?$', h)
                    is_lambda = "=>" in h
                    if m and not is_lambda and not re.match(r'^(if|while|for|foreach|switch|using|lock|catch|else if)\b', h):
                        for prm in m.group(1).split(","):
                            prm = prm.strip().split("=")[0].strip()
                            bits = prm.split()
                            if len(bits) >= 2 and re.match(r'^[A-Za-z_]\w*$', bits[-1]): new.names.add(bits[-1])
                    # header declarations (for/foreach/using/catch) are processed before this point
                    process(frag_txt, start_line)
                    for nm, ln in pending:
                        if nm in new.names or any(nm in p.names for p in iter_parents(cur)):
                            problems.append((ln, nm, "CS0136/CS0128 loop/catch variable conflicts"))
                        elif nm in new.desc: pass
                        new.names.add(nm)
                    pending = []
                cur = new
            else:
                closing = cur
                if closing.parent is not None:
                    closing.parent.desc |= closing.names | closing.desc
                cur = closing.parent or root
            buf = []; start_line = line
        else:
            if not buf and not c.isspace(): start_line = line
            buf.append(c)
        i += 1
    return problems

def iter_parents(s):
    while s is not None:
        yield s; s = s.parent

total = 0
for f in sorted(glob.glob(sys.argv[1] + "/**/*.cs", recursive=True)):
    if "/obj/" in f or "/bin/" in f: continue
    for ln, name, kind in scan(f):
        print(f"{os.path.relpath(f, sys.argv[1])}:{ln}: '{name}' {kind}"); total += 1
print("scope problems:", total)
