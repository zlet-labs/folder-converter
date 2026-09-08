# Manual clean-machine verification for v0.0.3

Record Windows version, display resolution, scaling, installed Word/Excel/
PowerPoint versions, ZIP size, unpacked size, commit SHA, release tag, and tester
name.

This checklist is manual verification for the published **v0.0.3 PRE-ALPHA**
product. PRE-ALPHA is the maturity label; v0.0.3 itself is a normal GitHub
Release, not a GitHub Pre-release.

## Package integrity

- Download `ZletConverter-v0.0.3-win-x64.zip` from the v0.0.3 GitHub Release.
- Compare the downloaded ZIP SHA-256 with `SHA256SUMS.txt`.
- Fully extract the ZIP to a new local folder.
- Confirm `ZletConverter.exe` and
  `Zlet.FolderConverter.OfficeWorker.exe` are present.
- Confirm there are no source, test, local config, Python, Java, or Office
  runtime files.
- Start `ZletConverter.exe` from the fully extracted folder.
- Confirm the title is `Zlet Converter v0.0.3`.
- Confirm the portable package name is
  `ZletConverter-v0.0.3-win-x64.zip`.

## Installer clean install

- Use `ZletConverter-v0.0.3-Setup-win-x64.exe` from the published release.
- Compare the installer SHA-256 with `SHA256SUMS.txt`.
- Install for the current user on a machine without an existing installation.
- Confirm the installer, Installed Apps entry, Start-menu shortcut, optional
  desktop shortcut, and post-install launch text use `Zlet Converter`.
- Confirm a clean install uses the visible install folder name `Zlet Converter`.
- Confirm the installed app title/version show v0.0.3.
- Confirm uninstall succeeds and removes app-owned installed files/shortcuts.

## Installer upgrade from the previous public name

When an old-name installer fixture is available, use the published v0.0.2
installer as the upgrade baseline.

- Install the historical `Zlet Batch Converter` v0.0.2 build first.
- Run the v0.0.3 `Zlet Converter` installer without manually uninstalling the old
  build.
- Confirm the existing Inno Setup AppId is treated as the same product and only
  one Installed Apps entry remains, named `Zlet Converter`.
- Confirm there are no duplicate app-owned Start-menu or desktop shortcuts.
- Confirm the old app-owned `ZletBatchConverter.exe` host files are not left as
  a second launchable application in the install directory.
- Confirm `ZletConverter.exe` launches normally after the upgrade.
- Confirm uninstall still succeeds after the upgrade.
- Do not delete or rename unrelated/user-created files while checking cleanup.

## Language, Settings and About

- On a clean first launch, confirm the RU/EN language choice is shown.
- Switch language from Settings and confirm Preview/results are preserved.
- Restart and confirm the language preference persists.
- Confirm About shows `Zlet Converter v0.0.3` and PRE-ALPHA maturity.
- Confirm no account, cloud sign-in, telemetry consent, or background update
  prompt is introduced.

## Office capability display

- Confirm the UI always shows separate Word, Excel, and PowerPoint statuses.
- On a machine with Word and Excel but no PowerPoint, confirm:
  - DOC is ready;
  - XLS/XLSX worksheet export is ready;
  - PPT says `Требуется Microsoft PowerPoint`;
  - the batch can still process DOC and Excel operations.
- With PowerPoint already open, confirm PPT reports
  `PowerPoint уже запущен. Закройте его и повторите преобразование.`;
  confirm the user presentation stays open and DOC/XLS still process.
- Confirm modern DOCX/XLSX/PPTX files show safe-copy behavior without requiring
  the corresponding Office application.

## Source and selection workflow

- Enter a source path manually.
- Paste a source path surrounded by quotes.
- Choose a folder with the picker.
- Scan with subfolders on and off.
- Confirm reparse folders/files and Office `~$` files are skipped.
- Select individual files.
- Use Select all, Clear selection, and Invert selection.
- Change a rule and confirm Preview refreshes.
- Confirm filtering/sorting never silently changes checkbox values or the
  selected execution count.

## Preview filtering, sorting and numbering

Use a mixed folder with at least 20 files and multiple formats/sizes.

- Click a format row in Rules and confirm Preview filters to that group.
- Confirm the active Rules row is visible and `Показать все / Show all` appears.
- Clear the filter with Show all.
- Re-apply a filter and clear it via `Все / All` in the Preview filter control.
- Re-apply a filter and clear it by clicking the active Rules row again.
- Confirm `Other` shows only the expected generic/other files.
- Sort Source file, Action, Status, Result, Size, and Time ascending/descending.
- Confirm Size sorting is numeric, for example 900 KB sorts below 10 MB.
- Confirm Time sorting uses elapsed values, not formatted text.
- Apply a format filter, sort inside the filtered view, then Show all.
- Confirm visible row numbering is 1-based and follows the current displayed
  filter + sort order.
- Confirm clearing a filter rebuilds full-list numbering from 1.
- Confirm Preview row content is vertically centered and remains readable at
  100% and 125% scaling.
- Confirm selection count and execution set are unchanged by filter/sort alone.

## Excel worksheet export

Use non-sensitive XLS and XLSX workbooks with at least two non-empty worksheets,
plus hidden/very-hidden and empty sheets where possible.

- Confirm CSV mode creates one UTF-8 CSV per eligible worksheet.
- Confirm TSV mode creates one UTF-8 TSV per eligible worksheet.
- Confirm each worksheet appears as its own Preview operation.
- Confirm hidden/very-hidden worksheet state is represented and not silently
  treated as a normal selected sheet.
- Confirm empty worksheets are skipped explicitly.
- Confirm generated worksheet filenames are deterministic and Windows-safe.
- Confirm Unicode cell content is preserved in the exported text encoding.
- Record separately which real Excel conversions/exports were actually run.

## Expanded safe copy

Use non-sensitive examples of supported formats.

- Confirm DOCX/XLSX/PPTX safe copy without Office conversion.
- Confirm PDF, CSV, TSV and EPUB safe copy.
- Confirm representative supported images, for example PNG/JPEG/WebP, are
  copied unchanged.
- Confirm safe-copy operations remain available when unrelated Office apps are
  missing.
- Confirm source SHA-256 values do not change.

## Folder output

- Choose a result folder.
- Confirm nested relative paths are preserved.
- Convert/copy a mixed batch.
- Confirm one failed file or worksheet does not stop later selected operations.
- Confirm source SHA-256 values do not change.
- Create an existing target file and directory; confirm neither is overwritten.
- Run the same batch again and confirm conflicts are reported.
- Confirm temporary operation folders are removed.
- Confirm `ZletConverter-report.txt` is created with relative paths and does not
  expose document contents or full local source paths.

## ZIP output

- Choose a new `.zip` path.
- Confirm only successful selected outputs are included.
- Confirm nested relative paths are preserved.
- Confirm `ZletConverter-report.txt` is present at the ZIP root.
- Confirm an existing ZIP is not overwritten.
- Repeat the run and confirm the conflict is clear.

## Office behavior

- Use non-sensitive DOC, XLS, and PPT fixtures.
- Confirm the relevant Office UI and dialogs do not appear.
- Confirm original files are unchanged.
- Confirm produced DOCX/XLSX/PPTX files open normally.
- Trigger or simulate timeout and confirm already-open user Office documents are
  not terminated.
- Record separately which real conversions were actually executed.

## Stop and partial results

- Start a mixed batch and use Stop while work is active.
- Confirm already completed outputs remain available.
- Confirm new queued operations stop starting.
- Confirm partial results/report remain readable.
- Confirm unrelated Office processes are not terminated by name alone.

## Layout

- Verify at 100% and 125% Windows scaling.
- Verify at 1366x768 where practical.
- Verify windowed and maximized states.
- Confirm source/output controls, capability statuses, Rules icons, Preview rows,
  filter reset action, sorting indicators, selection controls, report, and
  primary action remain visible and usable.

Do not mark full clean-machine or real Microsoft Office integration verification
as passed from automated tests alone. Record every unperformed item explicitly.
