import sys
import os
import time
import json
import argparse
import psutil
from pathlib import Path

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

def get_process_memory_mb():
    process = psutil.Process(os.getpid())
    return process.memory_info().rss / (1024 * 1024)

def run_baseline_fixture(fixture_path):
    ext = fixture_path.suffix.lower()
    start_time = time.perf_counter()
    start_rss = get_process_memory_mb()
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
    peak_rss = get_process_memory_mb()
    
    meta = {
        "arm": "baseline",
        "fixture": fixture_path.name,
        "duration_seconds": round(duration, 4),
        "start_rss_mb": round(start_rss, 2),
        "peak_rss_mb": round(peak_rss, 2),
        "success": error is None,
        "error": error,
        "char_count": len(md_content),
        "line_count": len(md_content.splitlines())
    }
    return md_content, meta

def run_docling_fixture(converter, fixture_path):
    start_time = time.perf_counter()
    start_rss = get_process_memory_mb()
    md_content = ""
    error = None
    provenance_info = {}
    
    try:
        result = converter.convert(str(fixture_path))
        md_content = result.document.export_to_markdown()
        doc_dict = result.document.export_to_dict()
        
        # Analyze structure / provenance
        texts = doc_dict.get("texts", [])
        tables = doc_dict.get("tables", [])
        pictures = doc_dict.get("pictures", [])
        pages = doc_dict.get("pages", {})
        
        has_bboxes = any("prov" in t for t in texts if isinstance(t, dict))
        provenance_info = {
            "num_pages": len(pages),
            "num_texts": len(texts),
            "num_tables": len(tables),
            "num_pictures": len(pictures),
            "has_provenance_bboxes": has_bboxes,
            "keys": list(doc_dict.keys())
        }
    except Exception as ex:
        error = f"{type(ex).__name__}: {str(ex)}"
        
    duration = time.perf_counter() - start_time
    peak_rss = get_process_memory_mb()
    
    meta = {
        "arm": "docling",
        "fixture": fixture_path.name,
        "duration_seconds": round(duration, 4),
        "start_rss_mb": round(start_rss, 2),
        "peak_rss_mb": round(peak_rss, 2),
        "success": error is None,
        "error": error,
        "char_count": len(md_content),
        "line_count": len(md_content.splitlines()),
        "provenance": provenance_info
    }
    return md_content, meta

def run_suite(fixture_names, arm="both", determinism_check=True):
    print(f"==================================================")
    print(f"Starting Evaluation Suite")
    print(f"Fixtures count: {len(fixture_names)}")
    print(f"Arm: {arm}")
    print(f"==================================================")
    
    converter = None
    if arm in ("docling", "both"):
        print("Initializing Docling DocumentConverter...")
        t0 = time.perf_counter()
        from docling.document_converter import DocumentConverter
        converter = DocumentConverter()
        print(f"DocumentConverter ready in {time.perf_counter() - t0:.2f}s")
        
    summary = []
    
    for fname in fixture_names:
        fpath = FIXTURES_DIR / fname
        if not fpath.exists():
            print(f"Warning: Fixture {fname} not found!")
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
    if determinism_check and arm in ("docling", "both"):
        print("\n--- Running Determinism Verification (F01 & F03) ---")
        for d_fname in ["F01_simple_text.pdf", "F03_table.pdf"]:
            df_path = FIXTURES_DIR / d_fname
            if not df_path.exists():
                continue
            base_id = d_fname.split(".")[0]
            first_run_md = (OUTPUTS_DIR / "docling" / f"{base_id}.md").read_text(encoding="utf-8")
            second_md, _ = run_docling_fixture(converter, df_path)
            is_identical = (first_run_md == second_md)
            print(f"  {d_fname} run1 == run2: {is_identical} (bytes: {len(first_run_md.encode('utf-8'))} vs {len(second_md.encode('utf-8'))})")

    # Save complete summary
    summary_path = OUTPUTS_DIR / f"summary_{'gate' if len(fixture_names) == 8 else 'all'}_{arm}.json"
    summary_path.write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(f"\nEvaluation run complete. Summary written to {summary_path}")

def main():
    parser = argparse.ArgumentParser(description="Docling Evaluation Benchmark Runner")
    parser.add_argument("--set", choices=["gate", "all"], default="gate", help="Fixture set: gate (8) or all (15)")
    parser.add_argument("--arm", choices=["docling", "baseline", "both"], default="both", help="Evaluation arm")
    parser.add_argument("--no-determinism", action="store_true", help="Skip determinism verification")
    args = parser.parse_args()
    
    fixtures = GATE_FIXTURES if args.set == "gate" else ALL_FIXTURES
    run_suite(fixtures, arm=args.arm, determinism_check=not args.no_determinism)

if __name__ == "__main__":
    main()
