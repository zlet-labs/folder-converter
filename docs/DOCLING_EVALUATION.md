# Docling Evaluation for Zlet Converter

## Executive Decision: USE DOCLING SELECTIVELY

Based on empirical benchmarking across 15 synthetic, degraded, and structural fixtures on Windows x64, the verdict is **USE DOCLING SELECTIVELY** as an isolated out-of-process conversion worker for Zlet Converter.

### Key Justifications:
1. **Unrivaled Layout & Structural Fidelity:** Docling successfully resolved 2D multi-column reading order on an interleaved stream order fixture (`F02`), converted complex tables to GitHub-Flavored Markdown (`F03`), eliminated running headers and footers as furniture artifacts (`F04`), dehyphenated broken line wraps while preserving compound terms (`F05`), and recognized degraded raster text through built-in RapidOCR without external Tesseract dependencies (`F06`).
2. **Sub-second Office & HTML Ingestion:** Office documents (DOCX `F08`: 0.16s, PPTX `F09`: 0.05s, XLSX `F10`: 0.02s) and HTML (`F11`: 0.04s) convert without heavyweight ML pipeline overhead and without requiring Microsoft Office COM automation. Real DOCX `<w:hyperlink>` XML elements are cleanly converted to Markdown anchors.
3. **Permissive Licensing Architecture:** Docling code is MIT-licensed, its layout model (`docling-layout-heron`) is Apache-2.0, its table model (`tableformer`) is CDLA-Permissive-2.0, and OCR is Apache-2.0. The audit verified zero strong copyleft (GPL/AGPL); one weak file-level copyleft dependency (`certifi` under MPL-2.0) is present and commercially distributable unmodified.
4. **Verified Offline Privacy:** Socket-level monitoring proved zero external network requests during offline execution (`HF_HUB_OFFLINE=1`), ensuring zero document leakage.
5. **Observed Limitations & Why "SELECTIVELY":**
   - **Flattened List Indentation (`F12`):** Docling flattens multi-tier nested PDF lists to column zero, losing sub-item indentation levels. Indentation restoration must be owned by the Zlet Quality Layer.
   - **Resource Footprint:** PDF deep-learning conversion demands ~566 MB model cache, ~1.17 GB virtual environment, and ~1.0–1.3 GB peak RSS with 2–10s per-page CPU execution time.
   - Consequently, Docling should be deployed as an **isolated, optional worker process** with on-demand model acquisition rather than bundled directly into the base lightweight WPF MSI installer.

---

## Tested Environment

- **Date:** September 10, 2026
- **Operating System:** Windows 11 Home / Windows x64 (Build 10.0.26100)
- **Processor:** 11th Gen Intel(R) Core(TM) i5-11400H @ 2.70GHz (6 Cores, 12 Threads)
- **System Memory:** 24 GB RAM
- **Python Environment:** Python 3.11.16 x64 (CPython)
- **Docling Version:** `docling==2.126.0` (`docling-core==2.95.0`, `docling-parse==7.18.0`)
- **PyTorch Engine:** `torch==2.14.0+cpu` (CPU execution)
- **OCR Engine:** `rapidocr==3.9.2` (ONNX / Torch CPU models)
- **Dependency Pinning:** Pinned primary requirements in `evaluation/requirements.txt` with exact 107-package lockfile in `evaluation/requirements-lock.txt`.

---

## Fixture Matrix

All 15 evaluation fixtures were generated programmatically and sanitized to guarantee reproducibility without proprietary data.

| Fixture ID | Filename | Format | Key Evaluation Purpose |
|------------|----------|--------|------------------------|
| `F01` | `F01_simple_text.pdf` | PDF | Baseline text extraction, heading hierarchy, paragraph flow |
| `F02` | `F02_multicolumn.pdf` | PDF | Two-column layout with physically interleaved stream drawing order |
| `F03` | `F03_table.pdf` | PDF | 4x4 tabular data; table border and cell alignment |
| `F04` | `F04_header_footer.pdf` | PDF | 2-page document with running headers and footers; furniture stripping |
| `F05` | `F05_broken_wrap.pdf` | PDF | Artificially hyphenated words across line boundaries; dehyphenation |
| `F06` | `F06_scanned.pdf` | PDF (Raster) | Scanned document with noise, lens blur, and rotational skew; OCR fidelity |
| `F07` | `F07_cyrillic.pdf` | PDF | Russian Cyrillic glyph decoding, UTF-8 integrity |
| `F08` | `F08_structured.docx` | DOCX | Native DOCX parsing: headings, nested lists, table, `<w:hyperlink>` XML element |
| `F09` | `F09_slides.pptx` | PPTX | Native PPTX parsing: title slide, bullet slide, table slide |
| `F10` | `F10_sheets.xlsx` | XLSX | Multi-sheet workbook: transaction table + summary metrics |
| `F11` | `F11_structural.html` | HTML | Semantic HTML tags: `<h1>`, `<ul>`, `<table>`, `<a>` to Markdown |
| `F12` | `F12_nested_lists.pdf` | PDF | Multi-tier ordered and unordered nested lists (indentation test) |
| `F13` | `F13_empty.pdf` | PDF | Single blank page; zero-content graceful handling |
| `F14` | `F14_corrupted.pdf` | PDF | Malformed binary header; exception handling and error resilience |
| `F15` | `F15_mixed_unicode.pdf` | PDF | Trilingual document with mandatory CJK font verification (Latin, Cyrillic, CJK) |

---

## Comparison Method

The evaluation executed a multi-arm benchmark comparing Docling against both lightweight baseline parsers and evaluating alternative modern pipelines:

1. **Arm A — Baseline (Direct Extractors):**
   - PDF: `pypdf 6.18.0` direct font/stream text extraction.
   - DOCX: `python-docx 1.2.0` paragraph/run iterator.
   - PPTX: `python-pptx 1.0.2` text frame extraction.
   - XLSX: `openpyxl 3.1.5` cell iterator.
   - HTML: `BeautifulSoup4 4.15.0` text stripping.
2. **Arm B — Docling 2.126.0 (Target Candidate):**
   - Deep-learning layout analysis via `docling-layout-heron`.
   - Table structure parsing via `tableformer`.
   - Optical character recognition via `rapidocr`.
   - Native document tree export (`DoclingDocument`) with spatial bounding-box provenance and canonical Markdown export.
3. **Alternative Parser Assessment (Marker & PyMuPDF4LLM):**
   - *PyMuPDF4LLM:* Evaluated but **disqualified** due to AGPL-3.0 copyleft license restrictions that conflict with Zlet Converter's MIT license.
   - *Marker:* Evaluated for comparison; however, Marker relies on OpenRAIL-M model licenses (commercial use restricted above $5M revenue) and introduces version pin conflicts with modern `pillow`/`pypdfium2` stacks on Windows x64.

---

## Results & Quality Dimensions

### PDF Findings
- **Multi-Column Reading Order (`F02`):** The PDF stream order was deliberately scrambled by interleaving left and right drawing operations (Left Heading $\to$ Right Heading $\to$ Left P1 $\to$ Right P1 $\to$ Left P2 $\to$ Right P2). Naive sequential stream extraction (`pypdf`) completely failed, outputting interleaved sentences. Docling's 2D layout model successfully separated the columns, reading Left Column completely before advancing to Right Column.
- **Table Structure Extraction (`F03`):** Docling reconstructed a perfect GitHub-Flavored Markdown table with headers, pipes, and data rows. Baseline extraction collapsed the table into unstructured whitespace-separated text without markdown pipe syntax.
- **Header/Footer Stripping (`F04`):** Docling identified repeated running headers (`Company Confidential Report`) and footers (`Page X of Y`) as page furniture and excluded them from the markdown stream. Baseline text extraction contaminated the document body on every page.
- **Dehyphenation & Paragraph Continuity (`F05`):** Words broken across line breaks (`encoun-ter`, `con-straints`, `docu-ment`) were cleanly dehyphenated into coherent tokens, while genuine hyphens (`state-of-the-art`) were preserved.
- **Nested Lists Limitation (`F12`):** While Docling captured list markers (`1.`, `- a.`, `- i.`), it flattened all items to column zero, losing sub-item indentation levels. This is a documented limitation: hierarchical list indentation restoration must be handled by the Zlet Quality Layer.

### OCR Findings (`F06`)
- Tested on an image-only PDF with realistic scanner degradation: off-white paper stock (RGB 247, 245, 240), Gaussian scanner noise ($\sigma=3.5$), optical lens blur (radius 0.5), and rotational scanner skew ($-0.75^{\circ}$).
- Built-in `RapidOCR` triggered automatically on CPU, achieving 100% character recognition accuracy (482 characters).
- No local Tesseract binary installation was needed. Baseline extracted 0 characters.

### Cyrillic & Multilingual Unicode Findings (`F07`, `F15`)
- Cyrillic Russian text was extracted without encoding degradation or mojibake (`F07`: 553 chars).
- In the mixed trilingual fixture (`F15`), Latin diacritics (`café`, `über`), Russian Cyrillic (`Привет мир`), and Chinese CJK (`测试中文文档解析能力: 你好世界`) were preserved simultaneously in valid UTF-8 Markdown, backed by mandatory CJK TrueType font validation in the fixture generator.

### DOCX, PPTX, XLSX, HTML Applicability (`F08`–`F11`)
- **DOCX (`F08`):** Converted in **163 ms**. Converted actual Word `<w:hyperlink>` XML elements into proper Markdown links (`[Zlet Converter GitHub Repository](https://github.com/zlet-labs/zlet-converter)`) alongside headings, lists, and tables.
- **PPTX (`F09`):** Converted in **50 ms**. Generated clean markdown slides with `#` slide headers, bullet points, and tables.
- **XLSX (`F10`):** Converted in **18 ms**. Each worksheet was formatted into a distinct markdown table.
- **HTML (`F11`):** Converted in **36 ms**. Converted standard HTML markup directly into markdown with hyperlinked anchors.

### Determinism Verification
- Executed two consecutive runs on `F01_simple_text.pdf` and `F03_table.pdf`.
- Byte-for-byte comparison confirmed `run1 == run2` with 100% identical byte length (874 bytes and 445 bytes respectively). Output generation is fully deterministic.

### Failure & Error Handling (`F13`, `F14`)
- **Empty Document (`F13`):** Docling processed the single blank page in 2.02s and emitted 0 characters cleanly without exception or null pointer errors.
- **Corrupted Document (`F14`):** When presented with invalid binary header data, Docling threw a structured `ConversionError` from `docling-parse` within 432 ms. It did not crash the host process or hang indefinitely.

---

## Comparison Summary

| Fixture | Description | Docling Time | Docling RSS | Docling Quality | Baseline Time | Baseline Quality |
|---------|-------------|--------------|-------------|-----------------|---------------|------------------|
| `F01` | Simple text PDF | 8.42s | 1037.2 MB | High (clean headings & flow) | 0.13s | Medium (raw text) |
| `F02` | Multi-column PDF (interleaved stream) | 5.22s | 1140.3 MB | High (2D reading order resolved) | 0.01s | Poor (interleaved sentences) |
| `F03` | Table PDF | 9.83s | 1110.2 MB | High (GFM markdown table) | 0.01s | Poor (unstructured text) |
| `F04` | Header/Footer PDF | 3.87s | 1208.3 MB | High (furniture stripped) | 0.01s | Poor (text polluted) |
| `F05` | Broken Wrap PDF | 2.08s | 1216.0 MB | High (dehyphenated) | 0.00s | Medium (broken lines) |
| `F06` | Scanned Image PDF (degraded) | 8.15s | 1287.8 MB | High (100% OCR accuracy) | 0.01s | Failed (0 chars extracted) |
| `F07` | Cyrillic PDF | 2.28s | 1258.8 MB | High (clean UTF-8) | 0.01s | High (text extracted) |
| `F08` | Structured DOCX (hyperlink element) | 0.16s | 1260.0 MB | High (headings, lists, table, link) | 0.02s | Low (flat text lines) |
| `F09` | Slide Deck PPTX | 0.05s | 1265.8 MB | High (slides, tables, bullets) | 0.01s | Low (flat text) |
| `F10` | Multi-Sheet XLSX | 0.02s | 1266.2 MB | High (multi-table markdown) | 0.01s | Medium (pipe delimited) |
| `F11` | Semantic HTML | 0.04s | 1266.2 MB | High (GFM formatting & links) | 0.00s | Low (stripped tags) |
| `F12` | Nested Lists PDF | 2.06s | 1281.9 MB | Partial / Limitation (flattened list) | 0.01s | Medium (flat text) |
| `F13` | Empty PDF | 2.02s | 1282.8 MB | High (clean 0-byte output) | 0.00s | High (0-byte output) |
| `F14` | Corrupted PDF | 0.43s | 1282.8 MB | High (graceful ConversionError) | 0.00s | High (graceful PdfStreamError) |
| `F15` | Mixed Unicode PDF | 2.36s | 1258.8 MB | High (Latin + Cyrillic + CJK) | 0.01s | High (Unicode preserved) |

---

## Local & Offline Verification

Offline execution was validated through a strict isolation test (`evaluation/verify_offline.py`):
1. Environment variables set: `HF_HUB_OFFLINE=1`, `TRANSFORMERS_OFFLINE=1`, `HTTP_PROXY=http://0.0.0.0:1`, `HTTPS_PROXY=http://0.0.0.0:1`.
2. Python `socket.socket.connect` was dynamically intercepted to record and immediately abort any outgoing network connection.
3. Automated test script verified that all conversions succeeded offline (`F01`: 9.12s, `F06` degraded OCR: 7.83s, `F08`: 0.14s) and strictly exits nonzero if any failure or network call occurs.
4. **Result:** All conversions succeeded offline. Exactly **0 network connection attempts** were made.
5. **Verdict:** Docling operates 100% locally with zero cloud telemetry and zero document leakage.

---

## Model & Download Behavior

During initial setup and cold execution, Docling fetches required models from Hugging Face and ModelScope:
- **`docling-project/docling-layout-heron`:** 163.81 MB (Layout segmentation ONNX / SafeTensors).
- **`docling-project/docling-models` (TableFormer):** 358.21 MB (Accurate + Fast table structure models).
- **`RapidOCR` models (PP-OCRv6):** 61.02 MB (Detection, classification, and recognition models).
- **Total Local Model Footprint:** **566.47 MB**.
- **Model Cache Locations:**
  - HuggingFace: Dynamically resolved from `HF_HOME` / `%USERPROFILE%\.cache\huggingface\hub\`.
  - RapidOCR: `<venv>\Lib\site-packages\rapidocr\models\`.
- **Pre-warming:** Once cached, no internet access is required.

---

## Licensing Audit

An automated audit of all 107 installed packages in the evaluation virtual environment was conducted via `pip-licenses`:
- **Docling Code:** MIT License.
- **Core Parsers (`docling-parse`, `docling-core`):** MIT License. C++ PDFium bindings are permissively licensed.
- **Layout Model (`docling-layout-heron`):** Apache-2.0.
- **Table Structure Model (`tableformer`):** CDLA-Permissive-2.0.
- **OCR Engine (`RapidOCR`):** Apache-2.0.
- **Deep Learning Dependencies (`torch`, `torchvision`, `transformers`):** BSD-3 / Apache-2.0.
- **Office Parsers (`python-docx`, `python-pptx`, `openpyxl`, `lxml`):** MIT / BSD.
- **Copyleft Assessment:**
  - **Zero Strong Copyleft:** No GPL, AGPL, or SSPL components exist.
  - **Weak File-Level Copyleft (`certifi`):** `certifi 2026.7.22` is licensed under Mozilla Public License 2.0 (MPL-2.0). Under MPL-2.0 §3, distributing `certifi` as an unmodified third-party library dependency does not subject surrounding application source code to copyleft terms.
  - (Note: `fpdf2` is LGPL-3.0, but it is strictly a test fixture generator tool and is not a runtime dependency of Docling).
- **Commercial Viability:** Fully compatible with Zlet Converter's MIT license and suitable for commercial desktop distribution.

---

## Windows Packaging & Integration Findings

1. **Pre-built Windows Wheels:** `docling-parse 7.18.0` ships pre-compiled `win_amd64` wheels on PyPI. No MSVC C++ toolchain or compilation is required on the end-user machine.
2. **Symlinks Notice:** On standard Windows installations where Developer Mode is disabled, Hugging Face Hub emits a benign symlink fallback warning and stores duplicated model files. This can be suppressed via `HF_HUB_DISABLE_SYMLINKS_WARNING=1`.
3. **Packaging Strategy:**
   - Bundling PyTorch and Docling (~1.7 GB uncompressed) directly into the primary Zlet Converter MSI installer would severely bloat the desktop application.
   - Recommended strategy: Distribute an isolated **Zlet AI Document Engine** worker package (either a self-contained embedded Python runtime or on-demand background model downloader) managed by the desktop UI.

---

## CPU, RAM, Startup & Performance Summary

- **Package Install Size (Disk):** 1171.34 MB (venv).
- **Model Weights (Disk):** 566.47 MB.
- **Cold Process Import Time:**
  - `import docling`: 0.04s.
  - `from docling.document_converter import DocumentConverter`: 5.83s.
  - `DocumentConverter()` instantiation: 0.08s.
  - Total cold process init: **~6.0s**.
- **Runtime Memory (Peak RSS):**
  - Office / HTML conversion: ~100–200 MB in isolation.
  - PDF layout and OCR conversion: **1018 MB – 1288 MB**.
- **Empirical Throughput (Intel i5-11400H CPU):**
  - **Non-PDF Formats:**
    - Structured DOCX (`F08`): **~6 documents/second** (0.16s).
    - Slide Deck PPTX (`F09`): **~20 presentations/second** (0.05s).
    - Multi-Sheet XLSX (`F10`): **~55 workbooks/second** (0.018s).
    - Semantic HTML (`F11`): **~28 documents/second** (0.036s).
  - **PDF Formats:**
    - Cold start / dense table parsing (`F01`, `F03`): **0.10–0.12 pages/second** (~8.4–9.8s per page due to TableFormer / layout inference).
    - Warm digital PDFs without complex tables (`F04`, `F05`, `F07`, `F12`, `F15`): **0.25–0.50 pages/second** (~2.0–3.9s per page).
    - Degraded scanned PDF with OCR (`F06`): **~0.12 pages/second** (~8.1s per page).

---

## Proposed Integration Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                 Zlet Converter Desktop App                   │
│                     (.NET 8 / WPF)                          │
└──────────────────────────────┬──────────────────────────────┘
                               │
                Spawns & Monitors Process
                               │
                               ▼
┌─────────────────────────────────────────────────────────────┐
│             DoclingWorkerProcessRunner (C#)                 │
│  - Stdin/Stdout JSON streaming                              │
│  - Job tracking & timeout enforcement                       │
│  - Cancellation via process tree kill                       │
└──────────────────────────────┬──────────────────────────────┘
                               │ JSON IPC (stdio)
                               ▼
┌─────────────────────────────────────────────────────────────┐
│             Isolated Python Worker (zlet_docling_worker.py) │
│  - Long-lived persistent daemon or on-demand worker        │
│  - Docling DocumentConverter instance                       │
│  - Emits: Markdown text + Document AST + Provenance         │
└─────────────────────────────────────────────────────────────┘
```

### 1. Process Isolation & Inter-Process Communication (IPC)
- Reuses the robust architectural pattern established by `MicrosoftOfficeWorkerProcessRunner`.
- The .NET application communicates with `zlet_docling_worker.py` over standard I/O (stdin JSON requests $\to$ stdout JSON responses).
- **Protocol:**
  ```json
  // Request
  { "id": "job-101", "sourcePath": "C:\\Docs\\report.pdf", "format": "PDF", "options": { "doOcr": true } }
  
  // Response
  { "id": "job-101", "success": true, "markdown": "# Title\n...", "metadata": { "pages": 2, "tables": 1 }, "error": null }
  ```

### 2. Cancellation & Fault Isolation
- If a document hangs or the user cancels conversion, `DoclingWorkerProcessRunner` immediately kills the Python process.
- The WPF desktop UI remains completely immune to Python crashes, memory leaks, or unhandled C++ segmentation faults in underlying PDFium libraries.

### 3. Proposed Normalized Document Model
Zlet Converter should define a parser-neutral document intermediate representation (AST) in C#:
- `DocumentNode`: `HeaderNode`, `ParagraphNode`, `TableNode`, `ListNode`, `CodeBlockNode`, `ImageNode`.
- Every node carries `SourceSpan` and optional `ProvenanceBoundingBox` (`PageNumber`, `X`, `Y`, `Width`, `Height`).
- Docling's `result.document.export_to_dict()` provides the exact data structure needed to populate this AST.

### 4. Provenance Strategy
- In Docling, `result.document.export_to_dict()` provides a `texts` list where each entry includes a nonempty `prov` block with verified `page_no` (int >= 1) and bounding coordinate dictionary (`l`, `t`, `r`, `b` with `coord_origin: 'BOTTOMLEFT'`).
- The benchmark runner verified valid bounding box coordinates across all text entries (`valid_bbox_count: 5` on `F01`).
- Zlet can store this provenance metadata alongside markdown chunks, enabling future UI features such as "Click Markdown element to view source document page".

### 5. Zlet Markdown Quality Layer Responsibilities
To achieve production-grade consistency across diverse converters, the post-processing Quality Layer in Zlet must handle:
- **Hierarchical List Indentation Restoration:** Docling emits list items at column zero (`F12`); the Quality Layer must re-indent nested sub-items based on AST depth.
- **YAML Frontmatter Injection:** Inject document metadata (title, author, source path, conversion timestamp, tool version).
- **GFM Table Normalization:** Clean pipe padding and enforce standard alignment markers (`|:---|:---|`).
- **Heading Level Adjustment:** Optional top-level heading offset adjustment (`#` $\to$ `##`).
- **Relative Asset Rebasing:** Extract embedded images to an assets subfolder and update markdown image paths.

---

## Risks & Mitigations

| Risk | Severity | Mitigation |
|------|----------|------------|
| Initial model download size (~566 MB) | High | Implement on-demand model download UI in Settings or provide an offline bundle installer. |
| High RAM consumption during PDF DL parsing (~1.2 GB) | Medium | Process documents sequentially in worker; restart worker if memory exceeds 1.5 GB. |
| Startup latency (~6s cold start) | Medium | Use a persistent long-running background worker during batch processing rather than spawning per file. |
| Flattened nested list indentation | Medium | Restore hierarchical indentation in Zlet Quality Layer post-processing. |
| Non-developer Windows symlink warnings | Low | Set `HF_HUB_DISABLE_SYMLINKS_WARNING=1` in the worker launcher. |

---

## Recommendations for ZC-043 (Implementation Phase)

1. **Implement `DoclingWorkerProcessRunner` in `FolderConverter.Core`:** Mirror `MicrosoftOfficeWorkerProcessRunner` for stdin/stdout JSON protocol.
2. **Create `zlet_docling_worker.py`:** Write a lightweight, resilient Python entry point with warm converter caching.
3. **Register `DoclingConversionAdapter`:**
   - Map `ConversionTarget.Markdown` for `SourceFormat.Pdf`.
   - Optionally offer Docling as a high-speed alternative for `SourceFormat.Docx`, `SourceFormat.Pptx`, and `SourceFormat.Xlsx` when Microsoft Office is not installed on the user machine.
4. **Settings & Runtime Management:**
   - Add a "Docling AI Engine" status check in the Settings view (inspecting Python venv and model presence).
   - Provide a 1-click "Download / Update Models" action.

---

## Zlet AI Bench Compatibility

The evaluation scripts created under `evaluation/` (`create_fixtures.py`, `run_evaluation.py`, `verify_offline.py`, `measure_performance.py`) along with the 15 synthetic fixtures and output baselines in `evaluation/outputs/` form a self-contained, automated test bench. This suite can be integrated directly into Zlet AI Bench regression suites for continuous quality verification across releases.
