"""Line-by-line transcription of the C# engine (DistributionGenerator, Single/MultiServerSimulation,
SimulationSummary, TimeBasedStatistics, ReplicationService/Statistics) checked against recorded real-backend output."""
import os
HERE = os.path.dirname(os.path.abspath(__file__))
GOLDEN = os.path.join(HERE, "..", "..", "tests", "QueueLab.Tests", "Golden")
import json, math, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mt_proto import PyRandom
from round_proto import py_round
from scipy.stats import t as student_t

def psum(vals):                              # PyMath.Sum
    r = 0.0; c = 0.0
    for x in vals:
        t = r + x
        if abs(r) >= abs(x): c += (r - t) + x
        else: c += (x - t) + r
        r = t
    if c != 0.0 and math.isfinite(c): r += c
    return r

def generate(n, lam, mu, exp_a, exp_s, seed):          # DistributionGenerator.Generate
    rng = PyRandom(seed) if seed is not None else PyRandom(12345)
    out = []; at = 0.0
    for i in range(n):
        if i > 0: at += rng.expovariate(lam) if exp_a else 1.0 / lam
        st = rng.expovariate(mu) if exp_s else 1.0 / mu
        out.append((f"C{i+1}", py_round(at, 6), py_round(st, 6)))
    return out

def time_stats(res, servers):                          # TimeBasedStatistics.Calculate
    first = min(r["arr"] for r in res); last = max(r["end"] for r in res)
    obs = last - first
    qe, se = [], []; busy = [0.0] * (servers + 1)
    for r in res:
        qe += [(r["arr"], 1), (r["start"], -1)]; se += [(r["arr"], 1), (r["end"], -1)]
        busy[r["server"]] += r["end"] - r["start"]
    def twa(ev):
        ev.sort(key=lambda e: e[0]); pop = 0; prev = first; area = 0.0; i = 0
        while i < len(ev):
            t = ev[i][0]; area += pop * (t - prev); net = 0
            while i < len(ev) and ev[i][0] == t: net += ev[i][1]; i += 1
            pop += net; prev = t
        area += pop * (last - prev)
        return area / obs
    su = {str(s): py_round(busy[s] / obs, 6) for s in range(1, servers + 1)}
    total_busy = psum(busy[1:])
    return {"q": py_round(twa(qe), 6), "s": py_round(twa(se), 6), "util": py_round(total_busy / (servers * obs), 6), "su": su}

def summary(kind, servers, res, idle):                 # SimulationSummary.Build
    ts = time_stats(res, servers); n = len(res)
    tserv = psum(r["svc"] for r in res); twait = psum(r["wait"] for r in res); tsys = psum(r["sys"] for r in res)
    sim_time = max(r["end"] for r in res) - min(r["arr"] for r in res)
    return {"type": kind, "server_count": servers, "customers_processed": n,
            "arrival_rate": py_round(n / sim_time, 6), "service_rate": py_round(n / tserv, 6) if tserv > 0 else None,
            "utilization": ts["util"], "average_waiting_time": py_round(twait / n, 6), "average_system_time": py_round(tsys / n, 6),
            "average_number_in_queue": ts["q"], "average_number_in_system": ts["s"], "throughput": py_round(n / sim_time, 6),
            "total_idle_time": py_round(idle, 6), "server_utilization": ts["su"]}, res

def run(kind, customers, servers):                     # Single/MultiServerSimulation.Run
    customers = sorted(customers, key=lambda c: c[1])  # stable
    avail = [0.0] * servers; idles = [0.0] * servers; res = []; single_idle = 0.0
    for cid, arr, svc in customers:
        k = min(range(servers), key=lambda i: (avail[i], i)) if kind == "multi_server" else 0
        a = avail[k]
        if arr > a:
            idle = arr - a; start = arr
            if kind == "multi_server": idles[k] += idle
            else: single_idle += idle
        else: idle = 0.0; start = a
        wait = start - arr; end = start + svc; sysm = end - arr; avail[k] = end
        res.append({"id": cid, "arr": py_round(arr, 3), "svc": py_round(svc, 3), "start": py_round(start, 3), "end": py_round(end, 3),
                    "wait": py_round(wait, 3), "sys": py_round(sysm, 3), "server": k + 1, "idle": py_round(idle, 3)})
    return summary(kind, servers, res, psum(idles) if kind == "multi_server" else single_idle)

METRICS = ["average_waiting_time", "average_system_time", "average_number_in_queue", "average_number_in_system", "utilization", "throughput"]

def stats(values, cl):                                  # ReplicationStatistics.Summarize
    n = len(values); avg = psum(values) / n
    if n == 1: return {"mean": py_round(avg, 6), "sd": None, "ci": None}
    m = avg; ss = psum([(v - m) ** 2 for v in values]); sd = math.sqrt(ss / (n - 1)); se = sd / math.sqrt(n)
    crit = student_t.ppf((1 + cl) / 2, n - 1); moe = crit * se
    return {"mean": py_round(avg, 6), "sd": py_round(sd, 6), "ci": (py_round(avg - moe, 6), py_round(avg + moe, 6), py_round(moe, 6))}

def replicate(kind, n, lam, mu, reps, servers, ea, es, seed, cl):   # ReplicationService.RunFromJson (valid input)
    sims = []
    for i in range(reps):
        cust = generate(n, lam, mu, ea == "exponential", es == "exponential", None if seed is None else seed + i)
        sims.append(run(kind, cust, servers if kind == "multi_server" else 1)[0])
    out = {}
    for m in METRICS:
        vals = [s[{"average_waiting_time": "average_waiting_time", "average_system_time": "average_system_time", "average_number_in_queue": "average_number_in_queue", "average_number_in_system": "average_number_in_system", "utilization": "utilization", "throughput": "throughput"}[m]] for s in sims]
        out[m] = stats(vals, cl)
    return out, sims

def close(a, b, tol=1e-9):
    if a is None or b is None: return a is None and b is None
    return abs(a - b) <= tol * max(1.0, abs(b))

if __name__ == "__main__":
    cases = {c["name"]: c for c in json.load(open(os.path.join(GOLDEN, "api_golden.json")))["cases"]}
    bad = checked = 0
    def chk(label, got, exp):
        global bad, checked
        checked += 1
        if not close(got, exp): bad += 1; print("DIFF", label, got, exp)

    # --- generate-and-run
    gens = [("gen single seed42", "single_server", 100, 0.5, 1.0, True, True, 42, 1),
            ("gen multi seed42 c3", "multi_server", 100, 0.5, 0.3, True, True, 42, 3),
            ("gen multi seed7 big", "multi_server", 500, 0.7, 0.2, True, True, 7, 4),
            ("gen deterministic", "single_server", 10, 0.5, 2, False, False, 42, 1),
            ("gen det arrival exp service", "single_server", 25, 0.5, 1.0, False, True, 42, 1),
            ("gen negative seed", "single_server", 20, 0.5, 1.0, True, True, -5, 1),
            ("gen huge seed", "single_server", 20, 0.5, 1.0, True, True, 2**40 + 9, 1),
            ("gen seed 0", "single_server", 20, 0.5, 1.0, True, True, 0, 1),
            ("gen unstable ok", "single_server", 50, 2, 1, True, True, 42, 1)]
    for name, kind, n, lam, mu, ea, es, seed, c in gens:
        s, res = run(kind, generate(n, lam, mu, ea, es, seed), c)
        e = cases[name]["expected"]["simulation"]
        for k, v in s.items():
            if k == "server_utilization":
                for sid, val in v.items(): chk(f"{name} util {sid}", val, e[k][sid])
            elif k == "type": checked += 1; bad += (v != e[k])
            else: chk(f"{name} {k}", v, e[k])
        for mine, theirs in zip(sorted(res, key=lambda r: r["arr"]), cases[name]["expected"]["customers"]):
            for a, b in (("arr", "arrival_time"), ("svc", "service_time"), ("start", "service_start"), ("end", "service_end"), ("wait", "waiting_time"), ("sys", "system_time"), ("idle", "server_idle_time")):
                chk(f"{name} {mine['id']} {b}", mine[a], theirs[b])
            checked += 1; bad += (mine["server"] != theirs["server_id"] or mine["id"] != theirs["customer_id"])

    # --- replicate
    reps = [("replicate single", "single_server", 50, 0.5, 1.0, 5, 1, "exponential", "exponential", 100, 0.95),
            ("replicate multi", "multi_server", 50, 0.5, 0.6, 12, 2, "exponential", "exponential", 42, 0.9),
            ("replicate cl 99", "single_server", 80, 0.5, 1.0, 30, 1, "exponential", "exponential", 3, 0.99),
            ("replicate two reps", "single_server", 50, 0.5, 1.0, 2, 1, "exponential", "exponential", 100, 0.95),
            ("replicate det", "single_server", 50, 0.5, 1.0, 3, 1, "deterministic", "deterministic", 100, 0.95)]
    for name, kind, n, lam, mu, r, c, ea, es, seed, cl in reps:
        out, sims = replicate(kind, n, lam, mu, r, c, ea, es, seed, cl)
        exp = cases[name]["expected"]["summary"]["metrics"]
        for m in METRICS:
            e = exp[m]; g = out[m]
            chk(f"{name} {m} mean", g["mean"], e["mean"]); chk(f"{name} {m} sd", g["sd"], e["standard_deviation"])
            ci = e["confidence_interval"]
            if g["ci"] is None: checked += 1; bad += (ci is not None)
            else:
                chk(f"{name} {m} lower", g["ci"][0], ci["lower"]); chk(f"{name} {m} upper", g["ci"][1], ci["upper"]); chk(f"{name} {m} moe", g["ci"][2], ci["margin_of_error"])
        for i, s in enumerate(sims):
            for m in METRICS: chk(f"{name} rep{i+1} {m}", s[m], cases[name]["expected"]["replications"][i][m])

    # --- compare (defaults: seed 42, cl .95)
    for sc, label in zip(cases["compare 2"]["expected"]["scenarios"], ("One counter", "Two counters")):
        c = 1 if label == "One counter" else 2
        out, _ = replicate("multi_server", 30, 0.5, 1.0, 3, c, "exponential", "exponential", 42, 0.95)
        for m in METRICS: chk(f"compare {label} {m}", out[m]["mean"], sc["metrics"][m]["mean"])
    print(f"engine mirror: {checked} values checked, mismatches: {bad}")
