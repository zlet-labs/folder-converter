# Zlet Converter

<p align="center">
  <img src="docs/assets/zlet-batch-converter-hero.svg" alt="Zlet Converter — local Windows file converter from Zlet Labs" width="100%">
</p>

<p align="center">
  <strong>English</strong> · <a href="README_RU.md">Русский</a>
</p>

<p align="center">
  <img alt="Version v0.0.3" src="https://img.shields.io/badge/version-v0.0.3-2563eb">
  <img alt="PRE-ALPHA" src="https://img.shields.io/badge/status-PRE--ALPHA-f59e0b">
  <img alt="Windows x64" src="https://img.shields.io/badge/platform-Windows%20x64-0078D4">
  <img alt="Local processing" src="https://img.shields.io/badge/processing-local%20only-16a34a">
  <img alt="MIT License" src="https://img.shields.io/badge/license-MIT-22c55e">
</p>

Zlet Converter is a small local Windows utility for batch-processing files in folders and subfolders. It converts supported legacy Microsoft Office files, exports Excel worksheets, safely copies already-compatible files, preserves relative folder structure, and keeps processing on your computer.

The application UI supports Russian and English in the same package. On first launch, choose a language explicitly; change it later under **Settings → Language** without restarting or losing the current preview/results. Only the language setting is stored in `%LOCALAPPDATA%\Zlet Labs\Zlet Converter\settings.json`; no account, cloud service, or backend is required. Packagers can set the initial choice with `ZletConverter.exe --language=ru-RU` or `--language=en-US`.

> **v0.0.3 is PRE-ALPHA software, but it is published as a normal GitHub Release, not as a GitHub Pre-release.** PRE-ALPHA describes product maturity. The installer is currently unsigned, so Windows may show an Unknown publisher or SmartScreen warning. Microsoft Office is not included.

> **Rename note:** v0.0.2 was published under the previous public name `Zlet Batch Converter`. Its historical release title and asset names remain unchanged. v0.0.3 uses the current `Zlet Converter` / `ZletConverter` naming.

## Download v0.0.3

| Windows installer | Portable ZIP |
|---|---|
| **[⬇ Download installer](https://github.com/zlet-labs/zlet-converter/releases/download/v0.0.3/ZletConverter-v0.0.3-Setup-win-x64.exe)** | **[📦 Download portable](https://github.com/zlet-labs/zlet-converter/releases/download/v0.0.3/ZletConverter-v0.0.3-win-x64.zip)** |
| `ZletConverter-v0.0.3-Setup-win-x64.exe` | `ZletConverter-v0.0.3-win-x64.zip` |

[Release notes](https://github.com/zlet-labs/zlet-converter/releases/tag/v0.0.3) · [SHA-256 checksums](https://github.com/zlet-labs/zlet-converter/releases/download/v0.0.3/SHA256SUMS.txt)

### Why use it?

| 🔒 Local | ⚡ Batch | 🛡 Defensive |
|---|---|---|
| No accounts, no cloud uploads | Scan folders and subfolders in one run | Existing outputs are not silently overwritten |
| Document contents stay on your PC | Select only the operations you want | Office processes are never killed by name alone |

## Gemini Notebook and modern document workflows

Preparing document collections for **Gemini Notebook** is one practical use case for Zlet Converter. v0.0.3 expands local preparation beyond legacy Office conversion: Excel workbooks can be exported to one UTF-8 CSV or TSV per worksheet, and already-compatible PDFs, CSV/TSV files, EPUBs and supported image files can be safely copied unchanged.

Zlet Converter remains a general-purpose local conversion and file preparation tool. Check the destination service's current format requirements before using the results; CSV/TSV exports do not imply integration or guaranteed acceptance by Gemini Notebook.

Gemini Notebook source support: [Google Help](https://support.google.com/gemininotebook/answer/16215270?co=GENIE.Platform%3DDesktop&hl=en)

**There is no Gemini Notebook integration or automatic upload.** Zlet Converter processes files locally; you decide if and when to upload the resulting files to Gemini Notebook or another service.

## Supported formats in v0.0.3

| Source | Result | Requirement |
|---|---|---|
| `.doc` | `.docx` | Microsoft Word installed |
| `.xls` | `.xlsx` | Microsoft Excel installed |
| `.xls`, `.xlsx` | one UTF-8 `.csv` per worksheet | Microsoft Excel installed |
| `.xls`, `.xlsx` | one UTF-8 `.tsv` per worksheet | Microsoft Excel installed |
| `.ppt` | `.pptx` | Microsoft PowerPoint installed |
| `.docx`, `.xlsx`, `.pptx` | unchanged safe copy | Office not required |
| `.pdf`, `.csv`, `.tsv`, `.epub` | unchanged safe copy | Office not required |
| `.avif`, `.bmp`, `.gif`, `.heic`, `.heif`, `.ico`, `.jp2`, `.jpe`, `.jpeg`, `.jpg`, `.png`, `.tif`, `.tiff`, `.webp` | unchanged safe copy | Office not required |
| `.json` | `.txt` or `.md` | Office not required |

For Excel sheet exports, each worksheet is a separate Preview operation. Hidden and very-hidden worksheets remain visible but are not selected by default; empty worksheets are skipped explicitly. Output names are deterministic and Windows-safe, such as `sales__Summary.csv`.

v0.0.3 also generates a human-readable `ZletConverter-report.txt` with relative paths, batch counters, worksheet accounting, statuses and safe diagnostics. Existing report names are not silently overwritten; deterministic `-2`, `-3`, ... suffixes are used.

The final panel retains aggregate and per-workbook sheet summaries, including hidden and empty sheets skipped. A workbook counts as one source file even when it produces several worksheet files. Reports are saved in the result folder or at the ZIP root, including after Stop or partial failure. Report failures remain visible. New controls and report labels follow the existing RU/EN language setting; switching language preserves the preview and results.

Preview can be filtered by the Rules format rows without changing checkbox selection or the conversion execution set. A visible **Show all** action clears the active format filter. Source file, action, status, result, size and time columns can be sorted ascending/descending, and visible rows are numbered from 1 according to the current filter + sort order.

Real Excel sheet tests are opt-in: set `ZLET_OFFICE_INTEGRATION=1`, `ZLET_OFFICE_XLSX_SHEETS_FIXTURE` and/or `ZLET_OFFICE_XLS_SHEETS_FIXTURE` to local multi-sheet workbooks with at least two nonempty two-column worksheets, then run the `OfficeIntegration` test category. Include hidden/very-hidden sheets, Unicode values and cross-sheet formulas in manual QA. Automated tests do not substitute for full clean-machine and real Microsoft Office verification.

Word, Excel, and PowerPoint are detected independently. If one Office application is missing, only the corresponding conversion becomes unavailable; safe-copy operations continue without Office.

> **PowerPoint safety:** PPT conversion is refused while user PowerPoint is already running. This avoids interfering with an open presentation. DOC/XLS conversion and unchanged PPTX copying remain available.

## Quick start

1. Download the installer or portable ZIP above.
2. Run `ZletConverter.exe`.
3. Choose a source folder and scan it.
4. Review Preview and select the operations you want.
5. Choose the output location/mode and start processing.
6. Review per-file results and the final batch summary.

Packaged builds are self-contained for .NET 8, so the .NET runtime does not need to be installed separately.

## What you get

- Preview before processing with format filtering, sorting and visible row numbering.
- Selection of individual operations before execution.
- Relative subfolder structure preserved in output.
- Per-file status, stage progress, source size, and execution time.
- Safe Stop that prevents new queued operations from starting.
- Conversion list copy using relative paths only.
- Separate final counters for converted, copied, failed, conflict, unavailable, skipped, and unselected items.
- Per-worksheet Excel export planning and batch reporting.
- Persistent human-readable `ZletConverter-report.txt` in folder or ZIP output.
- Safer multi-file Office processing through reusable worker/session handling where appropriate.

## Limitations

Zlet Converter is still **PRE-ALPHA**. Complex, corrupted, password-protected, or unsupported legacy documents may fail to convert. Conversion fidelity depends on the installed Microsoft Office version and document features.

Original files are not intentionally modified, but keep backups of important data when testing PRE-ALPHA software.

<details>
<summary><strong>Safety and privacy details</strong></summary>

The converter is intentionally local-first and defensive around user files.

- Files are processed locally and are not uploaded.
- The UI process does not perform Office COM automation directly.
- Office conversion runs through an isolated STA worker process.
- Legacy files and Excel workbooks used for worksheet export are opened read-only.
- Macros, dialogs, and Recent/MRU additions are disabled for worker-controlled Office sessions.
- Source files must remain inside the selected source folder.
- Reparse-point files and folders are skipped.
- Source SHA-256 is checked before and after processing.
- Outputs are produced in temporary/staging locations and validated before the final move.
- Existing output files/directories are not overwritten.
- Office processes are never terminated by process name alone.
- Forced cleanup is allowed only for an app-owned process whose PID and start time were proven.
- Failure of one file or worksheet does not automatically stop the remaining selected operations.
- TXT reports use relative paths and must not contain document contents, passwords, secrets or tokens.

Technical diagnostics may contain error codes and process metadata. They must not contain document contents, secrets, or full local document paths.

</details>

<details>
<summary><strong>Build from source and package locally</strong></summary>

Requirements:

- Windows x64
- .NET 8 SDK

```powershell
dotnet restore FolderConverter.sln
dotnet build FolderConverter.sln -c Release
dotnet test FolderConverter.sln -c Release
```

If you cloned the repository before it was renamed to `zlet-converter`, update the existing remote instead of recloning:

```powershell
git remote set-url origin https://github.com/zlet-labs/zlet-converter.git
```

Build the portable package:

```powershell
.\scripts\publish-portable.ps1
```

Expected local ZIP at the current source version:

```text
artifacts/portable/win-x64/ZletConverter-v0.0.3-win-x64.zip
```

Build the Windows installer with Inno Setup 6:

```powershell
.\scripts\build-installer.ps1
```

The installer script first builds the portable payload, then creates the Windows x64 installer and prints its SHA-256 and Authenticode status.

</details>

<details>
<summary><strong>Real Microsoft Office integration tests</strong></summary>

Real Office integration tests are opt-in because they require installed Microsoft Office and real legacy fixtures:

```powershell
$env:ZLET_OFFICE_INTEGRATION = "1"
$env:ZLET_OFFICE_WORD_FIXTURE = "C:\fixtures\sample.doc"
$env:ZLET_OFFICE_WORD_BATCH_FIXTURE_DIR = "C:\fixtures\word-batch"
$env:ZLET_OFFICE_EXCEL_FIXTURE = "C:\fixtures\sample.xls"
$env:ZLET_OFFICE_POWERPOINT_FIXTURE = "C:\fixtures\sample.ppt"
dotnet test FolderConverter.sln -c Release --filter Category=OfficeIntegration
```

If an Office application or required fixture is missing, the corresponding integration test is skipped rather than counted as passed.

</details>

## Project

Zlet Converter is a **Zlet Labs** project: small, practical, self-serve tools without unnecessary SaaS machinery.

[Zlet Labs](https://zlet.app/) · [GitHub Issues](https://github.com/zlet-labs/zlet-converter/issues) · [All releases](https://github.com/zlet-labs/zlet-converter/releases) · [MIT License](LICENSE)

Manual release verification checklist: [docs/manual-clean-machine-verification.md](docs/manual-clean-machine-verification.md)
