"""Transcription of the C# route-level validation (RequestParsing / *Endpoints / ReplicationService /
ComparisonService / DistributionGenerator checks) compared with the recorded status + message of the real backend."""
import os, json, math, sys
HERE = os.path.dirname(os.path.abspath(__file__))
GOLDEN = os.path.join(HERE, "..", "..", "tests", "QueueLab.Tests", "Golden")

class VE(Exception): pass
def is_int(v): return isinstance(v, int) and not isinstance(v, bool)
def is_num(v): return (isinstance(v, (int, float))) and not isinstance(v, bool)
def fin(x): return math.isfinite(x)
def get(d, k, default=None): return d[k] if k in d else default
def tname(v):
    return "NoneType" if v is None else "bool" if isinstance(v, bool) else "str" if isinstance(v, str) else "int" if isinstance(v, int) else "float" if isinstance(v, float) else "dict" if isinstance(v, dict) else "list"
def pyrepr_str(s): return "'" + s + "'"
def to_float(v):
    if isinstance(v, bool): return 1.0 if v else 0.0
    if is_num(v): return float(v)
    if isinstance(v, str):
        try: return float(v.strip().replace("_", "")) if v.strip() else (_ for _ in ()).throw(ValueError())
        except ValueError: raise VE("could not convert string to float: " + pyrepr_str(v))
    raise VE(f"float() argument must be a string or a real number, not '{tname(v)}'")

SIM = "simulation_type must be 'single_server' or 'multi_server'."
def is_sim(v): return v in ("single_server", "multi_server")

# ---- DistributionGenerator.GenerateCustomers (validation only)
def gen_validate(count, ar, sr, ad, sd, seed):
    if not is_int(count) or count < 1: raise VE("Customer count must be a positive integer.")
    if count > 1_000_000: raise VE("Customer count must not exceed 1,000,000.")
    for v, nm in ((ar, "Arrival rate"), (sr, "Service rate")):
        if not is_num(v) or not fin(float(v)) or float(v) <= 0: raise VE(nm + " must be a finite positive number.")
    if ad not in ("exponential", "deterministic"): raise VE("Unsupported arrival distribution.")
    if sd not in ("exponential", "deterministic"): raise VE("Unsupported service distribution.")
    if seed is not None and not is_int(seed): raise VE("Seed must be an integer or None.")

def mm1(body):
    if not isinstance(body, dict): return 400, "Provide a JSON request body."
    a, s = get(body, "arrival_rate"), get(body, "service_rate")
    if a is None or s is None: return 400, "arrival_rate and service_rate are required."
    try:
        lam, mu = to_float(a), to_float(s)
        if not fin(lam): raise VE("Arrival rate must be a finite number.")
        if not fin(mu): raise VE("Service rate must be a finite number.")
        if lam <= 0: raise VE("Arrival rate must be greater than zero.")
        if mu <= 0: raise VE("Service rate must be greater than zero.")
        if lam >= mu: raise VE("M/M/1 is unstable: arrival rate must be less than service rate.")
    except VE as e: return 400, str(e)
    return 200, None

def mmc(body):
    if not isinstance(body, dict): return 400, "Provide a JSON request body."
    a, s, c = get(body, "arrival_rate"), get(body, "service_rate"), get(body, "server_count")
    if a is None or s is None or c is None: return 400, "arrival_rate, service_rate, and server_count are required."
    try:
        if isinstance(c, bool): raise VE("Server count must be a positive integer.")
        n = to_float(c)
        if not fin(n) or n != math.floor(n): raise VE("Server count must be a positive integer.")
        servers = int(max(-100000, min(100000, n)))
        lam, mu = to_float(a), to_float(s)
        if not fin(lam): raise VE("Arrival rate must be finite.")
        if not fin(mu): raise VE("Service rate must be finite.")
        if lam <= 0: raise VE("Arrival rate must be greater than zero.")
        if mu <= 0: raise VE("Service rate must be greater than zero.")
        if servers < 1: raise VE("Server count must be at least 1.")
        if lam >= servers * mu: raise VE("M/M/c is unstable: arrival rate must be less than the total service capacity.")
    except VE as e: return 400, str(e)
    return 200, None

def run(body):
    if not isinstance(body, dict): return 400, "Provide a valid JSON request body."
    try:
        st, cd = get(body, "simulation_type"), get(body, "customers")
        if not is_sim(st): return 400, SIM
        if not isinstance(cd, list) or not cd: return 400, "customers must be a non-empty list."
        if st == "multi_server":
            raw = get(body, "server_count")
            if isinstance(raw, bool) or raw is None: raise VE("server_count must be a positive integer.")
            n = to_float(raw)
            if not fin(n) or n != math.floor(n) or n < 1: raise VE("server_count must be a positive integer.")
            if n > 100000: raise VE("Server count must not exceed 100,000.")
        ids = set()
        for index in range(1, len(cd) + 1):
            item = cd[index - 1]
            if not isinstance(item, dict): raise VE(f"Customer at index {index-1} must be an object.")
            rid = get(item, "customer_id", f"Customer-{index:03d}")
            if not isinstance(rid, str) or not rid.strip(): raise VE(f"Customer at index {index-1} must have a valid customer_id.")
            cid = rid.strip()
            if cid in ids: raise VE(f"Duplicate customer_id: {cid}.")
            ids.add(cid)
            a, s = get(item, "arrival_time"), get(item, "service_time")
            if a is None or s is None: raise VE(f"Customer {cid} must include arrival_time and service_time.")
            if isinstance(a, bool) or isinstance(s, bool): raise VE(f"Customer {cid} has invalid time values.")
            at, sv = to_float(a), to_float(s)
            if not fin(at) or not fin(sv): raise VE(f"Customer {cid} has non-finite time values.")
            if at < 0: raise VE(f"Arrival time for {cid} cannot be negative.")
            if sv <= 0: raise VE(f"Service time for {cid} must be greater than zero.")
    except VE as e: return 400, str(e)
    return 200, None

def require_rate(v, nm):
    if isinstance(v, bool) or v is None or not is_num(v): raise VE(nm + " must be a positive number.")
    if not fin(float(v)) or float(v) <= 0: raise VE(nm + " must be a finite positive number.")

def generate(body):
    if not isinstance(body, dict): return 400, "Provide a valid JSON request body."
    try:
        st = get(body, "simulation_type", "single_server"); cc = get(body, "customer_count")
        ar, sr = get(body, "arrival_rate"), get(body, "service_rate")
        ad, sd = get(body, "arrival_distribution", "exponential"), get(body, "service_distribution", "exponential")
        seed, rsc = get(body, "seed"), get(body, "server_count", 1)
        if not is_sim(st): raise VE(SIM)
        if not is_int(cc) or cc < 1: raise VE("customer_count must be a positive integer.")
        require_rate(ar, "arrival_rate"); require_rate(sr, "service_rate")
        if st != "single_server":
            if not is_int(rsc) or rsc < 1: raise VE("server_count must be a positive integer.")
            if rsc > 100000: raise VE("Server count must not exceed 100,000.")
        if seed is not None and not is_int(seed): raise VE("seed must be an integer or null.")
        gen_validate(cc, ar, sr, ad, sd, seed)
    except VE as e: return 400, str(e)
    return 200, None

def finpos(v): return is_num(v) and fin(float(v)) and float(v) > 0

def replicate_validate(st, cc, ar, sr, rc, sc, ad, sd, seed, cl):
    if not is_num(cl) or not fin(float(cl)) or not (0 < float(cl) < 1): raise VE("confidence_level must be a number between 0 and 1.")
    if not is_sim(st): raise VE("simulation_type must be 'single_server' or 'multi_server'.")
    if not is_int(cc) or cc < 1: raise VE("customer_count must be a positive integer.")
    if not is_int(rc) or rc < 2 or rc > 1000: raise VE("replication_count must be an integer between 2 and 1000.")
    if not finpos(ar): raise VE("arrival_rate must be a finite positive number.")
    if not finpos(sr): raise VE("service_rate must be a finite positive number.")
    if st == "multi_server":
        if not is_int(sc) or sc < 1: raise VE("server_count must be a positive integer.")
        if sc > 100000: raise VE("Server count must not exceed 100,000.")
    if seed is not None and not is_int(seed): raise VE("seed must be an integer or null.")
    if cc * rc > 50_000_000: raise VE("customer_count x replication_count must not exceed 50,000,000.")
    gen_validate(cc, ar, sr, ad, sd, seed + 0 if seed is not None else None)

def replicate(body):
    if not isinstance(body, dict): return 400, "Request body must be a JSON object."
    try:
        replicate_validate(get(body, "simulation_type", "single_server"), get(body, "customer_count", 100), get(body, "arrival_rate", 0.5),
                           get(body, "service_rate", 1.0), get(body, "replication_count", 30), get(body, "server_count", 1),
                           get(body, "arrival_distribution", "exponential"), get(body, "service_distribution", "exponential"),
                           get(body, "seed"), get(body, "confidence_level", 0.95))
    except VE as e: return 400, str(e)
    return 200, None

def compare(body):
    if not isinstance(body, dict): return 400, "Request body must be a JSON object."
    sc = get(body, "scenarios")
    try:
        if not isinstance(sc, list) or not sc: raise VE("scenarios must be a non-empty list.")
        if len(sc) > 20: raise VE("A maximum of 20 scenarios is allowed.")
        for i, s in enumerate(sc):
            if not isinstance(s, dict): raise VE(f"Scenario {i+1} must be an object.")
            name = get(s, "name", f"Scenario {i+1}")
            if not isinstance(name, str) or not name.strip(): raise VE(f"Scenario {i+1} name must be a non-empty string.")
            replicate_validate(get(s, "simulation_type", "single_server"), get(s, "customer_count", 100), get(s, "arrival_rate", 0.5),
                               get(s, "service_rate", 1.0), get(s, "replication_count", 30), get(s, "server_count", 1),
                               get(s, "arrival_distribution", "exponential"), get(s, "service_distribution", "exponential"),
                               get(s, "seed", 42), get(s, "confidence_level", 0.95))
    except VE as e: return 400, str(e)
    return 200, None

def export_rows(body):
    if not isinstance(body, dict): return 400, "Request body must be a JSON object."
    rows = get(body, "rows")
    try:
        if not isinstance(rows, list): raise VE("rows must be a list.")
        if any(not isinstance(r, dict) for r in rows): raise VE("Each row must be an object.")
        if not rows: raise VE("Cannot export an empty dataset.")
    except VE as e: return 400, str(e)
    return 200, None

def run_export(body):
    try:
        if not isinstance(body, dict): raise VE("Request body must be a JSON object.")
        st = get(body, "simulation_type", "single_server")
        if not is_sim(st): raise VE(SIM)
        cc, ar, sr = get(body, "customer_count", 100), get(body, "arrival_rate", 0.5), get(body, "service_rate", 1.0)
        ad, sd = get(body, "arrival_distribution", "exponential"), get(body, "service_distribution", "exponential")
        seed, rsc = get(body, "seed"), get(body, "server_count", 1)
        if not is_int(cc) or cc <= 0: raise VE("customer_count must be a positive integer.")
        if not is_int(rsc) or rsc <= 0: raise VE("server_count must be a positive integer.")
        for v, nm in ((ar, "arrival_rate"), (sr, "service_rate")):
            if isinstance(v, bool) or not is_num(v) or not fin(float(v)) or float(v) <= 0: raise VE(nm + " must be a positive finite number.")
        if seed is not None and not is_int(seed): raise VE("seed must be an integer or null.")
        if ad not in ("exponential", "deterministic"): raise VE("arrival_distribution must be 'exponential' or 'deterministic'.")
        if sd not in ("exponential", "deterministic"): raise VE("service_distribution must be 'exponential' or 'deterministic'.")
        gen_validate(cc, ar, sr, ad, sd, seed)
        if st != "single_server" and rsc > 100000: raise VE("Server count must not exceed 100,000.")
        et = get(body, "export_type", "customers")
        if et not in ("customers", "summary"): raise VE("export_type must be 'customers' or 'summary'.")
    except VE as e: return 400, str(e)
    return 200, None

ROUTES = {"/api/queueing/mm1": mm1, "/api/queueing/mmc": mmc, "/api/simulation/run": run, "/api/simulation/generate-and-run": generate,
          "/api/simulation/replicate": replicate, "/api/simulation/compare": compare, "/api/simulation/export/csv": export_rows,
          "/api/simulation/export/excel": export_rows, "/api/simulation/run-and-export/csv": run_export, "/api/simulation/run-and-export/excel": run_export}

if __name__ == "__main__":
    cases = json.load(open(os.path.join(GOLDEN, "api_golden.json")))["cases"]
    checked = bad = 0
    for c in cases:
        fn = ROUTES.get(c["path"])
        if c["method"] != "POST" or fn is None: continue
        if "raw_body" in c: body = None            # not JSON content type -> body treated as absent
        else: body = c["body"]
        status, msg = fn(body); checked += 1
        exp = c.get("expected") or {}
        want_msg = exp.get("message") or exp.get("error")
        if status != c["status"] or (status == 400 and msg != want_msg):
            bad += 1; print("DIFF", c["name"], "| mine:", status, msg, "| real:", c["status"], want_msg)
    print(f"route validation mirror: {checked} requests, mismatches: {bad}")
