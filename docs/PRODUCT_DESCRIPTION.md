# Zlet Converter — Product Description

[English](#english) · [Русский](#русский)

## English

### Short description

**Zlet Converter is a local Windows utility for batch-converting legacy Microsoft Office files, exporting Excel worksheets, safely copying compatible documents and media, and preparing local file collections without cloud uploads.**

### Repository / catalog description

Zlet Converter helps process many files in folders and subfolders while keeping the work local on the user's PC. v0.0.3 converts supported legacy Office formats such as DOC, XLS and PPT using installed Microsoft Office applications, exports XLS/XLSX worksheets to UTF-8 CSV or TSV, safely copies supported modern files unchanged, preserves relative folder structure, and avoids silently overwriting existing results.

The app is designed for simple self-serve use: choose a folder, review the planned actions, filter or sort Preview, select what to process, choose folder or ZIP output, run the batch, and inspect the result summary and persistent TXT report. No account, backend or cloud upload is required.

### Gemini Notebook use case

One practical use case is preparing local document collections for **Gemini Notebook** and similar document workflows. Zlet Converter can convert DOC → DOCX and PPT → PPTX, convert JSON → TXT/Markdown, export Excel worksheets to CSV/TSV, and safely copy supported PDFs, Office Open XML files, EPUBs and images without changing their contents.

Zlet Converter does not connect to Gemini Notebook and does not upload files to it. Conversion remains local; the user decides whether and when to upload the results. Destination-service format support can change, so CSV/TSV or other outputs do not imply direct integration or guaranteed acceptance.

Gemini Notebook source support: https://support.google.com/gemininotebook/answer/16215270?co=GENIE.Platform%3DDesktop&hl=en

### Key points in v0.0.3

- Windows x64 desktop utility.
- Local processing only; files are not uploaded.
- Russian and English UI in the same package.
- DOC → DOCX through installed Microsoft Word.
- XLS → XLSX through installed Microsoft Excel.
- XLS/XLSX → one UTF-8 CSV or TSV per worksheet through installed Microsoft Excel.
- PPT → PPTX through installed Microsoft PowerPoint.
- Safe unchanged copy for DOCX/XLSX/PPTX, PDF, CSV, TSV, EPUB and supported images.
- JSON → TXT or Markdown conversion.
- Folder and subfolder scanning with relative structure preservation.
- Preview filtering by format, visible clear-filter action, sortable columns and 1-based visible row numbering.
- Selection remains independent from filtering/sorting and controls the real execution set.
- Folder or ZIP output.
- Persistent `ZletConverter-report.txt` with relative paths, counters, worksheet accounting, statuses and safe diagnostics.
- Conflict protection: existing result files/directories are not silently overwritten.
- Per-file status, progress, source size and elapsed time.
- Safe batch cancellation that does not terminate unrelated user Office processes.
- PRE-ALPHA product maturity; complex, corrupted, password-protected or unsupported documents may fail.

### Current release classification

`v0.0.3` is published as a normal GitHub Release (`prerelease=false`). **PRE-ALPHA** describes product maturity only.

### One-line GitHub About text

`Local Windows batch file converter with Office conversion, Excel sheet export and safe local file processing. No cloud uploads.`

## Русский

### Короткое описание

**Zlet Converter — локальная Windows-утилита для пакетного преобразования старых форматов Microsoft Office, экспорта листов Excel, безопасного копирования совместимых документов и медиа и подготовки локальных коллекций файлов без загрузки в облако.**

### Описание продукта

Zlet Converter помогает обрабатывать сразу много файлов в папках и подпапках, оставляя всю работу на компьютере пользователя. В v0.0.3 утилита преобразует DOC, XLS и PPT через установленные приложения Microsoft Office, экспортирует листы XLS/XLSX в UTF-8 CSV или TSV, безопасно копирует поддерживаемые современные файлы без изменений, сохраняет относительную структуру каталогов и не перезаписывает существующие результаты молча.

Основной сценарий простой: выбрать папку, посмотреть план операций, отфильтровать или отсортировать Preview, отметить нужные файлы, выбрать вывод в папку или ZIP, запустить пакетную обработку и проверить итоговую сводку и постоянный TXT-отчёт. Аккаунт, backend и загрузка документов в облако не требуются.

### Сценарий Gemini Notebook

Один из практичных сценариев — подготовка локальных коллекций документов для **Gemini Notebook** и похожих document workflows. Zlet Converter может подготовить DOC → DOCX и PPT → PPTX, преобразовать JSON → TXT/Markdown, экспортировать листы Excel в CSV/TSV и безопасно скопировать поддерживаемые PDF, Office Open XML, EPUB и изображения без изменения содержимого.

Zlet Converter не подключается к Gemini Notebook и не загружает туда файлы. Преобразование остаётся локальным; пользователь сам решает, загружать ли результат и когда. Требования целевого сервиса могут меняться, поэтому наличие CSV/TSV или других результатов не означает прямую интеграцию или гарантированный приём.

Поддерживаемые источники Gemini Notebook: https://support.google.com/gemininotebook/answer/16215270?co=GENIE.Platform%3DDesktop&hl=ru

### Основные возможности v0.0.3

- Windows x64 desktop-утилита.
- Полностью локальная обработка; файлы никуда не загружаются.
- Русский и английский интерфейс в одном пакете.
- DOC → DOCX через установленный Microsoft Word.
- XLS → XLSX через установленный Microsoft Excel.
- XLS/XLSX → отдельный UTF-8 CSV или TSV для каждого листа через установленный Microsoft Excel.
- PPT → PPTX через установленный Microsoft PowerPoint.
- Безопасное копирование DOCX/XLSX/PPTX, PDF, CSV, TSV, EPUB и поддерживаемых изображений без изменений.
- JSON → TXT или Markdown.
- Сканирование папок и подпапок с сохранением относительной структуры.
- Фильтрация Preview по форматам, видимое действие очистки фильтра, сортировка колонок и нумерация видимых строк с 1.
- Фильтр/сортировка не меняют checkbox selection и реальный execution set.
- Вывод в папку или ZIP.
- Постоянный `ZletConverter-report.txt` с относительными путями, счётчиками, статистикой листов, статусами и безопасной диагностикой.
- Защита от конфликтов: существующие файлы/каталоги результата не перезаписываются молча.
- Статус, прогресс, размер исходника и время выполнения по каждому файлу.
- Безопасная остановка batch без завершения посторонних пользовательских процессов Office.
- Зрелость PRE-ALPHA: сложные, повреждённые, защищённые паролем или неподдерживаемые документы могут не преобразоваться.

### Классификация текущего релиза

`v0.0.3` опубликован как обычный GitHub Release (`prerelease=false`). **PRE-ALPHA** обозначает только зрелость продукта.

### Короткое описание для GitHub About

`Локальный Windows batch-конвертер с Office-конвертацией, экспортом листов Excel и безопасной локальной обработкой файлов. Без облака.`
