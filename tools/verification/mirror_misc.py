import os
HERE = os.path.dirname(os.path.abspath(__file__))
GOLDEN = os.path.join(HERE, "..", "..", "tests", "QueueLab.Tests", "Golden")
import json, math, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from round_proto import py_round
from repr_proto import py_repr_from_net, net_r
from mirror_engine import generate, run, psum

cases = {c["name"]: c for c in json.load(open(os.path.join(GOLDEN, "api_golden.json")))["cases"]}
bad = checked = 0

# ---- MMC (transcribed from MMCQueue.Calculate)
def mmc(lam, mu, c):
    a = lam / mu; rho = lam / (c * mu)
    terms = []; fact = 1.0
    for n in range(c):
        if n > 0: fact *= n
        terms.append(a ** n / fact)
    s = psum(terms); factc = fact * c; tail = a ** c / (factc * (1 - rho))
    p0 = 1 / (s + tail); pw = tail * p0; lq = pw * rho / (1 - rho); l = lq + a; wq = lq / lam; w = wq + 1 / mu
    return {"probability_empty": py_round(p0, 6), "probability_wait": py_round(pw, 6), "average_number_in_queue": py_round(lq, 6),
            "average_number_in_system": py_round(l, 6), "average_waiting_time": py_round(wq, 6), "average_system_time": py_round(w, 6)}
for name, (lam, mu, c) in {"mmc 4/5/2": (4, 5, 2), "mmc 3/1/4": (3, 1, 4), "mmc 9/1/10": (9, 1, 10)}.items():
    e = cases[name]["expected"]["result"]["metrics"]
    for k, v in mmc(lam, mu, c).items():
        checked += 1
        if v != e[k]: bad += 1; print("MMC", name, k, v, e[k])

# ---- CSV writer (transcribed from ExportService.ToCsv / PyRepr.Str)
def pystr(v):
    if v is None: return ""
    if isinstance(v, bool): return "True" if v else "False"
    if isinstance(v, float): return py_repr_from_net(net_r(v))
    if isinstance(v, (int, str)): return str(v)
    if isinstance(v, dict): return "{" + ", ".join(f"{pyrepr(k)}: {pyrepr(x)}" for k, x in v.items()) + "}"
    if isinstance(v, list): return "[" + ", ".join(pyrepr(x) for x in v) + "]"
def pyrepr(v):
    if v is None: return "None"
    if isinstance(v, str):
        q = '"' if ("'" in v and '"' not in v) else "'"
        return q + v.replace("\\", "\\\\").replace(q, "\\" + q) + q
    return pystr(v)
def csv_text(rows):
    names = []
    for r in rows:
        for k in r:
            if k not in names: names.append(k)
    def line(cells):
        if len(cells) == 1 and cells[0] == "": return '""\r\n'
        out = []
        for c in cells:
            out.append('"' + c.replace('"', '""') + '"' if any(ch in c for ch in ',"\r\n') else c)
        return ",".join(out) + "\r\n"
    text = line(names)
    for r in rows: text += line([pystr(r.get(n)) for n in names])
    return text

body = json.loads(json.dumps(cases["export csv rows"]["body"]))
checked += 1
if csv_text(body["rows"]) != cases["export csv rows"]["expected_text"]: bad += 1; print("CSV rows differ")

def sim_rows(kind, n, lam, mu, c, seed, ea=True, es=True, export="customers"):
    summ, res = run(kind, generate(n, lam, mu, ea, es, seed), c)
    if export == "summary":
        return [{"simulation_type": summ["type"], "server_count": summ["server_count"], "customers_processed": summ["customers_processed"],
                 "arrival_rate": summ["arrival_rate"], "service_rate": summ["service_rate"], "utilization": summ["utilization"],
                 "average_waiting_time": summ["average_waiting_time"], "average_system_time": summ["average_system_time"],
                 "average_number_in_queue": summ["average_number_in_queue"], "average_number_in_system": summ["average_number_in_system"],
                 "throughput": summ["throughput"], "total_idle_time": summ["total_idle_time"], "server_utilization": summ["server_utilization"]}]
    return [{"customer_id": r["id"], "arrival_time": r["arr"], "service_time": r["svc"], "service_start": r["start"], "service_end": r["end"],
             "waiting_time": r["wait"], "system_time": r["sys"], "server_id": r["server"], "server_idle_time": r["idle"],
             "simulation_type": kind, "server_count": c} for r in sorted(res, key=lambda r: r["arr"])]

for name, rows in {
    "run-export csv customers": sim_rows("single_server", 10, 0.5, 1.0, 1, 42),
    "run-export csv summary": sim_rows("single_server", 10, 0.5, 1.0, 1, 42, export="summary"),
    "run-export csv multi summary": sim_rows("multi_server", 10, 0.5, 0.3, 3, 42, export="summary"),
    "run-export csv multi customers": sim_rows("multi_server", 10, 0.5, 1.0, 2, 42),
}.items():
    checked += 1
    got = csv_text(rows); exp = cases[name]["expected_text"]
    if got != exp: bad += 1; print("CSV DIFF", name); print(repr(got[:300])); print(repr(exp[:300]))
print(f"misc mirror: {checked} checks, mismatches: {bad}")
