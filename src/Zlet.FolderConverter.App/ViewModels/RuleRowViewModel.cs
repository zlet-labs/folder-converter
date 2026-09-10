using System.ComponentModel;
using System.Runtime.CompilerServices;
using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.App.Localization;

namespace Zlet.FolderConverter.App.ViewModels;

public sealed class RuleRowViewModel : INotifyPropertyChanged
{
    private readonly Action<SourceFormat, ConversionTarget> _selectionChanged;
    private readonly IReadOnlyList<ScannedFile> _extensionFiles;
    private readonly LocalizationService _localization;
    private ConversionTargetOption _selectedTarget;
    private bool _isSelected;

    public RuleRowViewModel(
        FormatCapability capability,
        int count,
        ConversionTarget selectedTarget,
        Action<SourceFormat, ConversionTarget> selectionChanged,
        IReadOnlyList<ScannedFile>? extensionFiles = null,
        LocalizationService? localization = null)
    {
        SourceFormat = capability.SourceFormat;
        Count = count;
        _extensionFiles = extensionFiles ?? [];
        _localization = localization ?? LocalizationService.Current;
        Targets = capability.AllowedTargets
            .Select(target => new ConversionTargetOption(target, target switch
            {
                ConversionTarget.Skip => _localization.Get("TargetSkip"),
                ConversionTarget.Copy => _localization.Get("TargetCopy"),
                ConversionTarget.Markdown => _localization.Get("TargetMarkdown"),
                ConversionTarget.Csv => _localization.Get("TargetCsvSheets"),
                ConversionTarget.Tsv => _localization.Get("TargetTsvSheets"),
                _ => target.ToDisplayName()
            }))
            .ToArray();
        _selectedTarget = Targets.Single(option => option.Target == selectedTarget);
        _selectionChanged = selectionChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public SourceFormat SourceFormat { get; }
    public FormatSemanticFamily SemanticFamily => SourceFormat.GetSemanticFamily();
    public string FormatLabel => SourceFormat switch
    {
        SourceFormat.Image => _localization.Get("FormatImage"),
        SourceFormat.Archive => _localization.Get("FormatArchive"),
        SourceFormat.Unknown => _localization.Get("FormatUnknown"),
        _ => SourceFormat.ToDisplayName()
    };
    public int Count { get; }
    public string ExtensionBreakdown => ExtensionBreakdownFormatter.Format(_extensionFiles, _localization);
    public bool HasExtensionBreakdown => !string.IsNullOrWhiteSpace(ExtensionBreakdown);
    public IReadOnlyList<ConversionTargetOption> Targets { get; private set; }
    public bool HasMultipleTargets => Targets.Count > 1;
    public bool IsSingleAction => Targets.Count <= 1;
    public string SingleActionReason => _localization.Get("RuleSingleActionReason");
    public string SingleActionTooltip => string.Format(_localization.Culture, _localization.Get("RuleOnlyActionAvailable"), SelectedTarget.Label);

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public ConversionTargetOption SelectedTarget
    {
        get => _selectedTarget;
        set
        {
            if (value is null || value == _selectedTarget)
            {
                return;
            }

            _selectedTarget = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SingleActionTooltip));
            _selectionChanged(SourceFormat, value.Target);
        }
    }

    public void RefreshLocalization()
    {
        var selected = _selectedTarget.Target;
        Targets = Targets.Select(option => new ConversionTargetOption(
            option.Target,
            option.Target switch
            {
                ConversionTarget.Skip => _localization.Get("TargetSkip"),
                ConversionTarget.Copy => _localization.Get("TargetCopy"),
                ConversionTarget.Markdown => _localization.Get("TargetMarkdown"),
                ConversionTarget.Csv => _localization.Get("TargetCsvSheets"),
                ConversionTarget.Tsv => _localization.Get("TargetTsvSheets"),
                _ => option.Target.ToDisplayName()
            })).ToArray();
        _selectedTarget = Targets.Single(option => option.Target == selected);
        OnPropertyChanged(nameof(Targets));
        OnPropertyChanged(nameof(SelectedTarget));
        OnPropertyChanged(nameof(FormatLabel));
        OnPropertyChanged(nameof(ExtensionBreakdown));
        OnPropertyChanged(nameof(HasExtensionBreakdown));
        OnPropertyChanged(nameof(SingleActionReason));
        OnPropertyChanged(nameof(SingleActionTooltip));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
