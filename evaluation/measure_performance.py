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

# Measure sizes
venv_size = get_dir_size_mb("evaluation/venv")
hf_cache = get_dir_size_mb("C:/Users/IQPulse/.cache/huggingface/hub")
rapidocr_models = get_dir_size_mb("evaluation/venv/Lib/site-packages/rapidocr/models")

print(f"=== Storage Footprint ===")
print(f"Virtual Environment Size: {venv_size} MB")
print(f"HuggingFace Models Cache: {hf_cache} MB")
print(f"RapidOCR Models Size:     {rapidocr_models} MB")
print(f"Total Models Storage:     {round(hf_cache + rapidocr_models, 2)} MB")

# Measure import & init times in clean process
print(f"\n=== Process Startup & Init Timing ===")
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

