"""Line-by-line transcription of the C# RawTable (CSV path) + SimulationUploadLoader, to check the *design*
against the recorded outputs of the real Python backend."""
import os
HERE = os.path.dirname(os.path.abspath(__file__))
GOLDEN = os.path.join(HERE, "..", "..", "tests", "QueueLab.Tests", "Golden")
import json, math, re, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

NA = {"", "#N/A", "#N/A N/A", "#NA", "-1.#IND", "-1.#QNAN", "-NaN", "-nan", "1.#IND", "1.#QNAN", "<NA>", "N/A", "NA", "NULL", "NaN", "None", "n/a", "nan", "null"}
TRUE = {"True", "TRUE", "true"}; FALSE = {"False", "FALSE", "false"}
NUM = re.compile(r"^(?:\d+(?:_\d+)*(?:\.(?:\d+(?:_\d+)*)?)?|\.\d+(?:_\d+)*)(?:[eE][+-]?\d+(?:_\d+)*)?$")

def py_float_try(text):                     # PyFloat.TryParse
    s = text.strip()
    if not s: return None
    neg = False; pos = 0
    if s[0] in "+-": neg = s[0] == "-"; pos = 1
    rest = s[pos:]; low = rest.lower()
    if low in ("inf", "infinity"): return -math.inf if neg else math.inf
    if low == "nan": return math.nan
    if not NUM.match(rest): return None
    v = float(rest.replace("_", ""))
    return -v if neg else v

def try_number(tok):                        # RawTable.TryParseNumber
    return None if "_" in tok else py_float_try(tok)

def parse_records(text):                    # ParseCsvRecords
    records, record, field = [], [], []
    in_q = False; started = False; content = False; i = 0
    while i < len(text):
        ch = text[i]
        if in_q:
            if ch == '"':
                if i + 1 < len(text) and text[i+1] == '"': field.append('"'); i += 2; continue
                in_q = False; i += 1; continue
            field.append(ch); i += 1; continue
        if ch == '"' and not started: in_q = True; started = True; content = True; i += 1; continue
        if ch == ",": record.append("".join(field)); field = []; started = False; content = True; i += 1; continue
        if ch in "\r\n":
            if ch == "\r" and i + 1 < len(text) and text[i+1] == "\n": i += 1
            i += 1
            if content: record.append("".join(field)); records.append(record)
            record, field, started, content = [], [], False, False
            continue
        field.append(ch); started = True; content = True; i += 1
    if in_q: raise ValueError("unterminated")
    if content: record.append("".join(field)); records.append(record)
    return records

def make_names(header):                     # MakeColumnNames
    names = []
    for i, h in enumerate(header):
        names.append(f"Unnamed: {i}" if h == "" else h)
    counts = {}
    for i in range(len(names)):
        name = names[i]; cur = counts.get(name, 0)
        while cur > 0:
            counts[name] = cur + 1
            name = f"{name}.{cur}"; cur = counts.get(name, 0)
        names[i] = name; counts[name] = cur + 1
    return names

def is_int_tok(t):
    s = t.strip()
    if not s: return False
    st = 1 if s[0] in "+-" else 0
    return st < len(s) and all("0" <= c <= "9" for c in s[st:])

def infer(name, tokens):                    # InferCsvColumn -> (kind, values)
    count = len(tokens); missing = sum(t is None for t in tokens)
    if missing == count: return name, "Float", [None] * count
    non = [t for t in tokens if t is not None]
    all_bool = all(t in TRUE or t in FALSE for t in non)
    all_int = all(is_int_tok(t) for t in non)
    all_num = all(try_number(t) is not None for t in non)
    if all_bool:
        return name, ("Bool" if missing == 0 else "Object"), [None if t is None else (t in TRUE) for t in tokens]
    if all_int:
        fits = all(-2**63 <= int(t.strip()) < 2**63 for t in non)
        if not fits: return name, "Object", [None if t is None else int(t.strip()) for t in tokens]
        if missing == 0: return name, "Int", [int(t.strip()) for t in tokens]
        return name, "Float", [None if t is None else float(int(t.strip())) for t in tokens]
    if all_num:
        vals = []
        for t in tokens:
            if t is None: vals.append(None); continue
            v = try_number(t); vals.append(None if (isinstance(v, float) and math.isnan(v)) else v)
        return name, "Float", vals
    return name, "Object", list(tokens)

def from_csv(data):
    text = data.decode("utf-8")
    if text.startswith("\ufeff"): text = text[1:]
    recs = parse_records(text)
    if not recs: raise ValueError("no columns")
    header = recs[0]; width = len(header)
    cols = [[] for _ in range(width)]
    for r in recs[1:]:
        if len(r) > width: raise ValueError("tokenizing")
        for c in range(width):
            tok = r[c] if c < len(r) else None
            cols[c].append(None if tok is None or tok in NA else tok)
    names = make_names(header)
    return [infer(names[c], cols[c]) for c in range(width)], len(recs) - 1

def row_kind(columns):
    kinds = [k for _, k, _ in columns]
    if kinds and all(k == "Int" for k in kinds): return "Int"
    if kinds and all(k in ("Int", "Float") for k in kinds): return "Float"
    return "Object"

def pystr(v):
    if isinstance(v, bool): return "True" if v else "False"
    return repr(v) if isinstance(v, float) else str(v)

class Err(Exception): pass

def to_float(v):                            # PyTypes.ToFloat
    if isinstance(v, bool): return 1.0 if v else 0.0
    if isinstance(v, (int, float)): return float(v)
    if isinstance(v, str):
        p = py_float_try(v)
        if p is None: raise Err("conv")
        return p
    raise Err("conv")

def load(data, filename):                   # SimulationUploadLoader.Load
    if data is None or not filename: raise Err("No file was uploaded.")
    name = filename.split("/")[-1]; dot = name.rfind(".")
    ext = "" if dot <= 0 or dot == len(name) - 1 else name[dot:].lower()
    if ext not in (".csv", ".xlsx", ".xls"): raise Err("Unsupported file type. Upload CSV, XLSX, or XLS.")
    try:
        if ext != ".csv": raise ValueError("excel not mirrored")
        columns, nrows = from_csv(data)
    except Exception:
        raise Err("Could not read the uploaded file. Check its format.")
    if nrows == 0 or not columns: raise Err("The uploaded file contains no customers.")
    if nrows > 100000: raise Err("The file exceeds the limit of 100,000 customers.")
    by = {}
    for col in columns:
        n = col[0].strip().lower()
        if n in by: raise Err("The file contains duplicate column names.")
        by[n] = col
    missing = sorted(r for r in ("arrival_time", "service_time") if r not in by)
    if missing: raise Err("Missing required columns: " + ", ".join(missing))
    rk = row_kind(columns)
    def rowval(col, i):
        v = col[2][i]
        if v is None: return None
        if rk == "Float" and isinstance(v, int) and not isinstance(v, bool): return float(v)
        return v
    out = []; seen = set()
    for i in range(nrows):
        rn = i + 2
        a = rowval(by["arrival_time"], i); s = rowval(by["service_time"], i)
        msg = f"Row {rn}: arrival_time and service_time must be numeric."
        if a is None or s is None or isinstance(a, bool) or isinstance(s, bool): raise Err(msg)
        try: at, st = to_float(a), to_float(s)
        except Err: raise Err(msg)
        if not (math.isfinite(at) and math.isfinite(st)): raise Err(f"Row {rn}: time values must be finite.")
        if at < 0: raise Err(f"Row {rn}: arrival_time cannot be negative.")
        if st <= 0: raise Err(f"Row {rn}: service_time must be positive.")
        gen = f"Customer-{len(out)+1:03d}"
        if "customer_id" in by:
            raw = rowval(by["customer_id"], i)
            text = None if raw is None else pystr(raw).strip()
            cid = gen if not text else text
        else: cid = gen
        if cid in seen: raise Err(f"Row {rn}: duplicate customer_id '{cid}'.")
        seen.add(cid); out.append((cid, at, st))
    return out

if __name__ == "__main__":
    import base64
    cases = json.load(open(os.path.join(GOLDEN, "api_golden.json")))["cases"]
    checked = bad = 0
    for c in cases:
        if c["method"] != "UPLOAD" or c["path"] != "/api/simulation/upload-and-run": continue
        # Route-level checks (missing file, invalid simulation_type) run before the loader; not part of this mirror.
        if c["name"] in ("upload csv bad sim type", "upload missing file"): continue
        f = c.get("file")
        if f and not f["name"].lower().endswith(".csv") and not f["name"].lower().endswith(".txt"): continue
        form = c["form"]
        if (form.get("simulation_type") or "single_server").strip().lower() == "multi_server" and "server_count" not in form and False: pass
        data = base64.b64decode(f["content_base64"]) if f else None
        try:
            got = load(data, f["name"] if f else None); status = 200
        except Err as e:
            got = str(e); status = 400
        exp = c["expected"]; checked += 1
        if status != c["status"]:
            # the HTTP layer (missing file / multi-server count) is covered separately
            if c["name"] in ("upload csv multi no count", "upload csv multi bad count"): continue
            bad += 1; print("STATUS", c["name"], status, c["status"], got); continue
        if status == 400:
            want = exp.get("message")
            if got != want: bad += 1; print("MSG", c["name"], repr(got), "!=", repr(want))
        else:
            ids = [x["customer_id"] for x in exp["customers"]]
            mine = sorted(got, key=lambda t: t[1])      # engine sorts by arrival (stable)
            if [m[0] for m in mine] != ids:
                bad += 1; print("IDS", c["name"], [m[0] for m in mine], ids)
    print(f"upload csv cases checked: {checked}, mismatches: {bad}")
