import base64, io, json, math, random, sys
import os
HERE = os.path.dirname(os.path.abspath(__file__))
BACKEND = os.environ.get("BACKEND_DIR", os.path.join(HERE, "..", "..", "..", "simulation-backend-main"))
sys.path.insert(0, BACKEND)
try:
    import flask_cors  # noqa: F401
except ImportError:  # flask-cors only adds CORS headers; a stub is enough to import the app
    import types
    stub = types.ModuleType("flask_cors"); stub.CORS = lambda app, *a, **k: app
    sys.modules["flask_cors"] = stub
import numpy as np
from openpyxl import Workbook
from app import create_app

app = create_app(); app.config["TESTING"] = True
client = app.test_client()
cases = []

def clean(o):
    """Make NaN/inf JSON-safe markers (should not occur)."""
    return o

def add_json(name, method, path, body=None, raw=None, content_type="application/json"):
    if method == "GET":
        r = client.get(path)
    elif raw is not None:
        r = client.post(path, data=raw, content_type=content_type)
    else:
        r = client.post(path, data=json.dumps(body), content_type="application/json")
    case = {"name": name, "method": method, "path": path, "status": r.status_code}
    if raw is not None: case["raw_body"] = raw; case["content_type"] = content_type
    elif body is not None: case["body"] = body
    if r.mimetype == "application/json":
        case["expected"] = r.get_json()
    elif r.mimetype == "text/csv":
        case["expected_text"] = r.get_data(as_text=True); case["mimetype"] = "text/csv"
    else:
        case["mimetype"] = r.mimetype
        case["expected_len_min"] = 1
    cases.append(case)

def add_upload(name, path, filename, content_bytes, form=None):
    data = dict(form or {})
    if content_bytes is not None:
        data["file"] = (io.BytesIO(content_bytes), filename)
    r = client.post(path, data=data, content_type="multipart/form-data")
    case = {"name": name, "method": "UPLOAD", "path": path, "status": r.status_code, "form": form or {}}
    if content_bytes is not None:
        case["file"] = {"name": filename, "content_base64": base64.b64encode(content_bytes).decode()}
    case["expected"] = r.get_json()
    cases.append(case)

# --- simple GETs
add_json("health", "GET", "/api/health")
add_json("nadra dataset", "GET", "/api/dataset/nadra")
add_json("nadra single", "GET", "/api/simulation/single-server/nadra")
for c in (1, 2, 3, 5):
    add_json(f"nadra multi {c}", "GET", f"/api/simulation/multi-server/nadra/{c}")

# --- analytical models
add_json("mm1 4/5", "POST", "/api/queueing/mm1", {"arrival_rate": 4, "service_rate": 5})
add_json("mm1 0.8/1", "POST", "/api/queueing/mm1", {"arrival_rate": 0.8, "service_rate": 1})
add_json("mm1 unstable", "POST", "/api/queueing/mm1", {"arrival_rate": 5, "service_rate": 4})
add_json("mm1 missing", "POST", "/api/queueing/mm1", {"arrival_rate": 4})
add_json("mm1 zero", "POST", "/api/queueing/mm1", {"arrival_rate": 0, "service_rate": 4})
add_json("mm1 not object", "POST", "/api/queueing/mm1", [1, 2])
add_json("mm1 not json", "POST", "/api/queueing/mm1", raw="hello", content_type="text/plain")
add_json("mmc 4/5/2", "POST", "/api/queueing/mmc", {"arrival_rate": 4, "service_rate": 5, "server_count": 2})
add_json("mmc 3/1/4", "POST", "/api/queueing/mmc", {"arrival_rate": 3, "service_rate": 1, "server_count": 4})
add_json("mmc 9/1/10", "POST", "/api/queueing/mmc", {"arrival_rate": 9, "service_rate": 1, "server_count": 10})
add_json("mmc float count ok", "POST", "/api/queueing/mmc", {"arrival_rate": 4, "service_rate": 5, "server_count": 2.0})
add_json("mmc fractional count", "POST", "/api/queueing/mmc", {"arrival_rate": 4, "service_rate": 5, "server_count": 2.5})
add_json("mmc bool count", "POST", "/api/queueing/mmc", {"arrival_rate": 4, "service_rate": 5, "server_count": True})
add_json("mmc unstable", "POST", "/api/queueing/mmc", {"arrival_rate": 11, "service_rate": 5, "server_count": 2})
add_json("mmc zero servers", "POST", "/api/queueing/mmc", {"arrival_rate": 4, "service_rate": 5, "server_count": 0})
add_json("mmc missing", "POST", "/api/queueing/mmc", {"arrival_rate": 4, "service_rate": 5})

# --- custom run
cust3 = [{"customer_id": "C1", "arrival_time": 0, "service_time": 5},
         {"customer_id": "C2", "arrival_time": 2, "service_time": 3},
         {"customer_id": "C3", "arrival_time": 10, "service_time": 4}]
add_json("run single", "POST", "/api/simulation/run", {"simulation_type": "single_server", "customers": cust3})
add_json("run multi 2", "POST", "/api/simulation/run", {"simulation_type": "multi_server", "server_count": 2,
    "customers": [{"customer_id": "C1", "arrival_time": 0, "service_time": 5},
                  {"customer_id": "C2", "arrival_time": 2, "service_time": 3},
                  {"customer_id": "C3", "arrival_time": 3, "service_time": 4}]})
add_json("run auto ids + unsorted", "POST", "/api/simulation/run", {"simulation_type": "single_server",
    "customers": [{"arrival_time": 4.5, "service_time": 1.25}, {"arrival_time": 0.1, "service_time": 2}, {"arrival_time": "3", "service_time": "0.75"}]})
add_json("run ties", "POST", "/api/simulation/run", {"simulation_type": "multi_server", "server_count": 2,
    "customers": [{"customer_id": f"T{i}", "arrival_time": 1.0, "service_time": 2.0} for i in range(5)]})
add_json("run bad type", "POST", "/api/simulation/run", {"simulation_type": "x", "customers": cust3})
add_json("run empty", "POST", "/api/simulation/run", {"simulation_type": "single_server", "customers": []})
add_json("run dup id", "POST", "/api/simulation/run", {"simulation_type": "single_server", "customers": [cust3[0], cust3[0]]})
add_json("run negative arrival", "POST", "/api/simulation/run", {"simulation_type": "single_server", "customers": [{"customer_id": "a", "arrival_time": -1, "service_time": 5}]})
add_json("run zero service", "POST", "/api/simulation/run", {"simulation_type": "single_server", "customers": [{"customer_id": "a", "arrival_time": 1, "service_time": 0}]})
add_json("run server_count 0", "POST", "/api/simulation/run", {"simulation_type": "multi_server", "server_count": 0, "customers": cust3})
add_json("run server_count missing", "POST", "/api/simulation/run", {"simulation_type": "multi_server", "customers": cust3})
add_json("run string time", "POST", "/api/simulation/run", {"simulation_type": "single_server", "customers": [{"customer_id": "a", "arrival_time": "abc", "service_time": 5}]})
add_json("run missing time", "POST", "/api/simulation/run", {"simulation_type": "single_server", "customers": [{"customer_id": "a", "arrival_time": 1}]})
add_json("run bool time", "POST", "/api/simulation/run", {"simulation_type": "single_server", "customers": [{"customer_id": "a", "arrival_time": True, "service_time": 5}]})
add_json("run blank id", "POST", "/api/simulation/run", {"simulation_type": "single_server", "customers": [{"customer_id": "  ", "arrival_time": 1, "service_time": 5}]})
add_json("run non-object customer", "POST", "/api/simulation/run", {"simulation_type": "single_server", "customers": [5]})
add_json("run list time", "POST", "/api/simulation/run", {"simulation_type": "single_server", "customers": [{"customer_id": "a", "arrival_time": [1], "service_time": 5}]})
add_json("run not object", "POST", "/api/simulation/run", [1])

# --- generate and run
base = {"simulation_type": "single_server", "customer_count": 100, "arrival_rate": 0.5, "service_rate": 1,
        "arrival_distribution": "exponential", "service_distribution": "exponential", "seed": 42}
add_json("gen single seed42", "POST", "/api/simulation/generate-and-run", base)
add_json("gen multi seed42 c3", "POST", "/api/simulation/generate-and-run", {**base, "simulation_type": "multi_server", "server_count": 3, "service_rate": 0.3})
add_json("gen multi seed7 big", "POST", "/api/simulation/generate-and-run", {**base, "simulation_type": "multi_server", "server_count": 4, "service_rate": 0.2, "arrival_rate": 0.7, "customer_count": 500, "seed": 7})
add_json("gen deterministic", "POST", "/api/simulation/generate-and-run", {**base, "arrival_distribution": "deterministic", "service_distribution": "deterministic", "customer_count": 10, "arrival_rate": 0.5, "service_rate": 2})
add_json("gen det arrival exp service", "POST", "/api/simulation/generate-and-run", {**base, "arrival_distribution": "deterministic", "customer_count": 25})
add_json("gen negative seed", "POST", "/api/simulation/generate-and-run", {**base, "seed": -5, "customer_count": 20})
add_json("gen huge seed", "POST", "/api/simulation/generate-and-run", {**base, "seed": 2**40 + 9, "customer_count": 20})
add_json("gen seed 0", "POST", "/api/simulation/generate-and-run", {**base, "seed": 0, "customer_count": 20})
add_json("gen unstable ok", "POST", "/api/simulation/generate-and-run", {**base, "arrival_rate": 2, "service_rate": 1, "customer_count": 50})
add_json("gen bad distribution", "POST", "/api/simulation/generate-and-run", {**base, "arrival_distribution": "invalid"})
add_json("gen zero customers", "POST", "/api/simulation/generate-and-run", {**base, "customer_count": 0})
add_json("gen float customers", "POST", "/api/simulation/generate-and-run", {**base, "customer_count": 10.5})
add_json("gen float whole customers", "POST", "/api/simulation/generate-and-run", {**base, "customer_count": 10.0})
add_json("gen missing rate", "POST", "/api/simulation/generate-and-run", {k: v for k, v in base.items() if k != "service_rate"})
add_json("gen string rate", "POST", "/api/simulation/generate-and-run", {**base, "arrival_rate": "0.5"})
add_json("gen bool seed", "POST", "/api/simulation/generate-and-run", {**base, "seed": True})
add_json("gen float seed", "POST", "/api/simulation/generate-and-run", {**base, "seed": 1.5})
add_json("gen bad sim type", "POST", "/api/simulation/generate-and-run", {**base, "simulation_type": "nope"})
add_json("gen multi bad servers", "POST", "/api/simulation/generate-and-run", {**base, "simulation_type": "multi_server", "server_count": 0})

# --- replications
rep = {"simulation_type": "single_server", "customer_count": 50, "arrival_rate": 0.5, "service_rate": 1.0, "replication_count": 5, "seed": 100, "confidence_level": 0.95}
add_json("replicate single", "POST", "/api/simulation/replicate", rep)
add_json("replicate multi", "POST", "/api/simulation/replicate", {**rep, "simulation_type": "multi_server", "server_count": 2, "service_rate": 0.6, "replication_count": 12, "seed": 42, "confidence_level": 0.9})
add_json("replicate cl 99", "POST", "/api/simulation/replicate", {**rep, "replication_count": 30, "customer_count": 80, "confidence_level": 0.99, "seed": 3})
add_json("replicate two reps", "POST", "/api/simulation/replicate", {**rep, "replication_count": 2})
add_json("replicate det", "POST", "/api/simulation/replicate", {**rep, "arrival_distribution": "deterministic", "service_distribution": "deterministic", "replication_count": 3})
add_json("replicate rep=1", "POST", "/api/simulation/replicate", {**rep, "replication_count": 1})
add_json("replicate rep=1001", "POST", "/api/simulation/replicate", {**rep, "replication_count": 1001})
add_json("replicate cl 1.0", "POST", "/api/simulation/replicate", {**rep, "confidence_level": 1.0})
add_json("replicate cl string", "POST", "/api/simulation/replicate", {**rep, "confidence_level": "0.95"})
add_json("replicate bad type", "POST", "/api/simulation/replicate", {**rep, "simulation_type": "bad"})
add_json("replicate bad rate", "POST", "/api/simulation/replicate", {**rep, "arrival_rate": -1})
add_json("replicate bad seed", "POST", "/api/simulation/replicate", {**rep, "seed": 2.5})
add_json("replicate not json", "POST", "/api/simulation/replicate", raw="not json", content_type="text/plain")
add_json("replicate not object", "POST", "/api/simulation/replicate", [1])

# --- compare
sc = [{"name": "One counter", "simulation_type": "multi_server", "server_count": 1, "customer_count": 30, "arrival_rate": 0.5, "service_rate": 1.0, "replication_count": 3, "seed": 42},
      {"name": "Two counters", "simulation_type": "multi_server", "server_count": 2, "customer_count": 30, "arrival_rate": 0.5, "service_rate": 1.0, "replication_count": 3, "seed": 42}]
add_json("compare 2", "POST", "/api/simulation/compare", {"scenarios": sc})
add_json("compare default seed", "POST", "/api/simulation/compare", {"scenarios": [{"name": "A", "arrival_rate": 0.8, "service_rate": 1, "customer_count": 60, "replication_count": 6}, {"simulation_type": "multi_server", "server_count": 3}]})
add_json("compare empty", "POST", "/api/simulation/compare", {"scenarios": []})
add_json("compare missing", "POST", "/api/simulation/compare", {})
add_json("compare 21", "POST", "/api/simulation/compare", {"scenarios": [{}] * 21})
add_json("compare non-object", "POST", "/api/simulation/compare", {"scenarios": ["x"]})
for nm in (None, 123, "", "   "):
    add_json(f"compare bad name {nm!r}", "POST", "/api/simulation/compare", {"scenarios": [{"name": nm}]})

# --- export (generic rows)
rows = [{"customer_id": "C1", "waiting_time": 0.0, "system_time": 2.5, "n": 3, "flag": True, "none": None, "txt": 'a,b "q"'},
        {"customer_id": "C2", "waiting_time": 1.5, "system_time": 4.0, "extra": 1e-05, "big": 1e22, "n": 10**17, "nested": {"1": 0.5, "x": [1, "y", None, True]}}]
add_json("export csv rows", "POST", "/api/simulation/export/csv", {"rows": rows})
add_json("export csv empty", "POST", "/api/simulation/export/csv", {"rows": []})
add_json("export csv bad row", "POST", "/api/simulation/export/csv", {"rows": ["x"]})
add_json("export csv missing", "POST", "/api/simulation/export/csv", {})
add_json("export csv not json", "POST", "/api/simulation/export/csv", raw="invalid", content_type="text/plain")
add_json("export excel empty", "POST", "/api/simulation/export/excel", {"rows": []})
add_json("export excel ok", "POST", "/api/simulation/export/excel", {"rows": rows})

# --- run and export
rx = {"simulation_type": "single_server", "customer_count": 10, "arrival_rate": 0.5, "service_rate": 1.0, "seed": 42}
add_json("run-export csv customers", "POST", "/api/simulation/run-and-export/csv", rx)
add_json("run-export csv summary", "POST", "/api/simulation/run-and-export/csv", {**rx, "export_type": "summary"})
add_json("run-export csv multi summary", "POST", "/api/simulation/run-and-export/csv", {**rx, "simulation_type": "multi_server", "server_count": 3, "service_rate": 0.3, "export_type": "summary"})
add_json("run-export csv multi customers", "POST", "/api/simulation/run-and-export/csv", {**rx, "simulation_type": "multi_server", "server_count": 2})
add_json("run-export excel customers", "POST", "/api/simulation/run-and-export/excel", rx)
add_json("run-export excel summary", "POST", "/api/simulation/run-and-export/excel", {**rx, "export_type": "summary"})
add_json("run-export bad sim type", "POST", "/api/simulation/run-and-export/csv", {**rx, "simulation_type": "invalid"})
add_json("run-export bad export type", "POST", "/api/simulation/run-and-export/excel", {**rx, "export_type": "invalid"})
add_json("run-export not object", "POST", "/api/simulation/run-and-export/csv", ["invalid"])
add_json("run-export bad dist", "POST", "/api/simulation/run-and-export/csv", {**rx, "arrival_distribution": "weibull"})
for field, value in [("customer_count", 0), ("customer_count", -1), ("customer_count", 1.5), ("arrival_rate", 0), ("service_rate", -1), ("server_count", 0)]:
    p = {**rx, "simulation_type": "multi_server", "server_count": 2}; p[field] = value
    add_json(f"run-export bad {field}={value}", "POST", "/api/simulation/run-and-export/csv", p)

# --- uploads (csv)
csv_ok = b"customer_id,arrival_time,service_time\nC1,0,5\nC2,2,3\nC3,10,4\n"
add_upload("upload csv single", "/api/simulation/upload-and-run", "customers.csv", csv_ok, {"simulation_type": "single_server"})
add_upload("upload csv multi", "/api/simulation/upload-and-run", "customers.csv", csv_ok, {"simulation_type": "multi_server", "server_count": "2"})
add_upload("upload csv multi no count", "/api/simulation/upload-and-run", "customers.csv", csv_ok, {"simulation_type": "multi_server"})
add_upload("upload csv multi bad count", "/api/simulation/upload-and-run", "customers.csv", csv_ok, {"simulation_type": "multi_server", "server_count": "2.5"})
add_upload("upload csv multi count 2.0", "/api/simulation/upload-and-run", "customers.csv", csv_ok, {"simulation_type": "multi_server", "server_count": "2.0"})
add_upload("upload csv bad sim type", "/api/simulation/upload-and-run", "customers.csv", csv_ok, {"simulation_type": "zzz"})
add_upload("upload csv upper-case headers", "/api/simulation/upload-and-run", "c.csv", b" Arrival_Time , SERVICE_TIME\n1,2\n3,4\n", {})
add_upload("upload csv no ids", "/api/simulation/upload-and-run", "c.csv", b"arrival_time,service_time\n0,1.5\n1,2.5\n\n4,1\n", {"simulation_type": "single_server"})
add_upload("upload csv numeric ids", "/api/simulation/upload-and-run", "c.csv", b"customer_id,arrival_time,service_time\n007,0,1\n8,1,1\n", {})
add_upload("upload csv float ids", "/api/simulation/upload-and-run", "c.csv", b"customer_id,arrival_time,service_time\n1.5,0,1\n2,1,1\n", {})
add_upload("upload csv blank id", "/api/simulation/upload-and-run", "c.csv", b"customer_id,arrival_time,service_time\nA,0,1\n,1,1\nC,2,1\n", {})
add_upload("upload csv numeric ids with blank", "/api/simulation/upload-and-run", "c.csv", b"customer_id,arrival_time,service_time\n1,0,1\n,1,1\n3,2,1\n", {})
add_upload("upload csv missing cols", "/api/simulation/upload-and-run", "invalid.csv", b"customer_id,arrival_time\nC1,0\n", {})
add_upload("upload txt", "/api/simulation/upload-and-run", "customers.txt", b"some content", {})
add_upload("upload empty csv", "/api/simulation/upload-and-run", "empty.csv", b"", {})
add_upload("upload header only", "/api/simulation/upload-and-run", "h.csv", b"arrival_time,service_time\n", {})
add_upload("upload missing file", "/api/simulation/upload-and-run", None, None, {"simulation_type": "single_server"})
for i, content in enumerate(["arrival_time,service_time\n-1,5\n", "arrival_time,service_time\n0,0\n", "arrival_time,service_time\nNaN,5\n",
                              "arrival_time,service_time\n0,inf\n", "arrival_time,service_time\nabc,5\n", "arrival_time,service_time\n0,\n",
                              "arrival_time,service_time\n1,2\nx,3\n"]):
    add_upload(f"upload csv bad values {i}", "/api/simulation/upload-and-run", "bad.csv", content.encode(), {})
add_upload("upload csv dup ids", "/api/simulation/upload-and-run", "d.csv", b"customer_id,arrival_time,service_time\nC1,0,5\nC1,2,3\n", {})
add_upload("upload csv dup headers", "/api/simulation/upload-and-run", "d.csv", b"arrival_time,arrival_time,service_time\n1,2,3\n", {})
add_upload("upload csv case-dup headers", "/api/simulation/upload-and-run", "d.csv", b"Arrival_Time,arrival_time,service_time\n1,2,3\n", {})

# --- uploads (xlsx)
def make_xlsx(rows, header):
    wb = Workbook(); ws = wb.active
    ws.append(header)
    for r in rows: ws.append(r)
    b = io.BytesIO(); wb.save(b); return b.getvalue()
x1 = make_xlsx([["A", 0, 5], ["B", 2.5, 3], ["C", 10, 4.25]], ["customer_id", "arrival_time", "service_time"])
add_upload("upload xlsx single", "/api/simulation/upload-and-run", "c.xlsx", x1, {"simulation_type": "single_server"})
add_upload("upload xlsx multi", "/api/simulation/upload-and-run", "c.xlsx", x1, {"simulation_type": "multi_server", "server_count": "2"})
x2 = make_xlsx([[1, 0, 5], [2, 2.5, 3], [3, 10, 4.25]], ["Customer_ID", "Arrival_Time", "Service_Time"])
add_upload("upload xlsx numeric ids", "/api/simulation/upload-and-run", "c.xlsx", x2, {})
x3 = make_xlsx([[0, 5], [2, None]], ["arrival_time", "service_time"])
add_upload("upload xlsx missing value", "/api/simulation/upload-and-run", "c.xlsx", x3, {})
add_upload("upload xlsx describe", "/api/statistics/describe-upload", "c.xlsx", x1, {})

# --- describe
add_upload("describe csv", "/api/statistics/describe-upload", "c.csv", b"customer_id,arrival_time,service_time\nC1,0,5\nC2,2,3\nC3,6,4\n")
add_upload("describe csv big", "/api/statistics/describe-upload", "c.csv", ("arrival_time,service_time\n" + "".join(f"{i*1.7:.1f},{1+(i%7)*0.5}\n" for i in range(57))).encode())
add_upload("describe csv same arrival", "/api/statistics/describe-upload", "c.csv", b"arrival_time,service_time\n5,1\n5,2\n")
add_upload("describe csv single row", "/api/statistics/describe-upload", "c.csv", b"arrival_time,service_time\n5,1\n")
add_upload("describe invalid", "/api/statistics/describe-upload", "invalid.csv", b"customer_id,arrival_time\nC1,0\n")
add_upload("describe no file", "/api/statistics/describe-upload", None, None)

# --- low-level helper goldens ---
rng_cases = []
for seed in [0, 1, 42, 123456789, 2**32 + 5, 2**40 + 9, 10**30, -7, -1]:
    r = random.Random(seed)
    rnd = [r.random() for _ in range(12)]
    r = random.Random(seed)
    exp = [r.expovariate(0.5) for _ in range(8)]
    r = random.Random(seed)
    skip = [r.random() for _ in range(1300)]       # cross the 624-word refill boundary twice
    rng_cases.append({"seed": seed, "random": rnd, "expovariate_0_5": exp, "random_1299": skip[-1], "random_1300_next": r.random()})

rnd = random.Random(5)
round_cases = []
for _ in range(1500):
    x = rnd.uniform(-20, 300) * rnd.choice([1, 1e-3, 1e-6]); n = rnd.choice([3, 6])
    round_cases.append([x, n, round(x, n)])
for k in range(0, 300):
    for (x, n) in [(k / 8.0, 2), (k / 16.0, 3), (k / 128.0, 6), (-k / 16.0, 3), (k + 0.0005, 3), (k + 0.0000005, 6)]:
        round_cases.append([x, n, round(x, n)])
round_cases += [[0.0, 3, 0.0], [-0.0, 3, -0.0], [-0.0001, 3, round(-0.0001, 3)], [1e-9, 6, 0.0], [5e15, 3, round(5e15, 3)]]

mean_cases = []
for n in [1, 2, 3, 7, 8, 9, 31, 100, 127, 128, 129, 257, 1000]:
    vals = [round(rnd.uniform(0, 250), 1) for _ in range(n)]
    mean_cases.append({"values": vals, "np_mean": float(np.mean(np.array(vals, dtype=np.float64)))})

repr_vals = [0.0, 1.0, 100.0, 0.5, 1e15, 1e16, 1.5e16, 1e-4, 1e-5, 1.5e-5, 5e-324, 1.7976931348623157e308, 0.1, 0.30000000000000004, 1e22, 123.456, -42.0, -0.00001234, 2.0**53]
repr_cases = [[v, repr(v)] for v in repr_vals]

helpers = {"rng": rng_cases, "round": round_cases, "mean": mean_cases, "repr": repr_cases}

out = os.path.join(HERE, "..", "..", "tests", "QueueLab.Tests", "Golden") + os.sep
json.dump({"cases": cases}, open(out + "api_golden.json", "w"), indent=1, allow_nan=False)
json.dump(helpers, open(out + "helper_golden.json", "w"), indent=1, allow_nan=False)
print("api cases:", len(cases))
from collections import Counter
print(Counter(c["status"] for c in cases))
