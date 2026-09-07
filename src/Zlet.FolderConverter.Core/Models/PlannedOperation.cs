namespace Zlet.FolderConverter.Core.Models;

public sealed record PlannedOperation(
    string SourcePath,
    string RelativePath,
    SourceFormat SourceFormat,
    ConversionTarget Target,
    string TargetExtension,
    string TargetPath,
    bool AdapterAvailable,
    OperationStatus Status,
    string Message,
    string OutputRootPath = "",
    string SourceRootPath = "",
    long SourceSizeBytes = 0,
    string WorksheetName = "",
    WorksheetVisibility WorksheetVisibility = WorksheetVisibility.Visible,
    bool WorksheetIsEmpty = false,
    bool DefaultSelected = true,
    string ResultRelativePath = "")
{
    public string OperationKey => $"{SourcePath}\0{WorksheetName}\0{Target}";
    public string TargetFormat => Target == ConversionTarget.Skip
        ? "Пропускаем"
        : Target.ToDisplayName();

    public bool IsWorksheetOperation => !string.IsNullOrWhiteSpace(WorksheetName);
    public bool IsWorksheetExport => SourceFormat is SourceFormat.Xls or SourceFormat.Xlsx
        && Target is ConversionTarget.Csv or ConversionTarget.Tsv;
}
