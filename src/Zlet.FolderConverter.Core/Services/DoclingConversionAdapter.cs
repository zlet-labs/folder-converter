using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public sealed class DoclingConversionAdapter : IConversionAdapter
{
    private readonly IDoclingWorkerRunner _workerRunner;
    private readonly SafeFileOperationExecutor _executor;

    public DoclingConversionAdapter(
        IDoclingWorkerRunner workerRunner,
        IOutputResultValidator validator,
        string? temporaryRoot = null)
    {
        _workerRunner = workerRunner;
        _executor = new SafeFileOperationExecutor(validator, temporaryRoot);
    }

    public bool IsAvailable => _workerRunner.IsAvailable;

    public string AvailabilityMessage => _workerRunner.AvailabilityMessage;

    public bool CanConvert(SourceFormat sourceFormat, ConversionTarget target) =>
        target == ConversionTarget.Markdown
        && sourceFormat is SourceFormat.Pdf
            or SourceFormat.Docx
            or SourceFormat.Pptx
            or SourceFormat.Xlsx
            or SourceFormat.Html
            or SourceFormat.Txt;

    public Task<ConversionResult> ConvertAsync(
        PlannedOperation operation,
        CancellationToken cancellationToken) =>
        ConvertAsync(operation, progress: null, cancellationToken);

    public Task<ConversionResult> ConvertAsync(
        PlannedOperation operation,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        if (!CanConvert(operation.SourceFormat, operation.Target))
        {
            return Task.FromResult(new ConversionResult(
                operation,
                OperationStatus.Unsupported,
                "Выбранное преобразование не поддерживается.",
                new ConversionDiagnostic("markdown_mapping_unsupported")));
        }

        if (!IsAvailable)
        {
            return Task.FromResult(new ConversionResult(
                operation,
                OperationStatus.EngineUnavailable,
                AvailabilityMessage,
                new ConversionDiagnostic("docling_worker_missing")));
        }

        return _executor.ExecuteAsync(
            operation,
            operation.Target,
            async (temporaryOutput, token) =>
            {
                var request = new DoclingWorkerRequest(
                    Guid.NewGuid().ToString("N"),
                    operation.SourcePath,
                    temporaryOutput,
                    operation.SourceFormat);

                var workerResult = await _workerRunner.RunAsync(request, token);
                return workerResult.Success
                    ? new TemporaryOutputProductionResult(true)
                    : new TemporaryOutputProductionResult(
                        false,
                        workerResult.ErrorCode,
                        ToUserMessage(workerResult),
                        workerResult.TimedOut,
                        workerResult.ExitCode,
                        workerResult.HasStandardOutput,
                        workerResult.HasStandardError);
            },
            "Преобразовано.",
            progress,
            cancellationToken);
    }

    private static string ToUserMessage(DoclingWorkerExecutionResult result) =>
        result.ErrorCode switch
        {
            "scanned_pdf_unsupported" =>
                "PDF не содержит извлекаемого текста (возможно, отсканированный документ). Оптическое распознавание текста (OCR) не поддерживается.",
            "docling_worker_timeout" when result.TimedOut =>
                "Преобразование превысило допустимое время.",
            "docling_worker_missing" =>
                "Компонент Markdown недоступен.",
            "docling_version_incompatible" =>
                "Версия компонента Markdown несовместима с текущим приложением.",
            "docling_worker_start_failure" =>
                "Не удалось запустить процесс Markdown.",
            _ when !string.IsNullOrWhiteSpace(result.ErrorMessage) =>
                result.ErrorMessage,
            _ => "Не удалось преобразовать документ в Markdown."
        };
}
