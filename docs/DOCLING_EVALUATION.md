# Docling Evaluation for Zlet Converter

## Executive Decision: USE DOCLING SELECTIVELY

Based on empirical benchmarking across 15 synthetic, degraded, and structural fixtures on Windows x64, the verdict is **USE DOCLING SELECTIVELY** as an isolated out-of-process conversion worker for Zlet Converter.

### Key Justifications:
1. **Layout & Structural Extraction:** Docling successfully resolved 2D multi-column reading order on an interleaved stream order fixture (`F02`), converted complex tables into structured GitHub-Flavored Markdown (`F03`), eliminated running headers and footers as furniture artifacts (`F04`), dehyphenated broken line wraps while preserving compound terms (`F05`), and recognized degraded raster text through built-in RapidOCR without external Tesseract dependencies (`F06`: 475/475 normalized semantic characters recognized with 0 character errors, CER 0.00%, exact normalized match verified mechanically against ground truth in `evaluation/outputs/ocr_validation.json`; raw Markdown length is 482 characters including 7 heading and newline formatting tokens).
2. **Sub-second Office & HTML Ingestion:** Office documents (DOCX `F08`: 0.14s–0.33s, PPTX `F09`: 0.04s–0.06s, XLSX `F10`: 0.01s) and HTML (`F11`: 0.04s) convert without heavyweight ML pipeline overhead and without requiring Microsoft Office COM automation. Native DOCX `<w:hyperlink>` XML elements are converted into Markdown links with distinct anchor text.
3. **Licensing Architecture:** Core Docling code is MIT-licensed, its layout model (`docling-layout-heron`) is Apache-2.0, its table model (`tableformer`) is CDLA-Permissive-2.0, and OCR is Apache-2.0. The audit verified zero strong copyleft (GPL/AGPL) in runtime dependencies; one weak file-level copyleft dependency (`certifi` under MPL-2.0) is present and commercially distributable unmodified. (The test harness uses `fpdf2` under LGPL-3.0 strictly as an offline test fixture generator tool).
4. **Verified Local Offline Execution:** Intercepting Python socket calls (`connect`, `connect_ex`, `sendto`, `getaddrinfo`, `create_connection`) under offline configuration (`HF_HUB_OFFLINE=1`, dummy proxy) verified 0 outbound network requests or DNS resolutions during tested offline conversions (`F01`, `F06`, `F08`).
5. **Observed Limitations & Why "SELECTIVELY":**
   - **Flattened List Indentation (`F12`):** Docling flattens multi-tier nested PDF lists to column zero, losing sub-item indentation levels. Indentation restoration must be handled by the Zlet Quality Layer.
   - **Resource Footprint:** PDF deep-learning conversion demands ~566 MB model cache, ~1.17 GB virtual environment, and ~1.07–1.62 GB process-tree peak RSS with 2.4–16.2s per-page CPU execution time.
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

The evaluation executed an empirical conversion benchmark comparing Docling against lightweight direct extractors on identical fixtures. Alternative modern pipelines (Marker and PyMuPDF4LLM) were assessed through architectural, dependency, and licensing feasibility analysis:

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
   - *PyMuPDF4LLM:* Evaluated at the architectural level but **disqualified** without running a full conversion arm due to AGPL-3.0 copyleft license restrictions that conflict with Zlet Converter's MIT distribution model.
   - *Marker:* Evaluated at the feasibility level; disqualified from production consideration due to OpenRAIL-M model licensing restrictions (commercial use restricted above $5M revenue) and heavy Windows dependency/VRAM requirements (no lightweight CPU-only wheels matching our minimal runtime target).

---

## Results & Quality Dimensions

### PDF Findings
- **Multi-Column Reading Order (`F02`):** The PDF stream order was deliberately scrambled by interleaving left and right drawing operations (Left Heading $\to$ Right Heading $\to$ Left P1 $\to$ Right P1 $\to$ Left P2 $\to$ Right P2). Naive sequential stream extraction (`pypdf`) completely failed, outputting interleaved sentences. Docling's 2D layout model successfully separated the columns, reading Left Column completely before advancing to Right Column.
- **Table Structure Extraction (`F03`):** Docling reconstructed a structured GitHub-Flavored Markdown table with headers, pipes, and data rows. Baseline extraction collapsed the table into unstructured whitespace-separated text without markdown pipe syntax.
- **Header/Footer Stripping (`F04`):** Docling identified repeated running headers (`Company Confidential Report`) and footers (`Page X of Y`) as page furniture and excluded them from the markdown stream. Baseline text extraction contaminated the document body on every page.
- **Dehyphenation & Paragraph Continuity (`F05`):** Words broken across line breaks (`encoun-ter`, `con-straints`, `docu-ment`) were cleanly dehyphenated into coherent tokens, while genuine hyphens (`state-of-the-art`) were preserved.
- **Nested Lists Limitation (`F12`):** While Docling captured list markers (`1.`, `- a.`, `- i.`), it flattened all items to column zero, losing sub-item indentation levels. This is a documented limitation: hierarchical list indentation restoration must be handled by the Zlet Quality Layer.

### OCR Findings (`F06`)
- Tested on an image-only PDF with deterministic, reproducible synthetic scanner degradation (NumPy RNG seed 42): off-white paper stock (RGB 247, 245, 240), Gaussian scanner noise ($\sigma=3.5$), optical lens blur (radius 0.5), and rotational scanner skew ($-0.75^{\circ}$).
- Built-in `RapidOCR` triggered automatically on CPU. Automated mechanical verification (`evaluation/validate_ocr.py`) comparing canonical ground truth against extracted Markdown confirmed:
  - **Ground Truth Reference:** 475 normalized semantic characters (476 raw characters, 66 words).
  - **Extracted Docling Output:** 475 normalized semantic characters (482 raw Markdown characters including `## ` header markup and newline delimiters, 66 words).
  - **Exact Normalized Match:** `True` (0 character edit distance errors, 0 word errors).
  - **Character Error Rate (CER):** **0.00%**.
  - **Character Recall / Accuracy:** **100.00%** (475/475 semantic characters).
  - **Baseline Extractor (`pypdf`):** Extracted 0 characters (no OCR capability).
  - Persisted machine-readable evidence: `evaluation/outputs/ocr_validation.json`.

### Cyrillic & Multilingual Unicode Findings (`F07`, `F15`)
- Cyrillic Russian text was extracted without encoding degradation or mojibake (`F07`: 553 chars).
- In the mixed trilingual fixture (`F15`), Latin diacritics (`café`, `über`), Russian Cyrillic (`Привет мир`), and Chinese CJK (`测试中文文档解析能力: 你好世界`) were preserved simultaneously in valid UTF-8 Markdown, backed by mandatory CJK TrueType font validation in the fixture generator.

### DOCX, PPTX, XLSX, HTML Applicability (`F08`–`F11`)
- **DOCX (`F08`):** Converted in **142–328 ms** (0.14s in gate suite, 0.33s in full suite). Converted native Word `<w:hyperlink>` XML elements into proper Markdown links (`[Zlet Converter GitHub Repository](https://github.com/zlet-labs/zlet-converter)`) alongside headings, lists, and tables.
- **PPTX (`F09`):** Converted in **39–58 ms**. Generated clean markdown slides with `#` slide headers, bullet points, and tables.
- **XLSX (`F10`):** Converted in **14 ms**. Each worksheet was formatted into a distinct markdown table.
- **HTML (`F11`):** Converted in **39 ms**. Converted standard HTML markup directly into markdown with hyperlinked anchors.

### Determinism Verification
- Executed consecutive runs across all 14 content-bearing fixtures (`F01`–`F13`, `F15`).
- Cryptographic SHA-256 digest comparison confirmed byte-for-byte equality across all tested fixtures (`run1_sha256 == run2_sha256`), with full hash logs recorded in `evaluation/outputs/determinism_all.json`.

### Failure & Error Handling (`F13`, `F14`)
- **Empty Document (`F13`):** Docling processed the single blank page in 1.97–2.68s and emitted 0 characters cleanly without exception or null pointer errors.
- **Corrupted Document (`F14`):** When presented with invalid binary header data, Docling threw a structured `ConversionError` from `docling-parse` within 385 ms. It did not crash the host process or hang indefinitely.

---

## Comparison Summary

The values below reflect the full 15-fixture evaluation run recorded in `evaluation/outputs/summary_all_both.json`:

| Fixture | Description | Docling Time | Docling RSS (Peak) | Docling Quality | Baseline Time | Baseline Quality |
|---------|-------------|--------------|--------------------|-----------------|---------------|------------------|
| `F01` | Simple text PDF | 10.60s | 1072.5 MB | High (clean headings & flow; first in-suite conversion loads model weights) | 0.1921s | Medium (raw text) |
| `F02` | Multi-column PDF (interleaved stream) | 5.52s | 1176.2 MB | High (2D reading order resolved) | 0.0061s | Poor (interleaved sentences) |
| `F03` | Table PDF | 9.73s | 1508.9 MB | High (GFM markdown table) | 0.0043s | Poor (unstructured text) |
| `F04` | Header/Footer PDF | 4.50s | 1251.4 MB | High (furniture stripped) | 0.0046s | Poor (text polluted) |
| `F05` | Broken Wrap PDF | 2.42s | 1252.0 MB | High (dehyphenated) | 0.0039s | Medium (broken lines) |
| `F06` | Scanned Image PDF (degraded) | 9.94s | 1620.3 MB | High (475/475 norm chars, 0 errors, CER 0.00%) | 0.0058s | Failed (0 chars extracted) |
| `F07` | Cyrillic PDF | 3.22s | 1315.0 MB | High (clean UTF-8) | 0.0110s | High (text extracted) |
| `F08` | Structured DOCX (hyperlink element) | 0.33s | 1255.1 MB | High (headings, lists, table, link) | 0.0298s | Low (flat text lines) |
| `F09` | Slide Deck PPTX | 0.06s | 1261.5 MB | High (slides, tables, bullets) | 0.0076s | Low (flat text) |
| `F10` | Multi-Sheet XLSX | 0.01s | 1261.9 MB | High (multi-table markdown) | 0.0053s | Medium (pipe delimited) |
| `F11` | Semantic HTML | 0.04s | 1261.9 MB | High (GFM formatting & links) | 0.0030s | Low (stripped tags) |
| `F12` | Nested Lists PDF | 3.28s | 1317.5 MB | Partial / Limitation (flattened list) | 0.0058s | Medium (flat text) |
| `F13` | Empty PDF | 2.68s | 1322.3 MB | High (clean 0-byte output) | 0.0016s | High (0-byte output) |
| `F14` | Corrupted PDF | 0.38s | 1283.2 MB | High (graceful ConversionError) | 0.0005s | High (graceful PdfStreamError) |
| `F15` | Mixed Unicode PDF | 2.40s | 1329.1 MB | High (Latin + Cyrillic + CJK) | 0.0065s | High (Unicode preserved) |

> *Note on Memory Measurement:* `Docling RSS (Peak)` represents true process-tree peak RSS (parent process plus all child processes) continuously monitored at 10ms intervals during conversion, avoiding point-in-time GC sampling artifacts.


---

## Local & Offline Verification

Offline execution was validated through `evaluation/verify_offline.py`:
1. Environment variables set: `HF_HUB_OFFLINE=1`, `TRANSFORMERS_OFFLINE=1`, `HTTP_PROXY=http://0.0.0.0:1`, `HTTPS_PROXY=http://0.0.0.0:1`, `NO_PROXY=""`.
2. Python socket and DNS APIs were intercepted (`socket.connect`, `socket.connect_ex`, `socket.sendto`, `socket.getaddrinfo`, `socket.gethostbyname`, `socket.create_connection`) to log and immediately block any outgoing network connection attempt.
3. Automated test script verified that all conversions succeeded offline (`F01`: 9.08s, `F06` degraded OCR: 9.05s, `F08`: 0.19s) and strictly exits non-zero if any conversion failure or network call occurs.
4. **Observed Evidence:** All 3 test fixtures converted successfully offline. Exactly **0 network connection attempts or name lookups** were intercepted across the runtime session.
5. **Scope & Privacy Note:** While this confirms that Docling and its evaluated Python/ONNX models do not initiate network calls or telemetry at the Python socket layer during offline execution, it is scoped to Python-level socket observation rather than OS-level kernel packet inspection. For air-gapped production environments, standard OS firewall rules or containerization should be used.

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

Every numeric value below is sourced directly from committed evidence files (`evaluation/outputs/performance_footprint.json`, `evaluation/outputs/summary_all_both.json`, and `evaluation/outputs/summary_gate_both.json`).

- **Storage Footprint (`performance_footprint.json`):**
  - Python Virtual Environment: **1171.34 MB** (`evaluation/venv`).
  - Model Weights Cache: **566.47 MB** total (Hugging Face hub cache: 505.45 MB; RapidOCR models: 61.02 MB).
- **Process Startup & Init Timing (`performance_footprint.json`):**
  - `import docling`: **0.0402s**.
  - `from docling.document_converter import DocumentConverter`: **6.8135s** (loads PyTorch, Transformers, ONNX dependencies).
  - `DocumentConverter()` instantiation: **0.0953s**.
  - Total cold process initialization: **6.9088s**.
- **Execution Lifecycle & F01 Conversion Benchmarks (`performance_footprint.json` & `summary_all_both.json`):**
  - **Cold Child Process (Fresh OS Subprocess):** **16.22s** total duration (`cold_child_process_duration_seconds: 16.2215`), with **1107.57 MB** process-tree peak RSS. Includes clean Python process startup, dependency imports, converter initialization, and first conversion.
  - **In-Process First Conversion (Model Weights Load):** **12.41s** duration (`in_process_first_conversion_duration_seconds: 12.4060`), with **1099.75 MB** process peak RSS. Within an already-initialized process, the first conversion incurs one-time deep-learning weight loading into memory. In the 15-fixture evaluation suite run (`summary_all_both.json`), the initial conversion of `F01` took **10.60s** (peak RSS **1072.55 MB**).
  - **In-Process Warm Conversion (Weights Resident):** **2.95s** duration (`in_process_warm_conversion_duration_seconds: 2.9465`), with **1084.87 MB** process peak RSS. Represents pure inference latency when converter instance and neural weights are already resident in RAM.
- **Empirical Throughput & Latency by Document Type:**
  - **Digital Single-Page PDFs (Warm Converter State):**
    - Observed in full 15-fixture suite (`summary_all_both.json`): `F05` broken wrap: **2.42s** (0.41 pages/s); `F15` mixed unicode: **2.40s** (0.42 pages/s); `F07` cyrillic: **3.22s** (0.31 pages/s); `F12` nested lists: **3.28s** (0.30 pages/s); `F04` header/footer: **4.50s** (0.22 pages/s); `F02` multicolumn: **5.52s** (0.18 pages/s).
    - Observed in 8-fixture gate suite (`summary_gate_both.json`): `F07` cyrillic: **2.07s** (0.48 pages/s); `F02` multicolumn: **3.06s** (0.33 pages/s).
    - Dedicated warm single-page benchmark (`performance_footprint.json`): `F01` warm: **2.95s** (0.34 pages/s).
    - **Throughput Range (Warm Digital PDFs):** **2.07s – 5.52s per page** (**0.18 – 0.48 pages/second**). Peak RSS ranges from **1084.87 MB to 1329.07 MB**.
  - **Dense Table Structure Extraction (`F03`):**
    - Deep-learning TableFormer inference on 4x4 tabular data: **9.73s** (`summary_all_both.json`) to **11.70s** (`summary_gate_both.json`) per page (**~0.08 – 0.10 pages/second**). Process-tree peak RSS reaches **1432.01 MB – 1508.92 MB**.
  - **Degraded Scanned PDF with OCR (`F06`):**
    - RapidOCR detection, angle classification, and character recognition on synthetic degraded raster: **9.29s** (`summary_gate_both.json`) to **9.94s** (`summary_all_both.json`) per page (**~0.10 – 0.11 pages/second**). Process-tree peak RSS reaches **1514.07 MB – 1620.31 MB** (the highest peak RSS in the evaluation suite).
  - **Non-PDF Formats (`F08`–`F11`):**
    - Converted via lightweight native parsers without heavy neural models:
      - Structured DOCX (`F08`): **0.142s – 0.328s** (**~3.0 – 7.0 documents/second**).
      - Slide Deck PPTX (`F09`): **0.039s – 0.058s** (**~17.2 – 25.6 presentations/second**).
      - Multi-Sheet XLSX (`F10`): **0.014s** (**~71.9 workbooks/second**).
      - Semantic HTML (`F11`): **0.039s** (**~25.6 documents/second**).
- **Process Memory Sizing Recommendation:**
  - Memory footprint remains around ~1.08–1.33 GB for digital PDFs, but peaks at **1508.9 MB** during TableFormer table parsing (`F03`) and **1620.3 MB** during RapidOCR raster inference (`F06`).
  - Therefore, the background worker watchdog should configure an auto-restart ceiling of **1.75 GB – 2.0 GB** process-tree peak RSS to guarantee stability during prolonged batch operations.


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

### 5. Zlet Markdown Quality Layer Responsibilities (Aligned with ZC-045)
To ensure production-grade consistency, privacy, and determinism across converter backends, the downstream Quality Layer in Zlet (ZC-045) handles post-processing without introducing nondeterministic or data-leaking metadata:
- **Hierarchical List Indentation Restoration:** Docling emits list items at column zero (`F12`); the Quality Layer re-indents nested sub-items according to document hierarchy.
- **Deterministic Formatting Normalization:** Enforce consistent table pipe padding, column delimiter alignment (`|:---|:---|`), line break normalization (LF), and whitespace trimming.
- **Privacy-Preserving Asset Rebasing:** Extract and remap embedded images to clean relative subfolder paths (e.g. `./assets/img_01.png`) without exposing local host directory structures or absolute source paths.
- **Strict Determinism Enforcement:** In full alignment with ZC-045, the Quality Layer **does not inject timestamps, local absolute file paths, or machine usernames** into Markdown or frontmatter, ensuring byte-identical reproducibility across independent runs.
- **Heading Level Calibration:** Optional uniform heading offset adjustment (e.g., normalising top-level titles to `#` or `##`) based on user-configured output conventions.

---

## Risks & Mitigations

| Risk | Severity | Mitigation |
|------|----------|------------|
| Initial model download size (~566 MB) | High | Implement on-demand model download UI in Settings or provide an offline bundle installer. |
| High RAM consumption during PDF DL parsing (~1.6 GB peak on OCR) | Medium | Process documents sequentially in worker; restart worker if memory exceeds 1.75–2.0 GB. |
| Startup latency (~6.9s cold initialization) | Medium | Use a persistent long-running background worker during batch processing rather than spawning per file. |
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
