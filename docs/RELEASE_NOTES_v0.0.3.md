# Zlet Converter v0.0.3

[English](#english) · [Русский](#русский)

> **PRE-ALPHA · Windows x64 · Local processing only**

## English

Zlet Converter v0.0.3 is a PRE-ALPHA release focused on a clearer batch workflow, safer local conversion, better Preview navigation and bilingual desktop usability.

### Highlights

- **Russian and English UI.** First-launch language choice, live language switching in Settings, and persisted language preference.
- **Settings and About.** General settings, Updates, Diagnostics and About are available in one place.
- **Manual update check.** Update checks run only when the user explicitly starts them; there is no startup/background update polling.
- **Excel worksheet export.** `.xls` and `.xlsx` workbooks can be exported to one UTF-8 CSV or TSV per worksheet through Microsoft Excel.
- **Worksheet-aware preview and results.** Hidden/very-hidden and empty worksheet states are handled explicitly, with deterministic Windows-safe output naming.
- **Expanded safe copy.** Supported PDF, CSV, TSV, EPUB and image files can be copied unchanged without Office conversion.
- **Preview format filtering.** Clicking a format row in Rules filters Preview to that group without changing checkbox selection or the conversion execution set.
- **Obvious filter reset.** A visible `Show all` action restores the complete Preview list when a format filter is active.
- **Preview sorting.** Source file, action, status, result, size and time columns support deterministic ascending/descending sorting; size and time use numeric values rather than display text.
- **Preview row numbering and alignment.** Visible rows are numbered from 1 according to the current filter + sort order, and row content is vertically centered for easier scanning.
- **Format and Office indicators.** Rules use compact local format icons and the Office availability strip uses simple Word/Excel/PowerPoint badges without remote assets.
- **Persistent batch report.** `ZletConverter-report.txt` records relative paths, counters, worksheet accounting, localized statuses and safe diagnostics without document contents.
- **Existing legacy Office conversion remains.** DOC → DOCX, XLS → XLSX and PPT → PPTX continue to use installed Microsoft Office applications.
- **Safe Stop and process ownership.** Cancellation preserves completed outputs and never terminates unrelated Office processes by name alone; forced cleanup requires app-owned process identity.
- **Folder and ZIP output.** Existing destination conflict protection, source integrity checks and no-silent-overwrite behavior remain in place.

### Supported operations

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

Microsoft Office is not included in Zlet Converter.

### Important notes

- This release is **PRE-ALPHA**.
- Files are processed locally; document contents are not uploaded to a cloud service.
- Existing destination files/directories are not silently overwritten.
- The installer is currently unsigned, so Windows may show an Unknown publisher or SmartScreen warning.
- Legacy DOC/XLS/PPT conversion requires the corresponding installed Microsoft Office application in v0.0.3.
- LibreOffice fallback and LibreOffice download/install support are **not included** in v0.0.3.
- System/Light/Dark theme work is **not included** in v0.0.3.
- Complex, corrupted, password-protected or unsupported legacy files may fail to convert.
- Conversion fidelity depends on the installed Microsoft Office version and document features.

### Verification status

The publication workflow runs restore, Release build, the normal automated test suite, installer/portable packaging and SHA-256 validation before publishing the pre-release.

The latest Preview filtering/sorting/numbering/alignment changes received a manual UI sanity pass before release. Full clean-machine verification and real Microsoft Office integration tests remain separate opt-in/manual verification and are not claimed as passed unless they were actually run.

Full manual checklist: [`docs/manual-clean-machine-verification.md`](manual-clean-machine-verification.md)

---

## Русский

Zlet Converter v0.0.3 — PRE-ALPHA релиз с более понятным пакетным процессом, безопасной локальной конвертацией, удобной навигацией по Preview и полноценным RU/EN интерфейсом.

### Главное в v0.0.3

- **Интерфейс на русском и английском.** Выбор языка при первом запуске, переключение языка в Settings без перезапуска и сохранение выбранного языка.
- **Settings и About.** Общие настройки, Updates, Diagnostics и About собраны в одном месте.
- **Ручная проверка обновлений.** Сетевая проверка запускается только явным действием пользователя; фоновой/стартовой проверки обновлений нет.
- **Экспорт листов Excel.** `.xls` и `.xlsx` можно экспортировать в отдельный UTF-8 CSV или TSV для каждого листа через Microsoft Excel.
- **Worksheet-aware Preview и результаты.** Скрытые/very-hidden и пустые листы учитываются явно; имена результатов формируются детерминированно и безопасно для Windows.
- **Расширенная безопасная копия.** Поддерживаемые PDF, CSV, TSV, EPUB и изображения можно копировать без Office-конвертации.
- **Фильтрация Preview по форматам.** Нажатие строки формата в Rules фильтрует Preview по этой группе, не меняя чекбоксы и фактический набор файлов для обработки.
- **Очевидный сброс фильтра.** При активном фильтре видимое действие `Показать все` возвращает полный список Preview.
- **Сортировка Preview.** Колонки исходного файла, действия, статуса, результата, размера и времени сортируются по возрастанию/убыванию; размер и время сортируются по числовым значениям, а не по отображаемому тексту.
- **Нумерация и выравнивание строк Preview.** Видимые строки нумеруются с 1 по текущему порядку после filter + sort, содержимое строк выровнено по вертикальному центру.
- **Индикаторы форматов и Office.** В Rules используются компактные локальные иконки форматов, а доступность Word/Excel/PowerPoint обозначается простыми W/X/P badges без удалённых ресурсов.
- **Постоянный отчёт по пакету.** `ZletConverter-report.txt` сохраняет относительные пути, счётчики, информацию по листам, локализованные статусы и безопасную диагностику без содержимого документов.
- **Legacy Office-конвертация сохранена.** DOC → DOCX, XLS → XLSX и PPT → PPTX продолжают использовать установленные приложения Microsoft Office.
- **Безопасный Stop и владение процессами.** Остановка сохраняет уже готовые результаты и не завершает посторонние Office-процессы по имени; принудительная очистка требует подтверждённого app-owned process identity.
- **Вывод в папку и ZIP.** Сохранены защита от конфликтов, проверка неизменности исходников и запрет на тихое перезаписывание результатов.

### Поддерживаемые операции

| Исходник | Результат | Требование |
|---|---|---|
| `.doc` | `.docx` | установлен Microsoft Word |
| `.xls` | `.xlsx` | установлен Microsoft Excel |
| `.xls`, `.xlsx` | отдельный UTF-8 `.csv` на каждый лист | установлен Microsoft Excel |
| `.xls`, `.xlsx` | отдельный UTF-8 `.tsv` на каждый лист | установлен Microsoft Excel |
| `.ppt` | `.pptx` | установлен Microsoft PowerPoint |
| `.docx`, `.xlsx`, `.pptx` | безопасная копия без изменений | Office не нужен |
| `.pdf`, `.csv`, `.tsv`, `.epub` | безопасная копия без изменений | Office не нужен |
| `.avif`, `.bmp`, `.gif`, `.heic`, `.heif`, `.ico`, `.jp2`, `.jpe`, `.jpeg`, `.jpg`, `.png`, `.tif`, `.tiff`, `.webp` | безопасная копия без изменений | Office не нужен |
| `.json` | `.txt` или `.md` | Office не нужен |

Microsoft Office в состав Zlet Converter не входит.

### Важно

- Версия имеет статус **PRE-ALPHA**.
- Файлы обрабатываются локально, содержимое документов не отправляется в облачный сервис.
- Существующие файлы/каталоги результата не перезаписываются молча.
- Установщик пока не подписан, поэтому Windows может показать Unknown publisher или предупреждение SmartScreen.
- В v0.0.3 для legacy DOC/XLS/PPT требуется соответствующее установленное приложение Microsoft Office.
- LibreOffice fallback и установка/загрузка LibreOffice **не входят** в v0.0.3.
- Поддержка System/Light/Dark тем **не входит** в v0.0.3.
- Сложные, повреждённые, защищённые паролем или неподдерживаемые legacy-файлы могут не преобразоваться.
- Точность результата зависит от установленной версии Microsoft Office и особенностей документа.

### Статус проверки

Workflow публикации выполняет restore, Release build, обычный набор автоматических тестов, сборку installer/portable и проверку SHA-256 до публикации pre-release.

Последние изменения Preview: фильтрация, сортировка, нумерация и выравнивание строк прошли ручной UI sanity перед релизом. Полная проверка на чистой машине и реальные Microsoft Office integration tests остаются отдельной opt-in/manual проверкой и не считаются пройденными без фактического запуска.

Полный ручной чек-лист: [`docs/manual-clean-machine-verification.md`](manual-clean-machine-verification.md)
