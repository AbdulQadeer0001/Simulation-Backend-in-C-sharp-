"""Cheap stand-in for a compiler's name-resolution pass: checks that referenced project types/members exist
and that the needed `using` directives are present. Not a full C# parser."""
import re, glob, os, sys
root = sys.argv[1]
files = [f for f in glob.glob(root + "/**/*.cs", recursive=True) if "/obj/" not in f and "/bin/" not in f]

def strip(t):
    t = re.sub(r'//[^\n]*', '', t); t = re.sub(r'/\*.*?\*/', '', t, flags=re.S)
    t = re.sub(r'@"(?:[^"]|"")*"', '""', t); t = re.sub(r'"(?:\\.|[^"\\\n])*"', '""', t)
    return t

src = {f: strip(open(f, encoding="utf-8").read()) for f in files}
types = {}      # type name -> (namespace, file, members set, kind)
for f, t in src.items():
    ns = re.search(r'^namespace\s+([\w.]+)', t, re.M)
    ns = ns.group(1) if ns else ""
    for m in re.finditer(r'\b(?:public|internal)?\s*(?:static\s+|sealed\s+|abstract\s+|partial\s+)*(class|struct|enum|interface|record)\s+(\w+)', t):
        name = m.group(2)
        if name in types and types[name][0] != ns and name not in ("Program",):
            print("NOTE duplicate type name", name, ns, types[name][0])
        types.setdefault(name, (ns, f, set(), m.group(1)))
# members: collect identifiers declared as methods/properties/fields/consts/enum values per type (approximate: all words followed by ( = { ; , within file)
for name, (ns, f, mem, kind) in types.items():
    t = src[f]
    # slice the type body
    m = re.search(r'\b(?:class|struct|enum|interface|record)\s+' + name + r'\b', t)
    body = t[m.end():] if m else t
    for w in re.finditer(r'\b([A-Za-z_]\w*)\s*(?:\(|\{\s*get|=>|=|;|,|\{)', body):
        mem.add(w.group(1))
    if kind == "enum":
        pass

problems = 0
for f, t in src.items():
    ns = re.search(r'^namespace\s+([\w.]+)', t, re.M); ns = ns.group(1) if ns else ""
    usings = set(re.findall(r'^using\s+([\w.]+)\s*;', t, re.M))
    # implicit usings of web projects & global
    # 1) static member references  Type.Member
    for m in re.finditer(r'\b([A-Z]\w+)\.([A-Za-z_]\w*)', t):
        tn, mem = m.group(1), m.group(2)
        if tn in types and types[tn][3] in ("class", "struct", "enum"):
            tns, tf, members, kind = types[tn]
            if mem not in members and tf != f:
                # nested types / generic: skip when mem is a nested type name
                if mem in types: continue
                print(f"{os.path.relpath(f, root)}: {tn}.{mem} not found in {os.path.relpath(tf, root)}"); problems += 1
    # 2) namespace imports for used project types
    for tn, (tns, tf, members, kind) in types.items():
        if tf == f or not tns: continue
        if re.search(r'(?<![\w.])' + tn + r'\b(?!\s*=)', t):
            ok = ns == tns or ns.startswith(tns + ".") or tns in usings
            # parent namespace visibility (e.g. QueueLab.Desktop.Pages sees QueueLab.Desktop.Theme)
            if not ok:
                parts = ns.split(".")
                for i in range(len(parts), 0, -1):
                    if ".".join(parts[:i]) == tns: ok = True
            # nested type referenced with qualification (Outer.Inner) is fine
            if not ok and not re.search(r'\b\w+\.' + tn + r'\b', t):
                print(f"{os.path.relpath(f, root)}: uses {tn} ({tns}) without 'using {tns};'"); problems += 1
print("cross-reference problems:", problems, "| types indexed:", len(types))
