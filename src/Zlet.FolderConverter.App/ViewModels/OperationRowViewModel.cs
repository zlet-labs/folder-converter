using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Zlet.FolderConverter.App.Localization;
using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.App.ViewModels;

public sealed class OperationRowViewModel : INotifyPropertyChanged
{
    private readonly Action? _selectionChanged;
    private bool _isSelected;
    private long? _executionStartTimestamp;
    private TimeSpan? _executionElapsed;
    private TimeSpan? _liveExecutionElapsed;
    private int? _operationPercent;
    private bool _isNotSelected;
    private readonly LocalizationService _localization;

    public OperationRowViewModel(
        PlannedOperation operation,
        ConversionResult? result = null,
        bool? isSelected = null,
        bool isNotSelected = false,
        Action? selectionChanged = null,
        LocalizationService? localization = null)
    {
        Operation = result is null
            ? operation
            : result.Operation with { Status = result.Status, Message = result.Message };
        _isSelected = Operation.Status == OperationStatus.Ready
            && (isSelected ?? Operation.DefaultSelected);
        _isNotSelected = isNotSelected;
        Result = result;
        _selectionChanged = selectionChanged;
        _localization = localization ?? LocalizationService.Current;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public PlannedOperation Operation { get; private set; }
    public ConversionResult? Result { get; private set; }
    public bool CanSelect => (Operation.Status is OperationStatus.Ready
        or OperationStatus.Cancelled or OperationStatus.NotProcessed);
    public bool IsNotSelected => _isNotSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            var next = CanSelect && value;
            var clearPreviousBatchState = next && _isNotSelected;
            if (_isSelected == next && !clearPreviousBatchState) return;
            _isSelected = next;
            if (clearPreviousBatchState)
            {
                _isNotSelected = false;
                _operationPercent = null;
                _executionStartTimestamp = null;
                _executionElapsed = null;
                _liveExecutionElapsed = null;
                Result = null;
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(StatusTone));
            OnPropertyChanged(nameof(ExecutionTimeText));
            _selectionChanged?.Invoke();
        }
    }

    public string SourcePath => Operation.SourcePath;
    public string FilePath => Operation.IsWorksheetOperation
        ? $"{Operation.RelativePath} / {Operation.WorksheetName}"
        : Operation.RelativePath;
    public string ActionLabel => Operation.Target switch
    {
        ConversionTarget.Skip => _localization.Get("TargetSkip"),
        ConversionTarget.Copy => _localization.Get("TargetCopy"),
        _ => $"{Operation.SourceFormat.ToDisplayName()} → {Operation.Target.ToDisplayName()}"
    };
    public string TargetPath => Operation.TargetPath;
    public string ResultPath => Operation.Target == ConversionTarget.Skip
        ? "—"
        : !string.IsNullOrWhiteSpace(Operation.ResultRelativePath)
            ? Operation.ResultRelativePath
            : Path.ChangeExtension(Operation.RelativePath, Operation.TargetExtension);
    public string FileSizeText => _localization.FormatFileSize(Operation.SourceSizeBytes);
    public string ExecutionTimeText => (_executionElapsed ?? _liveExecutionElapsed) is { } elapsed
        ? _localization.FormatExecutionTime(elapsed)
        : "—";
    public string Status
    {
        get
        {
            var text = _isNotSelected && Operation.Status == OperationStatus.Ready ? _localization.Get("StatusNotSelected") : LocalizeStatus(
                Operation.Status,
                Operation.Target,
                Operation.Message,
                Result?.Diagnostic?.ErrorCode ?? string.Empty,
                _localization);
            if (Operation.IsWorksheetOperation)
            {
                if (Operation.WorksheetIsEmpty) text += " · " + _localization.Get("EmptyLabel");
                if (Operation.WorksheetVisibility != WorksheetVisibility.Visible) text += " · " + _localization.Get(
                    Operation.WorksheetVisibility == WorksheetVisibility.VeryHidden ? "VeryHiddenLabel" : "HiddenLabel");
            }
            return _operationPercent.HasValue ? $"{text} · {_operationPercent.Value.ToString(_localization.Culture)}%" : text;
        }
    }
    public string StatusTone => _isNotSelected ? "Cancelled" : Operation.Status switch
    {
        OperationStatus.Succeeded => "Success",
        OperationStatus.Converting => "InProgress",
        OperationStatus.Ready => "Ready",
        OperationStatus.Conflict or OperationStatus.EngineUnavailable
            or OperationStatus.Unsupported or OperationStatus.Skipped => "Warning",
        OperationStatus.Failed => "Danger",
        OperationStatus.Cancelled or OperationStatus.NotProcessed => "Cancelled",
        _ => "Neutral"
    };
    public string Message => OperationMessageLocalizer.Localize(
        Operation.Status,
        Operation.Target,
        Operation.Message,
        Result?.Diagnostic?.ErrorCode,
        _localization);

    public void BeginExecution(long timestamp, int? percent = null)
    {
        if (Operation.Status != OperationStatus.Converting)
        {
            _executionStartTimestamp = timestamp;
            _executionElapsed = null;
            _liveExecutionElapsed = null;
            Result = null;
        }
        if (percent.HasValue)
        {
            var nextPercent = Math.Clamp(percent.Value, 0, 99);
            _operationPercent = _operationPercent.HasValue
                ? Math.Max(_operationPercent.Value, nextPercent)
                : nextPercent;
        }
        else
        {
            _operationPercent = null;
        }
        _isSelected = false;
        _isNotSelected = false;
        Operation = Operation with { Status = OperationStatus.Converting, Message = string.Empty };
        NotifyExecutionChanged();
    }

    public void CompleteExecution(ConversionResult result, TimeProvider timeProvider, long timestamp)
    {
        FreezeExecutionTime(timeProvider, timestamp);
        _operationPercent = result.Status == OperationStatus.Succeeded ? 100 : null;
        Result = result;
        Operation = result.Operation with { Status = result.Status, Message = result.Message };
        _isSelected = result.Status == OperationStatus.Cancelled;
        _isNotSelected = false;
        NotifyExecutionChanged();
    }

    public void CancelExecution(TimeProvider timeProvider, long timestamp)
    {
        FreezeExecutionTime(timeProvider, timestamp);
        _operationPercent = null;
        Operation = Operation with { Status = OperationStatus.Cancelled, Message = string.Empty };
        Result = new ConversionResult(
            Operation,
            OperationStatus.Cancelled,
            string.Empty);
        _isSelected = true;
        NotifyExecutionChanged();
    }

    public void MarkNotProcessed()
    {
        _operationPercent = null;
        _executionStartTimestamp = null;
        _executionElapsed = null;
        _liveExecutionElapsed = null;
        Result = null;
        Operation = Operation with { Status = OperationStatus.NotProcessed, Message = string.Empty };
        _isSelected = true;
        NotifyExecutionChanged();
    }

    public void MarkNotSelected()
    {
        _isNotSelected = true;
        _isSelected = false;
        _operationPercent = null;
        _executionStartTimestamp = null;
        _executionElapsed = null;
        _liveExecutionElapsed = null;
        Result = null;
        NotifyExecutionChanged();
    }

    public void RefreshExecutionTime(TimeProvider timeProvider, long timestamp)
    {
        if (_executionStartTimestamp is not long start || _executionElapsed.HasValue) return;
        _liveExecutionElapsed = timeProvider.GetElapsedTime(start, timestamp);
        OnPropertyChanged(nameof(ExecutionTimeText));
    }

    public static string LocalizeStatus(
        OperationStatus status,
        ConversionTarget target = ConversionTarget.Skip,
        string message = "",
        string errorCode = "",
        LocalizationService? localization = null)
    {
        localization ??= LocalizationService.Current;
        return status switch
        {
            OperationStatus.Ready when target == ConversionTarget.Copy => localization.Get("StatusReadyCopy"),
            OperationStatus.Ready => localization.Get("StatusReadyConvert"),
            OperationStatus.Skipped => localization.Get("StatusSkipped"),
            OperationStatus.Converting => localization.Get("StatusConverting"),
            OperationStatus.Succeeded when target == ConversionTarget.Copy => localization.Get("StatusCopied"),
            OperationStatus.Succeeded => localization.Get("StatusConverted"),
            OperationStatus.Conflict => localization.Get("StatusConflict"),
            OperationStatus.Failed when !string.IsNullOrWhiteSpace(message) || !string.IsNullOrWhiteSpace(errorCode) => localization.Format(
                "StatusFailedDetail",
                OperationMessageLocalizer.Localize(status, target, message, errorCode, localization)),
            OperationStatus.Failed => localization.Get("StatusFailed"),
            OperationStatus.EngineUnavailable or OperationStatus.Unsupported
                when !string.IsNullOrWhiteSpace(message) || !string.IsNullOrWhiteSpace(errorCode) => localization.Format(
                    "StatusUnavailableDetail",
                    OperationMessageLocalizer.Localize(status, target, message, errorCode, localization)),
            OperationStatus.EngineUnavailable or OperationStatus.Unsupported => localization.Get("StatusUnavailable"),
            OperationStatus.Cancelled => localization.Get("StatusCancelled"),
            OperationStatus.NotProcessed => localization.Get("StatusNotProcessed"),
            _ => localization.Get("StatusUnknown")
        };
    }

    public static string FormatFileSize(long bytes)
    {
        return LocalizationService.Current.FormatFileSize(bytes);
    }

    public static string FormatExecutionTime(TimeSpan elapsed)
    {
        return LocalizationService.Current.FormatExecutionTime(elapsed);
    }

    private void FreezeExecutionTime(TimeProvider timeProvider, long timestamp)
    {
        if (_executionStartTimestamp is long start)
            _executionElapsed = timeProvider.GetElapsedTime(start, timestamp);
        _liveExecutionElapsed = null;
    }

    private void NotifyExecutionChanged()
    {
        OnPropertyChanged(nameof(Operation));
        OnPropertyChanged(nameof(CanSelect));
        OnPropertyChanged(nameof(IsSelected));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusTone));
        OnPropertyChanged(nameof(Message));
        OnPropertyChanged(nameof(ExecutionTimeText));
    }

    public void RefreshLocalization() => OnLanguageChanged(this, EventArgs.Empty);

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ActionLabel));
        OnPropertyChanged(nameof(FileSizeText));
        OnPropertyChanged(nameof(ExecutionTimeText));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(Message));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
