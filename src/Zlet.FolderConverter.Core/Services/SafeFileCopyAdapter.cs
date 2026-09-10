using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public sealed class SafeFileCopyAdapter : IConversionAdapter
{
    private readonly SafeFileOperationExecutor _executor;

    public SafeFileCopyAdapter(
        IOutputResultValidator validator,
        string? temporaryRoot = null)
    {
        _executor = new SafeFileOperationExecutor(validator, temporaryRoot);
    }

    public bool IsAvailable => true;

    public string AvailabilityMessage => "Безопасное копирование доступно.";

    public bool CanConvert(SourceFormat sourceFormat, ConversionTarget target) =>
        FormatCapabilityCatalog.IsSafeCopy(sourceFormat, target);

    public Task<ConversionResult> ConvertAsync(
        PlannedOperation operation,
        CancellationToken cancellationToken) =>
        ConvertAsync(operation, progress: null, cancellationToken);

    public Task<ConversionResult> ConvertAsync(
        PlannedOperation operation,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        var validationTarget = operation.SourceFormat switch
        {
            SourceFormat.Docx => ConversionTarget.Docx,
            SourceFormat.Xlsx => ConversionTarget.Xlsx,
            SourceFormat.Pptx => ConversionTarget.Pptx,
            SourceFormat.Pdf => ConversionTarget.Pdf,
            SourceFormat.Csv => ConversionTarget.Csv,
            SourceFormat.Tsv => ConversionTarget.Tsv,
            SourceFormat.Epub or SourceFormat.Image or SourceFormat.Html or SourceFormat.Txt => ConversionTarget.Copy,
            _ => ConversionTarget.Skip
        };
        if (validationTarget == ConversionTarget.Skip)
        {
            return Task.FromResult(new ConversionResult(
                operation,
                OperationStatus.Unsupported,
                "Копирование этого формата не поддерживается.",
                new ConversionDiagnostic("copy_mapping_unsupported")));
        }

        return _executor.ExecuteAsync(
            operation,
            validationTarget,
            async (temporaryOutput, token) =>
            {
                await using var source = new FileStream(
                    operation.SourcePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await using var destination = new FileStream(
                    temporaryOutput,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await source.CopyToAsync(destination, token);
                await destination.FlushAsync(token);
                return new TemporaryOutputProductionResult(true);
            },
            "Скопировано.",
            progress,
            cancellationToken);
    }
}
