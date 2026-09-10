import sys
import os
import time
import json
import argparse
import threading
import hashlib
import psutil
from pathlib import Path

# Enforce UTF-8 on stdout/stderr
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

FIXTURES_DIR = Path("evaluation/fixtures")
OUTPUTS_DIR = Path("evaluation/outputs")

GATE_FIXTURES = [
    "F01_simple_text.pdf",
    "F02_multicolumn.pdf",
    "F03_table.pdf",
    "F06_scanned.pdf",
    "F07_cyrillic.pdf",
    "F08_structured.docx",
    "F09_slides.pptx",
    "F13_empty.pdf"
]

ALL_FIXTURES = [
    "F01_simple_text.pdf",
    "F02_multicolumn.pdf",
    "F03_table.pdf",
    "F04_header_footer.pdf",
    "F05_broken_wrap.pdf",
    "F06_scanned.pdf",
    "F07_cyrillic.pdf",
    "F08_structured.docx",
    "F09_slides.pptx",
    "F10_sheets.xlsx",
    "F11_structural.html",
    "F12_nested_lists.pdf",
    "F13_empty.pdf",
    "F14_corrupted.pdf",
    "F15_mixed_unicode.pdf"
]

EXPECTED_TO_FAIL = ["F14_corrupted.pdf"]
EXPECTED_EMPTY = ["F13_empty.pdf"]

class ProcessTreeMemoryMonitor:
    """
    Monitors RSS memory of current process and all child processes concurrently
    during conversion execution to capture true peak RSS footprint.
    """
    def __init__(self, interval_sec=0.01):
        self.interval = interval_sec
        self.proc = psutil.Process(os.getpid())
        self.start_rss = 0
        self.peak_rss = 0
        self.post_rss = 0
        self.stop_event = threading.Event()
        self.thread = None

    def _sample(self):
        try:
            total = self.proc.memory_info().rss
            for child in self.proc.children(recursive=True):
                try:
                    total += child.memory_info().rss
                except (psutil.NoSuchProcess, psutil.AccessDenied):
                    pass
            return total
        except (psutil.NoSuchProcess, psutil.AccessDenied):
            return 0

    def _run(self):
        while not self.stop_event.is_set():
            val = self._sample()
            if val > self.peak_rss:
                self.peak_rss = val
            self.stop_event.wait(self.interval)

    def start(self):
        self.start_rss = self._sample()
        self.peak_rss = self.start_rss
        self.stop_event.clear()
        self.thread = threading.Thread(target=self._run, daemon=True)
        self.thread.start()

    def stop(self):
        self.stop_event.set()
        if self.thread:
            self.thread.join(timeout=1.0)
        self.post_rss = self._sample()
        if self.post_rss > self.peak_rss:
            self.peak_rss = self.post_rss
        return {
            "start_rss_mb": round(self.start_rss / (1024 * 1024), 2),
            "post_rss_mb": round(self.post_rss / (1024 * 1024), 2),
            "peak_rss_mb": round(self.peak_rss / (1024 * 1024), 2),
        }

def check_provenance_bboxes(texts):
    """
    Inspect nonempty provenance entries for required page and coordinate data (l, t, r, b).
    Returns (has_valid_bboxes, valid_count).
    """
    valid_count = 0
    for t in texts:
        if isinstance(t, dict):
            prov_list = t.get("prov")
            if isinstance(prov_list, list) and len(prov_list) > 0:
                for p in prov_list:
                    if isinstance(p, dict):
                        bbox = p.get("bbox")
                        page_no = p.get("page_no")
                        if (
                            isinstance(page_no, int)
                            and isinstance(bbox, dict)
                            and all(k in bbox for k in ("l", "t", "r", "b"))
                        ):
                            valid_count += 1
    return (valid_count > 0), valid_count

def run_baseline_fixture(fixture_path):
    ext = fixture_path.suffix.lower()
    mem_monitor = ProcessTreeMemoryMonitor(interval_sec=0.01)
    mem_monitor.start()
    start_time = time.perf_counter()
    md_content = ""
    error = None
    
    try:
        if ext == ".pdf":
            import pypdf
            reader = pypdf.PdfReader(str(fixture_path))
            pages_text = []
            for i, page in enumerate(reader.pages):
                pt = page.extract_text() or ""
                if pt.strip():
                    pages_text.append(pt)
            md_content = "\n\n---\n\n".join(pages_text)
        elif ext == ".docx":
            import docx
            doc = docx.Document(str(fixture_path))
            lines = [p.text for p in doc.paragraphs if p.text.strip()]
            md_content = "\n\n".join(lines)
        elif ext == ".pptx":
            import pptx
            prs = pptx.Presentation(str(fixture_path))
            slide_texts = []
            for slide in prs.slides:
                st = []
                for shape in slide.shapes:
                    if shape.has_text_frame:
                        st.append(shape.text_frame.text)
                if st:
                    slide_texts.append("\n".join(st))
            md_content = "\n\n---\n\n".join(slide_texts)
        elif ext == ".xlsx":
            import openpyxl
            wb = openpyxl.load_workbook(str(fixture_path), data_only=True)
            sheet_blocks = []
            for name in wb.sheetnames:
                ws = wb[name]
                rows = []
                for row in ws.iter_rows(values_only=True):
                    row_str = " | ".join([str(c) if c is not None else "" for c in row])
                    if row_str.strip():
                        rows.append(row_str)
                if rows:
                    sheet_blocks.append(f"### Sheet: {name}\n" + "\n".join(rows))
            md_content = "\n\n".join(sheet_blocks)
        elif ext == ".html":
            from bs4 import BeautifulSoup
            with open(fixture_path, "r", encoding="utf-8") as f:
                soup = BeautifulSoup(f.read(), "html.parser")
            md_content = soup.get_text()
        else:
            md_content = fixture_path.read_text(encoding="utf-8", errors="replace")
    except Exception as ex:
        error = f"{type(ex).__name__}: {str(ex)}"
        
    duration = time.perf_counter() - start_time
    mem_stats = mem_monitor.stop()
    
    meta = {
        "arm": "baseline",
        "fixture": fixture_path.name,
        "duration_seconds": round(duration, 4),
        "start_rss_mb": mem_stats["start_rss_mb"],
        "post_rss_mb": mem_stats["post_rss_mb"],
        "peak_rss_mb": mem_stats["peak_rss_mb"],
        "success": error is None,
        "error": error,
        "char_count": len(md_content),
        "line_count": len(md_content.splitlines())
    }
    return md_content, meta

def run_docling_fixture(converter, fixture_path):
    mem_monitor = ProcessTreeMemoryMonitor(interval_sec=0.01)
    mem_monitor.start()
    start_time = time.perf_counter()
    md_content = ""
    error = None
    provenance_info = {}
    
    try:
        result = converter.convert(str(fixture_path))
        md_content = result.document.export_to_markdown()
        doc_dict = result.document.export_to_dict()
        
        texts = doc_dict.get("texts", [])
        tables = doc_dict.get("tables", [])
        pictures = doc_dict.get("pictures", [])
        pages = doc_dict.get("pages", {})
        
        has_bboxes, valid_bbox_count = check_provenance_bboxes(texts)
        provenance_info = {
            "num_pages": len(pages),
            "num_texts": len(texts),
            "num_tables": len(tables),
            "num_pictures": len(pictures),
            "has_provenance_bboxes": has_bboxes,
            "valid_bbox_count": valid_bbox_count,
            "keys": list(doc_dict.keys())
        }
    except Exception as ex:
        error = f"{type(ex).__name__}: {str(ex)}"
        
    duration = time.perf_counter() - start_time
    mem_stats = mem_monitor.stop()
    
    meta = {
        "arm": "docling",
        "fixture": fixture_path.name,
        "duration_seconds": round(duration, 4),
        "start_rss_mb": mem_stats["start_rss_mb"],
        "post_rss_mb": mem_stats["post_rss_mb"],
        "peak_rss_mb": mem_stats["peak_rss_mb"],
        "success": error is None,
        "error": error,
        "char_count": len(md_content),
        "line_count": len(md_content.splitlines()),
        "provenance": provenance_info
    }
    return md_content, meta

def run_suite(fixture_names, arm="both", determinism_check=True):
    print("==================================================")
    print("Starting Evaluation Suite")
    print(f"Fixtures count: {len(fixture_names)}")
    print(f"Arm: {arm}")
    print("==================================================")
    
    converter = None
    if arm in ("docling", "both"):
        print("Initializing Docling DocumentConverter...")
        t0 = time.perf_counter()
        from docling.document_converter import DocumentConverter
        converter = DocumentConverter()
        print(f"DocumentConverter ready in {time.perf_counter() - t0:.2f}s")
        
    summary = []
    failed_fixtures = []
    
    for fname in fixture_names:
        fpath = FIXTURES_DIR / fname
        if not fpath.exists():
            print(f"Error: Fixture {fname} not found!")
            failed_fixtures.append((fname, "Fixture file not found"))
            continue
            
        print(f"\nEvaluating: {fname} ({fpath.stat().st_size} bytes)")
        
        # Docling arm
        if arm in ("docling", "both"):
            out_arm_dir = OUTPUTS_DIR / "docling"
            out_arm_dir.mkdir(parents=True, exist_ok=True)
            
            md_doc, meta_doc = run_docling_fixture(converter, fpath)
            
            base_id = fname.split(".")[0]
            (out_arm_dir / f"{base_id}.md").write_text(md_doc, encoding="utf-8")
            (out_arm_dir / f"{base_id}.meta.json").write_text(json.dumps(meta_doc, indent=2), encoding="utf-8")
            
            status_str = "SUCCESS" if meta_doc["success"] else f"FAILED ({meta_doc['error'][:40]}...)"
            print(f"  [docling]  {status_str} | {meta_doc['duration_seconds']}s | {meta_doc['char_count']} chars | peak RSS {meta_doc['peak_rss_mb']}MB")
            summary.append(meta_doc)
            
            # Check expected pass/fail status
            if fname in EXPECTED_TO_FAIL:
                if meta_doc["success"]:
                    print(f"  --> UNEXPECTED PASS for {fname} (was expected to fail)")
                    failed_fixtures.append((fname, "Expected to fail, but succeeded"))
            else:
                if not meta_doc["success"]:
                    print(f"  --> CONVERSION FAILED for {fname}: {meta_doc['error']}")
                    failed_fixtures.append((fname, meta_doc["error"]))
                elif fname not in EXPECTED_EMPTY and meta_doc["char_count"] == 0:
                    print(f"  --> ZERO CHARACTERS extracted for non-empty fixture {fname}")
                    failed_fixtures.append((fname, "Zero characters extracted"))

            # Automatic mechanical OCR validation for F06
            if fname == "F06_scanned.pdf" and meta_doc["success"]:
                try:
                    from evaluation.validate_ocr import run_ocr_validation
                except ImportError:
                    try:
                        from validate_ocr import run_ocr_validation
                    except ImportError:
                        run_ocr_validation = None
                if run_ocr_validation:
                    print("  --> Executing mechanical OCR accuracy validation against ground truth...")
                    ocr_res = run_ocr_validation()
                    meta_doc["ocr_validation"] = {
                        "normalized_chars": ocr_res["ground_truth"]["normalized_character_count"],
                        "character_errors": ocr_res["metrics"]["character_errors"],
                        "exact_match": ocr_res["metrics"]["exact_match"],
                        "cer_percent": ocr_res["metrics"]["character_error_rate_percent"],
                        "recall_percent": ocr_res["metrics"]["character_recall_percent"]
                    }
                    if not ocr_res["metrics"]["exact_match"]:
                        failed_fixtures.append((fname, f"OCR validation mismatch: CER {ocr_res['metrics']['character_error_rate_percent']}%"))

            
        # Baseline arm
        if arm in ("baseline", "both"):
            out_arm_dir = OUTPUTS_DIR / "baseline"
            out_arm_dir.mkdir(parents=True, exist_ok=True)
            
            md_base, meta_base = run_baseline_fixture(fpath)
            
            base_id = fname.split(".")[0]
            (out_arm_dir / f"{base_id}.md").write_text(md_base, encoding="utf-8")
            (out_arm_dir / f"{base_id}.meta.json").write_text(json.dumps(meta_base, indent=2), encoding="utf-8")
            
            status_str = "SUCCESS" if meta_base["success"] else f"FAILED ({meta_base['error'][:40]}...)"
            print(f"  [baseline] {status_str} | {meta_base['duration_seconds']}s | {meta_base['char_count']} chars | peak RSS {meta_base['peak_rss_mb']}MB")
            summary.append(meta_base)

    # Determinism test
    determinism_results = []
    determinism_failures = []
    if determinism_check and arm in ("docling", "both"):
        print("\n--- Running Comprehensive Determinism Verification ---")
        det_fixtures = [f for f in fixture_names if f not in EXPECTED_TO_FAIL]
        for d_fname in det_fixtures:
            df_path = FIXTURES_DIR / d_fname
            if not df_path.exists():
                continue
            base_id = d_fname.split(".")[0]
            md_file = OUTPUTS_DIR / "docling" / f"{base_id}.md"
            if not md_file.exists():
                continue
            first_run_md = md_file.read_text(encoding="utf-8")
            second_md, _ = run_docling_fixture(converter, df_path)
            
            h1 = hashlib.sha256(first_run_md.encode("utf-8")).hexdigest()
            h2 = hashlib.sha256(second_md.encode("utf-8")).hexdigest()
            is_identical = (h1 == h2)
            
            det_record = {
                "fixture": d_fname,
                "is_identical": is_identical,
                "bytes": len(first_run_md.encode("utf-8")),
                "run1_sha256": h1,
                "run2_sha256": h2
            }
            determinism_results.append(det_record)
            status_tag = "MATCH" if is_identical else "MISMATCH"
            print(f"  [{status_tag}] {d_fname:<25} sha256:{h1[:12]}... (bytes: {det_record['bytes']})")
            if not is_identical:
                determinism_failures.append(d_fname)
                
        suite_type = 'gate' if len(fixture_names) == 8 else 'all' if len(fixture_names) == 15 else 'custom'
        det_path = OUTPUTS_DIR / f"determinism_{suite_type}.json"
        det_path.write_text(json.dumps(determinism_results, indent=2), encoding="utf-8")
        # Also maintain a master determinism_summary.json for all fixtures
        if suite_type == 'all':
            (OUTPUTS_DIR / "determinism_summary.json").write_text(json.dumps(determinism_results, indent=2), encoding="utf-8")
        print(f"Determinism verification evidence written to {det_path}")

    # Save summary
    suite_type = 'gate' if len(fixture_names) == 8 else 'all' if len(fixture_names) == 15 else 'custom'
    summary_path = OUTPUTS_DIR / f"summary_{suite_type}_{arm}.json"
    summary_path.write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(f"\nEvaluation run complete. Summary written to {summary_path}")

    # Enforce failure exit status on errors
    total_errors = len(failed_fixtures) + len(determinism_failures)
    if total_errors > 0:
        print(f"\n[FAIL] SUITE FAILED with {total_errors} issue(s):")
        for f, err in failed_fixtures:
            print(f"  - Fixture Failure: {f} ({err})")
        for d in determinism_failures:
            print(f"  - Determinism Mismatch: {d}")
        sys.exit(1)
    else:
        print("\n[PASS] SUITE PASSED: All fixtures performed according to expected criteria.")
        sys.exit(0)

def main():
    parser = argparse.ArgumentParser(description="Docling Evaluation Benchmark Runner")
    parser.add_argument("--set", choices=["gate", "all"], default="gate", help="Fixture set: gate (8) or all (15)")
    parser.add_argument("--fixtures", type=str, default=None, help="Comma-separated list of fixture filenames")
    parser.add_argument("--arm", choices=["docling", "baseline", "both"], default="both", help="Evaluation arm")
    parser.add_argument("--no-determinism", action="store_true", help="Skip determinism verification")
    args = parser.parse_args()
    
    if args.fixtures:
        fixtures = [f.strip() for f in args.fixtures.split(",") if f.strip()]
    else:
        fixtures = GATE_FIXTURES if args.set == "gate" else ALL_FIXTURES
    run_suite(fixtures, arm=args.arm, determinism_check=not args.no_determinism)

if __name__ == "__main__":
    main()
