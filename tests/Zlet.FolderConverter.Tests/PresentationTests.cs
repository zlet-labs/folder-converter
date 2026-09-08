using Zlet.FolderConverter.App.Localization;
using Zlet.FolderConverter.App.ViewModels;
using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;
using System.ComponentModel;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Zlet.FolderConverter.Tests;

public sealed class PresentationTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "zlet-folder-converter-presentation-tests",
        Guid.NewGuid().ToString("N"));

    public PresentationTests() => Directory.CreateDirectory(_rootPath);

    [Theory]
    [InlineData(OperationStatus.Ready, "Готово к преобразованию")]
    [InlineData(OperationStatus.Skipped, "Пропущено")]
    [InlineData(OperationStatus.Converting, "В процессе")]
    [InlineData(OperationStatus.Succeeded, "Преобразовано")]
    [InlineData(OperationStatus.Conflict, "Конфликт")]
    [InlineData(OperationStatus.Failed, "Ошибка")]
    [InlineData(OperationStatus.EngineUnavailable, "Недоступно")]
    [InlineData(OperationStatus.Unsupported, "Недоступно")]
    public void OperationRowViewModel_localizes_statuses(
        OperationStatus status,
        string expected)
    {
        Assert.Equal(expected, OperationRowViewModel.LocalizeStatus(status));
    }

    [Theory]
    [InlineData(OperationStatus.Ready, ConversionTarget.Copy, false, "ReadyCopy")]
    [InlineData(OperationStatus.Ready, ConversionTarget.Docx, false, "ReadyConvert")]
    [InlineData(OperationStatus.Converting, ConversionTarget.Docx, false, "InProgress")]
    [InlineData(OperationStatus.Succeeded, ConversionTarget.Copy, false, "Copied")]
    [InlineData(OperationStatus.Succeeded, ConversionTarget.Docx, false, "Success")]
    [InlineData(OperationStatus.Skipped, ConversionTarget.Skip, false, "Warning")]
    [InlineData(OperationStatus.Conflict, ConversionTarget.Docx, false, "Conflict")]
    [InlineData(OperationStatus.Failed, ConversionTarget.Docx, false, "Danger")]
    [InlineData(OperationStatus.EngineUnavailable, ConversionTarget.Docx, false, "Unavailable")]
    [InlineData(OperationStatus.Unsupported, ConversionTarget.Docx, false, "Unavailable")]
    [InlineData(OperationStatus.Cancelled, ConversionTarget.Docx, false, "Cancelled")]
    [InlineData(OperationStatus.NotProcessed, ConversionTarget.Docx, false, "Cancelled")]
    [InlineData(OperationStatus.Ready, ConversionTarget.Docx, true, "Cancelled")]
    public void OperationRowViewModel_maps_semantic_status_tones(
        OperationStatus status,
        ConversionTarget target,
        bool isNotSelected,
        string expectedTone)
    {
        var op = new PlannedOperation(
            Path.Combine(_rootPath, "file.doc"), "file.doc", SourceFormat.Doc,
            target, ".docx", Path.Combine(_rootPath, "file.docx"), true,
            status, "msg", _rootPath, _rootPath);
        var row = new OperationRowViewModel(op, isNotSelected: isNotSelected);
        Assert.Equal(expectedTone, row.StatusTone);
    }

    [Fact]
    public void AppStyles_contains_all_semantic_status_brushes_and_chip_styles()
    {
        var uri = new Uri("/ZletConverter;component/Resources/AppStyles.xaml", UriKind.Relative);
        var styles = new System.Windows.ResourceDictionary { Source = uri };

        var requiredKeys = new[]
        {
            "ReadyCopyStatusBackgroundBrush", "ReadyCopyStatusBorderBrush", "ReadyCopyStatusForegroundBrush",
            "ReadyConvertStatusBackgroundBrush", "ReadyConvertStatusBorderBrush", "ReadyConvertStatusForegroundBrush",
            "InProgressStatusBackgroundBrush", "InProgressStatusBorderBrush", "InProgressStatusForegroundBrush",
            "CopiedStatusBackgroundBrush", "CopiedStatusBorderBrush", "CopiedStatusForegroundBrush",
            "SuccessStatusBackgroundBrush", "SuccessStatusBorderBrush", "SuccessStatusForegroundBrush",
            "WarningStatusBackgroundBrush", "WarningStatusBorderBrush", "WarningStatusForegroundBrush",
            "ConflictStatusBackgroundBrush", "ConflictStatusBorderBrush", "ConflictStatusForegroundBrush",
            "DangerStatusBackgroundBrush", "DangerStatusBorderBrush", "DangerStatusForegroundBrush",
            "UnavailableStatusBackgroundBrush", "UnavailableStatusBorderBrush", "UnavailableStatusForegroundBrush",
            "CancelledStatusBackgroundBrush", "CancelledStatusBorderBrush", "CancelledStatusForegroundBrush",
            "StatusChipBorderStyle", "StatusChipTextStyle"
        };

        foreach (var key in requiredKeys)
        {
            Assert.True(styles.Contains(key), $"Missing resource key: {key}");
        }

        var chipTextStyle = (System.Windows.Style)styles["StatusChipTextStyle"];
        Assert.DoesNotContain(
            chipTextStyle.Setters.OfType<System.Windows.Setter>(),
            s => s.Property == System.Windows.FrameworkElement.MaxWidthProperty);

        var trimmingSetter = chipTextStyle.Setters.OfType<System.Windows.Setter>()
            .FirstOrDefault(s => s.Property == System.Windows.Controls.TextBlock.TextTrimmingProperty);
        Assert.NotNull(trimmingSetter);
        Assert.Equal(System.Windows.TextTrimming.CharacterEllipsis, trimmingSetter.Value);

        var chipBorderStyle = (System.Windows.Style)styles["StatusChipBorderStyle"];
        Assert.DoesNotContain(
            chipBorderStyle.Setters.OfType<System.Windows.Setter>(),
            s => s.Property == System.Windows.FrameworkElement.MaxWidthProperty);

        var alignmentSetter = chipBorderStyle.Setters.OfType<System.Windows.Setter>()
            .FirstOrDefault(s => s.Property == System.Windows.FrameworkElement.HorizontalAlignmentProperty);
        Assert.NotNull(alignmentSetter);
        Assert.Equal(System.Windows.HorizontalAlignment.Left, alignmentSetter.Value);

        var cellStyle = (System.Windows.Style)styles[typeof(System.Windows.Controls.DataGridCell)];
        var cellHAlign = cellStyle.Setters.OfType<System.Windows.Setter>()
            .FirstOrDefault(s => s.Property == System.Windows.Controls.Control.HorizontalContentAlignmentProperty);
        Assert.NotNull(cellHAlign);
        Assert.Equal(System.Windows.HorizontalAlignment.Stretch, cellHAlign.Value);
    }

    [Theory]
    [InlineData("Готово к преобразованию", 95, false)]
    [InlineData("Готово к преобразованию", 140, false)]
    [InlineData("Готово к преобразованию", 260, true)]
    [InlineData("Копировать без изменений", 260, true)]
    [InlineData("Ready to convert", 140, false)]
    [InlineData("Ready to convert", 260, false)]
    [InlineData("Copy without modification", 260, true)]
    public void StatusChip_responsive_measurement_without_fixed_cap(
        string statusText,
        double columnWidth,
        bool exceedsOldCapWhenWide)
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                var textBlock = new System.Windows.Controls.TextBlock
                {
                    Text = statusText,
                    FontSize = 12,
                    FontWeight = System.Windows.FontWeights.SemiBold,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    TextTrimming = System.Windows.TextTrimming.CharacterEllipsis
                };
                var border = new System.Windows.Controls.Border
                {
                    CornerRadius = new System.Windows.CornerRadius(5),
                    BorderThickness = new System.Windows.Thickness(1),
                    Padding = new System.Windows.Thickness(7, 2, 7, 2),
                    MinHeight = 22,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    Child = textBlock
                };
                var grid = new System.Windows.Controls.Grid
                {
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
                };
                grid.Children.Add(border);

                // In DataGridCell with Padding="11,6" (22px horizontal)
                double cellAvailableWidth = Math.Max(0, columnWidth - 22);

                grid.Measure(new System.Windows.Size(cellAvailableWidth, double.PositiveInfinity));
                grid.Arrange(new System.Windows.Rect(0, 0, cellAvailableWidth, grid.DesiredSize.Height));

                Assert.True(border.ActualWidth <= cellAvailableWidth,
                    $"Border actual width ({border.ActualWidth}) exceeded cell available width ({cellAvailableWidth})");

                if (cellAvailableWidth > 200)
                {
                    Assert.True(border.ActualWidth < cellAvailableWidth,
                        $"Border stretched across entire cell width ({cellAvailableWidth}) instead of staying compact");
                }

                if (exceedsOldCapWhenWide)
                {
                    Assert.True(border.ActualWidth > 126,
                        $"Border actual width ({border.ActualWidth}) remained capped below 126px despite wide column ({columnWidth}px)");
                }
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        bool finished = thread.Join(3000);
        Assert.True(finished, "Measurement thread timed out");
        if (threadEx != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(threadEx).Throw();
        }
    }


    [Fact]
    public void OperationRowViewModel_shows_running_powerpoint_message()
    {
        const string message =
            "PowerPoint уже запущен. Закройте его и повторите преобразование.";
        var operation = new PlannedOperation(
            Path.Combine(_rootPath, "legacy.ppt"),
            "legacy.ppt",
            SourceFormat.Ppt,
            ConversionTarget.Pptx,
            ".pptx",
            Path.Combine(_rootPath, "_converted", "legacy.pptx"),
            true,
            OperationStatus.Ready,
            "Готово к преобразованию.",
            Path.Combine(_rootPath, "_converted"),
            _rootPath);
        var result = new ConversionResult(
            operation,
            OperationStatus.Failed,
            message,
            new ConversionDiagnostic("powerpoint_already_running"));

        var row = new OperationRowViewModel(operation, result);

        Assert.Equal($"Ошибка: {message}", row.Status);
        Assert.Equal(message, row.Message);
    }

    [Fact]
    public void OperationRowViewModel_shows_any_file_failure_message_inline()
    {
        const string message =
            "PowerPoint не запустился. Откройте PowerPoint вручную и повторите.";
        var operation = new PlannedOperation(
            Path.Combine(_rootPath, "legacy.ppt"),
            "legacy.ppt",
            SourceFormat.Ppt,
            ConversionTarget.Pptx,
            ".pptx",
            Path.Combine(_rootPath, "_converted", "legacy.pptx"),
            true,
            OperationStatus.Ready,
            "Готово к преобразованию.",
            Path.Combine(_rootPath, "_converted"),
            _rootPath);
        var result = new ConversionResult(
            operation,
            OperationStatus.Failed,
            message,
            new ConversionDiagnostic("office_com_failure"));

        var row = new OperationRowViewModel(operation, result);

        Assert.Equal($"Ошибка: {message}", row.Status);
        Assert.Equal(message, row.Message);
    }

    [Fact]
    public void OperationRowViewModel_shows_relative_paths_and_russian_action()
    {
        var relative = Path.Combine("архив договоров", "old file.doc");
        var operation = new PlannedOperation(
            Path.Combine(_rootPath, relative),
            relative,
            SourceFormat.Doc,
            ConversionTarget.Docx,
            ".docx",
            Path.Combine(_rootPath, "_converted", "архив договоров", "old file.docx"),
            true,
            OperationStatus.Ready,
            "ready",
            Path.Combine(_rootPath, "_converted"));

        var row = new OperationRowViewModel(operation);

        Assert.Equal(relative, row.FilePath);
        Assert.Equal(Path.Combine("архив договоров", "old file.docx"), row.ResultPath);
        Assert.Equal("DOC → DOCX", row.ActionLabel);
    }

    [Fact]
    public async Task MainWindowViewModel_builds_default_rule_rows_for_found_formats()
    {
        Write("one.json", "{}");
        Write("two.docx", "synthetic");
        Write("manual.pdf", "%PDF-1.7");
        var viewModel = CreateViewModel();

        await viewModel.ScanAsync();

        Assert.Equal(3, viewModel.FormatRules.Count);
        Assert.Equal(ConversionTarget.Txt, RuleFor(viewModel, SourceFormat.Json).SelectedTarget.Target);
        Assert.Equal(ConversionTarget.Copy, RuleFor(viewModel, SourceFormat.Docx).SelectedTarget.Target);
        Assert.Equal(ConversionTarget.Copy, RuleFor(viewModel, SourceFormat.Pdf).SelectedTarget.Target);
        Assert.Equal(3, viewModel.ReadyCount);
        Assert.Equal(0, viewModel.SkippedCount);
    }

    [Fact]
    public async Task Changing_rule_rebuilds_preview_immediately()
    {
        Write("source.json", "{}");
        var viewModel = CreateViewModel();
        await viewModel.ScanAsync();
        var jsonRule = RuleFor(viewModel, SourceFormat.Json);
        var markdown = jsonRule.Targets.Single(option =>
            option.Target == ConversionTarget.Markdown);

        jsonRule.SelectedTarget = markdown;

        var operation = Assert.Single(viewModel.Operations).Operation;
        Assert.Equal(ConversionTarget.Markdown, operation.Target);
        Assert.EndsWith(".md", operation.TargetPath);
        Assert.Equal("Правило изменено. Предпросмотр обновлён.", viewModel.StateMessage);
    }

    [Fact]
    public async Task Unknown_format_is_visible_and_skipped()
    {
        Write("source.custom", "synthetic");
        var viewModel = CreateViewModel();

        await viewModel.ScanAsync();

        var rule = Assert.Single(viewModel.FormatRules);
        Assert.Equal(SourceFormat.Unknown, rule.SourceFormat);
        Assert.Equal("CUSTOM: 1", rule.ExtensionBreakdown);
        Assert.True(rule.HasExtensionBreakdown);
        Assert.Equal(OperationStatus.Skipped, Assert.Single(viewModel.Operations).Operation.Status);
    }

    [Fact]
    public async Task Preview_summary_separates_unavailable_and_failed_operations()
    {
        var statuses = new[]
        {
            OperationStatus.Ready,
            OperationStatus.Converting,
            OperationStatus.Succeeded,
            OperationStatus.Skipped,
            OperationStatus.EngineUnavailable,
            OperationStatus.Unsupported,
            OperationStatus.Conflict,
            OperationStatus.Failed
        };
        var viewModel = CreateStatusViewModel(statuses);

        await viewModel.ScanAsync();

        Assert.Equal(8, viewModel.FoundCount);
        Assert.Equal(2, viewModel.ReadyCount);
        Assert.Equal(1, viewModel.SkippedCount);
        Assert.Equal(2, viewModel.UnavailableCount);
        Assert.Equal(1, viewModel.ConflictCount);
        Assert.Equal(1, viewModel.ErrorCount);
        Assert.True(viewModel.HasEngineUnavailable);
    }

    public static IEnumerable<object[]> PreviewFilterCases()
    {
        var all = new[]
        {
            OperationStatus.Ready,
            OperationStatus.Converting,
            OperationStatus.Succeeded,
            OperationStatus.Skipped,
            OperationStatus.EngineUnavailable,
            OperationStatus.Unsupported,
            OperationStatus.Conflict,
            OperationStatus.Failed
        };
        yield return [PreviewFilter.All, all];
        yield return
        [
            PreviewFilter.Convert,
            new[]
            {
                OperationStatus.Ready,
                OperationStatus.Converting,
                OperationStatus.Succeeded
            }
        ];
        yield return [PreviewFilter.Skip, new[] { OperationStatus.Skipped }];
        yield return
        [
            PreviewFilter.Unavailable,
            new[]
            {
                OperationStatus.EngineUnavailable,
                OperationStatus.Unsupported
            }
        ];
        yield return [PreviewFilter.Conflicts, new[] { OperationStatus.Conflict }];
        yield return [PreviewFilter.Errors, new[] { OperationStatus.Failed }];
    }

    [Theory]
    [MemberData(nameof(PreviewFilterCases))]
    public async Task Preview_filters_match_only_their_statuses(
        PreviewFilter filter,
        OperationStatus[] expected)
    {
        var allStatuses = (OperationStatus[])PreviewFilterCases().First()[1];
        var viewModel = CreateStatusViewModel(allStatuses);
        await viewModel.ScanAsync();

        viewModel.SelectedPreviewFilter = viewModel.PreviewFilters.Single(option =>
            option.Filter == filter);

        Assert.Equal(expected, viewModel.VisibleOperations.Select(row =>
            row.Operation.Status));
    }

    [Theory]
    [InlineData(OperationStatus.EngineUnavailable, true)]
    [InlineData(OperationStatus.Unsupported, false)]
    [InlineData(OperationStatus.Ready, false)]
    public async Task Runtime_banner_is_visible_only_for_engine_unavailable(
        OperationStatus status,
        bool expected)
    {
        var viewModel = CreateStatusViewModel([status]);

        await viewModel.ScanAsync();

        Assert.Equal(expected, viewModel.HasEngineUnavailable);
    }

    [Fact]
    public async Task Preview_filter_shows_only_skipped_operations()
    {
        Write("source.json", "{}");
        Write("manual.pdf", "%PDF-1.7");
        var viewModel = CreateViewModel();
        await viewModel.ScanAsync();

        RuleFor(viewModel, SourceFormat.Pdf).SelectedTarget = RuleFor(viewModel, SourceFormat.Pdf).Targets.Single(option => option.Target == ConversionTarget.Skip);
        viewModel.SelectedPreviewFilter = viewModel.PreviewFilters.Single(option =>
            option.Filter == PreviewFilter.Skip);

        Assert.Equal(
            OperationStatus.Skipped,
            Assert.Single(viewModel.VisibleOperations).Operation.Status);
    }

    [Fact]
    public async Task Scan_captures_original_root_before_await()
    {
        var otherRoot = Path.Combine(_rootPath, "other");
        Directory.CreateDirectory(otherRoot);
        var scanner = new CallbackScanner(_rootPath);
        var planner = new RecordingPlanner();
        var viewModel = new MainWindowViewModel(scanner, planner)
        {
            SelectedFolder = _rootPath
        };
        scanner.Callback = () => viewModel.SelectedFolder = otherRoot;

        await viewModel.ScanAsync();

        Assert.Equal(_rootPath, scanner.ReceivedRoot);
        Assert.Equal(_rootPath, planner.ReceivedRoot);
    }

    [Fact]
    public async Task ConvertAsync_converts_ready_json_and_exposes_final_report()
    {
        const string source = """{"name":"Тест 😀"}""";
        Write("users.json", source);
        Write("manual.pdf", "%PDF-1.7");
        var viewModel = CreateViewModel();

        await viewModel.ScanAsync();
        await viewModel.ConvertAsync();

        Assert.True(viewModel.HasFinalReport);
        Assert.Equal(2, viewModel.FinalSucceeded);
        Assert.Equal(1, viewModel.FinalCopied);
        Assert.Equal(0, viewModel.FinalFailed);
        Assert.Equal(0, viewModel.FinalUnavailable);
        Assert.Equal(0, viewModel.FinalSkipped);
        Assert.Equal("Преобразовано · 100%", viewModel.Operations.Single(row =>
            row.Operation.SourceFormat == SourceFormat.Json).Status);
        Assert.True(File.Exists(Path.Combine(_rootPath, "_converted", "users.txt")));
        Assert.Equal(source, File.ReadAllText(Path.Combine(_rootPath, "users.json")));
    }

    [Fact]
    public async Task Conversion_exposes_percent_elapsed_eta_and_final_duration()
    {
        var operations = CreateStatusOperations(
            [OperationStatus.Ready, OperationStatus.Ready]);
        var clock = new ManualTimeProvider();
        MainWindowViewModel? viewModel = null;
        var processor = new TimedProgressProcessor(
            clock,
            () =>
            {
                Assert.NotNull(viewModel);
                Assert.Equal(50, viewModel.ProgressPercent);
                Assert.Equal("1 из 2", viewModel.ProgressCountText);
                Assert.Equal("Прошло: 00:10", viewModel.ElapsedTimeText);
                Assert.Equal("Осталось: ~00:10", viewModel.RemainingTimeText);
                Assert.Equal("status-1.custom", viewModel.CurrentFile);
            });
        viewModel = CreateStatusViewModel(operations, processor, clock);

        await viewModel.ScanAsync();
        await viewModel.ConvertAsync();

        Assert.Equal(100, viewModel.ProgressPercent);
        Assert.Equal("2 из 2", viewModel.ProgressCountText);
        Assert.Equal("Прошло: 00:14", viewModel.ElapsedTimeText);
        Assert.Equal("Время выполнения: 00:14", viewModel.FinalDurationText);

        clock.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal("Прошло: 00:14", viewModel.ElapsedTimeText);
        Assert.Equal("Время выполнения: 00:14", viewModel.FinalDurationText);
    }

    [Fact]
    public async Task New_scan_and_reset_clear_completed_duration()
    {
        var operations = CreateStatusOperations([OperationStatus.Ready]);
        var clock = new ManualTimeProvider();
        var processor = new CallbackProcessor((batch, _, _) =>
        {
            clock.Advance(TimeSpan.FromSeconds(9));
            var result = new ConversionResult(batch[0], OperationStatus.Succeeded, "ok");
            return Task.FromResult(new ConversionSummary(1, 0, 0, 0, 0, 0, [result]));
        });
        var viewModel = CreateStatusViewModel(operations, processor, clock);

        await viewModel.ScanAsync();
        await viewModel.ConvertAsync();
        Assert.Equal("Время выполнения: 00:09", viewModel.FinalDurationText);

        await viewModel.ScanAsync();
        Assert.Equal(string.Empty, viewModel.FinalDurationText);
        Assert.Equal("Прошло: 00:00", viewModel.ElapsedTimeText);

        await viewModel.ConvertAsync();
        Assert.Equal("Время выполнения: 00:09", viewModel.FinalDurationText);

        viewModel.ResetOutputPath();
        Assert.Equal(string.Empty, viewModel.FinalDurationText);
        Assert.Equal("Прошло: 00:00", viewModel.ElapsedTimeText);
        Assert.False(viewModel.HasFinalReport);
    }

    [Fact]
    public async Task Cancellation_freezes_elapsed_and_next_batch_starts_at_zero()
    {
        var operations = CreateStatusOperations([OperationStatus.Ready]);
        var clock = new ManualTimeProvider();
        MainWindowViewModel? viewModel = null;
        var invocation = 0;
        var processor = new CallbackProcessor((batch, _, cancellationToken) =>
        {
            invocation++;
            if (invocation == 1)
            {
                clock.Advance(TimeSpan.FromSeconds(7));
                throw new OperationCanceledException(cancellationToken);
            }

            Assert.NotNull(viewModel);
            Assert.Equal("Прошло: 00:00", viewModel.ElapsedTimeText);
            Assert.Equal(string.Empty, viewModel.FinalDurationText);
            clock.Advance(TimeSpan.FromSeconds(3));
            var result = new ConversionResult(batch[0], OperationStatus.Succeeded, "ok");
            return Task.FromResult(new ConversionSummary(1, 0, 0, 0, 0, 0, [result]));
        });
        viewModel = CreateStatusViewModel(operations, processor, clock);

        await viewModel.ScanAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => viewModel.ConvertAsync());
        Assert.Equal("Прошло: 00:07", viewModel.ElapsedTimeText);
        Assert.Equal("Время выполнения: 00:07", viewModel.FinalDurationText);

        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal("Прошло: 00:07", viewModel.ElapsedTimeText);
        Assert.Equal("Время выполнения: 00:07", viewModel.FinalDurationText);

        await viewModel.ConvertAsync();
        Assert.Equal("Время выполнения: 00:03", viewModel.FinalDurationText);
    }

    [Fact]
    public async Task Failed_conversion_exposes_file_message_code_and_hresult()
    {
        var operations = CreateStatusOperations([OperationStatus.Ready]);
        var failed = new ConversionResult(
            operations[0],
            OperationStatus.Failed,
            "PowerPoint не запустился.",
            new ConversionDiagnostic(
                "office_com_failure",
                HResult: unchecked((int)0x80080005)));
        var summary = new ConversionSummary(0, 0, 1, 0, 0, 0, [failed]);
        var viewModel = CreateStatusViewModel(
            operations,
            new StaticProcessor(summary));

        await viewModel.ScanAsync();
        await viewModel.ConvertAsync();

        var error = Assert.Single(viewModel.ErrorMessages);
        Assert.Contains("status-0.custom", error);
        Assert.Contains("PowerPoint не запустился.", error);
        Assert.Contains("office_com_failure", error);
        Assert.Contains("HRESULT 0x80080005", error);
    }

    [Fact]
    public async Task Mixed_batch_converts_json_without_counting_unavailable_doc_as_failure()
    {
        Write("data.json", """{"name":"Тест"}""");
        Write("legacy.doc", "synthetic");
        Write("manual.pdf", "%PDF-1.7");
        var resolver = new DefaultConversionAdapterResolver(
        [
            new JsonConversionAdapter(new OutputResultValidator()),
            new UnavailableAdapter(SourceFormat.Doc, ConversionTarget.Docx)
        ]);
        var viewModel = new MainWindowViewModel(
            new FileSystemFolderScanner(),
            new ConversionPlanner(resolver),
            new ConversionProcessor(resolver))
        {
            SelectedFolder = _rootPath
        };

        await viewModel.ScanAsync();

        Assert.Equal(1, viewModel.ReadyCount);
        Assert.Equal(2, viewModel.UnavailableCount);
        Assert.Equal(0, viewModel.SkippedCount);
        Assert.Equal(0, viewModel.ErrorCount);
        Assert.Equal("Обработать 1 файл", viewModel.ConvertButtonText);

        await viewModel.ConvertAsync();

        Assert.Equal(1, viewModel.FinalSucceeded);
        Assert.Equal(2, viewModel.FinalUnavailable);
        Assert.Equal(0, viewModel.FinalSkipped);
        Assert.Equal(0, viewModel.FinalFailed);
        Assert.Equal(0, viewModel.ReadyCount);
        Assert.False(viewModel.CanConvert);
        Assert.True(File.Exists(Path.Combine(_rootPath, "_converted", "data.txt")));
        Assert.False(File.Exists(Path.Combine(_rootPath, "_converted", "legacy.docx")));
    }

    [Fact]
    public async Task Final_unavailable_combines_engine_unavailable_and_unsupported()
    {
        var operations = CreateStatusOperations(
        [
            OperationStatus.Ready,
            OperationStatus.EngineUnavailable,
            OperationStatus.Unsupported
        ]);
        var completed = operations.Select(operation =>
        {
            var status = operation.Status == OperationStatus.Ready
                ? OperationStatus.Succeeded
                : operation.Status;
            return new ConversionResult(operation, status, status.ToString());
        }).ToArray();
        var summary = new ConversionSummary(
            Succeeded: 1,
            Conflicts: 0,
            Failed: 0,
            Skipped: 0,
            EngineUnavailable: 1,
            Unsupported: 1,
            completed);
        var viewModel = CreateStatusViewModel(
            operations,
            new StaticProcessor(summary));

        await viewModel.ScanAsync();
        await viewModel.ConvertAsync();

        Assert.Equal(2, viewModel.FinalUnavailable);
        Assert.Equal(0, viewModel.FinalFailed);
    }

    [Theory]
    [InlineData(1, "Обработать 1 файл")]
    [InlineData(2, "Обработать 2 файла")]
    [InlineData(5, "Обработать 5 файлов")]
    [InlineData(11, "Обработать 11 файлов")]
    [InlineData(21, "Обработать 21 файл")]
    public async Task Convert_button_uses_russian_declension(
        int readyCount,
        string expected)
    {
        var viewModel = CreateStatusViewModel(
            Enumerable.Repeat(OperationStatus.Ready, readyCount).ToArray());

        await viewModel.ScanAsync();

        Assert.Equal(expected, viewModel.ConvertButtonText);
    }

    [Fact]
    public void Selected_folder_display_keeps_short_path_and_trims_long_path_from_left()
    {
        const string shortPath = @"C:\Проекты\Тест";
        const string longPath =
            @"C:\Очень длинная родительская папка\Ещё один каталог\PROJECT\Поддержка кастомизации текстов";

        Assert.Equal(shortPath, PathDisplayFormatter.Format(shortPath));
        var display = PathDisplayFormatter.Format(longPath);
        Assert.StartsWith("…\\", display);
        Assert.EndsWith(@"PROJECT\Поддержка кастомизации текстов", display);
        Assert.Contains("Поддержка кастомизации текстов", display);
    }

    [Fact]
    public void Selected_folder_display_preserves_unicode_and_handles_roots()
    {
        var unicodePath =
            @"C:\parent folder with a long name\ещё одна папка\Проект Ω 😀\Финальная папка";

        var exception = Record.Exception(() =>
        {
            Assert.Equal(@"C:\", PathDisplayFormatter.Format(@"C:\"));
            Assert.Equal(@"\\server\share", PathDisplayFormatter.Format(@"\\server\share"));
            Assert.Contains("Финальная папка", PathDisplayFormatter.Format(unicodePath));
            Assert.Contains("Ω", PathDisplayFormatter.Format(unicodePath));
        });

        Assert.Null(exception);
    }

    [Fact]
    public void Selected_folder_display_has_clear_empty_placeholder()
    {
        Assert.Equal(
            PathDisplayFormatter.EmptyPathPlaceholder,
            PathDisplayFormatter.Format(string.Empty));
    }

    [Fact]
    public void Other_extension_breakdown_groups_case_insensitively_and_sorts()
    {
        var files = new[]
        {
            Scanned("one.PDF"),
            Scanned("two.pdf"),
            Scanned("image.PNG"),
            Scanned("nested/second.png"),
            Scanned("readme.TXT"),
            Scanned("LICENSE")
        };

        Assert.Equal(
            "PDF: 2 · PNG: 2 · TXT: 1 · Без расширения: 1",
            ExtensionBreakdownFormatter.Format(files));
    }

    [Fact]
    public void Other_extension_breakdown_does_not_expose_names_or_paths()
    {
        var file = new ScannedFile(
            Path.Combine(_rootPath, "secret-client-name.PDF"),
            Path.Combine("private-folder", "secret-client-name.PDF"),
            SourceFormat.Unknown);

        var breakdown = ExtensionBreakdownFormatter.Format([file]);

        Assert.Equal("PDF: 1", breakdown);
        Assert.DoesNotContain("secret", breakdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private", breakdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_rootPath, breakdown, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Repeated_scan_after_conversion_reports_conflict()
    {
        Write("source.json", "{}");
        var viewModel = CreateViewModel();

        await viewModel.ScanAsync();
        await viewModel.ConvertAsync();
        await viewModel.ScanAsync();

        Assert.Equal(OperationStatus.Conflict, Assert.Single(viewModel.Operations).Operation.Status);
        Assert.False(viewModel.CanConvert);
    }

    [Fact]
    public async Task Folder_change_invalidates_existing_preview()
    {
        Write("source.json", "{}");
        var nextFolder = Path.Combine(_rootPath, "next");
        Directory.CreateDirectory(nextFolder);
        var viewModel = CreateViewModel();
        await viewModel.ScanAsync();

        viewModel.SelectedFolder = nextFolder;

        Assert.Empty(viewModel.Operations);
        Assert.Empty(viewModel.FormatRules);
        Assert.False(viewModel.CanConvert);
    }

    [Fact]
    public async Task Scan_selects_only_ready_operations_by_default()
    {
        var viewModel = CreateStatusViewModel(
            [OperationStatus.Ready, OperationStatus.Skipped, OperationStatus.Conflict,
                OperationStatus.EngineUnavailable]);

        await viewModel.ScanAsync();

        Assert.True(viewModel.Operations[0].IsSelected);
        Assert.All(viewModel.Operations.Skip(1), row => Assert.False(row.IsSelected));
        Assert.Equal(1, viewModel.SelectedReadyCount);
    }

    [Fact]
    public async Task Selection_commands_affect_all_ready_rows_and_filters_preserve_selection()
    {
        var viewModel = CreateStatusViewModel(
            [OperationStatus.Ready, OperationStatus.Ready, OperationStatus.Skipped]);
        await viewModel.ScanAsync();

        viewModel.ClearSelection();
        Assert.Equal("Выберите файлы", viewModel.ConvertButtonText);
        Assert.False(viewModel.CanConvert);

        viewModel.Operations[0].IsSelected = true;
        viewModel.SelectedPreviewFilter = viewModel.PreviewFilters.Single(option =>
            option.Filter == PreviewFilter.Skip);
        Assert.True(viewModel.Operations[0].IsSelected);

        viewModel.InvertSelection();
        Assert.False(viewModel.Operations[0].IsSelected);
        Assert.True(viewModel.Operations[1].IsSelected);

        viewModel.SelectAll();
        Assert.Equal(2, viewModel.SelectedReadyCount);
    }

    [Fact]
    public async Task Convert_passes_only_selected_ready_operations_to_processor()
    {
        var operations = CreateStatusOperations(
            [OperationStatus.Ready, OperationStatus.Ready, OperationStatus.Skipped]);
        var processor = new RecordingProcessor();
        var viewModel = CreateStatusViewModel(operations, processor);
        await viewModel.ScanAsync();
        viewModel.Operations[1].IsSelected = false;

        await viewModel.ConvertAsync();

        Assert.Single(processor.Received);
        Assert.Equal(operations[0].SourcePath, processor.Received[0].SourcePath);
        Assert.Equal(1, viewModel.FinalNotSelected);
        Assert.Equal("Не выбрано", viewModel.Operations[1].Status);
    }

    [Fact]
    public async Task Quoted_source_path_is_trimmed_and_scanned()
    {
        Write("source.json", "{}");
        var viewModel = CreateViewModel();
        viewModel.SelectedFolder = $"  \"{_rootPath}\"  ";

        await viewModel.ScanAsync();

        Assert.Equal(_rootPath, viewModel.SelectedFolder);
        Assert.False(viewModel.HasSourcePathError);
        Assert.Single(viewModel.Operations);
    }

    [Fact]
    public async Task Invalid_manual_source_path_shows_inline_error_and_does_not_scan()
    {
        var viewModel = CreateViewModel();
        viewModel.SelectedFolder = Path.Combine(_rootPath, "missing");

        await viewModel.ScanAsync();

        Assert.True(viewModel.HasSourcePathError);
        Assert.False(viewModel.CanScan);
        Assert.Empty(viewModel.Operations);
    }

    [Theory]
    [InlineData(@"\\server\share\folder", @"\\server\share\folder")]
    [InlineData("  \"\\\\server\\share\\папка\"  ", @"\\server\share\папка")]
    public void Source_path_normalization_preserves_unc_paths(string input, string expected)
    {
        Assert.Equal(expected, MainWindowViewModel.NormalizePathInput(input));
    }

    [Fact]
    public void Output_defaults_manual_edit_and_reset_are_mode_specific()
    {
        var viewModel = CreateViewModel();
        var manualFolder = Path.Combine(_rootPath, "custom-results");
        viewModel.OutputPath = manualFolder;

        viewModel.SelectedOutputMode = OutputMode.Zip;
        Assert.EndsWith("ZletConverter-v0.0.3-results.zip", viewModel.OutputPath);
        var manualZip = Path.Combine(_rootPath, "manual.zip");
        viewModel.OutputPath = manualZip;

        viewModel.SelectedOutputMode = OutputMode.Folder;
        Assert.Equal(manualFolder, viewModel.OutputPath);
        viewModel.ResetOutputPath();
        Assert.Equal(Path.Combine(_rootPath, "_converted"), viewModel.OutputPath);

        viewModel.SelectedOutputMode = OutputMode.Zip;
        Assert.Equal(manualZip, viewModel.OutputPath);
    }

    [Fact]
    public async Task Partial_json_batch_creates_zip_with_only_success_and_preserves_sources()
    {
        var validPath = Write(Path.Combine("nested", "valid.json"), "{\"value\":1}");
        var invalidPath = Write("invalid.json", "{invalid");
        var validHash = Hash(validPath);
        var invalidHash = Hash(invalidPath);
        var zipPath = Path.Combine(_rootPath, "result.zip");
        var viewModel = CreateViewModel();
        viewModel.SelectedOutputMode = OutputMode.Zip;
        viewModel.OutputPath = zipPath;

        await viewModel.ScanAsync();
        var stagingRoot = viewModel.Operations[0].Operation.OutputRootPath;
        await viewModel.ConvertAsync();

        Assert.True(File.Exists(zipPath));
        using var archive = ZipFile.OpenRead(zipPath);
        Assert.Equal(new[] { "nested/valid.txt", "ZletConverter-report.txt" }, archive.Entries.Select(entry => entry.FullName));
        Assert.Equal(1, viewModel.FinalSucceeded);
        Assert.Equal(1, viewModel.FinalFailed);
        Assert.Equal(validHash, Hash(validPath));
        Assert.Equal(invalidHash, Hash(invalidPath));
        Assert.False(Directory.Exists(stagingRoot));
    }

    [Fact]
    public void RuleRowViewModel_single_action_presentation_properties_and_localization()
    {
        var localization = LocalizationService.CreateStandalone(AppLanguage.Russian);
        var singleTargetCapability = FormatCapabilityCatalog.Get(SourceFormat.Odt);
        var singleRule = new RuleRowViewModel(
            singleTargetCapability,
            5,
            ConversionTarget.Skip,
            (_, _) => { },
            localization: localization);

        Assert.True(singleRule.IsSingleAction);
        Assert.False(singleRule.HasMultipleTargets);
        Assert.Equal("Преобразование для этого формата не поддерживается", singleRule.SingleActionReason);
        Assert.Equal("Единственное доступное действие: Пропускаем", singleRule.SingleActionTooltip);

        localization.Apply(AppLanguage.English);
        singleRule.RefreshLocalization();

        Assert.True(singleRule.IsSingleAction);
        Assert.False(singleRule.HasMultipleTargets);
        Assert.Equal("Conversion for this format is not supported", singleRule.SingleActionReason);
        Assert.Equal("Only available action: Skip", singleRule.SingleActionTooltip);

        var multiTargetCapability = FormatCapabilityCatalog.Get(SourceFormat.Doc);
        var multiRule = new RuleRowViewModel(
            multiTargetCapability,
            3,
            ConversionTarget.Docx,
            (_, _) => { },
            localization: localization);

        Assert.False(multiRule.IsSingleAction);
        Assert.True(multiRule.HasMultipleTargets);
    }

    [Fact]
    public void OperationRowViewModel_starts_indeterminate_without_fake_percentage_hold()
    {
        var clock = new ManualTimeProvider();
        var operation = new PlannedOperation(
            Path.Combine(_rootPath, "file.doc"),
            "file.doc",
            SourceFormat.Doc,
            ConversionTarget.Docx,
            ".docx",
            Path.Combine(_rootPath, "_converted", "file.docx"),
            true,
            OperationStatus.Ready,
            "ready");

        var row = new OperationRowViewModel(operation);
        row.BeginExecution(clock.GetTimestamp(), null);

        Assert.Equal("В процессе", row.Status);
        Assert.DoesNotContain("%", row.Status);

        row.BeginExecution(clock.GetTimestamp(), 42);
        Assert.Equal("В процессе · 42%", row.Status);
    }

    [Fact]
    public async Task MainWindowViewModel_report_path_and_open_report_lifecycle()
    {
        Write("doc.json", "{}");
        var viewModel = CreateViewModel();

        await viewModel.ScanAsync();
        Assert.False(viewModel.CanOpenReport);
        Assert.Equal(string.Empty, viewModel.ReportPath);

        await viewModel.ConvertAsync();
        Assert.True(viewModel.HasFinalReport);
        Assert.True(viewModel.CanOpenReport);
        Assert.True(File.Exists(viewModel.ReportPath));
        Assert.EndsWith(".txt", viewModel.ReportPath, StringComparison.OrdinalIgnoreCase);

        viewModel.ResetOutputPath();
        Assert.False(viewModel.CanOpenReport);
        Assert.Equal(string.Empty, viewModel.ReportPath);
    }

    [Theory]
    [InlineData(SourceFormat.Doc, FormatSemanticFamily.Document)]
    [InlineData(SourceFormat.Docx, FormatSemanticFamily.Document)]
    [InlineData(SourceFormat.Odt, FormatSemanticFamily.Document)]
    [InlineData(SourceFormat.Xls, FormatSemanticFamily.Spreadsheet)]
    [InlineData(SourceFormat.Xlsx, FormatSemanticFamily.Spreadsheet)]
    [InlineData(SourceFormat.Ods, FormatSemanticFamily.Spreadsheet)]
    [InlineData(SourceFormat.Ppt, FormatSemanticFamily.Presentation)]
    [InlineData(SourceFormat.Pptx, FormatSemanticFamily.Presentation)]
    [InlineData(SourceFormat.Odp, FormatSemanticFamily.Presentation)]
    [InlineData(SourceFormat.Pdf, FormatSemanticFamily.Pdf)]
    [InlineData(SourceFormat.Json, FormatSemanticFamily.DataCode)]
    [InlineData(SourceFormat.Csv, FormatSemanticFamily.TextData)]
    [InlineData(SourceFormat.Tsv, FormatSemanticFamily.TextData)]
    [InlineData(SourceFormat.Image, FormatSemanticFamily.Image)]
    [InlineData(SourceFormat.Epub, FormatSemanticFamily.Ebook)]
    [InlineData(SourceFormat.Archive, FormatSemanticFamily.Generic)]
    [InlineData(SourceFormat.Unknown, FormatSemanticFamily.Generic)]
    public void SourceFormat_maps_to_expected_semantic_family(SourceFormat format, FormatSemanticFamily expectedFamily)
    {
        Assert.Equal(expectedFamily, format.GetSemanticFamily());
    }

    [Fact]
    public void RuleRowViewModel_exposes_semantic_family_matching_source_format()
    {
        var docxRule = new RuleRowViewModel(FormatCapabilityCatalog.Get(SourceFormat.Docx), 1, ConversionTarget.Copy, (_, _) => { });
        var xlsRule = new RuleRowViewModel(FormatCapabilityCatalog.Get(SourceFormat.Xls), 1, ConversionTarget.Xlsx, (_, _) => { });
        var pptRule = new RuleRowViewModel(FormatCapabilityCatalog.Get(SourceFormat.Ppt), 1, ConversionTarget.Pptx, (_, _) => { });
        var pdfRule = new RuleRowViewModel(FormatCapabilityCatalog.Get(SourceFormat.Pdf), 1, ConversionTarget.Copy, (_, _) => { });
        var jsonRule = new RuleRowViewModel(FormatCapabilityCatalog.Get(SourceFormat.Json), 1, ConversionTarget.Txt, (_, _) => { });
        var unknownRule = new RuleRowViewModel(FormatCapabilityCatalog.Get(SourceFormat.Unknown), 1, ConversionTarget.Skip, (_, _) => { });

        Assert.Equal(FormatSemanticFamily.Document, docxRule.SemanticFamily);
        Assert.Equal(FormatSemanticFamily.Spreadsheet, xlsRule.SemanticFamily);
        Assert.Equal(FormatSemanticFamily.Presentation, pptRule.SemanticFamily);
        Assert.Equal(FormatSemanticFamily.Pdf, pdfRule.SemanticFamily);
        Assert.Equal(FormatSemanticFamily.DataCode, jsonRule.SemanticFamily);
        Assert.Equal(FormatSemanticFamily.Generic, unknownRule.SemanticFamily);
    }

    [Fact]
    public void MainWindowViewModel_exposes_office_availability_flags()
    {
        var allAvailable = new StubOfficeCapabilityDetector(
            new(OfficeApplicationKind.Word, true),
            new(OfficeApplicationKind.Excel, true),
            new(OfficeApplicationKind.PowerPoint, true));
        var resolver = new DefaultConversionAdapterResolver();
        var vmAvailable = new MainWindowViewModel(
            new FileSystemFolderScanner(),
            new ConversionPlanner(resolver),
            officeCapabilityDetector: allAvailable);

        Assert.True(vmAvailable.IsWordOfficeAvailable);
        Assert.True(vmAvailable.IsExcelOfficeAvailable);
        Assert.True(vmAvailable.IsPowerPointOfficeAvailable);

        var noneAvailable = new StubOfficeCapabilityDetector(
            new(OfficeApplicationKind.Word, false),
            new(OfficeApplicationKind.Excel, false),
            new(OfficeApplicationKind.PowerPoint, false));
        var vmUnavailable = new MainWindowViewModel(
            new FileSystemFolderScanner(),
            new ConversionPlanner(resolver),
            officeCapabilityDetector: noneAvailable);

        Assert.False(vmUnavailable.IsWordOfficeAvailable);
        Assert.False(vmUnavailable.IsExcelOfficeAvailable);
        Assert.False(vmUnavailable.IsPowerPointOfficeAvailable);
    }

    [Fact]
    public async Task Known_format_row_filters_preview_operations()
    {
        Write("manual.pdf", "%PDF-1.7");
        Write("data.csv", "a,b,c");
        Write("presentation.pptx", "PK");
        var viewModel = CreateViewModel();
        await viewModel.ScanAsync();

        var pdfRule = Assert.Single(viewModel.FormatRules.Where(r => r.SourceFormat == SourceFormat.Pdf));
        viewModel.SelectRuleFilter(pdfRule);

        Assert.True(pdfRule.IsSelected);
        Assert.Equal(pdfRule, viewModel.SelectedRule);
        Assert.Equal(PreviewFilter.Format, viewModel.SelectedPreviewFilter.Filter);
        Assert.Equal(SourceFormat.Pdf, viewModel.SelectedPreviewFilter.Format);
        var visible = Assert.Single(viewModel.VisibleOperations);
        Assert.Equal(SourceFormat.Pdf, visible.Operation.SourceFormat);
        Assert.True(viewModel.IsPreviewFiltered);
    }

    [Fact]
    public async Task Other_row_filters_exact_unknown_operations()
    {
        Write("manual.pdf", "%PDF-1.7");
        Write("test-unknown.abc", "sample unknown data");
        Write("README-fixtures.txt", "readme text");
        var viewModel = CreateViewModel();
        await viewModel.ScanAsync();

        var otherRule = Assert.Single(viewModel.FormatRules.Where(r => r.SourceFormat == SourceFormat.Unknown));
        viewModel.SelectRuleFilter(otherRule);

        Assert.True(otherRule.IsSelected);
        Assert.Equal(otherRule, viewModel.SelectedRule);
        var visible = viewModel.VisibleOperations.ToArray();
        Assert.Equal(2, visible.Length);
        Assert.All(visible, op => Assert.Equal(SourceFormat.Unknown, op.Operation.SourceFormat));
        Assert.Contains(visible, op => op.Operation.RelativePath.EndsWith(".abc", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(visible, op => op.Operation.RelativePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Switching_rule_filters_updates_preview_and_active_state()
    {
        Write("manual.pdf", "%PDF-1.7");
        Write("data.csv", "a,b,c");
        Write("presentation.pptx", "PK");
        var viewModel = CreateViewModel();
        await viewModel.ScanAsync();

        var pdfRule = viewModel.FormatRules.Single(r => r.SourceFormat == SourceFormat.Pdf);
        var csvRule = viewModel.FormatRules.Single(r => r.SourceFormat == SourceFormat.Csv);
        var pptxRule = viewModel.FormatRules.Single(r => r.SourceFormat == SourceFormat.Pptx);

        viewModel.SelectRuleFilter(pdfRule);
        Assert.True(pdfRule.IsSelected);
        Assert.False(csvRule.IsSelected);
        Assert.Equal(SourceFormat.Pdf, Assert.Single(viewModel.VisibleOperations).Operation.SourceFormat);

        viewModel.SelectRuleFilter(csvRule);
        Assert.False(pdfRule.IsSelected);
        Assert.True(csvRule.IsSelected);
        Assert.Equal(SourceFormat.Csv, Assert.Single(viewModel.VisibleOperations).Operation.SourceFormat);

        viewModel.SelectRuleFilter(pptxRule);
        Assert.False(csvRule.IsSelected);
        Assert.True(pptxRule.IsSelected);
        Assert.Equal(SourceFormat.Pptx, Assert.Single(viewModel.VisibleOperations).Operation.SourceFormat);
    }

    [Fact]
    public async Task Reclicking_active_rule_toggles_filter_off_to_all()
    {
        Write("manual.pdf", "%PDF-1.7");
        Write("data.csv", "a,b,c");
        var viewModel = CreateViewModel();
        await viewModel.ScanAsync();

        var pdfRule = viewModel.FormatRules.Single(r => r.SourceFormat == SourceFormat.Pdf);
        viewModel.ToggleRuleFilter(pdfRule);
        Assert.True(pdfRule.IsSelected);
        Assert.Single(viewModel.VisibleOperations);

        viewModel.ToggleRuleFilter(pdfRule);
        Assert.False(pdfRule.IsSelected);
        Assert.Null(viewModel.SelectedRule);
        Assert.Equal(PreviewFilter.All, viewModel.SelectedPreviewFilter.Filter);
        Assert.Equal(2, viewModel.VisibleOperations.Count());
        Assert.False(viewModel.IsPreviewFiltered);
    }

    [Fact]
    public async Task Selecting_all_filter_clears_active_rule()
    {
        Write("manual.pdf", "%PDF-1.7");
        Write("data.csv", "a,b,c");
        var viewModel = CreateViewModel();
        await viewModel.ScanAsync();

        var pdfRule = viewModel.FormatRules.Single(r => r.SourceFormat == SourceFormat.Pdf);
        viewModel.SelectRuleFilter(pdfRule);
        Assert.True(pdfRule.IsSelected);

        viewModel.SelectedPreviewFilter = viewModel.PreviewFilters.Single(o => o.Filter == PreviewFilter.All);
        Assert.Null(viewModel.SelectedRule);
        Assert.False(pdfRule.IsSelected);
        Assert.Equal(2, viewModel.VisibleOperations.Count());
    }

    [Fact]
    public async Task Selection_checkbox_states_survive_preview_filtering()
    {
        Write("manual1.pdf", "%PDF-1.7");
        Write("manual2.pdf", "%PDF-1.7");
        Write("data.csv", "a,b,c");
        var viewModel = CreateViewModel();
        await viewModel.ScanAsync();

        viewModel.ClearSelection();
        viewModel.Operations[0].IsSelected = true;
        viewModel.Operations[2].IsSelected = true;
        Assert.Equal(2, viewModel.SelectedReadyCount);

        var pdfRule = viewModel.FormatRules.Single(r => r.SourceFormat == SourceFormat.Pdf);
        viewModel.SelectRuleFilter(pdfRule);
        Assert.Equal(2, viewModel.SelectedReadyCount);

        var csvRule = viewModel.FormatRules.Single(r => r.SourceFormat == SourceFormat.Csv);
        viewModel.SelectRuleFilter(csvRule);
        Assert.Equal(2, viewModel.SelectedReadyCount);

        viewModel.ClearRuleFilter();
        Assert.Equal(2, viewModel.SelectedReadyCount);
        Assert.True(viewModel.Operations[0].IsSelected);
        Assert.False(viewModel.Operations[1].IsSelected);
        Assert.True(viewModel.Operations[2].IsSelected);
    }

    [Fact]
    public async Task SelectAll_ClearSelection_InvertSelection_preserve_global_semantics_under_filter()
    {
        Write("manual1.pdf", "%PDF-1.7");
        Write("manual2.pdf", "%PDF-1.7");
        Write("data.csv", "a,b,c");
        var viewModel = CreateViewModel();
        await viewModel.ScanAsync();

        var pdfRule = viewModel.FormatRules.Single(r => r.SourceFormat == SourceFormat.Pdf);
        viewModel.SelectRuleFilter(pdfRule);
        Assert.Equal(2, viewModel.VisibleOperations.Count());

        viewModel.ClearSelection();
        Assert.All(viewModel.Operations, op => Assert.False(op.IsSelected));

        viewModel.SelectAll();
        Assert.All(viewModel.Operations, op => Assert.True(op.IsSelected));

        viewModel.InvertSelection();
        Assert.All(viewModel.Operations, op => Assert.False(op.IsSelected));
    }

    [Fact]
    public async Task ConvertAsync_processes_all_checked_operations_not_just_filtered_view()
    {
        Write("manual1.pdf", "%PDF-1.7");
        Write("manual2.pdf", "%PDF-1.7");
        Write("data.csv", "a,b,c");
        var recordingProcessor = new RecordingProcessor();
        var resolver = new DefaultConversionAdapterResolver();
        var viewModel = new MainWindowViewModel(
            new FileSystemFolderScanner(),
            new ConversionPlanner(resolver),
            conversionProcessor: recordingProcessor);
        viewModel.SelectedFolder = _rootPath;
        await viewModel.ScanAsync();

        Assert.Equal(3, viewModel.SelectedReadyCount);

        var pdfRule = viewModel.FormatRules.Single(r => r.SourceFormat == SourceFormat.Pdf);
        viewModel.SelectRuleFilter(pdfRule);
        Assert.Equal(2, viewModel.VisibleOperations.Count());

        await viewModel.ConvertAsync();
        Assert.Equal(3, recordingProcessor.Received.Count);
    }

    [Fact]
    public async Task Filtered_count_summary_localizes_correctly_in_ru_and_en()
    {
        Write("manual1.pdf", "%PDF-1.7");
        Write("manual2.pdf", "%PDF-1.7");
        Write("data.csv", "a,b,c");
        var localization = LocalizationService.Current;
        localization.Apply(AppLanguage.Russian);
        var viewModel = CreateViewModel();
        await viewModel.ScanAsync();

        Assert.False(viewModel.IsPreviewFiltered);

        var pdfRule = viewModel.FormatRules.Single(r => r.SourceFormat == SourceFormat.Pdf);
        viewModel.SelectRuleFilter(pdfRule);
        Assert.True(viewModel.IsPreviewFiltered);
        Assert.Equal("Показано: 2 из 3", viewModel.FilteredCountSummary);

        localization.Apply(AppLanguage.English);
        Assert.Equal("Shown: 2 of 3", viewModel.FilteredCountSummary);

        localization.Apply(AppLanguage.Russian);
    }

    [Fact]
    public async Task ResetPreviewFilter_clears_active_filter_and_rule_and_restores_all_items()
    {
        Write("manual.pdf", "%PDF-1.7");
        Write("data.csv", "a,b,c");
        Write("document.docx", "PK");
        var viewModel = CreateViewModel();
        await viewModel.ScanAsync();

        var pdfRule = viewModel.FormatRules.Single(r => r.SourceFormat == SourceFormat.Pdf);
        viewModel.SelectRuleFilter(pdfRule);
        Assert.True(viewModel.IsPreviewFiltered);
        Assert.Single(viewModel.VisibleOperations);
        Assert.True(pdfRule.IsSelected);
        Assert.Equal(pdfRule, viewModel.SelectedRule);

        viewModel.ResetPreviewFilter();

        Assert.False(viewModel.IsPreviewFiltered);
        Assert.Equal(PreviewFilter.All, viewModel.SelectedPreviewFilter.Filter);
        Assert.Null(viewModel.SelectedRule);
        Assert.False(pdfRule.IsSelected);
        Assert.Equal(3, viewModel.VisibleOperations.Count());
    }

    [Fact]
    public async Task ResetPreviewFilter_clears_status_based_preview_filter()
    {
        Write("manual.pdf", "%PDF-1.7");
        Write("data.csv", "a,b,c");
        var viewModel = CreateViewModel();
        await viewModel.ScanAsync();

        var convertFilter = viewModel.PreviewFilters.First(f => f.Filter == PreviewFilter.Convert);
        viewModel.SelectedPreviewFilter = convertFilter;
        Assert.True(viewModel.IsPreviewFiltered);

        viewModel.ResetPreviewFilter();
        Assert.False(viewModel.IsPreviewFiltered);
        Assert.Equal(PreviewFilter.All, viewModel.SelectedPreviewFilter.Filter);
    }

    [Fact]
    public void ShowAll_string_resource_exists_in_russian_and_english()
    {
        var localization = LocalizationService.Current;
        localization.Apply(AppLanguage.Russian);
        Assert.Equal("Показать все", localization.Get("ShowAll"));

        localization.Apply(AppLanguage.English);
        Assert.Equal("Show all", localization.Get("ShowAll"));

        localization.Apply(AppLanguage.Russian);
    }

    [Fact]
    public async Task Preview_sort_by_size_uses_numeric_bytes_not_string()
    {
        // Sizes: 1 MB, 10 MB, 2 MB
        // In string comparison: "1 MB" < "10 MB" < "2 MB"
        // In numeric comparison: 1 MB < 2 MB < 10 MB
        var ops = new[]
        {
            new PlannedOperation(Path.Combine(_rootPath, "file-1mb.bin"), "file-1mb.bin", SourceFormat.Unknown, ConversionTarget.Skip, "", "", false, OperationStatus.Ready, "", SourceSizeBytes: 1024 * 1024),
            new PlannedOperation(Path.Combine(_rootPath, "file-10mb.bin"), "file-10mb.bin", SourceFormat.Unknown, ConversionTarget.Skip, "", "", false, OperationStatus.Ready, "", SourceSizeBytes: 10 * 1024 * 1024),
            new PlannedOperation(Path.Combine(_rootPath, "file-2mb.bin"), "file-2mb.bin", SourceFormat.Unknown, ConversionTarget.Skip, "", "", false, OperationStatus.Ready, "", SourceSizeBytes: 2 * 1024 * 1024),
        };

        var viewModel = CreateStatusViewModel(ops);
        await viewModel.ScanAsync();
        viewModel.SortBy(PreviewSortColumn.Size, ListSortDirection.Ascending);

        var visibleAsc = viewModel.VisibleOperations.ToArray();
        Assert.Equal("file-1mb.bin", visibleAsc[0].FilePath);
        Assert.Equal("file-2mb.bin", visibleAsc[1].FilePath);
        Assert.Equal("file-10mb.bin", visibleAsc[2].FilePath);

        viewModel.SortBy(PreviewSortColumn.Size, ListSortDirection.Descending);
        var visibleDesc = viewModel.VisibleOperations.ToArray();
        Assert.Equal("file-10mb.bin", visibleDesc[0].FilePath);
        Assert.Equal("file-2mb.bin", visibleDesc[1].FilePath);
        Assert.Equal("file-1mb.bin", visibleDesc[2].FilePath);
    }

    [Fact]
    public async Task Preview_sort_by_time_uses_numeric_duration_and_handles_nulls_safely()
    {
        var timeProvider = new ManualTimeProvider();
        var ops = new[]
        {
            new PlannedOperation(Path.Combine(_rootPath, "fast.bin"), "fast.bin", SourceFormat.Unknown, ConversionTarget.Skip, "", "", false, OperationStatus.Ready, ""),
            new PlannedOperation(Path.Combine(_rootPath, "slow.bin"), "slow.bin", SourceFormat.Unknown, ConversionTarget.Skip, "", "", false, OperationStatus.Ready, ""),
            new PlannedOperation(Path.Combine(_rootPath, "notime.bin"), "notime.bin", SourceFormat.Unknown, ConversionTarget.Skip, "", "", false, OperationStatus.Ready, ""),
        };

        var viewModel = CreateStatusViewModel(ops, timeProvider: timeProvider);
        await viewModel.ScanAsync();

        // Fast row takes 1 second
        var fastRow = viewModel.Operations[0];
        fastRow.BeginExecution(timeProvider.GetTimestamp());
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        fastRow.CompleteExecution(new ConversionResult(fastRow.Operation, OperationStatus.Succeeded, "ok"), timeProvider, timeProvider.GetTimestamp());

        // Slow row takes 10 seconds
        var slowRow = viewModel.Operations[1];
        slowRow.BeginExecution(timeProvider.GetTimestamp());
        timeProvider.Advance(TimeSpan.FromSeconds(10));
        slowRow.CompleteExecution(new ConversionResult(slowRow.Operation, OperationStatus.Succeeded, "ok"), timeProvider, timeProvider.GetTimestamp());

        // notime.bin has null execution elapsed

        // Sort ascending: fast (1s) < slow (10s), then unmeasured
        viewModel.SortBy(PreviewSortColumn.Time, ListSortDirection.Ascending);
        var visibleAsc = viewModel.VisibleOperations.ToArray();
        Assert.Equal("fast.bin", visibleAsc[0].FilePath);
        Assert.Equal("slow.bin", visibleAsc[1].FilePath);
        Assert.Equal("notime.bin", visibleAsc[2].FilePath);

        // Sort descending: slow (10s) > fast (1s), then unmeasured
        viewModel.SortBy(PreviewSortColumn.Time, ListSortDirection.Descending);
        var visibleDesc = viewModel.VisibleOperations.ToArray();
        Assert.Equal("slow.bin", visibleDesc[0].FilePath);
        Assert.Equal("fast.bin", visibleDesc[1].FilePath);
        Assert.Equal("notime.bin", visibleDesc[2].FilePath);
    }

    [Fact]
    public async Task Preview_sort_by_source_file_is_case_insensitive_and_deterministic()
    {
        var ops = new[]
        {
            new PlannedOperation(Path.Combine(_rootPath, "b.txt"), "b.txt", SourceFormat.Unknown, ConversionTarget.Skip, "", "", false, OperationStatus.Ready, ""),
            new PlannedOperation(Path.Combine(_rootPath, "A.txt"), "A.txt", SourceFormat.Unknown, ConversionTarget.Skip, "", "", false, OperationStatus.Ready, ""),
            new PlannedOperation(Path.Combine(_rootPath, "c.txt"), "c.txt", SourceFormat.Unknown, ConversionTarget.Skip, "", "", false, OperationStatus.Ready, ""),
        };

        var viewModel = CreateStatusViewModel(ops);
        await viewModel.ScanAsync();
        viewModel.SortBy(PreviewSortColumn.SourceFile, ListSortDirection.Ascending);

        var visibleAsc = viewModel.VisibleOperations.ToArray();
        Assert.Equal("A.txt", visibleAsc[0].FilePath);
        Assert.Equal("b.txt", visibleAsc[1].FilePath);
        Assert.Equal("c.txt", visibleAsc[2].FilePath);

        viewModel.SortBy(PreviewSortColumn.SourceFile, ListSortDirection.Descending);
        var visibleDesc = viewModel.VisibleOperations.ToArray();
        Assert.Equal("c.txt", visibleDesc[0].FilePath);
        Assert.Equal("b.txt", visibleDesc[1].FilePath);
        Assert.Equal("A.txt", visibleDesc[2].FilePath);
    }

    [Fact]
    public async Task Preview_sort_by_action_and_status_uses_stable_internal_values_independent_of_locale()
    {
        var ops = new[]
        {
            new PlannedOperation(Path.Combine(_rootPath, "failed.bin"), "failed.bin", SourceFormat.Unknown, ConversionTarget.Skip, "", "", false, OperationStatus.Failed, ""),
            new PlannedOperation(Path.Combine(_rootPath, "ready.bin"), "ready.bin", SourceFormat.Unknown, ConversionTarget.Skip, "", "", false, OperationStatus.Ready, ""),
            new PlannedOperation(Path.Combine(_rootPath, "succeeded.bin"), "succeeded.bin", SourceFormat.Unknown, ConversionTarget.Skip, "", "", false, OperationStatus.Succeeded, ""),
        };

        var localization = LocalizationService.Current;
        localization.Apply(AppLanguage.Russian);
        var viewModel = CreateStatusViewModel(ops);
        await viewModel.ScanAsync();

        viewModel.SortBy(PreviewSortColumn.Status, ListSortDirection.Ascending);
        var ruOrder = viewModel.VisibleOperations.Select(r => r.FilePath).ToArray();

        localization.Apply(AppLanguage.English);
        var enOrder = viewModel.VisibleOperations.Select(r => r.FilePath).ToArray();

        Assert.Equal(ruOrder, enOrder);
        Assert.Equal("ready.bin", ruOrder[0]);
        Assert.Equal("succeeded.bin", ruOrder[1]);
        Assert.Equal("failed.bin", ruOrder[2]);

        localization.Apply(AppLanguage.Russian);
    }

    [Fact]
    public async Task Preview_sort_by_result_is_deterministic()
    {
        var ops = new[]
        {
            new PlannedOperation(Path.Combine(_rootPath, "c.doc"), "c.doc", SourceFormat.Doc, ConversionTarget.Docx, ".docx", Path.Combine(_rootPath, "c.docx"), true, OperationStatus.Ready, "", ResultRelativePath: "out_c.docx"),
            new PlannedOperation(Path.Combine(_rootPath, "a.doc"), "a.doc", SourceFormat.Doc, ConversionTarget.Docx, ".docx", Path.Combine(_rootPath, "a.docx"), true, OperationStatus.Ready, "", ResultRelativePath: "out_a.docx"),
            new PlannedOperation(Path.Combine(_rootPath, "b.doc"), "b.doc", SourceFormat.Doc, ConversionTarget.Docx, ".docx", Path.Combine(_rootPath, "b.docx"), true, OperationStatus.Ready, "", ResultRelativePath: "out_b.docx"),
        };

        var viewModel = CreateStatusViewModel(ops);
        await viewModel.ScanAsync();
        viewModel.SortBy(PreviewSortColumn.Result, ListSortDirection.Ascending);

        var visibleAsc = viewModel.VisibleOperations.ToArray();
        Assert.Equal("out_a.docx", visibleAsc[0].ResultPath);
        Assert.Equal("out_b.docx", visibleAsc[1].ResultPath);
        Assert.Equal("out_c.docx", visibleAsc[2].ResultPath);

        viewModel.SortBy(PreviewSortColumn.Result, ListSortDirection.Descending);
        var visibleDesc = viewModel.VisibleOperations.ToArray();
        Assert.Equal("out_c.docx", visibleDesc[0].ResultPath);
        Assert.Equal("out_b.docx", visibleDesc[1].ResultPath);
        Assert.Equal("out_a.docx", visibleDesc[2].ResultPath);
    }

    [Fact]
    public async Task Preview_sorting_toggles_asc_then_desc()
    {
        var ops = new[]
        {
            new PlannedOperation(Path.Combine(_rootPath, "b.txt"), "b.txt", SourceFormat.Unknown, ConversionTarget.Skip, "", "", false, OperationStatus.Ready, "", SourceSizeBytes: 100),
            new PlannedOperation(Path.Combine(_rootPath, "a.txt"), "a.txt", SourceFormat.Unknown, ConversionTarget.Skip, "", "", false, OperationStatus.Ready, "", SourceSizeBytes: 200),
        };

        var viewModel = CreateStatusViewModel(ops);
        await viewModel.ScanAsync();
        Assert.Equal(PreviewSortColumn.None, viewModel.CurrentSortColumn);

        viewModel.SortBy(PreviewSortColumn.Size);
        Assert.Equal(PreviewSortColumn.Size, viewModel.CurrentSortColumn);
        Assert.Equal(ListSortDirection.Ascending, viewModel.CurrentSortDirection);
        Assert.Equal("b.txt", viewModel.VisibleOperations.First().FilePath);

        viewModel.SortBy(PreviewSortColumn.Size);
        Assert.Equal(PreviewSortColumn.Size, viewModel.CurrentSortColumn);
        Assert.Equal(ListSortDirection.Descending, viewModel.CurrentSortDirection);
        Assert.Equal("a.txt", viewModel.VisibleOperations.First().FilePath);

        viewModel.SortBy(PreviewSortColumn.SourceFile);
        Assert.Equal(PreviewSortColumn.SourceFile, viewModel.CurrentSortColumn);
        Assert.Equal(ListSortDirection.Ascending, viewModel.CurrentSortDirection);
        Assert.Equal("a.txt", viewModel.VisibleOperations.First().FilePath);
    }

    [Fact]
    public async Task Filter_and_sort_interaction_preserves_sort_across_filter_switches_and_reset()
    {
        Write("p1.pdf", "%PDF-1.7 A");
        Write("p2.pdf", "%PDF-1.7 AAA");
        Write("c1.csv", "1");
        Write("c2.csv", "12345");
        var viewModel = CreateViewModel();
        await viewModel.ScanAsync();

        var pdfRule = viewModel.FormatRules.Single(r => r.SourceFormat == SourceFormat.Pdf);
        var csvRule = viewModel.FormatRules.Single(r => r.SourceFormat == SourceFormat.Csv);

        // 1. Filter to PDF
        viewModel.SelectRuleFilter(pdfRule);
        Assert.Equal(2, viewModel.VisibleOperations.Count());

        // 2. Sort by Size descending
        viewModel.SortBy(PreviewSortColumn.Size, ListSortDirection.Descending);
        var pdfSorted = viewModel.VisibleOperations.ToArray();
        Assert.Equal("p2.pdf", pdfSorted[0].FilePath);
        Assert.Equal("p1.pdf", pdfSorted[1].FilePath);

        // 3. Switch filter to CSV -> sort remains Size descending!
        viewModel.SelectRuleFilter(csvRule);
        var csvSorted = viewModel.VisibleOperations.ToArray();
        Assert.Equal("c2.csv", csvSorted[0].FilePath);
        Assert.Equal("c1.csv", csvSorted[1].FilePath);

        // 4. Click Show all (ResetPreviewFilter) -> all 4 files returned, still Size descending!
        viewModel.ResetPreviewFilter();
        var allSorted = viewModel.VisibleOperations.ToArray();
        Assert.Equal(4, allSorted.Length);
        Assert.Equal(PreviewSortColumn.Size, viewModel.CurrentSortColumn);
        Assert.Equal(ListSortDirection.Descending, viewModel.CurrentSortDirection);
        for (int i = 0; i < allSorted.Length - 1; i++)
        {
            Assert.True(allSorted[i].SourceSizeBytes >= allSorted[i + 1].SourceSizeBytes);
        }
    }

    [Fact]
    public async Task Preview_sorting_and_filtering_preserves_selection_and_execution_set()
    {
        Write("p1.pdf", "%PDF-1.7");
        Write("p2.pdf", "%PDF-1.7");
        Write("c1.csv", "a");
        Write("c2.csv", "b");
        var recordingProcessor = new RecordingProcessor();
        var resolver = new DefaultConversionAdapterResolver();
        var viewModel = new MainWindowViewModel(
            new FileSystemFolderScanner(),
            new ConversionPlanner(resolver),
            conversionProcessor: recordingProcessor)
        {
            SelectedFolder = _rootPath
        };
        await viewModel.ScanAsync();

        // Check 2 files only
        viewModel.ClearSelection();
        viewModel.Operations[0].IsSelected = true;
        viewModel.Operations[2].IsSelected = true;
        Assert.Equal(2, viewModel.SelectedReadyCount);

        // Filter to PDF -> only 1 of the selected files is visible
        var pdfRule = viewModel.FormatRules.Single(r => r.SourceFormat == SourceFormat.Pdf);
        viewModel.SelectRuleFilter(pdfRule);
        Assert.Equal(2, viewModel.VisibleOperations.Count());
        Assert.Equal(2, viewModel.SelectedReadyCount); // Total selected is still 2!

        // Sort by Size descending
        viewModel.SortBy(PreviewSortColumn.Size, ListSortDirection.Descending);
        Assert.Equal(2, viewModel.SelectedReadyCount);

        // Clear filter
        viewModel.ResetPreviewFilter();
        Assert.Equal(4, viewModel.VisibleOperations.Count());
        Assert.Equal(2, viewModel.SelectedReadyCount);
        Assert.True(viewModel.Operations[0].IsSelected);
        Assert.False(viewModel.Operations[1].IsSelected);
        Assert.True(viewModel.Operations[2].IsSelected);
        Assert.False(viewModel.Operations[3].IsSelected);

        // Global SelectAll works on all 4 files even under sort
        viewModel.SelectAll();
        Assert.Equal(4, viewModel.SelectedReadyCount);
        Assert.All(viewModel.Operations, op => Assert.True(op.IsSelected));

        // Global ClearSelection works on all 4 files
        viewModel.ClearSelection();
        Assert.Equal(0, viewModel.SelectedReadyCount);
        Assert.All(viewModel.Operations, op => Assert.False(op.IsSelected));

        // Global InvertSelection works on all 4 files
        viewModel.InvertSelection();
        Assert.Equal(4, viewModel.SelectedReadyCount);
        Assert.All(viewModel.Operations, op => Assert.True(op.IsSelected));

        // ConvertAsync processes all checked files
        await viewModel.ConvertAsync();
        Assert.Equal(4, recordingProcessor.Received.Count);
    }

    [Fact]
    public async Task Preview_sort_by_action_groups_identical_actions_together_across_different_source_formats()
    {
        var ops = new[]
        {
            new PlannedOperation(Path.Combine(_rootPath, "data.csv"), "data.csv", SourceFormat.Csv, ConversionTarget.Skip, "", "", false, OperationStatus.Ready, ""),
            new PlannedOperation(Path.Combine(_rootPath, "doc.pdf"), "doc.pdf", SourceFormat.Pdf, ConversionTarget.Markdown, ".md", Path.Combine(_rootPath, "doc.md"), true, OperationStatus.Ready, ""),
            new PlannedOperation(Path.Combine(_rootPath, "skip.pdf"), "skip.pdf", SourceFormat.Pdf, ConversionTarget.Skip, "", "", false, OperationStatus.Ready, ""),
            new PlannedOperation(Path.Combine(_rootPath, "file.txt"), "file.txt", SourceFormat.Unknown, ConversionTarget.Copy, "", Path.Combine(_rootPath, "file.txt"), true, OperationStatus.Ready, ""),
            new PlannedOperation(Path.Combine(_rootPath, "copy.csv"), "copy.csv", SourceFormat.Csv, ConversionTarget.Copy, "", Path.Combine(_rootPath, "copy.csv"), true, OperationStatus.Ready, ""),
        };

        var viewModel = CreateStatusViewModel(ops);
        await viewModel.ScanAsync();

        // Sort Action Ascending
        viewModel.SortBy(PreviewSortColumn.Action, ListSortDirection.Ascending);
        var asc = viewModel.VisibleOperations.Select(r => (r.FilePath, r.Operation.Target)).ToArray();

        // Verify that all Copy actions are together
        var copyIndices = asc.Select((item, idx) => (item, idx)).Where(x => x.item.Target == ConversionTarget.Copy).Select(x => x.idx).ToArray();
        Assert.Equal(2, copyIndices.Length);
        Assert.Equal(1, copyIndices[1] - copyIndices[0]);

        // Verify that all Skip actions are together (not separated by Markdown)
        var skipIndices = asc.Select((item, idx) => (item, idx)).Where(x => x.item.Target == ConversionTarget.Skip).Select(x => x.idx).ToArray();
        Assert.Equal(2, skipIndices.Length);
        Assert.Equal(1, skipIndices[1] - skipIndices[0]);

        // Sort Action Descending
        viewModel.SortBy(PreviewSortColumn.Action, ListSortDirection.Descending);
        var desc = viewModel.VisibleOperations.Select(r => (r.FilePath, r.Operation.Target)).ToArray();

        var descSkipIndices = desc.Select((item, idx) => (item, idx)).Where(x => x.item.Target == ConversionTarget.Skip).Select(x => x.idx).ToArray();
        Assert.Equal(2, descSkipIndices.Length);
        Assert.Equal(1, descSkipIndices[1] - descSkipIndices[0]);

        var descCopyIndices = desc.Select((item, idx) => (item, idx)).Where(x => x.item.Target == ConversionTarget.Copy).Select(x => x.idx).ToArray();
        Assert.Equal(2, descCopyIndices.Length);
        Assert.Equal(1, descCopyIndices[1] - descCopyIndices[0]);
    }

    [Fact]
    public async Task Preview_sort_by_status_re_sorts_immediately_when_not_selected_row_checkbox_restored()
    {
        var ops = new[]
        {
            new PlannedOperation(Path.Combine(_rootPath, "file1.bin"), "file1.bin", SourceFormat.Unknown, ConversionTarget.Skip, "", "", false, OperationStatus.Ready, ""),
            new PlannedOperation(Path.Combine(_rootPath, "file2.bin"), "file2.bin", SourceFormat.Unknown, ConversionTarget.Skip, "", "", false, OperationStatus.Ready, ""),
        };

        var viewModel = CreateStatusViewModel(ops);
        await viewModel.ScanAsync();

        // Simulate partial conversion where file1 was not selected
        var row1 = viewModel.Operations[0];
        var row2 = viewModel.Operations[1];
        row1.MarkNotSelected();
        Assert.True(row1.IsNotSelected);
        Assert.False(row2.IsNotSelected);

        // Sort by Status ascending -> row2 (Ready, rank 1) must be first, row1 (NotSelected, rank 10) must be last
        viewModel.SortBy(PreviewSortColumn.Status, ListSortDirection.Ascending);
        Assert.Equal("file2.bin", viewModel.VisibleOperations.First().FilePath);
        Assert.Equal("file1.bin", viewModel.VisibleOperations.Last().FilePath);

        // Check/restore file1 -> its effective status changes from NotSelected back to Ready
        row1.IsSelected = true;
        Assert.False(row1.IsNotSelected);

        // Verify visible operations immediately re-sorted without manual rescan:
        // Now both are Ready (rank 1), file1.bin comes before file2.bin alphabetically
        var visible = viewModel.VisibleOperations.ToArray();
        Assert.Equal("file1.bin", visible[0].FilePath);
        Assert.Equal("file2.bin", visible[1].FilePath);
    }

    [Fact]
    public async Task Preview_sort_by_time_re_sorts_during_live_elapsed_updates()
    {
        var clock = new ManualTimeProvider();
        var ops = new[]
        {
            new PlannedOperation(Path.Combine(_rootPath, "a.txt"), "a.txt", SourceFormat.Unknown, ConversionTarget.Docx, ".docx", Path.Combine(_rootPath, "a.docx"), true, OperationStatus.Ready, ""),
            new PlannedOperation(Path.Combine(_rootPath, "b.txt"), "b.txt", SourceFormat.Unknown, ConversionTarget.Docx, ".docx", Path.Combine(_rootPath, "b.docx"), true, OperationStatus.Ready, ""),
        };

        MainWindowViewModel? viewModel = null;
        var processor = new CallbackProcessor((batch, progress, cancellationToken) =>
        {
            Assert.NotNull(viewModel);

            // 1. row B starts and completes in 5 seconds
            progress?.Report(new ConversionProgress(0, 2, "b.txt", OperationStatus.Converting));
            clock.Advance(TimeSpan.FromSeconds(5));
            var resultB = new ConversionResult(batch[1], OperationStatus.Succeeded, "ok");
            progress?.Report(new ConversionProgress(1, 2, "b.txt", OperationStatus.Succeeded, resultB));

            // 2. row A starts converting
            progress?.Report(new ConversionProgress(1, 2, "a.txt", OperationStatus.Converting));

            // Clock advances 4 seconds: row A has 4s live elapsed
            clock.Advance(TimeSpan.FromSeconds(4));
            viewModel.RefreshConversionTiming();

            // Sort by Time Ascending: row A (4s) is first, row B (5s) is second
            viewModel.SortBy(PreviewSortColumn.Time, ListSortDirection.Ascending);
            var visibleBefore = viewModel.VisibleOperations.ToArray();
            Assert.Equal("a.txt", visibleBefore[0].FilePath);
            Assert.Equal("b.txt", visibleBefore[1].FilePath);

            // Clock advances 2 seconds: row A live elapsed becomes 6s (> 5s)
            clock.Advance(TimeSpan.FromSeconds(2));
            viewModel.RefreshConversionTiming();

            // VisibleOperations must automatically re-sort: row B (5s) is now before row A (6s)!
            var visibleAfter = viewModel.VisibleOperations.ToArray();
            Assert.Equal("b.txt", visibleAfter[0].FilePath);
            Assert.Equal("a.txt", visibleAfter[1].FilePath);

            // Complete row A
            var resultA = new ConversionResult(batch[0], OperationStatus.Succeeded, "ok");
            progress?.Report(new ConversionProgress(2, 2, "a.txt", OperationStatus.Succeeded, resultA));

            return Task.FromResult(new ConversionSummary(2, 0, 0, 0, 0, 0, [resultB, resultA]));
        });

        viewModel = CreateStatusViewModel(ops, processor, clock);
        await viewModel.ScanAsync();
        await viewModel.ConvertAsync();
    }

    [Fact]
    public async Task Preview_row_numbering_is_sequential_and_1_based_for_full_list()
    {
        Write("a.pdf", "%PDF-1.7 A");
        Write("b.csv", "1,2");
        Write("c.docx", "word");
        Write("d.txt", "text");
        var viewModel = CreateViewModel();
        await viewModel.ScanAsync();

        var visible = viewModel.VisibleOperations.ToArray();
        Assert.Equal(4, visible.Length);

        for (var i = 0; i < visible.Length; i++)
        {
            Assert.Equal(i + 1, visible[i].DisplayIndex);
            Assert.Equal(i + 1, visible[i].RowNumber);
            Assert.Equal((i + 1).ToString(), visible[i].DisplayIndexText);
        }
    }

    [Fact]
    public async Task Preview_row_numbering_restarts_at_1_when_filtered()
    {
        Write("p1.pdf", "%PDF-1.7 1");
        Write("c1.csv", "data");
        Write("p2.pdf", "%PDF-1.7 2");
        Write("c2.csv", "data2");
        var viewModel = CreateViewModel();
        await viewModel.ScanAsync();

        var pdfRule = viewModel.FormatRules.Single(r => r.SourceFormat == SourceFormat.Pdf);
        viewModel.SelectRuleFilter(pdfRule);

        var visible = viewModel.VisibleOperations.ToArray();
        Assert.Equal(2, visible.Length);
        Assert.Equal(1, visible[0].DisplayIndex);
        Assert.Equal(2, visible[1].DisplayIndex);
        Assert.Equal("1", visible[0].DisplayIndexText);
        Assert.Equal("2", visible[1].DisplayIndexText);

        var filteredOut = viewModel.Operations.Where(r => r.Operation.SourceFormat != SourceFormat.Pdf);
        Assert.All(filteredOut, r =>
        {
            Assert.Equal(0, r.DisplayIndex);
            Assert.Equal(string.Empty, r.DisplayIndexText);
        });
    }

    [Fact]
    public async Task Preview_row_numbering_follows_sorted_displayed_order()
    {
        Write("small.txt", "1");
        Write("large.txt", "1234567890");
        Write("medium.txt", "1234");
        var viewModel = CreateViewModel();
        await viewModel.ScanAsync();

        viewModel.SortBy(PreviewSortColumn.Size, ListSortDirection.Descending);
        var desc = viewModel.VisibleOperations.ToArray();
        Assert.Equal("large.txt", desc[0].FilePath);
        Assert.Equal(1, desc[0].DisplayIndex);
        Assert.Equal("medium.txt", desc[1].FilePath);
        Assert.Equal(2, desc[1].DisplayIndex);
        Assert.Equal("small.txt", desc[2].FilePath);
        Assert.Equal(3, desc[2].DisplayIndex);

        viewModel.SortBy(PreviewSortColumn.Size, ListSortDirection.Ascending);
        var asc = viewModel.VisibleOperations.ToArray();
        Assert.Equal("small.txt", asc[0].FilePath);
        Assert.Equal(1, asc[0].DisplayIndex);
        Assert.Equal("medium.txt", asc[1].FilePath);
        Assert.Equal(2, asc[1].DisplayIndex);
        Assert.Equal("large.txt", asc[2].FilePath);
        Assert.Equal(3, asc[2].DisplayIndex);
    }

    [Fact]
    public async Task Preview_row_numbering_correct_across_filter_sort_and_show_all()
    {
        Write("p1.pdf", "%PDF-1.7 1");
        Write("p2.pdf", "%PDF-1.7 222");
        Write("c1.csv", "1");
        Write("c2.csv", "222");
        var viewModel = CreateViewModel();
        await viewModel.ScanAsync();

        var pdfRule = viewModel.FormatRules.Single(r => r.SourceFormat == SourceFormat.Pdf);

        // 1. Filter to PDF
        viewModel.SelectRuleFilter(pdfRule);
        var pdfOnly = viewModel.VisibleOperations.ToArray();
        Assert.Equal(2, pdfOnly.Length);
        Assert.Equal(1, pdfOnly[0].DisplayIndex);
        Assert.Equal(2, pdfOnly[1].DisplayIndex);

        // 2. Sort PDF descending by size: p2 is bigger than p1
        viewModel.SortBy(PreviewSortColumn.Size, ListSortDirection.Descending);
        var pdfSorted = viewModel.VisibleOperations.ToArray();
        Assert.Equal("p2.pdf", pdfSorted[0].FilePath);
        Assert.Equal(1, pdfSorted[0].DisplayIndex);
        Assert.Equal("p1.pdf", pdfSorted[1].FilePath);
        Assert.Equal(2, pdfSorted[1].DisplayIndex);

        // 3. Show all (ResetPreviewFilter) -> all 4 files displayed, numbered 1..4 in sorted order
        viewModel.ResetPreviewFilter();
        var allSorted = viewModel.VisibleOperations.ToArray();
        Assert.Equal(4, allSorted.Length);
        for (var i = 0; i < allSorted.Length; i++)
        {
            Assert.Equal(i + 1, allSorted[i].DisplayIndex);
        }
    }

    [Fact]
    public async Task Preview_row_numbering_preserves_selection_and_execution_set()
    {
        Write("p1.pdf", "%PDF-1.7 a");
        Write("p2.pdf", "%PDF-1.7 bb");
        Write("p3.pdf", "%PDF-1.7 ccc");
        var recordingProcessor = new RecordingProcessor();
        var resolver = new DefaultConversionAdapterResolver();
        var viewModel = new MainWindowViewModel(
            new FileSystemFolderScanner(),
            new ConversionPlanner(resolver),
            conversionProcessor: recordingProcessor)
        {
            SelectedFolder = _rootPath
        };
        await viewModel.ScanAsync();

        viewModel.ClearSelection();
        viewModel.Operations[1].IsSelected = true; // Select p2.pdf
        Assert.Equal(1, viewModel.SelectedReadyCount);

        // Sort Size Descending -> p3, p2, p1
        viewModel.SortBy(PreviewSortColumn.Size, ListSortDirection.Descending);
        Assert.Equal(1, viewModel.SelectedReadyCount);
        var visible = viewModel.VisibleOperations.ToArray();
        Assert.Equal(1, visible[0].DisplayIndex);
        Assert.False(visible[0].IsSelected); // p3
        Assert.Equal(2, visible[1].DisplayIndex);
        Assert.True(visible[1].IsSelected);  // p2
        Assert.Equal(3, visible[2].DisplayIndex);
        Assert.False(visible[2].IsSelected); // p1

        // Execute conversion
        await viewModel.ConvertAsync();
        Assert.Single(recordingProcessor.Received);
        Assert.Equal("p2.pdf", recordingProcessor.Received[0].RelativePath);
    }

    [Fact]
    public async Task Preview_row_numbering_updates_when_status_sort_order_changes()
    {
        var ops = new[]
        {
            new PlannedOperation(Path.Combine(_rootPath, "file1.bin"), "file1.bin", SourceFormat.Unknown, ConversionTarget.Skip, "", "", false, OperationStatus.Ready, ""),
            new PlannedOperation(Path.Combine(_rootPath, "file2.bin"), "file2.bin", SourceFormat.Unknown, ConversionTarget.Skip, "", "", false, OperationStatus.Ready, ""),
        };

        var viewModel = CreateStatusViewModel(ops);
        await viewModel.ScanAsync();

        var row1 = viewModel.Operations[0];
        var row2 = viewModel.Operations[1];

        // Simulate row1 not selected
        row1.MarkNotSelected();

        // Sort by Status ascending -> row2 is 1st, row1 is 2nd
        viewModel.SortBy(PreviewSortColumn.Status, ListSortDirection.Ascending);
        var visible1 = viewModel.VisibleOperations.ToArray();
        Assert.Equal("file2.bin", visible1[0].FilePath);
        Assert.Equal(1, visible1[0].DisplayIndex);
        Assert.Equal("file1.bin", visible1[1].FilePath);
        Assert.Equal(2, visible1[1].DisplayIndex);

        // Re-select row1 -> row1 becomes Ready again -> file1.bin is 1st alphabetically, file2.bin is 2nd
        row1.IsSelected = true;
        var visible2 = viewModel.VisibleOperations.ToArray();
        Assert.Equal("file1.bin", visible2[0].FilePath);
        Assert.Equal(1, visible2[0].DisplayIndex);
        Assert.Equal("file2.bin", visible2[1].FilePath);
        Assert.Equal(2, visible2[1].DisplayIndex);
    }

    [Fact]
    public void Preview_data_grid_has_row_number_as_first_column_and_is_not_sortable()
    {
        var root = FindRepositoryRoot();
        var xamlPath = Path.Combine(root, "src", "Zlet.FolderConverter.App", "MainWindow.xaml");
        var xaml = File.ReadAllText(xamlPath);

        Assert.Contains("MinRowHeight=\"32\"", xaml);
        Assert.Contains("<DataGridTemplateColumn Header=\"#\" Width=\"38\" MinWidth=\"32\" CanUserSort=\"False\">", xaml);

        var numberColIndex = xaml.IndexOf("Header=\"#\"", StringComparison.Ordinal);
        var checkboxColIndex = xaml.IndexOf("Header=\"\" Width=\"34\"", StringComparison.Ordinal);
        var sourceFileColIndex = xaml.IndexOf("Header=\"{DynamicResource SourceFile}\"", StringComparison.Ordinal);

        Assert.True(numberColIndex > 0, "Header=# column must exist");
        Assert.True(checkboxColIndex > 0, "Checkbox column must exist");
        Assert.True(sourceFileColIndex > 0, "SourceFile column must exist");
        Assert.True(numberColIndex < checkboxColIndex, "Header=# must precede checkbox column");
        Assert.True(checkboxColIndex < sourceFileColIndex, "Checkbox column must precede SourceFile column");
    }

    [Fact]
    public void AppStyles_data_grid_cell_centers_content_vertically_and_defines_preview_text_style()
    {
        var root = FindRepositoryRoot();
        var stylesPath = Path.Combine(root, "src", "Zlet.FolderConverter.App", "Resources", "AppStyles.xaml");
        var styles = File.ReadAllText(stylesPath);

        Assert.Contains("<Setter Property=\"VerticalContentAlignment\" Value=\"Center\" />", styles);
        Assert.Contains("<ContentPresenter SnapsToDevicePixels=\"{TemplateBinding SnapsToDevicePixels}\"", styles);
        Assert.Contains("VerticalAlignment=\"{TemplateBinding VerticalContentAlignment}\"", styles);
        Assert.Contains("<Style x:Key=\"PreviewTextColumnElementStyle\" TargetType=\"TextBlock\">", styles);
        Assert.Contains("<Setter Property=\"VerticalAlignment\" Value=\"Center\" />", styles);
    }

    [Fact]
    public async Task Preview_twenty_plus_mixed_files_filter_other_sort_and_real_conversion()
    {
        for (var i = 1; i <= 10; i++)
        {
            Write($"doc{i:D2}.json", $"{{\"id\":{i}}}");
            Write($"file{i:D2}.otherbin", $"bin content {i}");
        }
        Write("extra.unknown", "unknown data");
        // Total: 10 json + 10 otherbin + 1 extra = 21 files

        var viewModel = CreateViewModel();
        await viewModel.ScanAsync();

        var visibleAll = viewModel.VisibleOperations.ToArray();
        Assert.Equal(21, visibleAll.Length);

        // 1. Verify 1..21 initial numbering
        for (var i = 0; i < 21; i++)
        {
            Assert.Equal(i + 1, visibleAll[i].DisplayIndex);
        }

        // 2. Filter to "Other" (SourceFormat.Unknown) via rule
        var otherRule = Assert.Single(viewModel.FormatRules.Where(r => r.SourceFormat == SourceFormat.Unknown));
        viewModel.SelectRuleFilter(otherRule);
        var visibleOther = viewModel.VisibleOperations.ToArray();
        Assert.Equal(11, visibleOther.Length); // 10 otherbin + 1 extra
        for (var i = 0; i < 11; i++)
        {
            Assert.Equal(i + 1, visibleOther[i].DisplayIndex);
        }

        // 3. Sort by SourceFile descending on filtered other files
        viewModel.SortBy(PreviewSortColumn.SourceFile, ListSortDirection.Descending);
        var visibleOtherSorted = viewModel.VisibleOperations.ToArray();
        Assert.Equal("file10.otherbin", visibleOtherSorted[0].FilePath);
        Assert.Equal(1, visibleOtherSorted[0].DisplayIndex);
        Assert.Equal("extra.unknown", visibleOtherSorted.Last().FilePath);
        Assert.Equal(11, visibleOtherSorted.Last().DisplayIndex);

        // 4. Show all -> 21 files, 1..21
        viewModel.ResetPreviewFilter();
        var allSortedAgain = viewModel.VisibleOperations.ToArray();
        Assert.Equal(21, allSortedAgain.Length);
        for (var i = 0; i < 21; i++)
        {
            Assert.Equal(i + 1, allSortedAgain[i].DisplayIndex);
        }

        // 5. Selection operations
        viewModel.ClearSelection();
        Assert.Equal(0, viewModel.SelectedReadyCount);
        viewModel.SelectAll();
        Assert.Equal(10, viewModel.SelectedReadyCount); // only JSON files are ready
        viewModel.InvertSelection();
        Assert.Equal(0, viewModel.SelectedReadyCount);
        viewModel.Operations.First(r => r.CanSelect).IsSelected = true;
        Assert.Equal(1, viewModel.SelectedReadyCount);

        // 6. Real conversion
        await viewModel.ConvertAsync();
        Assert.True(viewModel.HasFinalReport);
        Assert.True(viewModel.FinalSucceeded >= 1);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FolderConverter.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private MainWindowViewModel CreateViewModel()
    {
        var resolver = new DefaultConversionAdapterResolver();
        return new MainWindowViewModel(
            new FileSystemFolderScanner(),
            new ConversionPlanner(resolver),
            new ConversionProcessor(resolver))
        {
            SelectedFolder = _rootPath,
            IncludeSubfolders = true
        };
    }

    private MainWindowViewModel CreateStatusViewModel(OperationStatus[] statuses)
    {
        var operations = CreateStatusOperations(statuses);
        return CreateStatusViewModel(operations);
    }

    private PlannedOperation[] CreateStatusOperations(OperationStatus[] statuses)
    {
        return statuses.Select((status, index) =>
        {
            var relativePath = $"status-{index}.custom";
            return new PlannedOperation(
                Path.Combine(_rootPath, relativePath),
                relativePath,
                SourceFormat.Unknown,
                ConversionTarget.Skip,
                string.Empty,
                string.Empty,
                false,
                status,
                status.ToString());
        }).ToArray();
    }

    private MainWindowViewModel CreateStatusViewModel(
        PlannedOperation[] operations,
        IConversionProcessor? processor = null,
        TimeProvider? timeProvider = null)
    {
        var scan = new ScanResult(
            _rootPath,
            operations.Select(operation => new ScannedFile(
                operation.SourcePath,
                operation.RelativePath,
                operation.SourceFormat)).ToArray(),
            []);

        return new MainWindowViewModel(
            new StaticScanner(scan),
            new StaticPlanner(operations),
            processor,
            timeProvider: timeProvider)
        {
            SelectedFolder = _rootPath
        };
    }

    private ScannedFile Scanned(string relativePath) =>
        new(
            Path.Combine(_rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)),
            relativePath,
            SourceFormat.Unknown);

    private static RuleRowViewModel RuleFor(
        MainWindowViewModel viewModel,
        SourceFormat source) =>
        viewModel.FormatRules.Single(rule => rule.SourceFormat == source);

    private string Write(string relativePath, string content)
    {
        var path = Path.Combine(_rootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static string Hash(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private sealed class CallbackScanner(string root) : IFolderScanner
    {
        public Action? Callback { get; set; }
        public string? ReceivedRoot { get; private set; }

        public Task<ScanResult> ScanAsync(
            string rootPath,
            bool includeSubfolders,
            CancellationToken cancellationToken)
        {
            ReceivedRoot = rootPath;
            Callback?.Invoke();
            return Task.FromResult(new ScanResult(root, [], []));
        }
    }

    private sealed class RecordingPlanner : IConversionPlanner
    {
        public string? ReceivedRoot { get; private set; }

        public IReadOnlyList<PlannedOperation> CreatePlan(
            ScanResult scanResult,
            string rootPath,
            RuleSet ruleSet)
        {
            ReceivedRoot = rootPath;
            return [];
        }
    }

    private sealed class StaticScanner(ScanResult result) : IFolderScanner
    {
        public Task<ScanResult> ScanAsync(
            string rootPath,
            bool includeSubfolders,
            CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed class StaticPlanner(
        IReadOnlyList<PlannedOperation> operations) : IConversionPlanner
    {
        public IReadOnlyList<PlannedOperation> CreatePlan(
            ScanResult scanResult,
            string rootPath,
            RuleSet ruleSet) =>
            operations;
    }

    private sealed class UnavailableAdapter(
        SourceFormat source,
        ConversionTarget target) : IConversionAdapter
    {
        public bool IsAvailable => false;
        public string AvailabilityMessage => "unavailable";

        public bool CanConvert(
            SourceFormat sourceFormat,
            ConversionTarget conversionTarget) =>
            sourceFormat == source && conversionTarget == target;

        public Task<ConversionResult> ConvertAsync(
            PlannedOperation operation,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Unavailable adapter must not run.");
    }

    private sealed class StaticProcessor(
        ConversionSummary summary) : IConversionProcessor
    {
        public Task<ConversionSummary> ProcessAsync(
            IReadOnlyList<PlannedOperation> operations,
            IProgress<ConversionProgress>? progress,
            CancellationToken cancellationToken) =>
            Task.FromResult(summary);
    }

    private sealed class CallbackProcessor(
        Func<IReadOnlyList<PlannedOperation>, IProgress<ConversionProgress>?, CancellationToken,
            Task<ConversionSummary>> callback) : IConversionProcessor
    {
        public Task<ConversionSummary> ProcessAsync(
            IReadOnlyList<PlannedOperation> operations,
            IProgress<ConversionProgress>? progress,
            CancellationToken cancellationToken) =>
            callback(operations, progress, cancellationToken);
    }

    private sealed class TimedProgressProcessor(
        ManualTimeProvider clock,
        Action halfway) : IConversionProcessor
    {
        public Task<ConversionSummary> ProcessAsync(
            IReadOnlyList<PlannedOperation> operations,
            IProgress<ConversionProgress>? progress,
            CancellationToken cancellationToken)
        {
            var first = new ConversionResult(
                operations[0],
                OperationStatus.Succeeded,
                "ok");
            var second = new ConversionResult(
                operations[1],
                OperationStatus.Succeeded,
                "ok");
            progress?.Report(new ConversionProgress(
                0,
                2,
                operations[0].RelativePath,
                OperationStatus.Converting));
            clock.Advance(TimeSpan.FromSeconds(10));
            progress?.Report(new ConversionProgress(
                1,
                2,
                operations[0].RelativePath,
                OperationStatus.Succeeded,
                first));
            progress?.Report(new ConversionProgress(
                1,
                2,
                operations[1].RelativePath,
                OperationStatus.Converting));
            halfway();
            clock.Advance(TimeSpan.FromSeconds(4));
            progress?.Report(new ConversionProgress(
                2,
                2,
                operations[1].RelativePath,
                OperationStatus.Succeeded,
                second));
            return Task.FromResult(new ConversionSummary(2, 0, 0, 0, 0, 0, [first, second]));
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _timestamp;

        public void Advance(TimeSpan value) => _timestamp += value.Ticks;
    }

    private sealed class RecordingProcessor : IConversionProcessor
    {
        public IReadOnlyList<PlannedOperation> Received { get; private set; } = [];

        public Task<ConversionSummary> ProcessAsync(
            IReadOnlyList<PlannedOperation> operations,
            IProgress<ConversionProgress>? progress,
            CancellationToken cancellationToken)
        {
            Received = operations;
            var results = operations.Select(operation =>
                new ConversionResult(operation, OperationStatus.Succeeded, "ok")).ToArray();
            return Task.FromResult(new ConversionSummary(
                results.Length, 0, 0, 0, 0, 0, results));
        }
    }

    private sealed class StubOfficeCapabilityDetector(params OfficeApplicationAvailability[] availabilities)
        : IMicrosoftOfficeCapabilityDetector
    {
        public IReadOnlyList<OfficeApplicationAvailability> Detect() => availabilities;
    }
}
