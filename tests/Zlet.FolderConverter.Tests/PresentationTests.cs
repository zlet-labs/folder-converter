using Zlet.FolderConverter.App.Localization;
using Zlet.FolderConverter.App.ViewModels;
using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;
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

                // In DataGridCell with Padding="11,6" (22px horizontal)
                double cellAvailableWidth = Math.Max(0, columnWidth - 22);

                border.Measure(new System.Windows.Size(cellAvailableWidth, double.PositiveInfinity));
                border.Arrange(new System.Windows.Rect(0, 0, cellAvailableWidth, border.DesiredSize.Height));

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
}
