using Zlet.FolderConverter.App.ViewModels;
using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.Tests;

public sealed class UxPolishTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "zlet-ux-polish-tests", Guid.NewGuid().ToString("N"));

    public UxPolishTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task Copy_list_contains_only_selected_real_conversions_in_preview_order()
    {
        var operations = new[]
        {
            Operation(Path.Combine("database", "first.doc"), ConversionTarget.Docx),
            Operation("copy.docx", ConversionTarget.Copy),
            Operation("second.xls", ConversionTarget.Xlsx),
            Operation("manual.pdf", ConversionTarget.Skip, OperationStatus.Skipped),
            Operation("conflict.ppt", ConversionTarget.Pptx, OperationStatus.Conflict)
        };
        var viewModel = ViewModel(operations);
        await viewModel.ScanAsync();

        var text = viewModel.BuildConversionList();

        Assert.Equal(
            "database/first.doc → database/first.docx" + Environment.NewLine
            + "second.xls → second.xlsx",
            text);
        Assert.DoesNotContain(_root, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("copy.docx", text);
        Assert.DoesNotContain("manual.pdf", text);
        Assert.DoesNotContain("conflict.ppt", text);

        viewModel.ConfirmConversionListCopied();
        Assert.Equal("Скопировано 2 файла", viewModel.CopyListStatus);
    }

    [Fact]
    public async Task Copy_list_empty_state_is_persistent_and_does_not_expose_absolute_paths()
    {
        var viewModel = ViewModel([Operation("only.doc", ConversionTarget.Docx)]);
        await viewModel.ScanAsync();
        viewModel.ClearSelection();

        Assert.Equal(string.Empty, viewModel.BuildConversionList());
        Assert.Equal("Нет выбранных файлов для обработки", viewModel.CopyListStatus);
        Assert.DoesNotContain(_root, viewModel.CopyListStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Stop_preserves_completed_cancels_active_marks_pending_and_allows_new_run()
    {
        var clock = new ManualTimeProvider();
        var processor = new StopThenCompleteProcessor(clock);
        var viewModel = ViewModel(
            [
                Operation("first.doc", ConversionTarget.Docx),
                Operation("second.doc", ConversionTarget.Docx),
                Operation("third.doc", ConversionTarget.Docx)
            ],
            processor,
            clock);
        await viewModel.ScanAsync();

        var firstRun = viewModel.ConvertAsync();
        Assert.True(viewModel.StopConversion());
        Assert.False(viewModel.StopConversion());
        await firstRun;

        Assert.Equal("Преобразовано · 100%", viewModel.Operations[0].Status);
        Assert.Equal("Отменено", viewModel.Operations[1].Status);
        Assert.Equal("Не обработано", viewModel.Operations[2].Status);
        Assert.Equal("Остановлено пользователем", viewModel.FinalReportTitle);
        Assert.Equal(1, viewModel.FinalConverted);
        Assert.True(viewModel.CanConvert);
        Assert.Equal(2, processor.FirstRunStarted);

        await viewModel.ConvertAsync();

        Assert.Equal(2, viewModel.FinalConverted);
        Assert.All(viewModel.Operations, row => Assert.Equal(OperationStatus.Succeeded, row.Operation.Status));
        Assert.Equal(2, processor.SecondRunStarted);
    }

    [Fact]
    public async Task Previously_unselected_ready_row_can_be_selected_and_run_without_rescan()
    {
        var processor = new RecordingSuccessProcessor();
        var viewModel = ViewModel(
            [
                Operation("first.doc", ConversionTarget.Docx),
                Operation("second.doc", ConversionTarget.Docx)
            ],
            processor);
        await viewModel.ScanAsync();
        viewModel.Operations[1].IsSelected = false;

        await viewModel.ConvertAsync();

        var omitted = viewModel.Operations[1];
        Assert.Equal("Не выбрано", omitted.Status);
        Assert.True(omitted.CanSelect);
        Assert.Equal("—", omitted.ExecutionTimeText);

        viewModel.SelectAll();
        Assert.True(omitted.IsSelected);
        Assert.Equal("Готово к преобразованию", omitted.Status);
        viewModel.ClearSelection();
        Assert.False(omitted.IsSelected);
        viewModel.InvertSelection();
        Assert.True(omitted.IsSelected);

        await viewModel.ConvertAsync();

        Assert.Equal(OperationStatus.Succeeded, omitted.Operation.Status);
        Assert.Equal(2, processor.Batches.Count);
        Assert.Equal(["first.doc"], processor.Batches[0]);
        Assert.Equal(["second.doc"], processor.Batches[1]);
        Assert.Equal(1, viewModel.FinalConverted);
    }

    [Fact]
    public async Task Failed_diagnostic_and_counter_survive_stop_and_remaining_rerun_once()
    {
        const int hResult = unchecked((int)0x80004005);
        var processor = new FailedThenStopProcessor(hResult);
        var viewModel = ViewModel(
            [
                Operation("failed.doc", ConversionTarget.Docx),
                Operation("active.doc", ConversionTarget.Docx),
                Operation("pending.doc", ConversionTarget.Docx)
            ],
            processor);
        await viewModel.ScanAsync();

        var firstRun = viewModel.ConvertAsync();
        Assert.True(viewModel.StopConversion());
        await firstRun;

        Assert.Equal("Остановлено пользователем", viewModel.FinalReportTitle);
        Assert.Equal(OperationStatus.Failed, viewModel.Operations[0].Operation.Status);
        Assert.Equal(1, viewModel.FinalFailed);
        var diagnostic = Assert.Single(viewModel.ErrorMessages);
        Assert.Contains("failed.doc", diagnostic);
        Assert.Contains("test_error", diagnostic);
        Assert.Contains("HRESULT 0x80004005", diagnostic);

        await viewModel.ConvertAsync();

        Assert.Equal(OperationStatus.Failed, viewModel.Operations[0].Operation.Status);
        Assert.Equal(1, viewModel.FinalFailed);
        Assert.Single(viewModel.ErrorMessages);
        Assert.Equal(2, processor.RunCount);
    }

    [Fact]
    public async Task Final_counters_separate_conversion_copy_and_non_success_states()
    {
        var convert = Operation("convert.doc", ConversionTarget.Docx);
        var copy = Operation("copy.docx", ConversionTarget.Copy);
        var failed = Operation("failed.xls", ConversionTarget.Xlsx);
        var conflict = Operation("conflict.ppt", ConversionTarget.Pptx, OperationStatus.Conflict);
        var skipped = Operation("manual.pdf", ConversionTarget.Skip, OperationStatus.Skipped);
        var results = new[]
        {
            new ConversionResult(convert, OperationStatus.Succeeded, "ok"),
            new ConversionResult(copy, OperationStatus.Succeeded, "ok"),
            new ConversionResult(failed, OperationStatus.Failed, "bad")
        };
        var summary = new ConversionSummary(2, 0, 1, 0, 0, 0, results);
        var viewModel = ViewModel(
            [convert, copy, failed, conflict, skipped],
            new StaticProcessor(summary));
        await viewModel.ScanAsync();

        await viewModel.ConvertAsync();

        Assert.Equal(1, viewModel.FinalConverted);
        Assert.Equal(1, viewModel.FinalCopied);
        Assert.Equal(2, viewModel.FinalSucceeded);
        Assert.Equal(1, viewModel.FinalFailed);
        Assert.Equal(1, viewModel.FinalConflicts);
        Assert.Equal(1, viewModel.FinalSkipped);
    }

    [Fact]
    public void Row_progress_and_monotonic_execution_time_advance_then_freeze()
    {
        var clock = new ManualTimeProvider();
        var operation = Operation("timed.doc", ConversionTarget.Docx);
        var row = new OperationRowViewModel(operation);

        row.BeginExecution(clock.GetTimestamp(), 10);
        row.BeginExecution(clock.GetTimestamp(), 55);
        row.BeginExecution(clock.GetTimestamp(), 25);
        clock.Advance(TimeSpan.FromSeconds(3.2));
        row.RefreshExecutionTime(clock, clock.GetTimestamp());
        Assert.Equal("В процессе · 55%", row.Status);
        Assert.Equal("3,2 с", row.ExecutionTimeText);

        row.CompleteExecution(
            new ConversionResult(operation, OperationStatus.Succeeded, "ok"),
            clock,
            clock.GetTimestamp());
        Assert.Equal("Преобразовано · 100%", row.Status);
        Assert.Equal("3,2 с", row.ExecutionTimeText);

        clock.Advance(TimeSpan.FromMinutes(2));
        row.RefreshExecutionTime(clock, clock.GetTimestamp());
        Assert.Equal("3,2 с", row.ExecutionTimeText);
    }

    [Fact]
    public void Failed_cancelled_skipped_and_not_started_times_are_truthful()
    {
        var clock = new ManualTimeProvider();
        var failedOperation = Operation("failed.doc", ConversionTarget.Docx);
        var failed = new OperationRowViewModel(failedOperation);
        failed.BeginExecution(clock.GetTimestamp(), 10);
        clock.Advance(TimeSpan.FromSeconds(48));
        failed.CompleteExecution(
            new ConversionResult(failedOperation, OperationStatus.Failed, "bad"),
            clock,
            clock.GetTimestamp());

        var cancelled = new OperationRowViewModel(Operation("cancel.doc", ConversionTarget.Docx));
        cancelled.BeginExecution(clock.GetTimestamp(), 10);
        clock.Advance(TimeSpan.FromSeconds(72));
        cancelled.CancelExecution(clock, clock.GetTimestamp());

        Assert.Equal("48 с", failed.ExecutionTimeText);
        Assert.StartsWith("Ошибка", failed.Status);
        Assert.Equal("1:12", cancelled.ExecutionTimeText);
        Assert.Equal("Отменено", cancelled.Status);
        Assert.True(cancelled.IsSelected);
        cancelled.BeginExecution(clock.GetTimestamp(), 10);
        Assert.Equal("—", cancelled.ExecutionTimeText);
        clock.Advance(TimeSpan.FromSeconds(2));
        cancelled.RefreshExecutionTime(clock, clock.GetTimestamp());
        Assert.Equal("2 с", cancelled.ExecutionTimeText);
        var protocolCancelledOperation = Operation("protocol-cancel.doc", ConversionTarget.Docx);
        var protocolCancelled = new OperationRowViewModel(protocolCancelledOperation);
        protocolCancelled.BeginExecution(clock.GetTimestamp(), 10);
        protocolCancelled.CompleteExecution(
            new ConversionResult(
                protocolCancelledOperation,
                OperationStatus.Cancelled,
                "cancelled"),
            clock,
            clock.GetTimestamp());
        Assert.True(protocolCancelled.IsSelected);
        Assert.Equal("—", new OperationRowViewModel(
            Operation("skip.pdf", ConversionTarget.Skip, OperationStatus.Skipped)).ExecutionTimeText);
        Assert.Equal("—", new OperationRowViewModel(
            Operation("ready.doc", ConversionTarget.Docx)).ExecutionTimeText);
    }

    [Theory]
    [InlineData(0, "0 МБ")]
    [InlineData(880804, "0,84 МБ")]
    [InlineData(13212058, "12,6 МБ")]
    [InlineData(152043520, "145 МБ")]
    public void File_size_formatter_is_compact(long bytes, string expected) =>
        Assert.Equal(expected, OperationRowViewModel.FormatFileSize(bytes));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private MainWindowViewModel ViewModel(
        PlannedOperation[] operations,
        IConversionProcessor? processor = null,
        TimeProvider? clock = null)
    {
        var scan = new ScanResult(
            _root,
            operations.Select(operation => new ScannedFile(
                operation.SourcePath,
                operation.RelativePath,
                operation.SourceFormat,
                operation.SourceSizeBytes)).ToArray(),
            []);
        return new MainWindowViewModel(
            new StaticScanner(scan),
            new StaticPlanner(operations),
            processor,
            timeProvider: clock)
        {
            SelectedFolder = _root,
            OutputPath = Path.Combine(_root, "result")
        };
    }

    private PlannedOperation Operation(
        string relativePath,
        ConversionTarget target,
        OperationStatus status = OperationStatus.Ready)
    {
        var extension = target == ConversionTarget.Copy
            ? Path.GetExtension(relativePath)
            : target == ConversionTarget.Skip ? string.Empty : target.ToExtension();
        return new PlannedOperation(
            Path.Combine(_root, relativePath),
            relativePath,
            SourceFormatFor(relativePath),
            target,
            extension,
            target == ConversionTarget.Skip
                ? string.Empty
                : Path.Combine(_root, "result", Path.ChangeExtension(relativePath, extension)),
            true,
            status,
            status.ToString(),
            Path.Combine(_root, "result"),
            _root,
            880804);
    }

    private static SourceFormat SourceFormatFor(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".doc" => SourceFormat.Doc,
            ".docx" => SourceFormat.Docx,
            ".xls" => SourceFormat.Xls,
            ".ppt" => SourceFormat.Ppt,
            ".pdf" => SourceFormat.Pdf,
            _ => SourceFormat.Unknown
        };

    private sealed class StaticScanner(ScanResult result) : IFolderScanner
    {
        public Task<ScanResult> ScanAsync(
            string rootPath,
            bool includeSubfolders,
            CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class StaticPlanner(IReadOnlyList<PlannedOperation> operations)
        : IConversionPlanner
    {
        public IReadOnlyList<PlannedOperation> CreatePlan(
            ScanResult scanResult,
            string rootPath,
            RuleSet ruleSet) => operations;
    }

    private sealed class StaticProcessor(ConversionSummary summary) : IConversionProcessor
    {
        public Task<ConversionSummary> ProcessAsync(
            IReadOnlyList<PlannedOperation> operations,
            IProgress<ConversionProgress>? progress,
            CancellationToken cancellationToken) => Task.FromResult(summary);
    }

    private sealed class StopThenCompleteProcessor(ManualTimeProvider clock) : IConversionProcessor
    {
        private int _run;
        public int FirstRunStarted { get; private set; }
        public int SecondRunStarted { get; private set; }

        public Task<ConversionSummary> ProcessAsync(
            IReadOnlyList<PlannedOperation> operations,
            IProgress<ConversionProgress>? progress,
            CancellationToken cancellationToken)
        {
            _run++;
            if (_run == 1)
            {
                FirstRunStarted++;
                progress?.Report(new ConversionProgress(0, operations.Count,
                    operations[0].RelativePath, OperationStatus.Converting, OperationPercent: 10));
                clock.Advance(TimeSpan.FromSeconds(2));
                var first = new ConversionResult(operations[0], OperationStatus.Succeeded, "ok");
                progress?.Report(new ConversionProgress(1, operations.Count,
                    operations[0].RelativePath, OperationStatus.Succeeded, first, 100));
                FirstRunStarted++;
                progress?.Report(new ConversionProgress(1, operations.Count,
                    operations[1].RelativePath, OperationStatus.Converting, OperationPercent: 10));
                clock.Advance(TimeSpan.FromSeconds(3));
                var completion = new TaskCompletionSource<ConversionSummary>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
                return completion.Task;
            }

            var results = new List<ConversionResult>();
            for (var index = 0; index < operations.Count; index++)
            {
                SecondRunStarted++;
                progress?.Report(new ConversionProgress(index, operations.Count,
                    operations[index].RelativePath, OperationStatus.Converting, OperationPercent: 10));
                clock.Advance(TimeSpan.FromSeconds(1));
                var result = new ConversionResult(operations[index], OperationStatus.Succeeded, "ok");
                results.Add(result);
                progress?.Report(new ConversionProgress(index + 1, operations.Count,
                    operations[index].RelativePath, OperationStatus.Succeeded, result, 100));
            }
            return Task.FromResult(new ConversionSummary(
                results.Count, 0, 0, 0, 0, 0, results));
        }
    }

    private sealed class RecordingSuccessProcessor : IConversionProcessor
    {
        public List<string[]> Batches { get; } = [];

        public Task<ConversionSummary> ProcessAsync(
            IReadOnlyList<PlannedOperation> operations,
            IProgress<ConversionProgress>? progress,
            CancellationToken cancellationToken)
        {
            Batches.Add(operations.Select(operation => operation.RelativePath).ToArray());
            var results = operations.Select(operation =>
                new ConversionResult(operation, OperationStatus.Succeeded, "ok")).ToArray();
            return Task.FromResult(new ConversionSummary(
                results.Length, 0, 0, 0, 0, 0, results));
        }
    }

    private sealed class FailedThenStopProcessor(int hResult) : IConversionProcessor
    {
        public int RunCount { get; private set; }

        public Task<ConversionSummary> ProcessAsync(
            IReadOnlyList<PlannedOperation> operations,
            IProgress<ConversionProgress>? progress,
            CancellationToken cancellationToken)
        {
            RunCount++;
            if (RunCount == 1)
            {
                progress?.Report(new ConversionProgress(
                    0, operations.Count, operations[0].RelativePath,
                    OperationStatus.Converting, OperationPercent: 25));
                var failed = new ConversionResult(
                    operations[0],
                    OperationStatus.Failed,
                    "diagnostic failure",
                    new ConversionDiagnostic("test_error", HResult: hResult));
                progress?.Report(new ConversionProgress(
                    1, operations.Count, operations[0].RelativePath,
                    OperationStatus.Failed, failed, 55));
                progress?.Report(new ConversionProgress(
                    1, operations.Count, operations[1].RelativePath,
                    OperationStatus.Converting, OperationPercent: 25));
                var completion = new TaskCompletionSource<ConversionSummary>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
                return completion.Task;
            }

            var results = operations.Select(operation =>
                new ConversionResult(operation, OperationStatus.Succeeded, "ok")).ToArray();
            return Task.FromResult(new ConversionSummary(
                results.Length, 0, 0, 0, 0, 0, results));
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _timestamp;
        public void Advance(TimeSpan value) => _timestamp += value.Ticks;
    }
}
