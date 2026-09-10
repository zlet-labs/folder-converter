import sys
import os
import time
import json
import psutil
from pathlib import Path

sys.stdout.reconfigure(encoding='utf-8')

def get_dir_size_mb(path):
    p = Path(path)
    if not p.exists():
        return 0.0
    total = sum(f.stat().st_size for f in p.rglob('*') if f.is_file())
    return round(total / (1024 * 1024), 2)

# Dynamically resolve HF cache directory
hf_cache_dir = None
if os.environ.get("HF_HOME"):
    hf_cache_dir = Path(os.environ["HF_HOME"]) / "hub"
elif os.environ.get("HF_HUB_CACHE"):
    hf_cache_dir = Path(os.environ["HF_HUB_CACHE"])
else:
    try:
        from huggingface_hub.constants import HF_HUB_CACHE
        hf_cache_dir = Path(HF_HUB_CACHE)
    except Exception:
        hf_cache_dir = Path.home() / ".cache" / "huggingface" / "hub"

import subprocess
import threading

# Measure sizes
venv_size = get_dir_size_mb("evaluation/venv")
hf_cache = get_dir_size_mb(hf_cache_dir)
rapidocr_models = get_dir_size_mb("evaluation/venv/Lib/site-packages/rapidocr/models")
total_models = round(hf_cache + rapidocr_models, 2)

print("=== Storage Footprint ===")
print(f"Virtual Environment Size: {venv_size} MB")
print(f"HuggingFace Models Cache: {hf_cache} MB (path: {hf_cache_dir})")
print(f"RapidOCR Models Size:     {rapidocr_models} MB")
print(f"Total Models Storage:     {total_models} MB")

def sample_process_tree_rss(pid):
    try:
        proc = psutil.Process(pid)
        total = proc.memory_info().rss
        for child in proc.children(recursive=True):
            try:
                total += child.memory_info().rss
            except (psutil.NoSuchProcess, psutil.AccessDenied):
                pass
        return total
    except (psutil.NoSuchProcess, psutil.AccessDenied):
        return 0

# Measure import & init times in clean process
print("\n=== Process Startup & Init Timing ===")
t0 = time.perf_counter()
import docling
t_import = time.perf_counter() - t0
print(f"Import docling:           {t_import:.4f} s")

from docling.document_converter import DocumentConverter
t_conv_import = time.perf_counter() - t0
print(f"Import DocumentConverter: {t_conv_import:.4f} s")

t1 = time.perf_counter()
conv = DocumentConverter()
t_init = time.perf_counter() - t1
print(f"DocumentConverter init:   {t_init:.4f} s")

# Measure Cold vs Warm Conversion Footprint on F01
test_fixture = Path("evaluation/fixtures/F01_simple_text.pdf")
print("\n=== Cold vs Warm Process-Tree Peak Measurement (F01) ===")
print("Measuring cold execution in isolated child process...")
cmd = [
    sys.executable,
    "-c",
    f"from docling.document_converter import DocumentConverter; "
    f"conv = DocumentConverter(); "
    f"res = conv.convert(r'{test_fixture}'); "
    f"md = res.document.export_to_markdown()"
]
t_cold_start = time.perf_counter()
proc_cold = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
cold_peak_rss = 0
while proc_cold.poll() is None:
    rss = sample_process_tree_rss(proc_cold.pid)
    if rss > cold_peak_rss:
        cold_peak_rss = rss
    time.sleep(0.01)
rss = sample_process_tree_rss(proc_cold.pid)
if rss > cold_peak_rss:
    cold_peak_rss = rss
proc_cold.communicate()
cold_dur = time.perf_counter() - t_cold_start
cold_peak_mb = round(cold_peak_rss / (1024 * 1024), 2)
print(f"Cold Launch + Conversion: {cold_dur:.3f} s | Process-Tree Peak RSS: {cold_peak_mb} MB")

print("Measuring warm execution in current converter session...")
current_pid = os.getpid()
warm_peak_rss = 0
stop_event = threading.Event()

def warm_monitor():
    global warm_peak_rss
    while not stop_event.is_set():
        rss = sample_process_tree_rss(current_pid)
        if rss > warm_peak_rss:
            warm_peak_rss = rss
        stop_event.wait(0.01)

t_mon = threading.Thread(target=warm_monitor, daemon=True)
t_mon.start()
t_warm_start = time.perf_counter()
res_warm = conv.convert(str(test_fixture))
md_warm = res_warm.document.export_to_markdown()
warm_dur = time.perf_counter() - t_warm_start
stop_event.set()
t_mon.join(timeout=1.0)
rss = sample_process_tree_rss(current_pid)
if rss > warm_peak_rss:
    warm_peak_rss = rss
warm_peak_mb = round(warm_peak_rss / (1024 * 1024), 2)
print(f"Warm Conversion:          {warm_dur:.3f} s | Process-Tree Peak RSS: {warm_peak_mb} MB")

# Per-fixture summary from summary_all_both.json
summary_path = Path("evaluation/outputs/summary_all_both.json")
if summary_path.exists():
    with open(summary_path, "r", encoding="utf-8") as f:
        data = json.load(f)
    print(f"\n=== Benchmark Summary Data ({len(data)} runs) ===")
    docling_runs = [d for d in data if d.get("arm") == "docling"]
    baseline_runs = [d for d in data if d.get("arm") == "baseline"]
    
    print(f"{'Fixture':<25} | {'Docling Time':<12} | {'Docling RSS':<12} | {'Base Time':<10} | {'Status'}")
    print("-" * 75)
    for d in docling_runs:
        fix = d["fixture"]
        b = next((x for x in baseline_runs if x["fixture"] == fix), None)
        b_time = f"{b['duration_seconds']}s" if b else "N/A"
        d_status = "OK" if d["success"] else "FAIL"
        print(f"{fix:<25} | {d['duration_seconds']:>10.3f}s | {d['peak_rss_mb']:>10.1f}MB | {b_time:>10} | {d_status}")

# Persist footprint evidence
footprint_data = {
    "storage": {
        "venv_size_mb": venv_size,
        "huggingface_cache_mb": hf_cache,
        "rapidocr_models_mb": rapidocr_models,
        "total_models_mb": total_models
    },
    "startup_timings_seconds": {
        "import_docling": round(t_import, 4),
        "import_document_converter": round(t_conv_import, 4),
        "document_converter_init": round(t_init, 4),
        "total_cold_init": round(t_conv_import + t_init, 4)
    },
    "cold_vs_warm_f01": {
        "cold_duration_seconds": round(cold_dur, 4),
        "cold_peak_tree_rss_mb": cold_peak_mb,
        "warm_duration_seconds": round(warm_dur, 4),
        "warm_peak_tree_rss_mb": warm_peak_mb
    }
}
footprint_path = Path("evaluation/outputs/performance_footprint.json")
footprint_path.write_text(json.dumps(footprint_data, indent=2), encoding="utf-8")
print(f"\nPerformance footprint evidence written to {footprint_path}")

