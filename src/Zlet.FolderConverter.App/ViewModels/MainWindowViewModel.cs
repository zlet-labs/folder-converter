using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using Zlet.FolderConverter.App;
using Zlet.FolderConverter.App.Localization;
using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IFolderScanner _folderScanner;
    private readonly IConversionPlanner _conversionPlanner;
    private readonly IConversionProcessor _conversionProcessor;
    private readonly IReadOnlyList<OfficeApplicationAvailability> _officeAvailability;
    private readonly TimeProvider _timeProvider;
    private readonly DispatcherTimer _progressTimer;
    private readonly LocalizationService _localization;
    private readonly List<LocalizedErrorEntry> _errorEntries = [];
    private string _selectedFolder = string.Empty;
    private string _sourcePathError = string.Empty;
    private bool _includeSubfolders = true;
    private bool _isScanning;
    private bool _isConverting;
    private bool _isStopping;
    private CancellationTokenSource? _conversionCancellation;
    private CancellationTokenSource? _planningCancellation;
    private CancellationTokenSource? _scanCancellation;
    public Task PreviewPlanningTask { get; private set; } = Task.CompletedTask;
    public bool IsPlanning => _planningCancellation is not null;
    private string _copyListStatus = string.Empty;
    private int? _copiedListCount;
    private bool _copyListWasEmpty;
    private string _stateMessage = string.Empty;
    private string _emptyStateMessage = string.Empty;
    private string? _stateResourceKey = "InitialState";
    private object[] _stateArguments = [];
    private string? _emptyResourceKey = "InitialEmpty";
    private RuleSet _ruleSet = RuleSet.CreateDefault();
    private ScanResult? _lastScan;
    private string _scanRoot = string.Empty;
    private PreviewFilterOption _selectedPreviewFilter;
    private RuleRowViewModel? _selectedRule;
    private int _foundCount;
    private int _readyCount;
    private int _selectedReadyCount;
    private int _skippedCount;
    private int _unavailableCount;
    private int _conflictCount;
    private int _errorCount;
    private double _progressPercent;
    private string _currentFile = string.Empty;
    private int _progressCompleted;
    private int _progressTotal;
    private long _conversionStartTimestamp;
    private TimeSpan? _completedElapsed;
    private string _elapsedTimeText = string.Empty;
    private string _remainingTimeText = string.Empty;
    private string _finalDurationText = string.Empty;
    private bool _hasFinalReport;
    private string _finalReportTitle = string.Empty;
    private bool _finalWasStopped;
    private int _finalConverted;
    private int _finalCopied;
    private int _finalFailed;
    private int _finalConflicts;
    private int _finalUnavailable;
    private int _finalSkipped;
    private int _finalNotSelected;
    private string _resultFolder = string.Empty;
    private OutputMode _selectedOutputMode;
    private string _outputPath = string.Empty;
    private string _folderOutputPath = string.Empty;
    private string _zipOutputPath = string.Empty;
    private string _outputPathError = string.Empty;
    private bool _folderOutputEdited;
    private bool _zipOutputEdited;
    private bool _applyingOutputDefault;
    private readonly string _zipStagingRoot = Path.Combine(
        Path.GetTempPath(),
        "ZletBatchConverter",
        "result-staging",
        Guid.NewGuid().ToString("N"));

    public MainWindowViewModel(
        IFolderScanner folderScanner,
        IConversionPlanner conversionPlanner,
        IConversionProcessor? conversionProcessor = null,
        IMicrosoftOfficeCapabilityDetector? officeCapabilityDetector = null,
        TimeProvider? timeProvider = null,
        LocalizationService? localization = null)
    {
        _folderScanner = folderScanner;
        _localization = localization ?? LocalizationService.Current;
        _conversionPlanner = conversionPlanner;
        _conversionProcessor = conversionProcessor
            ?? new ConversionProcessor(new DefaultConversionAdapterResolver());
        _officeAvailability = (officeCapabilityDetector
            ?? new MicrosoftOfficeCapabilityDetector()).Detect();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _progressTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _progressTimer.Tick += (_, _) => RefreshConversionTiming();
        PreviewFilters = [];
        OutputModes = [];
        RebuildLocalizedOptions();
        _selectedPreviewFilter = PreviewFilters[0];
        _stateMessage = L("InitialState");
        _emptyStateMessage = L("InitialEmpty");
        _finalReportTitle = L("FinalComplete");
        ClearCompletedConversionTiming();
        _localization.LanguageChanged += (_, _) => RefreshLocalization();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<RuleRowViewModel> FormatRules { get; } = [];
    public ObservableCollection<OperationRowViewModel> Operations { get; } = [];
    public ObservableCollection<string> ErrorMessages { get; } = [];
    public ObservableCollection<PreviewFilterOption> PreviewFilters { get; }
    public string WordOfficeStatus => GetOfficeStatus(OfficeApplicationKind.Word);
    public string ExcelOfficeStatus => GetOfficeStatus(OfficeApplicationKind.Excel);
    public string PowerPointOfficeStatus => GetOfficeStatus(OfficeApplicationKind.PowerPoint);
    public bool IsWordOfficeAvailable => IsOfficeAvailable(OfficeApplicationKind.Word);
    public bool IsExcelOfficeAvailable => IsOfficeAvailable(OfficeApplicationKind.Excel);
    public bool IsPowerPointOfficeAvailable => IsOfficeAvailable(OfficeApplicationKind.PowerPoint);

    public IEnumerable<OperationRowViewModel> VisibleOperations =>
        ApplySort(Operations.Where(MatchesSelectedFilter), _sortColumn, _sortDirection).ToArray();

    public string SelectedFolder
    {
        get => _selectedFolder;
        set
        {
            if (SetProperty(ref _selectedFolder, value))
            {
                OnPropertyChanged(nameof(SelectedFolderDisplay));
                OnPropertyChanged(nameof(CanCopySelectedFolder));
                UpdateSourcePathError();
                RefreshDefaultOutputPaths();
                InvalidateScan("FolderChanged");
                NotifyAvailability();
            }
        }
    }

    public string SelectedFolderDisplay => PathDisplayFormatter.Format(SelectedFolder, localization: _localization);
    public bool CanCopySelectedFolder => !string.IsNullOrWhiteSpace(SelectedFolder);
    public string SourcePathError
    {
        get => _sourcePathError;
        private set
        {
            if (SetProperty(ref _sourcePathError, value))
            {
                OnPropertyChanged(nameof(HasSourcePathError));
            }
        }
    }
    public bool HasSourcePathError => !string.IsNullOrWhiteSpace(SourcePathError);

    public ObservableCollection<OutputModeOption> OutputModes { get; }

    public OutputModeOption SelectedOutputModeOption
    {
        get => OutputModes.Single(option => option.Mode == SelectedOutputMode);
        set
        {
            if (value is not null)
            {
                SelectedOutputMode = value.Mode;
                OnPropertyChanged();
            }
        }
    }

    public OutputMode SelectedOutputMode
    {
        get => _selectedOutputMode;
        set
        {
            if (!SetProperty(ref _selectedOutputMode, value))
            {
                return;
            }

            ApplyCurrentModePath();
            HasFinalReport = false;
            if (_lastScan is not null)
            {
                RebuildPreview();
            }
            OnPropertyChanged(nameof(OutputModeLabel));
            OnPropertyChanged(nameof(OutputBrowseButtonText));
            OnPropertyChanged(nameof(ResultActionText));
            OnPropertyChanged(nameof(SelectedOutputModeOption));
        }
    }

    public string OutputModeLabel => SelectedOutputMode == OutputMode.Folder
        ? L("OutputFolder")
        : L("OutputZip");

    public string OutputBrowseButtonText => SelectedOutputMode == OutputMode.Folder
        ? L("ChooseOutputFolder")
        : L("ChooseOutputZip");

    public string OutputPath
    {
        get => _outputPath;
        set
        {
            if (!SetProperty(ref _outputPath, value))
            {
                return;
            }

            if (SelectedOutputMode == OutputMode.Folder)
            {
                _folderOutputPath = value;
                if (!_applyingOutputDefault)
                {
                    _folderOutputEdited = true;
                }
            }
            else
            {
                _zipOutputPath = value;
                if (!_applyingOutputDefault)
                {
                    _zipOutputEdited = true;
                }
            }

            ValidateOutputPath();
            HasFinalReport = false;
            if (_lastScan is not null && SelectedOutputMode == OutputMode.Folder)
            {
                RebuildPreview();
            }
            NotifyAvailability();
        }
    }

    public string OutputPathError
    {
        get => _outputPathError;
        private set
        {
            if (SetProperty(ref _outputPathError, value))
            {
                OnPropertyChanged(nameof(HasOutputPathError));
            }
        }
    }

    public bool HasOutputPathError => !string.IsNullOrWhiteSpace(OutputPathError);

    public bool IncludeSubfolders
    {
        get => _includeSubfolders;
        set
        {
            if (SetProperty(ref _includeSubfolders, value))
            {
                InvalidateScan("SubfoldersChanged");
            }
        }
    }

    public PreviewFilterOption SelectedPreviewFilter
    {
        get => _selectedPreviewFilter;
        set
        {
            if (value is null)
            {
                return;
            }

            if (SetProperty(ref _selectedPreviewFilter, value))
            {
                if (value.Filter == PreviewFilter.Format && value.Format.HasValue)
                {
                    var matchingRule = FormatRules.FirstOrDefault(r => r.SourceFormat == value.Format.Value);
                    SetSelectedRuleInternal(matchingRule);
                }
                else
                {
                    SetSelectedRuleInternal(null);
                }

                OnPropertyChanged(nameof(VisibleOperations));
                OnPropertyChanged(nameof(IsPreviewFiltered));
                OnPropertyChanged(nameof(FilteredCountSummary));
            }
        }
    }

    public RuleRowViewModel? SelectedRule
    {
        get => _selectedRule;
        set
        {
            if (ReferenceEquals(_selectedRule, value))
            {
                return;
            }

            SetSelectedRuleInternal(value);
            if (value is not null)
            {
                var matchingFilter = PreviewFilters.FirstOrDefault(o =>
                    o.Filter == PreviewFilter.Format && o.Format == value.SourceFormat);
                if (matchingFilter is not null)
                {
                    SetProperty(ref _selectedPreviewFilter, matchingFilter, nameof(SelectedPreviewFilter));
                }
            }
            else
            {
                var allFilter = PreviewFilters.FirstOrDefault(o => o.Filter == PreviewFilter.All);
                if (allFilter is not null)
                {
                    SetProperty(ref _selectedPreviewFilter, allFilter, nameof(SelectedPreviewFilter));
                }
            }

            OnPropertyChanged(nameof(VisibleOperations));
            OnPropertyChanged(nameof(IsPreviewFiltered));
            OnPropertyChanged(nameof(FilteredCountSummary));
        }
    }

    public bool IsPreviewFiltered => SelectedPreviewFilter.Filter != PreviewFilter.All;
    public int VisibleOperationsCount => VisibleOperations.Count();
    public string FilteredCountSummary => _localization.Format("PreviewShownFormat", VisibleOperationsCount, Operations.Count);

    public void SelectRuleFilter(RuleRowViewModel rule)
    {
        SelectedRule = rule;
    }

    public void ClearRuleFilter()
    {
        SelectedRule = null;
    }

    public void ToggleRuleFilter(RuleRowViewModel rule)
    {
        if (ReferenceEquals(SelectedRule, rule) || (SelectedRule is not null && SelectedRule.SourceFormat == rule.SourceFormat))
        {
            ClearRuleFilter();
        }
        else
        {
            SelectRuleFilter(rule);
        }
    }

    public void ResetPreviewFilter()
    {
        var allOption = PreviewFilters.FirstOrDefault(o => o.Filter == PreviewFilter.All);
        if (allOption is not null)
        {
            SelectedPreviewFilter = allOption;
        }
        else
        {
            SelectedRule = null;
        }
    }

    private PreviewSortColumn _sortColumn = PreviewSortColumn.None;
    private ListSortDirection _sortDirection = ListSortDirection.Ascending;

    public PreviewSortColumn CurrentSortColumn => _sortColumn;
    public ListSortDirection CurrentSortDirection => _sortDirection;

    public void SortBy(PreviewSortColumn column)
    {
        if (_sortColumn == column)
        {
            _sortDirection = _sortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        }
        else
        {
            _sortColumn = column;
            _sortDirection = ListSortDirection.Ascending;
        }

        OnPropertyChanged(nameof(CurrentSortColumn));
        OnPropertyChanged(nameof(CurrentSortDirection));
        OnPropertyChanged(nameof(VisibleOperations));
    }

    public void SortBy(PreviewSortColumn column, ListSortDirection direction)
    {
        _sortColumn = column;
        _sortDirection = direction;
        OnPropertyChanged(nameof(CurrentSortColumn));
        OnPropertyChanged(nameof(CurrentSortDirection));
        OnPropertyChanged(nameof(VisibleOperations));
    }

    public void ClearSort()
    {
        if (_sortColumn == PreviewSortColumn.None && _sortDirection == ListSortDirection.Ascending)
        {
            return;
        }

        _sortColumn = PreviewSortColumn.None;
        _sortDirection = ListSortDirection.Ascending;
        OnPropertyChanged(nameof(CurrentSortColumn));
        OnPropertyChanged(nameof(CurrentSortDirection));
        OnPropertyChanged(nameof(VisibleOperations));
    }

    private void SetSelectedRuleInternal(RuleRowViewModel? rule)
    {
        if (ReferenceEquals(_selectedRule, rule))
        {
            return;
        }

        if (_selectedRule is not null)
        {
            _selectedRule.IsSelected = false;
        }

        _selectedRule = rule;

        if (_selectedRule is not null)
        {
            _selectedRule.IsSelected = true;
        }

        OnPropertyChanged(nameof(SelectedRule));
    }

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetProperty(ref _isScanning, value))
            {
                NotifyAvailability();
            }
        }
    }

    public bool IsConverting
    {
        get => _isConverting;
        private set
        {
            if (SetProperty(ref _isConverting, value))
            {
                NotifyAvailability();
                OnPropertyChanged(nameof(ShowProgress));
                OnPropertyChanged(nameof(ShowStopButton));
            }
        }
    }

    public bool IsStopping
    {
        get => _isStopping;
        private set
        {
            if (SetProperty(ref _isStopping, value))
            {
                OnPropertyChanged(nameof(StopButtonText));
                OnPropertyChanged(nameof(CanStop));
            }
        }
    }

    public bool IsBusy => IsScanning || IsConverting || IsPlanning;
    public bool CanScan => !IsBusy && Directory.Exists(NormalizePathInput(SelectedFolder));
    public bool CanConvert => !IsBusy
                              && SelectedReadyCount > 0
                              && !HasOutputPathError
                              && !string.IsNullOrWhiteSpace(OutputPath);
    public bool CanChangeSettings => !IsBusy;
    public bool CanChangeRules => !IsScanning && !IsConverting;
    public bool HasRules => FormatRules.Count > 0;
    public bool HasPreview => Operations.Count > 0;
    public bool HasErrors => ErrorMessages.Count > 0;
    public bool HasEngineUnavailable => Operations.Any(
        row => row.Operation.Status == OperationStatus.EngineUnavailable);
    public bool ShowProgress => IsConverting;
    public bool ShowStopButton => IsConverting;
    public bool CanStop => IsConverting && !IsStopping;
    public string StopButtonText => IsStopping ? L("Stopping") : L("Stop");
    public bool CanCopyConversionList => HasPreview && !IsBusy;
    public string CopyListStatus
    {
        get => _copyListStatus;
        private set => SetProperty(ref _copyListStatus, value);
    }
    public bool CanOpenResult => SelectedOutputMode == OutputMode.Folder
        ? Directory.Exists(ResultFolder)
        : File.Exists(ResultFolder);
    public string ResultActionText => SelectedOutputMode == OutputMode.Folder
        ? L("OpenResultFolder")
        : L("ShowZip");

    public int FoundCount
    {
        get => _foundCount;
        private set => SetProperty(ref _foundCount, value);
    }

    public int ReadyCount
    {
        get => _readyCount;
        private set
        {
            if (SetProperty(ref _readyCount, value))
            {
                OnPropertyChanged(nameof(CanConvert));
                OnPropertyChanged(nameof(ConvertButtonText));
            }
        }
    }

    public int SelectedReadyCount
    {
        get => _selectedReadyCount;
        private set
        {
            if (SetProperty(ref _selectedReadyCount, value))
            {
                OnPropertyChanged(nameof(CanConvert));
                OnPropertyChanged(nameof(ConvertButtonText));
                OnPropertyChanged(nameof(SelectionSummary));
            }
        }
    }

    public int SelectableCount => Operations.Count(row => row.CanSelect);
    public string SelectionSummary => _localization.Format("SelectionFormat", SelectedReadyCount, SelectableCount);

    public int SkippedCount
    {
        get => _skippedCount;
        private set => SetProperty(ref _skippedCount, value);
    }

    public int UnavailableCount
    {
        get => _unavailableCount;
        private set => SetProperty(ref _unavailableCount, value);
    }

    public int ConflictCount
    {
        get => _conflictCount;
        private set => SetProperty(ref _conflictCount, value);
    }

    public int ErrorCount
    {
        get => _errorCount;
        private set => SetProperty(ref _errorCount, value);
    }

    public string ConvertButtonText => SelectedReadyCount == 0
        ? L("ChooseFiles")
        : _localization.Format("ConvertFilesFormat", SelectedReadyCount, _localization.FileWord(SelectedReadyCount));

    public static string GetRussianFileWord(int count) => LocalizationService.Current.FileWord(count);

    public string StateMessage
    {
        get => _stateMessage;
        set
        {
            _stateResourceKey = null;
            SetProperty(ref _stateMessage, value);
        }
    }

    public string EmptyStateMessage
    {
        get => _emptyStateMessage;
        private set => SetProperty(ref _emptyStateMessage, value);
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        private set
        {
            if (SetProperty(ref _progressPercent, value))
            {
                OnPropertyChanged(nameof(ProgressPercentText));
            }
        }
    }

    public string ProgressPercentText => $"{ProgressPercent.ToString("0", _localization.Culture)}%";
    public string ProgressCountText => _localization.Format("ProgressCountFormat", _progressCompleted, _progressTotal);

    public string CurrentFile
    {
        get => _currentFile;
        private set
        {
            if (SetProperty(ref _currentFile, value)) OnPropertyChanged(nameof(CurrentFileText));
        }
    }

    public string ElapsedTimeText
    {
        get => _elapsedTimeText;
        private set => SetProperty(ref _elapsedTimeText, value);
    }

    public string RemainingTimeText
    {
        get => _remainingTimeText;
        private set => SetProperty(ref _remainingTimeText, value);
    }

    public string FinalDurationText
    {
        get => _finalDurationText;
        private set => SetProperty(ref _finalDurationText, value);
    }

    public bool HasFinalReport
    {
        get => _hasFinalReport;
        private set => SetProperty(ref _hasFinalReport, value);
    }

    public int FinalConverted
    {
        get => _finalConverted;
        private set
        {
            if (SetProperty(ref _finalConverted, value))
            {
                OnPropertyChanged(nameof(FinalSucceeded));
                OnPropertyChanged(nameof(FinalConvertedText));
            }
        }
    }
    public string CurrentFileText => _localization.Format("CurrentFileFormat", CurrentFile);

    public string FinalReportTitle
    {
        get => _finalReportTitle;
        private set => SetProperty(ref _finalReportTitle, value);
    }

    public int FinalCopied
    {
        get => _finalCopied;
        private set
        {
            if (SetProperty(ref _finalCopied, value))
            {
                OnPropertyChanged(nameof(FinalSucceeded));
                OnPropertyChanged(nameof(FinalCopiedText));
            }
        }
    }
    public int FinalSucceeded => FinalConverted + FinalCopied;
    public bool WasStoppedByUser => _finalWasStopped;
    public bool ZipPublishedByThisRun { get; private set; }
    public bool ZipPublicationFailed => SelectedOutputMode == OutputMode.Zip && FinalSucceeded > 0 && !ZipPublishedByThisRun;
    public LocalizationService Localization => _localization;
    private string _reportStatusKey = string.Empty;
    public string ReportStatusText => string.IsNullOrEmpty(_reportStatusKey) ? string.Empty : L(_reportStatusKey);
    private string _reportPath = string.Empty;
    public string ReportPath
    {
        get => _reportPath;
        set
        {
            if (_reportPath == value) return;
            _reportPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanOpenReport));
        }
    }
    public bool CanOpenReport => !string.IsNullOrWhiteSpace(_reportPath) && File.Exists(_reportPath) && _reportPath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);

    public void SetReportStatus(bool success, string? reportPath = null)
    {
        ReportPath = success && !string.IsNullOrWhiteSpace(reportPath) ? reportPath : string.Empty;
        _reportStatusKey = success ? "ReportWritten" : ZipPublicationFailed ? "ZipPublicationReportSkipped" : "ReportFailed";
        if (success && SelectedOutputMode == OutputMode.Zip)
        {
            ResultFolder = NormalizePathInput(OutputPath);
            OnPropertyChanged(nameof(CanOpenResult));
        }
        OnPropertyChanged(nameof(ReportStatusText));
        OnPropertyChanged(nameof(CanOpenReport));
    }
    public string WorksheetSummaryText
    {
        get
        {
            var rows = Operations.Where(row => row.Operation.IsWorksheetExport).ToArray();
            if (rows.Length == 0) return string.Empty;
            var groups = rows.GroupBy(row => row.SourcePath, StringComparer.OrdinalIgnoreCase).ToArray();
            return _localization.Format("WorkbookCountFormat", groups.Length) + Environment.NewLine
                + DescribeSheets(rows) + Environment.NewLine
                + string.Join(Environment.NewLine, groups.Select(group =>
                    group.First().Operation.RelativePath + ": " + DescribeSheets(group.ToArray())));
        }
    }
    private string DescribeSheets(OperationRowViewModel[] rows) => _localization.Format(
        "WorksheetSummaryFormat", rows.Count(row => row.Operation.IsWorksheetOperation),
        rows.Count(row => row.IsSelected || row.Result is not null || row.Operation.Status == OperationStatus.NotProcessed),
        rows.Count(row => row.Operation.Target == ConversionTarget.Csv && row.Operation.Status == OperationStatus.Succeeded),
        rows.Count(row => row.Operation.Target == ConversionTarget.Tsv && row.Operation.Status == OperationStatus.Succeeded),
        rows.Count(row => row.Operation.WorksheetVisibility != WorksheetVisibility.Visible && !row.IsSelected && row.Result is null && row.Operation.Status != OperationStatus.NotProcessed),
        rows.Count(row => row.Operation.IsWorksheetOperation && row.Operation.WorksheetIsEmpty),
        rows.Count(row => row.Operation.Status == OperationStatus.Failed),
        rows.Count(row => row.Operation.IsWorksheetOperation && row.Operation.Status == OperationStatus.Succeeded));
    public string FinalConvertedText => _localization.Format("FinalConvertedFormat", FinalConverted);
    public string FinalCopiedText => _localization.Format("FinalCopiedFormat", FinalCopied);
    public string FinalFailedText => _localization.Format("FinalFailedFormat", FinalFailed);
    public string FinalConflictsText => _localization.Format("FinalConflictsFormat", FinalConflicts);
    public string FinalUnavailableText => _localization.Format("FinalUnavailableFormat", FinalUnavailable);
    public string FinalSkippedText => _localization.Format("FinalSkippedFormat", FinalSkipped);
    public string FinalNotSelectedText => _localization.Format("FinalNotSelectedFormat", FinalNotSelected);

    public int FinalFailed
    {
        get => _finalFailed;
        private set { if (SetProperty(ref _finalFailed, value)) OnPropertyChanged(nameof(FinalFailedText)); }
    }

    public int FinalConflicts
    {
        get => _finalConflicts;
        private set { if (SetProperty(ref _finalConflicts, value)) OnPropertyChanged(nameof(FinalConflictsText)); }
    }

    public int FinalUnavailable
    {
        get => _finalUnavailable;
        private set { if (SetProperty(ref _finalUnavailable, value)) OnPropertyChanged(nameof(FinalUnavailableText)); }
    }

    public int FinalSkipped
    {
        get => _finalSkipped;
        private set { if (SetProperty(ref _finalSkipped, value)) OnPropertyChanged(nameof(FinalSkippedText)); }
    }

    public int FinalNotSelected
    {
        get => _finalNotSelected;
        private set { if (SetProperty(ref _finalNotSelected, value)) OnPropertyChanged(nameof(FinalNotSelectedText)); }
    }

    public string ResultFolder
    {
        get => _resultFolder;
        private set
        {
            if (SetProperty(ref _resultFolder, value))
            {
                OnPropertyChanged(nameof(CanOpenResult));
            }
        }
    }

    public async Task ScanAsync(CancellationToken cancellationToken = default)
    {
        _planningCancellation?.Cancel();
        _scanCancellation?.Cancel();
        var selectedFolder = NormalizePathInput(SelectedFolder);
        var includeSubfolders = IncludeSubfolders;
        if (!Directory.Exists(selectedFolder))
        {
            SourcePathError = L("FolderUnavailable");
            SetState("SelectedFolderUnavailable");
            return;
        }

        if (!string.Equals(SelectedFolder, selectedFolder, StringComparison.Ordinal))
        {
            _selectedFolder = selectedFolder;
            OnPropertyChanged(nameof(SelectedFolder));
            OnPropertyChanged(nameof(SelectedFolderDisplay));
            OnPropertyChanged(nameof(CanCopySelectedFolder));
        }
        SourcePathError = string.Empty;
        using var scanCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _scanCancellation = scanCancellation;
        IsScanning = true;
        SetState("Scanning");
        SetEmptyState("Scanning");
        ClearScanState();

        try
        {
            var excludedDirectory = SelectedOutputMode == OutputMode.Folder
                ? NormalizePathInput(OutputPath)
                : null;
            var excludedFile = SelectedOutputMode == OutputMode.Zip
                ? NormalizePathInput(OutputPath)
                : null;
            var scanResult = await _folderScanner.ScanAsync(
                selectedFolder,
                includeSubfolders,
                excludedDirectory,
                excludedFile,
                scanCancellation.Token);
            scanCancellation.Token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(_scanCancellation, scanCancellation)) return;
            _lastScan = scanResult;
            _scanRoot = selectedFolder;
            _ruleSet = RuleSet.CreateDefault();
            FoundCount = scanResult.Files.Count;

            foreach (var error in scanResult.Errors)
            {
                AddLocalizedError("ScanReadErrorFormat", Path.GetFileName(error.Path));
            }

            foreach (var group in scanResult.Files
                         .GroupBy(file => file.Format)
                         .OrderBy(group => (int)group.Key))
            {
                var capability = FormatCapabilityCatalog.Get(group.Key);
                FormatRules.Add(new RuleRowViewModel(
                    capability,
                    group.Count(),
                    _ruleSet.GetRule(group.Key).Target,
                    ChangeRule,
                    group.Key == SourceFormat.Unknown
                        ? group.ToArray()
                        : [],
                    _localization));
            }

            RebuildLocalizedOptions();
            _selectedPreviewFilter = PreviewFilters[0];
            SetSelectedRuleInternal(null);
            OnPropertyChanged(nameof(SelectedPreviewFilter));
            OnPropertyChanged(nameof(IsPreviewFiltered));
            OnPropertyChanged(nameof(FilteredCountSummary));

            await RebuildPreviewAsync(scanCancellation.Token);
            scanCancellation.Token.ThrowIfCancellationRequested();
            if (Operations.Count == 0) SetEmptyState("NoFiles");
            else { _emptyResourceKey = null; EmptyStateMessage = string.Empty; }
            SetState("ScanCompleteFormat", FoundCount);
        }
        finally
        {
            if (ReferenceEquals(_scanCancellation, scanCancellation))
            {
                _scanCancellation = null;
                IsScanning = false;
            }
        }
    }

    public async Task ConvertAsync(CancellationToken cancellationToken = default)
    {
        if (IsPlanning) return;
        var originalRows = Operations.ToArray();
        var selectedRows = originalRows
            .Where(row => row.CanSelect && row.IsSelected)
            .ToArray();
        var operations = selectedRows.Select(row => row.Operation with
        {
            Status = OperationStatus.Ready,
            Message = L("OperationReady")
        }).ToArray();
        if (operations.Length == 0)
        {
            return;
        }

        ValidateOutputPath();
        if (HasOutputPathError)
        {
            SetState("CheckOutputPath");
            return;
        }

        if (SelectedOutputMode == OutputMode.Zip)
        {
            TryDeleteZipStaging();
        }

        _conversionCancellation?.Dispose();
        _conversionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var batchStarted = DateTimeOffset.Now;
        IsStopping = false;
        IsConverting = true;
        HasFinalReport = false;
        ReportPath = string.Empty;
        CopyListStatus = string.Empty;
        _copiedListCount = null;
        _copyListWasEmpty = false;
        _finalWasStopped = false;
        FinalReportTitle = L("FinalComplete");
        ResetFinalCounters();
        _progressCompleted = 0;
        _progressTotal = operations.Length;
        OnPropertyChanged(nameof(ProgressCountText));
        ProgressPercent = 0;
        CurrentFile = string.Empty;
        StartConversionTiming();
        _progressTimer.Start();
        SetState("ConvertingFiles");
        var progress = new InlineProgress<ConversionProgress>(UpdateProgress);

        try
        {
            var summary = await _conversionProcessor.ProcessAsync(
                operations,
                progress,
                _conversionCancellation.Token);
            if (SelectedOutputMode == OutputMode.Zip)
            {
                try
                {
                    var zipResult = await new ResultZipPublisher().PublishAsync(
                        _zipStagingRoot,
                        NormalizePathInput(OutputPath),
                        summary,
                        _conversionCancellation.Token);
                    ZipPublishedByThisRun = zipResult.Created;
                    if (!zipResult.Created)
                    {
                        AddLocalizedError(zipResult.ErrorCode == "no_successful_outputs"
                            ? "ZipNoOutputs"
                            : "ZipCreateFailed");
                    }
                }
                catch (Exception exception) when (exception is IOException
                                                   or InvalidDataException
                                                   or UnauthorizedAccessException)
                {
                    AddLocalizedError("ZipCreateFailed");
                }
            }
            var resultsBySource = summary.Results.ToDictionary(
                result => result.Operation.OperationKey,
                StringComparer.OrdinalIgnoreCase);
            var now = _timeProvider.GetTimestamp();
            foreach (var row in Operations)
            {
                if (resultsBySource.TryGetValue(row.Operation.OperationKey, out var result))
                {
                    if (row.Operation.Status is OperationStatus.Ready
                        or OperationStatus.Converting
                        or OperationStatus.Cancelled
                        or OperationStatus.NotProcessed)
                    {
                        row.CompleteExecution(result, _timeProvider, now);
                    }
                    continue;
                }

                if (row.Operation.Status == OperationStatus.Ready && !row.IsSelected)
                {
                    row.MarkNotSelected();
                }
            }

            AddFailureErrorsFromRows();

            UpdatePreviewSummary();
            var unprocessedRows = originalRows.Where(row =>
                !resultsBySource.ContainsKey(row.Operation.OperationKey)).ToArray();
            FinalConverted = summary.Results.Count(result =>
                result.Status == OperationStatus.Succeeded
                && result.Operation.Target != ConversionTarget.Copy);
            FinalCopied = summary.Results.Count(result =>
                result.Status == OperationStatus.Succeeded
                && result.Operation.Target == ConversionTarget.Copy);
            FinalFailed = Operations.Count(row => row.Operation.Status == OperationStatus.Failed);
            FinalConflicts = summary.Conflicts + unprocessedRows.Count(row =>
                row.Operation.Status == OperationStatus.Conflict);
            FinalUnavailable = summary.EngineUnavailable + summary.Unsupported
                               + unprocessedRows.Count(row => row.Operation.Status is
                                   OperationStatus.EngineUnavailable or OperationStatus.Unsupported);
            FinalSkipped = summary.Skipped + unprocessedRows.Count(row =>
                row.Operation.Status == OperationStatus.Skipped);
            FinalNotSelected = unprocessedRows.Count(row =>
                row.Operation.Status == OperationStatus.Ready && !row.IsSelected);
            _progressCompleted = operations.Length;
            _progressTotal = operations.Length;
            ProgressPercent = 100;
            OnPropertyChanged(nameof(ProgressCountText));
            FreezeConversionTiming();
            ResultFolder = SelectedOutputMode == OutputMode.Folder
                ? NormalizePathInput(OutputPath)
                : ZipPublishedByThisRun
                    ? NormalizePathInput(OutputPath)
                    : string.Empty;
            HasFinalReport = true;
            SetState("BatchComplete");
            OnPropertyChanged(nameof(VisibleOperations));
            OnPropertyChanged(nameof(CanOpenResult));
        }
        catch (OperationCanceledException)
        {
            FreezeConversionTiming();
            if (IsStopping)
            {
                var now = _timeProvider.GetTimestamp();
                foreach (var row in Operations)
                {
                    if (!selectedRows.Any(selected => string.Equals(
                            selected.Operation.OperationKey, row.Operation.OperationKey, StringComparison.OrdinalIgnoreCase)))
                    {
                        if (row.Operation.Status == OperationStatus.Ready) row.MarkNotSelected();
                        continue;
                    }

                    if (row.Operation.Status == OperationStatus.Cancelled)
                        continue;
                    if (row.Operation.Status == OperationStatus.Converting)
                        row.CancelExecution(_timeProvider, now);
                    else if (row.Operation.Status is OperationStatus.Ready
                             or OperationStatus.Cancelled or OperationStatus.NotProcessed)
                        row.MarkNotProcessed();
                }

                AddFailureErrorsFromRows();
                UpdateFinalCountersFromRows(selectedRows);
                if (SelectedOutputMode == OutputMode.Zip && FinalSucceeded > 0)
                {
                    try
                    {
                        var partialSummary = CreateCompletedSummary(selectedRows);
                        var zipResult = await new ResultZipPublisher().PublishAsync(
                            _zipStagingRoot,
                            NormalizePathInput(OutputPath),
                            partialSummary,
                            CancellationToken.None);
                        ZipPublishedByThisRun = zipResult.Created;
                        if (!zipResult.Created)
                            AddLocalizedError("ZipPartialFailed");
                    }
                    catch (Exception exception) when (exception is IOException
                                                       or InvalidDataException
                                                       or UnauthorizedAccessException)
                    {
                        AddLocalizedError("ZipPartialFailed");
                    }
                }

                ResultFolder = SelectedOutputMode == OutputMode.Folder
                    ? NormalizePathInput(OutputPath)
                    : ZipPublishedByThisRun
                        ? NormalizePathInput(OutputPath)
                        : string.Empty;
                _finalWasStopped = true;
                FinalReportTitle = L("StoppedByUser");
                HasFinalReport = true;
                SetState("StoppedByUser");
                UpdatePreviewSummary();
                OnPropertyChanged(nameof(VisibleOperations));
            }
            else
            {
                RebuildPreview();
                SetState("CancelledPreviewUpdated");
                throw;
            }
        }
        finally
        {
            _progressTimer.Stop();
            if (HasFinalReport)
                await ConversionReportWriter.WriteAsync(this, batchStarted, DateTimeOffset.Now);
            if (SelectedOutputMode == OutputMode.Zip)
            {
                TryDeleteZipStaging();
            }
            IsConverting = false;
            IsStopping = false;
            CurrentFile = string.Empty;
            _conversionCancellation?.Dispose();
            _conversionCancellation = null;
        }
    }

    public bool StopConversion()
    {
        if (!IsConverting || IsStopping || _conversionCancellation is null)
            return false;

        IsStopping = true;
        SetState("Stopping");
        _conversionCancellation.Cancel();
        return true;
    }

    public string BuildConversionList()
    {
        var lines = Operations
            .Where(row => row.IsSelected
                          && row.CanSelect
                          && row.Operation.Target is not ConversionTarget.Copy
                              and not ConversionTarget.Skip
                          && (row.Operation.Status is OperationStatus.Ready
                              or OperationStatus.Cancelled or OperationStatus.NotProcessed))
            .Select(row => $"{NormalizeCopyPath(row.FilePath)} → {NormalizeCopyPath(row.ResultPath)}")
            .ToArray();
        if (lines.Length == 0)
        {
            _copiedListCount = null;
            _copyListWasEmpty = true;
            CopyListStatus = L("NoSelectedFiles");
            return string.Empty;
        }

        return string.Join(Environment.NewLine, lines);
    }

    public void ConfirmConversionListCopied()
    {
        var count = BuildConversionList().Split(
            Environment.NewLine,
            StringSplitOptions.RemoveEmptyEntries).Length;
        if (count > 0)
        {
            _copyListWasEmpty = false;
            _copiedListCount = count;
            CopyListStatus = _localization.Format("CopiedFilesFormat", count, _localization.FileWord(count));
        }
    }

    private static string NormalizeCopyPath(string path) =>
        path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');

    public void AddError(string message)
    {
        AddErrorEntry(new LocalizedErrorEntry($"raw:{message}", () => message));
    }

    public void AddLocalizedError(string resourceKey, params object[] arguments)
    {
        var identity = $"resource:{resourceKey}:{string.Join("|", arguments.Select(value => value?.ToString()))}";
        AddErrorEntry(new LocalizedErrorEntry(
            identity,
            () => _localization.Format(resourceKey, arguments)));
    }

    public void AddConversionError(ConversionResult result)
    {
        var identity = $"operation:{result.Operation.SourcePath}:{result.Diagnostic?.ErrorCode}:{result.Diagnostic?.HResult}";
        AddErrorEntry(new LocalizedErrorEntry(identity, () => FormatConversionError(result)));
    }

    private void AddErrorEntry(LocalizedErrorEntry entry)
    {
        if (_errorEntries.Any(existing => existing.Identity == entry.Identity)) return;
        _errorEntries.Add(entry);
        ErrorMessages.Add(entry.Render());
        OnPropertyChanged(nameof(HasErrors));
    }

    public void SelectAll()
    {
        foreach (var row in Operations.Where(row => row.CanSelect))
        {
            row.IsSelected = true;
        }
        SelectionChanged();
    }

    public void ClearSelection()
    {
        foreach (var row in Operations.Where(row => row.CanSelect))
        {
            row.IsSelected = false;
        }
        SelectionChanged();
    }

    public void InvertSelection()
    {
        foreach (var row in Operations.Where(row => row.CanSelect))
        {
            row.IsSelected = !row.IsSelected;
        }
        SelectionChanged();
    }

    public void ResetOutputPath()
    {
        ClearCompletedConversionTiming();
        HasFinalReport = false;
        ReportPath = string.Empty;
        if (SelectedOutputMode == OutputMode.Folder)
        {
            _folderOutputEdited = false;
        }
        else
        {
            _zipOutputEdited = false;
        }

        ApplyDefaultForCurrentMode();
    }

    private void ChangeRule(SourceFormat sourceFormat, ConversionTarget target)
    {
        if (IsScanning || IsConverting || _lastScan is null)
        {
            return;
        }

        _ruleSet = _ruleSet.WithRule(sourceFormat, target);
        HasFinalReport = false;
        RebuildPreview();
        SetState("RuleChanged");
    }

    private void RebuildPreview() => PreviewPlanningTask = RebuildPreviewAsync();

    public async Task RebuildPreviewAsync(CancellationToken cancellationToken = default)
    {
        _planningCancellation?.Cancel();
        using var request = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _planningCancellation = request;
        NotifyAvailability();
        try
        {
            var previousSelection = Operations
                .Where(row => row.CanSelect)
                .ToDictionary(
                    row => row.Operation.OperationKey,
                    row => row.IsSelected,
                    StringComparer.OrdinalIgnoreCase);
            if (_lastScan is not null)
            {
                var outputRoot = SelectedOutputMode == OutputMode.Folder
                    ? NormalizePathInput(OutputPath)
                    : _zipStagingRoot;
                var plan = await _conversionPlanner.CreatePlanAsync(
                             _lastScan,
                             _scanRoot,
                             outputRoot,
                             _ruleSet, request.Token);
                request.Token.ThrowIfCancellationRequested();
                if (!ReferenceEquals(_planningCancellation, request)) return;
                Operations.Clear();
                foreach (var operation in plan)
                {
                    Operations.Add(new OperationRowViewModel(
                        operation,
                        isSelected: operation.Status == OperationStatus.Ready
                            && (previousSelection.TryGetValue(operation.OperationKey, out var selected)
                                ? selected : operation.DefaultSelected),
                        selectionChanged: SelectionChanged,
                        localization: _localization));
                }
            }

            UpdatePreviewSummary();
            OnPropertyChanged(nameof(HasRules));
            OnPropertyChanged(nameof(HasPreview));
            OnPropertyChanged(nameof(HasEngineUnavailable));
            OnPropertyChanged(nameof(VisibleOperations));
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            if (ReferenceEquals(_planningCancellation, request) && !request.IsCancellationRequested)
                AddLocalizedError("ScanFailed");
        }
        finally
        {
            if (ReferenceEquals(_planningCancellation, request))
            {
                _planningCancellation = null;
                NotifyAvailability();
            }
        }
    }

    private void UpdatePreviewSummary()
    {
        OnPropertyChanged(nameof(WorksheetSummaryText));
        FoundCount = Operations.Select(row => row.SourcePath).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        ReadyCount = Operations.Count(row => row.Operation.Status is
            OperationStatus.Ready or OperationStatus.Converting
            or OperationStatus.Cancelled or OperationStatus.NotProcessed);
        SelectedReadyCount = Operations.Count(row => row.CanSelect && row.IsSelected);
        SkippedCount = Operations.Count(row => row.Operation.Status == OperationStatus.Skipped);
        UnavailableCount = Operations.Count(row => row.Operation.Status is
            OperationStatus.EngineUnavailable or OperationStatus.Unsupported);
        ConflictCount = Operations.Count(row => row.Operation.Status == OperationStatus.Conflict);
        ErrorCount = Operations.Count(row => row.Operation.Status == OperationStatus.Failed);
        OnPropertyChanged(nameof(SelectableCount));
        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(HasEngineUnavailable));
        OnPropertyChanged(nameof(IsPreviewFiltered));
        OnPropertyChanged(nameof(FilteredCountSummary));
        NotifyAvailability();
    }

    private void UpdateProgress(ConversionProgress progress)
    {
        _progressCompleted = progress.Completed;
        _progressTotal = progress.Total;
        OnPropertyChanged(nameof(ProgressCountText));
        CurrentFile = progress.RelativePath;
        var reportedPercent = progress.Total == 0
            ? 0
            : Math.Clamp(
                (progress.Completed + (progress.Status is OperationStatus.Converting
                    or OperationStatus.Cancelled
                    ? (progress.OperationPercent ?? 0) / 100d
                    : 0)) * 100d / progress.Total,
                0,
                100);
        ProgressPercent = Math.Max(ProgressPercent, reportedPercent);
        RefreshConversionTiming();

        var index = -1;
        for (var position = 0; position < Operations.Count; position++)
        {
            var operation = Operations[position].Operation;
            if (operation.RelativePath == progress.RelativePath
                && operation.WorksheetName == (progress.Result?.Operation.WorksheetName ?? progress.WorksheetName)
                && operation.Status is OperationStatus.Ready or OperationStatus.Converting
                    or OperationStatus.Cancelled or OperationStatus.NotProcessed)
            {
                index = position;
                break;
            }
        }

        if (index < 0)
        {
            return;
        }

        var row = Operations[index];
        if (progress.Status == OperationStatus.Converting)
        {
            row.BeginExecution(_timeProvider.GetTimestamp(), progress.OperationPercent);
        }
        else if (progress.Result is not null)
        {
            row.CompleteExecution(progress.Result, _timeProvider, _timeProvider.GetTimestamp());
        }

        UpdatePreviewSummary();
        OnPropertyChanged(nameof(VisibleOperations));
    }

    private bool MatchesSelectedFilter(OperationRowViewModel row) =>
        SelectedPreviewFilter.Filter switch
        {
            PreviewFilter.All => true,
            PreviewFilter.Format => row.Operation.SourceFormat == SelectedPreviewFilter.Format,
            PreviewFilter.Convert => row.Operation.Status is OperationStatus.Ready
                or OperationStatus.Converting
                or OperationStatus.Succeeded
                or OperationStatus.Cancelled
                or OperationStatus.NotProcessed,
            PreviewFilter.Skip => row.Operation.Status == OperationStatus.Skipped,
            PreviewFilter.Unavailable => row.Operation.Status is
                OperationStatus.EngineUnavailable or OperationStatus.Unsupported,
            PreviewFilter.Conflicts => row.Operation.Status == OperationStatus.Conflict,
            PreviewFilter.Errors => row.Operation.Status == OperationStatus.Failed,
            _ => true
        };

    private static IEnumerable<OperationRowViewModel> ApplySort(
        IEnumerable<OperationRowViewModel> source,
        PreviewSortColumn column,
        ListSortDirection direction)
    {
        if (column == PreviewSortColumn.None)
        {
            return source;
        }

        var isAsc = direction == ListSortDirection.Ascending;

        return column switch
        {
            PreviewSortColumn.SourceFile => isAsc
                ? source.OrderBy(r => r.FilePath, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(r => r.Operation.OperationKey, StringComparer.OrdinalIgnoreCase)
                : source.OrderByDescending(r => r.FilePath, StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(r => r.Operation.OperationKey, StringComparer.OrdinalIgnoreCase),

            PreviewSortColumn.Action => isAsc
                ? source.OrderBy(GetActionSortKey, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(r => r.FilePath, StringComparer.OrdinalIgnoreCase)
                : source.OrderByDescending(GetActionSortKey, StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(r => r.FilePath, StringComparer.OrdinalIgnoreCase),

            PreviewSortColumn.Status => isAsc
                ? source.OrderBy(GetStatusSortRank)
                        .ThenBy(r => r.FilePath, StringComparer.OrdinalIgnoreCase)
                : source.OrderByDescending(GetStatusSortRank)
                        .ThenByDescending(r => r.FilePath, StringComparer.OrdinalIgnoreCase),

            PreviewSortColumn.Result => isAsc
                ? source.OrderBy(r => r.ResultPath, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(r => r.FilePath, StringComparer.OrdinalIgnoreCase)
                : source.OrderByDescending(r => r.ResultPath, StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(r => r.FilePath, StringComparer.OrdinalIgnoreCase),

            PreviewSortColumn.Size => isAsc
                ? source.OrderBy(r => r.Operation.SourceSizeBytes)
                        .ThenBy(r => r.FilePath, StringComparer.OrdinalIgnoreCase)
                : source.OrderByDescending(r => r.Operation.SourceSizeBytes)
                        .ThenByDescending(r => r.FilePath, StringComparer.OrdinalIgnoreCase),

            PreviewSortColumn.Time => OrderByTime(source, isAsc),

            _ => source
        };
    }

    private static string GetActionSortKey(OperationRowViewModel row) =>
        $"{row.Operation.SourceFormat} -> {row.Operation.Target}";

    private static int GetStatusSortRank(OperationRowViewModel row)
    {
        if (row.IsNotSelected) return 10;
        return row.Operation.Status switch
        {
            OperationStatus.Ready => 1,
            OperationStatus.Converting => 2,
            OperationStatus.Succeeded => 3,
            OperationStatus.Skipped => 4,
            OperationStatus.Conflict => 5,
            OperationStatus.EngineUnavailable => 6,
            OperationStatus.Unsupported => 7,
            OperationStatus.Failed => 8,
            OperationStatus.Cancelled => 9,
            OperationStatus.NotProcessed => 11,
            _ => 99
        };
    }

    private static IOrderedEnumerable<OperationRowViewModel> OrderByTime(
        IEnumerable<OperationRowViewModel> source,
        bool isAsc)
    {
        if (isAsc)
        {
            return source.OrderBy(r => !r.ExecutionElapsed.HasValue)
                         .ThenBy(r => r.ExecutionElapsed?.Ticks ?? 0)
                         .ThenBy(r => r.FilePath, StringComparer.OrdinalIgnoreCase);
        }

        return source.OrderBy(r => !r.ExecutionElapsed.HasValue)
                     .ThenByDescending(r => r.ExecutionElapsed?.Ticks ?? 0)
                     .ThenByDescending(r => r.FilePath, StringComparer.OrdinalIgnoreCase);
    }

    private void InvalidateScan(string messageKey)
    {
        if (IsBusy || _lastScan is null)
        {
            return;
        }

        ClearScanState();
        _lastScan = null;
        _scanRoot = string.Empty;
        SetState(messageKey);
        SetEmptyState("NewPreviewNeeded");
    }

    private void ClearScanState()
    {
        FormatRules.Clear();
        Operations.Clear();
        ErrorMessages.Clear();
        _errorEntries.Clear();
        FoundCount = 0;
        ReadyCount = 0;
        SelectedReadyCount = 0;
        SkippedCount = 0;
        UnavailableCount = 0;
        ConflictCount = 0;
        ErrorCount = 0;
        HasFinalReport = false;
        ReportPath = string.Empty;
        CopyListStatus = string.Empty;
        _copiedListCount = null;
        _copyListWasEmpty = false;
        ResetFinalCounters();
        _progressCompleted = 0;
        _progressTotal = 0;
        ProgressPercent = 0;
        CurrentFile = string.Empty;
        ClearCompletedConversionTiming();
        ResultFolder = string.Empty;
        SetSelectedRuleInternal(null);
        _sortColumn = PreviewSortColumn.None;
        _sortDirection = ListSortDirection.Ascending;
        RebuildLocalizedOptions();
        _selectedPreviewFilter = PreviewFilters[0];
        OnPropertyChanged(nameof(CurrentSortColumn));
        OnPropertyChanged(nameof(CurrentSortDirection));
        OnPropertyChanged(nameof(SelectedPreviewFilter));
        OnPropertyChanged(nameof(IsPreviewFiltered));
        OnPropertyChanged(nameof(FilteredCountSummary));
        OnPropertyChanged(nameof(HasRules));
        OnPropertyChanged(nameof(HasPreview));
        OnPropertyChanged(nameof(HasEngineUnavailable));
        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(VisibleOperations));
        OnPropertyChanged(nameof(ProgressCountText));
    }

    private void RefreshConversionTiming()
    {
        if (!IsConverting || _completedElapsed.HasValue)
        {
            return;
        }

        var elapsed = GetConversionElapsed();
        var now = _timeProvider.GetTimestamp();
        foreach (var row in Operations)
            row.RefreshExecutionTime(_timeProvider, now);
        ElapsedTimeText = _localization.Format("ElapsedFormat", FormatDuration(elapsed));
        if (_progressTotal <= 0 || _progressCompleted <= 0)
        {
            RemainingTimeText = L("RemainingCalculating");
            return;
        }

        if (_progressCompleted >= _progressTotal)
        {
            RemainingTimeText = L("RemainingZero");
            return;
        }

        var remaining = TimeSpan.FromTicks((long)Math.Max(
            0,
            elapsed.Ticks * (double)(_progressTotal - _progressCompleted)
            / _progressCompleted));
        RemainingTimeText = _localization.Format("RemainingFormat", FormatDuration(remaining));
    }

    private void StartConversionTiming()
    {
        ClearCompletedConversionTiming();
        _conversionStartTimestamp = _timeProvider.GetTimestamp();
        RefreshConversionTiming();
    }

    private void FreezeConversionTiming()
    {
        if (_completedElapsed.HasValue)
        {
            return;
        }

        _completedElapsed = GetConversionElapsed();
        ElapsedTimeText = _localization.Format("ElapsedFormat", FormatDuration(_completedElapsed.Value));
        FinalDurationText = _localization.Format("FinalDurationFormat", FormatDuration(_completedElapsed.Value));
    }

    private void ClearCompletedConversionTiming()
    {
        _completedElapsed = null;
        ElapsedTimeText = _localization.Format("ElapsedFormat", "00:00");
        RemainingTimeText = L("RemainingCalculating");
        FinalDurationText = string.Empty;
    }

    private TimeSpan GetConversionElapsed() =>
        _timeProvider.GetElapsedTime(_conversionStartTimestamp, _timeProvider.GetTimestamp());

    private static string FormatDuration(TimeSpan duration)
    {
        var totalHours = (int)Math.Floor(Math.Max(0, duration.TotalHours));
        return totalHours > 0
            ? $"{totalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{duration.Minutes:00}:{duration.Seconds:00}";
    }

    private string FormatConversionError(ConversionResult result)
    {
        var details = new List<string>();
        if (!string.IsNullOrWhiteSpace(result.Diagnostic?.ErrorCode))
        {
            details.Add(_localization.Format("ErrorCodeFormat", result.Diagnostic.ErrorCode));
        }
        if (result.Diagnostic?.HResult is int hResult)
        {
            details.Add($"HRESULT 0x{unchecked((uint)hResult):X8}");
        }

        var diagnostic = details.Count == 0
            ? string.Empty
            : $" ({string.Join(", ", details)})";
        var message = OperationMessageLocalizer.Localize(
            result.Status,
            result.Operation.Target,
            result.Message,
            result.Diagnostic?.ErrorCode,
            _localization);
        return $"{result.Operation.RelativePath}: {message}{diagnostic}";
    }

    private void AddFailureErrorsFromRows()
    {
        foreach (var result in Operations
                     .Where(row => row.Operation.Status == OperationStatus.Failed)
                     .Select(row => row.Result)
                     .OfType<ConversionResult>()
                     .Where(result => result.Status == OperationStatus.Failed))
        {
            AddConversionError(result);
        }
    }

    private void ResetFinalCounters()
    {
        _reportStatusKey = string.Empty;
        ZipPublishedByThisRun = false;
        OnPropertyChanged(nameof(ReportStatusText));
        FinalConverted = 0;
        FinalCopied = 0;
        FinalFailed = 0;
        FinalConflicts = 0;
        FinalUnavailable = 0;
        FinalSkipped = 0;
        FinalNotSelected = 0;
    }

    private void UpdateFinalCountersFromRows(IReadOnlyList<OperationRowViewModel> selectedRows)
    {
        var selectedPaths = selectedRows.Select(row => row.Operation.OperationKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        FinalConverted = Operations.Count(row => selectedPaths.Contains(row.Operation.OperationKey)
                                                 && row.Operation.Status == OperationStatus.Succeeded
                                                 && row.Operation.Target != ConversionTarget.Copy);
        FinalCopied = Operations.Count(row => selectedPaths.Contains(row.Operation.OperationKey)
                                              && row.Operation.Status == OperationStatus.Succeeded
                                              && row.Operation.Target == ConversionTarget.Copy);
        FinalFailed = Operations.Count(row => row.Operation.Status == OperationStatus.Failed);
        FinalConflicts = Operations.Count(row => row.Operation.Status == OperationStatus.Conflict);
        FinalUnavailable = Operations.Count(row => row.Operation.Status is
            OperationStatus.EngineUnavailable or OperationStatus.Unsupported);
        FinalSkipped = Operations.Count(row => row.Operation.Status == OperationStatus.Skipped);
        FinalNotSelected = Operations.Count(row => row.IsNotSelected);
    }

    private ConversionSummary CreateCompletedSummary(
        IReadOnlyList<OperationRowViewModel> selectedRows)
    {
        var selectedPaths = selectedRows.Select(row => row.Operation.OperationKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var results = Operations
            .Where(row => selectedPaths.Contains(row.Operation.OperationKey)
                          && row.Operation.Status == OperationStatus.Succeeded)
            .Select(row => new ConversionResult(
                row.Operation,
                OperationStatus.Succeeded,
                row.Operation.Message))
            .ToArray();
        return new ConversionSummary(results.Length, 0, 0, 0, 0, 0, results);
    }

    private void NotifyAvailability()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(IsPlanning));
        OnPropertyChanged(nameof(CanChangeRules));
        OnPropertyChanged(nameof(CanScan));
        OnPropertyChanged(nameof(CanConvert));
        OnPropertyChanged(nameof(CanChangeSettings));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(CanCopyConversionList));
    }

    private string GetOfficeStatus(OfficeApplicationKind application)
    {
        var availability = _officeAvailability.Single(item => item.Application == application);
        return _localization.Format(
            "OfficeStatusFormat",
            application.ToShortDisplayName(),
            L(availability.IsAvailable ? "OfficeAvailable" : "OfficeNotInstalled"));
    }

    private bool IsOfficeAvailable(OfficeApplicationKind application) =>
        _officeAvailability.FirstOrDefault(item => item.Application == application)?.IsAvailable ?? false;

    private void RefreshDefaultOutputPaths()
    {
        var source = NormalizePathInput(SelectedFolder);
        if (string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        if (!_folderOutputEdited)
        {
            _folderOutputPath = Path.Combine(source, "_converted");
        }

        if (!_zipOutputEdited)
        {
            _zipOutputPath = Path.Combine(source, ProductIdentity.ResultZipFileName);
        }

        ApplyCurrentModePath();
    }

    private void ApplyDefaultForCurrentMode()
    {
        var source = NormalizePathInput(SelectedFolder);
        var value = SelectedOutputMode == OutputMode.Folder
            ? Path.Combine(source, "_converted")
            : Path.Combine(source, ProductIdentity.ResultZipFileName);
        if (SelectedOutputMode == OutputMode.Folder)
        {
            _folderOutputPath = value;
        }
        else
        {
            _zipOutputPath = value;
        }

        SetOutputPathWithoutMarkingEdited(value);
    }

    private void ApplyCurrentModePath()
    {
        var value = SelectedOutputMode == OutputMode.Folder
            ? _folderOutputPath
            : _zipOutputPath;
        if (string.IsNullOrWhiteSpace(value))
        {
            ApplyDefaultForCurrentMode();
            return;
        }

        SetOutputPathWithoutMarkingEdited(value);
    }

    private void SetOutputPathWithoutMarkingEdited(string value)
    {
        _applyingOutputDefault = true;
        try
        {
            OutputPath = value;
        }
        finally
        {
            _applyingOutputDefault = false;
        }
    }

    private void ValidateOutputPath()
    {
        var source = NormalizePathInput(SelectedFolder);
        var output = NormalizePathInput(OutputPath);
        if (string.IsNullOrWhiteSpace(source))
        {
            OutputPathError = string.Empty;
            return;
        }

        var validation = SelectedOutputMode == OutputMode.Folder
            ? OutputPathGuard.ValidateFolderDestination(source, output)
            : OutputPathGuard.ValidateZipDestination(source, output, _zipStagingRoot);
        OutputPathError = validation.IsValid
            ? string.Empty
            : validation.ErrorCode switch
            {
                "output_equals_source" => L("OutputEqualsSource"),
                "output_is_source_parent" => L("OutputIsSourceParent"),
                "output_is_file" => L("OutputIsFile"),
                "zip_target_conflict" => L("ZipTargetConflict"),
                "zip_extension_required" => L("ZipExtensionRequired"),
                _ => L("UnsafeOutputPath")
            };
    }

    private void TryDeleteZipStaging()
    {
        try
        {
            var expectedRoot = Path.GetFullPath(Path.Combine(
                Path.GetTempPath(),
                "ZletBatchConverter",
                "result-staging"));
            var staging = Path.GetFullPath(_zipStagingRoot);
            if (staging.StartsWith(
                    expectedRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException
                                           or UnauthorizedAccessException
                                           or ArgumentException)
        {
        }
    }

    private void SelectionChanged()
    {
        OnPropertyChanged(nameof(WorksheetSummaryText));
        SelectedReadyCount = Operations.Count(row => row.CanSelect && row.IsSelected);
        OnPropertyChanged(nameof(SelectableCount));
        OnPropertyChanged(nameof(SelectionSummary));
        NotifyAvailability();
    }

    private void UpdateSourcePathError()
    {
        var normalized = NormalizePathInput(SelectedFolder);
        SourcePathError = string.IsNullOrWhiteSpace(normalized) || Directory.Exists(normalized)
            ? string.Empty
            : L("FolderUnavailable");
    }

    public void RefreshLocalization()
    {
        var currentFilter = SelectedPreviewFilter;
        foreach (var rule in FormatRules) rule.RefreshLocalization();
        RebuildLocalizedOptions();
        _selectedPreviewFilter = PreviewFilters.FirstOrDefault(option =>
            option.Filter == currentFilter.Filter && option.Format == currentFilter.Format)
            ?? PreviewFilters.FirstOrDefault(option => option.Filter == currentFilter.Filter)
            ?? PreviewFilters[0];
        foreach (var row in Operations) row.RefreshLocalization();
        ValidateOutputPath();
        UpdateSourcePathError();
        if (_stateResourceKey is not null)
            SetProperty(ref _stateMessage, _localization.Format(_stateResourceKey, _stateArguments), nameof(StateMessage));
        if (_emptyResourceKey is not null)
            SetProperty(ref _emptyStateMessage, L(_emptyResourceKey), nameof(EmptyStateMessage));
        if (_copiedListCount is int copiedCount)
            CopyListStatus = _localization.Format("CopiedFilesFormat", copiedCount, _localization.FileWord(copiedCount));
        else if (_copyListWasEmpty)
            CopyListStatus = L("NoSelectedFiles");
        RelocalizeErrors();
        FinalReportTitle = L(_finalWasStopped ? "StoppedByUser" : "FinalComplete");
        if (_completedElapsed.HasValue) FreezeLocalizedTiming();
        else if (IsConverting) RefreshConversionTiming();
        else ClearCompletedConversionTiming();
        foreach (var property in new[]
                 {
                     nameof(WorksheetSummaryText), nameof(ReportStatusText),
                     nameof(SelectedPreviewFilter), nameof(OutputModes), nameof(SelectedOutputModeOption),
                     nameof(OutputModeLabel), nameof(OutputBrowseButtonText), nameof(ResultActionText),
                     nameof(StopButtonText), nameof(SelectionSummary), nameof(ConvertButtonText),
                     nameof(ProgressCountText), nameof(ProgressPercentText), nameof(WordOfficeStatus),
                     nameof(ExcelOfficeStatus), nameof(PowerPointOfficeStatus), nameof(VisibleOperations),
                     nameof(IsPreviewFiltered), nameof(FilteredCountSummary),
                     nameof(SelectedFolderDisplay), nameof(CurrentFileText), nameof(FinalConvertedText),
                     nameof(FinalCopiedText), nameof(FinalFailedText), nameof(FinalConflictsText),
                     nameof(FinalUnavailableText), nameof(FinalSkippedText), nameof(FinalNotSelectedText)
                 }) OnPropertyChanged(property);
    }

    private void RebuildLocalizedOptions()
    {
        PreviewFilters.Clear();
        PreviewFilters.Add(new(PreviewFilter.All, L("FilterAll")));
        foreach (var rule in FormatRules)
        {
            PreviewFilters.Add(new(PreviewFilter.Format, rule.FormatLabel, rule.SourceFormat));
        }
        PreviewFilters.Add(new(PreviewFilter.Convert, L("FilterConvert")));
        PreviewFilters.Add(new(PreviewFilter.Skip, L("FilterSkip")));
        PreviewFilters.Add(new(PreviewFilter.Unavailable, L("FilterUnavailable")));
        PreviewFilters.Add(new(PreviewFilter.Conflicts, L("FilterConflicts")));
        PreviewFilters.Add(new(PreviewFilter.Errors, L("FilterErrors")));
        OutputModes.Clear();
        OutputModes.Add(new(OutputMode.Folder, L("OutputFolder")));
        OutputModes.Add(new(OutputMode.Zip, L("OutputZip")));
    }

    private void FreezeLocalizedTiming()
    {
        var elapsed = _completedElapsed!.Value;
        ElapsedTimeText = _localization.Format("ElapsedFormat", FormatDuration(elapsed));
        FinalDurationText = _localization.Format("FinalDurationFormat", FormatDuration(elapsed));
        RemainingTimeText = L("RemainingZero");
    }

    private string L(string key) => _localization.Get(key);

    private void RelocalizeErrors()
    {
        ErrorMessages.Clear();
        foreach (var entry in _errorEntries) ErrorMessages.Add(entry.Render());
        OnPropertyChanged(nameof(HasErrors));
    }

    public void SetLocalizedState(string key, params object[] arguments) => SetState(key, arguments);

    private void SetState(string key, params object[] arguments)
    {
        _stateResourceKey = key;
        _stateArguments = arguments;
        SetProperty(ref _stateMessage, _localization.Format(key, arguments), nameof(StateMessage));
    }

    private void SetEmptyState(string key)
    {
        _emptyResourceKey = key;
        SetProperty(ref _emptyStateMessage, L(key), nameof(EmptyStateMessage));
    }

    public static string NormalizePathInput(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length >= 2
            && normalized[0] == '"'
            && normalized[^1] == '"')
        {
            normalized = normalized[1..^1].Trim();
        }

        return normalized;
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed record LocalizedErrorEntry(string Identity, Func<string> Render);
}
