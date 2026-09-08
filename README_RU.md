# Zlet Converter

<p align="center">
  <img src="docs/assets/zlet-batch-converter-hero.svg" alt="Zlet Converter — локальный конвертер файлов для Windows от Zlet Labs" width="100%">
</p>

<p align="center">
  <a href="README.md">English</a> · <strong>Русский</strong>
</p>

<p align="center">
  <img alt="Версия v0.0.3" src="https://img.shields.io/badge/version-v0.0.3-2563eb">
  <img alt="PRE-ALPHA" src="https://img.shields.io/badge/status-PRE--ALPHA-f59e0b">
  <img alt="Windows x64" src="https://img.shields.io/badge/platform-Windows%20x64-0078D4">
  <img alt="Локальная обработка" src="https://img.shields.io/badge/processing-local%20only-16a34a">
  <img alt="MIT License" src="https://img.shields.io/badge/license-MIT-22c55e">
</p>

Zlet Converter — небольшая локальная утилита для Windows, которая пакетно обрабатывает файлы в папках и подпапках. Она преобразует поддерживаемые старые форматы Microsoft Office, экспортирует листы Excel, безопасно копирует уже совместимые файлы, сохраняет относительную структуру папок и выполняет обработку на вашем компьютере.

Один пакет приложения поддерживает русский и английский интерфейс. При первом запуске язык нужно подтвердить явно; позже его можно сразу сменить через **Настройки → Язык**, не перезапуская приложение и не теряя текущий Preview/результаты. Сохраняется только настройка языка в `%LOCALAPPDATA%\Zlet Labs\Zlet Converter\settings.json`; аккаунт, облако и backend не требуются. Для bootstrap доступен запуск `ZletConverter.exe --language=ru-RU` или `--language=en-US`.

> **v0.0.3 имеет зрелость PRE-ALPHA, но опубликован как обычный GitHub Release, а не GitHub Pre-release.** PRE-ALPHA здесь означает зрелость продукта. Установщик пока не подписан Authenticode, поэтому Windows может показать Unknown publisher или предупреждение SmartScreen. Microsoft Office в комплект не входит.

> **О переименовании:** v0.0.2 был опубликован под прежним публичным названием `Zlet Batch Converter`. Его исторические название релиза и имена assets остаются без изменений. v0.0.3 использует актуальное название `Zlet Converter` / `ZletConverter`.

## Скачать v0.0.3

| Установщик Windows | Portable ZIP |
|---|---|
| **[⬇ Скачать установщик](https://github.com/zlet-labs/zlet-converter/releases/download/v0.0.3/ZletConverter-v0.0.3-Setup-win-x64.exe)** | **[📦 Скачать portable](https://github.com/zlet-labs/zlet-converter/releases/download/v0.0.3/ZletConverter-v0.0.3-win-x64.zip)** |
| `ZletConverter-v0.0.3-Setup-win-x64.exe` | `ZletConverter-v0.0.3-win-x64.zip` |

[Описание релиза](https://github.com/zlet-labs/zlet-converter/releases/tag/v0.0.3) · [SHA-256](https://github.com/zlet-labs/zlet-converter/releases/download/v0.0.3/SHA256SUMS.txt)

### Зачем использовать

| 🔒 Локально | ⚡ Пакетно | 🛡 Осторожно с файлами |
|---|---|---|
| Без аккаунтов и загрузки в облако | Папки и подпапки за один запуск | Существующие результаты не перезаписываются молча |
| Содержимое документов остаётся на ПК | Можно выбрать только нужные операции | Office-процессы не завершаются только по имени |

## Для Gemini Notebook и современных document/AI workflows

Подготовка коллекций документов для **Gemini Notebook** — один из практичных сценариев Zlet Converter. В v0.0.3 локальная подготовка выходит за рамки legacy Office: книги Excel можно экспортировать в отдельный UTF-8 CSV или TSV для каждого листа, а уже совместимые PDF, CSV/TSV, EPUB и поддерживаемые изображения безопасно копировать без изменений.

Zlet Converter остаётся универсальным локальным инструментом преобразования и подготовки файлов. Перед использованием результата проверьте актуальные требования целевого сервиса: экспорт CSV/TSV не означает интеграцию или гарантированный приём файлов Gemini Notebook.

Поддерживаемые источники Gemini Notebook: [справка Google](https://support.google.com/gemininotebook/answer/16215270?co=GENIE.Platform%3DDesktop&hl=ru)

**Прямой интеграции с Gemini Notebook и автоматической загрузки нет.** Zlet Converter обрабатывает файлы локально; вы сами решаете, загружать ли результат в Gemini Notebook или другой сервис и когда это делать.

## Поддерживаемые форматы в v0.0.3

| Исходник | Результат | Требование |
|---|---|---|
| `.doc` | `.docx` | установлен Microsoft Word |
| `.xls` | `.xlsx` | установлен Microsoft Excel |
| `.xls`, `.xlsx` | отдельный UTF-8 `.csv` для каждого листа | установлен Microsoft Excel |
| `.xls`, `.xlsx` | отдельный UTF-8 `.tsv` для каждого листа | установлен Microsoft Excel |
| `.ppt` | `.pptx` | установлен Microsoft PowerPoint |
| `.docx`, `.xlsx`, `.pptx` | безопасная копия без изменений | Office не нужен |
| `.pdf`, `.csv`, `.tsv`, `.epub` | безопасная копия без изменений | Office не нужен |
| `.avif`, `.bmp`, `.gif`, `.heic`, `.heif`, `.ico`, `.jp2`, `.jpe`, `.jpeg`, `.jpg`, `.png`, `.tif`, `.tiff`, `.webp` | безопасная копия без изменений | Office не нужен |
| `.json` | `.txt` или `.md` | Office не нужен |

При экспорте Excel каждый лист становится отдельной операцией Preview. Скрытые и very-hidden листы видны, но не выбраны по умолчанию; полностью пустые листы явно пропускаются. Имена результата детерминированы и безопасны для Windows, например `sales__Summary.csv`.

v0.0.3 также создаёт человекочитаемый `ZletConverter-report.txt` с относительными путями, итоговыми счётчиками, статистикой листов, статусами и безопасной диагностикой. Существующий отчёт не перезаписывается: используются суффиксы `-2`, `-3` и далее.

Итоговая панель сохраняет общую статистику листов и сводку по каждой книге, включая пропущенные скрытые и пустые листы. Книга считается одним исходным файлом независимо от числа результатов. Отчёт находится в папке результата или в корне ZIP, в том числе после остановки и частичных ошибок. Ошибка записи отчёта остаётся видимой. Элементы и подписи отчёта используют существующую настройку RU/EN; смена языка сохраняет Preview и результаты.

Preview можно фильтровать нажатием строк форматов в Rules без изменения checkbox selection и фактического набора файлов для обработки. Видимое действие **Показать все** очищает активный фильтр. Колонки исходного файла, действия, статуса, результата, размера и времени сортируются по возрастанию/убыванию, а видимые строки нумеруются с 1 по текущему порядку после filter + sort.

Тесты с настоящим Excel запускаются только явно: задайте `ZLET_OFFICE_INTEGRATION=1`, `ZLET_OFFICE_XLSX_SHEETS_FIXTURE` и/или `ZLET_OFFICE_XLS_SHEETS_FIXTURE` с путями к локальным книгам, содержащим минимум два непустых листа с двумя колонками, затем запустите категорию `OfficeIntegration`. В ручной проверке нужны скрытые и very-hidden листы, Unicode и формулы со ссылками на другие листы. Автоматические тесты не заменяют полную clean-machine проверку и реальные Microsoft Office integration tests.

Word, Excel и PowerPoint определяются независимо. Если одно приложение отсутствует, недоступна только связанная с ним конвертация; safe-copy операции продолжают работать без Office.

> **Безопасность PowerPoint:** преобразование PPT не запускается, пока у пользователя уже открыт PowerPoint. Это защищает открытую презентацию от вмешательства. DOC/XLS и копирование готовых PPTX при этом остаются доступны.

## Быстрый старт

1. Скачайте установщик или portable ZIP выше.
2. Запустите `ZletConverter.exe`.
3. Выберите исходную папку и выполните сканирование.
4. Проверьте Preview и отметьте нужные операции.
5. Выберите место/режим сохранения результата и запустите обработку.
6. Проверьте результаты по файлам и итоговую сводку пачки.

Готовые сборки self-contained для .NET 8, поэтому отдельно устанавливать .NET Runtime не требуется.

## Что получает пользователь

- Preview до начала обработки с фильтрацией по форматам, сортировкой и нумерацией видимых строк.
- Выбор отдельных операций перед запуском.
- Сохранение относительной структуры подпапок в результате.
- Статус, stage-based progress, размер исходника и время выполнения по каждому файлу.
- Безопасную кнопку Stop, которая не запускает следующие операции из очереди.
- Копирование списка преобразований только с относительными путями.
- Раздельные итоговые счётчики converted, copied, failed, conflict, unavailable, skipped и unselected.
- Планирование экспорта Excel по листам и итоговую статистику workbook/worksheet.
- Постоянный человекочитаемый `ZletConverter-report.txt` для folder/ZIP output.
- Более безопасную пакетную Office-обработку за счёт переиспользования worker/session там, где это допустимо.

## Ограничения

Zlet Converter всё ещё находится в статусе **PRE-ALPHA**. Сложные, повреждённые, защищённые паролем или использующие неподдерживаемые возможности legacy-документы могут не преобразоваться. Точность результата зависит от установленной версии Microsoft Office и особенностей конкретного документа.

Исходные файлы не должны намеренно изменяться, но для важных данных при тестировании PRE-ALPHA ПО рекомендуется иметь резервную копию.

<details>
<summary><strong>Подробно о безопасности и приватности</strong></summary>

Конвертер специально сделан локальным и осторожным по отношению к пользовательским файлам.

- Файлы обрабатываются локально и не загружаются наружу.
- UI-процесс не выполняет Office COM automation напрямую.
- Office-конвертация идёт через изолированный STA worker-процесс.
- Legacy-файлы и книги Excel для экспорта листов открываются read-only.
- Макросы, диалоги и добавление в Recent/MRU отключены для Office-сессий, которыми управляет worker.
- Исходник должен оставаться внутри выбранной исходной папки.
- Reparse-point файлы и папки пропускаются.
- SHA-256 исходника проверяется до и после обработки.
- Результат сначала создаётся во временном/staging расположении и проверяется перед финальным перемещением.
- Существующие файлы и каталоги результата не перезаписываются.
- Office-процессы никогда не завершаются только по имени процесса.
- Принудительное завершение допускается только для app-owned процесса с подтверждёнными PID и временем старта.
- Ошибка одного файла или листа не останавливает остальные выбранные операции автоматически.
- TXT-отчёты используют относительные пути и не должны содержать содержимое документов, пароли, secrets или tokens.

Техническая диагностика может содержать коды ошибок и служебные данные процесса. Она не должна содержать содержимое документов, секреты или полные локальные пути документов.

</details>

<details>
<summary><strong>Сборка из исходников и локальная упаковка</strong></summary>

Требования:

- Windows x64
- .NET 8 SDK

```powershell
dotnet restore FolderConverter.sln
dotnet build FolderConverter.sln -c Release
dotnet test FolderConverter.sln -c Release
```

Если репозиторий был клонирован до переименования в `zlet-converter`, обновите существующий `origin`, а не клонируйте проект заново:

```powershell
git remote set-url origin https://github.com/zlet-labs/zlet-converter.git
```

Сборка portable-пакета:

```powershell
.\scripts\publish-portable.ps1
```

Ожидаемый локальный ZIP для текущей версии исходников:

```text
artifacts/portable/win-x64/ZletConverter-v0.0.3-win-x64.zip
```

Сборка Windows installer через Inno Setup 6:

```powershell
.\scripts\build-installer.ps1
```

Скрипт установщика сначала собирает portable payload, затем создаёт Windows x64 installer и выводит его SHA-256 и статус Authenticode.

</details>

<details>
<summary><strong>Реальные Microsoft Office integration tests</strong></summary>

Реальные интеграционные тесты Office запускаются только явно, потому что требуют установленный Microsoft Office и настоящие legacy-файлы:

```powershell
$env:ZLET_OFFICE_INTEGRATION = "1"
$env:ZLET_OFFICE_WORD_FIXTURE = "C:\fixtures\sample.doc"
$env:ZLET_OFFICE_WORD_BATCH_FIXTURE_DIR = "C:\fixtures\word-batch"
$env:ZLET_OFFICE_EXCEL_FIXTURE = "C:\fixtures\sample.xls"
$env:ZLET_OFFICE_POWERPOINT_FIXTURE = "C:\fixtures\sample.ppt"
dotnet test FolderConverter.sln -c Release --filter Category=OfficeIntegration
```

Если нужное приложение Office или фикстура отсутствует, соответствующий integration test пропускается, а не считается успешно пройденным.

</details>

## Проект

Zlet Converter — проект **Zlet Labs**: небольшие, практичные, self-serve инструменты без лишней SaaS-машины.

[Zlet Labs](https://zlet.app/) · [GitHub Issues](https://github.com/zlet-labs/zlet-converter/issues) · [Все релизы](https://github.com/zlet-labs/zlet-converter/releases) · [MIT License](LICENSE)

Чек-лист ручной проверки релиза: [docs/manual-clean-machine-verification.md](docs/manual-clean-machine-verification.md)
