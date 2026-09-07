using System.IO;
using System.IO.Compression;
using System.Text;
using Zlet.FolderConverter.App.ViewModels;
using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;
using Zlet.FolderConverter.App.Localization;

namespace Zlet.FolderConverter.App;

public static class ConversionReportWriter
{
    private const string BaseReportName = "ZletConverter-report.txt";

    public static async Task WriteAsync(
        MainWindowViewModel viewModel,
        DateTimeOffset started,
        DateTimeOffset finished,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var report = BuildReport(viewModel, started, finished);
            if (viewModel.SelectedOutputMode == OutputMode.Folder)
            {
                var root = MainWindowViewModel.NormalizePathInput(viewModel.OutputPath);
                Directory.CreateDirectory(root);
                var path = GetAvailableReportPath(root);
                if (!OutputPathGuard.IsSafeTargetPath(path, root)) throw new IOException();
                await using var reportStream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await using var reportWriter = new StreamWriter(reportStream, new UTF8Encoding(false));
                await reportWriter.WriteAsync(report.AsMemory(), cancellationToken);
                viewModel.SetReportStatus(true, path);
                return;
            }

            // A report-only archive is legitimate only when no outputs succeeded.
            if (viewModel.ZipPublicationFailed) throw new IOException();
            var zipPath = MainWindowViewModel.NormalizePathInput(viewModel.OutputPath);
            var directory = Path.GetDirectoryName(zipPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidDataException("ZIP output directory is missing.");
            }
            Directory.CreateDirectory(directory);
            if (!OutputPathGuard.IsSafeTargetPath(zipPath, directory)) throw new IOException();
            if (File.Exists(zipPath) && !viewModel.ZipPublishedByThisRun) throw new IOException();
            await WriteZipReportAsync(zipPath, report, viewModel.ZipPublishedByThisRun, cancellationToken);
            viewModel.SetReportStatus(true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
                                           or UnauthorizedAccessException
                                           or InvalidDataException
                                           or ArgumentException)
        {
            viewModel.SetReportStatus(false);
            viewModel.AddLocalizedError("ReportFailed");
        }
    }

    public static string BuildReport(
        MainWindowViewModel viewModel,
        DateTimeOffset started,
        DateTimeOffset finished)
    {
        var l = viewModel.Localization;
        var operations = viewModel.Operations.ToArray();
        var sourceCount = operations
            .Select(row => row.Operation.SourcePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var duration = finished >= started ? finished - started : TimeSpan.Zero;
        var builder = new StringBuilder();
        builder.AppendLine($"{ProductIdentity.Name} v{ProductIdentity.Version}");
        builder.AppendLine(l.Get("ReportTitle"));
        builder.AppendLine($"{l.Get("ReportStarted")}: {started.LocalDateTime:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"{l.Get("ReportFinished")}: {finished.LocalDateTime:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"{l.Get("ReportDuration")}: {FormatDuration(duration)}");
        builder.AppendLine($"{l.Get("ReportOutcome")}: {l.Get(viewModel.WasStoppedByUser ? "ReportOutcomeStopped"
            : viewModel.FinalFailed + viewModel.FinalConflicts + viewModel.FinalUnavailable > 0 ? "ReportOutcomePartial" : "FinalComplete")}");
        builder.AppendLine();
        builder.AppendLine(l.Get("ReportSummary"));
        builder.AppendLine();
        builder.AppendLine($"{l.Get("ReportSources")}: {sourceCount}");
        builder.AppendLine($"{l.Get("ReportConverted")}: {viewModel.FinalConverted}");
        builder.AppendLine($"{l.Get("ReportCopied")}: {viewModel.FinalCopied}");
        builder.AppendLine($"{l.Get("ReportSkipped")}: {viewModel.FinalSkipped}");
        builder.AppendLine($"{l.Get("ReportNotSelected")}: {viewModel.FinalNotSelected}");
        builder.AppendLine($"{l.Get("ReportUnavailable")}: {viewModel.FinalUnavailable}");
        builder.AppendLine($"{l.Get("ReportConflicts")}: {viewModel.FinalConflicts}");
        builder.AppendLine($"{l.Get("ReportFailedCount")}: {viewModel.FinalFailed}");

        var worksheetRows = operations
            .Where(row => row.Operation.IsWorksheetExport)
            .ToArray();
        if (worksheetRows.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine(l.Get("ReportExcel"));
            builder.AppendLine();
            AppendWorksheetAggregate(builder, worksheetRows, l);
            foreach (var workbook in worksheetRows
                         .GroupBy(row => row.Operation.SourcePath, StringComparer.OrdinalIgnoreCase)
                         .OrderBy(group => group.First().Operation.RelativePath, StringComparer.OrdinalIgnoreCase))
            {
                builder.AppendLine();
                builder.AppendLine(SafeRelativePath(workbook.First().Operation.RelativePath));
                AppendWorksheetAggregate(builder, workbook.ToArray(), l, perWorkbook: true);
            }
        }

        builder.AppendLine();
        builder.AppendLine(l.Get("ReportDetails"));
        builder.AppendLine();
        foreach (var row in operations)
        {
            builder.AppendLine($"[{ToReportStatus(row, l)}]");
            builder.AppendLine(row.Operation.IsWorksheetOperation
                ? $"{SafeRelativePath(row.Operation.RelativePath)} / {SafeRelativePath(row.Operation.WorksheetName)}"
                : SafeRelativePath(row.Operation.RelativePath));
            builder.AppendLine(row.ActionLabel);
            if (!string.IsNullOrWhiteSpace(row.ResultPath) && row.ResultPath != "—")
            {
                builder.AppendLine($"{l.Get("ReportResult")}: {SafeRelativePath(row.ResultPath)}");
            }
            builder.AppendLine($"{l.Get("ReportReason")}: {OperationMessageLocalizer.ForReport(
                row.Operation, row.Result?.Diagnostic?.ErrorCode, l)}");
            if (row.Result?.Diagnostic?.ErrorCode is { Length: > 0 } errorCode
                && OperationMessageLocalizer.IsKnownErrorCode(errorCode))
            {
                builder.AppendLine($"{l.Get("ReportErrorCode")}: {errorCode}");
            }
            if (row.Result?.Diagnostic?.HResult is int hResult)
            {
                builder.AppendLine($"HRESULT: 0x{unchecked((uint)hResult):X8}");
            }
            builder.AppendLine($"{l.Get("ReportTime")}: {row.ExecutionTimeText}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static void AppendWorksheetAggregate(
        StringBuilder builder,
        IReadOnlyList<OperationRowViewModel> rows,
        LocalizationService l,
        bool perWorkbook = false)
    {
        var selected = rows.Count(IsSelectedForRun);
        var csvExported = rows.Count(row =>
            row.Operation.Target == ConversionTarget.Csv
            && row.Operation.Status == OperationStatus.Succeeded);
        var tsvExported = rows.Count(row =>
            row.Operation.Target == ConversionTarget.Tsv
            && row.Operation.Status == OperationStatus.Succeeded);
        var hiddenSkipped = rows.Count(row =>
            row.Operation.WorksheetVisibility != WorksheetVisibility.Visible
            && !IsSelectedForRun(row));
        var emptySkipped = rows.Count(row => row.Operation.IsWorksheetOperation && row.Operation.WorksheetIsEmpty);
        var sheetsFound = rows.Count(row => row.Operation.IsWorksheetOperation);
        var failed = rows.Count(row => row.Operation.Status == OperationStatus.Failed);
        if (!perWorkbook)
        {
            var workbooks = rows
                .Select(row => row.Operation.SourcePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            builder.AppendLine($"{l.Get("ReportWorkbooksProcessed")}: {workbooks}");
            builder.AppendLine($"{l.Get("ReportWorksheetsFound")}: {sheetsFound}");
            builder.AppendLine($"{l.Get("ReportWorksheetsSelected")}: {selected}");
            builder.AppendLine($"{l.Get("ReportCsvExported")}: {csvExported}");
            builder.AppendLine($"{l.Get("ReportTsvExported")}: {tsvExported}");
            builder.AppendLine($"{l.Get("ReportHiddenSkipped")}: {hiddenSkipped}");
            builder.AppendLine($"{l.Get("ReportEmptySkipped")}: {emptySkipped}");
            builder.AppendLine($"{l.Get("ReportFailedCount")}: {failed}");
            return;
        }

        builder.AppendLine($"{l.Get("ReportSheetsFound")}: {sheetsFound}");
        builder.AppendLine($"{l.Get("ReportSelected")}: {selected}");
        builder.AppendLine($"{l.Get("ReportCsvExported")}: {csvExported}");
        builder.AppendLine($"{l.Get("ReportTsvExported")}: {tsvExported}");
        builder.AppendLine($"{l.Get("ReportHiddenSkipped")}: {hiddenSkipped}");
        builder.AppendLine($"{l.Get("ReportEmptySkipped")}: {emptySkipped}");
        builder.AppendLine($"{l.Get("ReportFailedCount")}: {failed}");
    }

    private static bool IsSelectedForRun(OperationRowViewModel row) =>
        row.Result is not null
        || row.Operation.Status is OperationStatus.Succeeded
            or OperationStatus.Failed
            or OperationStatus.Cancelled
            or OperationStatus.NotProcessed;

    private static string ToReportStatus(OperationRowViewModel row, LocalizationService localization)
    {
        if (row.IsNotSelected)
        {
            return localization.Get("ReportStatusNotSelected");
        }

        var key = row.Operation.Status switch
        {
            OperationStatus.Succeeded when row.Operation.Target == ConversionTarget.Copy => "ReportStatusCopied",
            OperationStatus.Succeeded => "ReportStatusConverted",
            OperationStatus.Skipped => "ReportStatusSkipped",
            OperationStatus.EngineUnavailable or OperationStatus.Unsupported => "ReportStatusUnavailable",
            OperationStatus.Conflict => "ReportStatusConflict",
            OperationStatus.Failed => "ReportStatusFailed",
            OperationStatus.Cancelled => "ReportStatusCancelled",
            OperationStatus.NotProcessed => "ReportStatusNotProcessed",
            OperationStatus.Ready => "ReportStatusNotSelected",
            _ => null
        };
        return key is null ? row.Operation.Status.ToString() : localization.Get(key);
    }

    private static string SafeRelativePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        return Path.IsPathRooted(path) || normalized.Contains(':') || normalized.Split('/').Contains("..")
            || normalized.Any(char.IsControl) ? "[redacted]" : normalized;
    }

    private static async Task WriteZipReportAsync(string zipPath, string report, bool ownsArchive, CancellationToken token)
    {
        var temporary = Path.Combine(Path.GetDirectoryName(zipPath)!, $".zlet-report-{Guid.NewGuid():N}.tmp");
        try
        {
            // Build the amended archive separately: a report failure must not damage completed outputs.
            await using (var destination = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                if (ownsArchive)
                {
                    await using var original = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    await original.CopyToAsync(destination, token);
                    destination.Position = 0;
                }
                using var archive = new ZipArchive(destination, ownsArchive ? ZipArchiveMode.Update : ZipArchiveMode.Create);
                var name = ownsArchive ? GetAvailableEntryName(archive) : BaseReportName;
                var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                await using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                await writer.WriteAsync(report.AsMemory(), token);
            }
            token.ThrowIfCancellationRequested();
            if (ownsArchive) File.Replace(temporary, zipPath, null);
            else File.Move(temporary, zipPath, overwrite: false);
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static string FormatDuration(TimeSpan duration)
    {
        var totalHours = (int)Math.Floor(Math.Max(0, duration.TotalHours));
        return totalHours > 0
            ? $"{totalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{duration.Minutes:00}:{duration.Seconds:00}";
    }

    private static string GetAvailableReportPath(string root)
    {
        var first = Path.Combine(root, BaseReportName);
        if (!File.Exists(first) && !Directory.Exists(first))
        {
            return first;
        }

        for (var index = 2; index < int.MaxValue; index++)
        {
            var candidate = Path.Combine(root, $"ZletConverter-report-{index}.txt");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }
        }
        throw new IOException("No available report filename.");
    }

    private static string GetAvailableEntryName(ZipArchive archive)
    {
        var existing = archive.Entries
            .Select(entry => entry.FullName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!existing.Contains(BaseReportName))
        {
            return BaseReportName;
        }

        for (var index = 2; index < int.MaxValue; index++)
        {
            var candidate = $"ZletConverter-report-{index}.txt";
            if (!existing.Contains(candidate))
            {
                return candidate;
            }
        }
        throw new IOException("No available report entry name.");
    }
}
