using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public sealed class LegacyOfficeToMarkdownConversionAdapter : IConversionAdapter
{
    private readonly OfficeApplicationKind _application;
    private readonly bool _officeAvailable;
    private readonly IMicrosoftOfficeWorkerRunner _officeWorkerRunner;
    private readonly IDoclingWorkerRunner _doclingWorkerRunner;
    private readonly IOutputResultValidator _validator;
    private readonly SafeFileOperationExecutor _executor;

    public LegacyOfficeToMarkdownConversionAdapter(
        OfficeApplicationKind application,
        IMicrosoftOfficeCapabilityDetector capabilityDetector,
        IMicrosoftOfficeWorkerRunner officeWorkerRunner,
        IDoclingWorkerRunner doclingWorkerRunner,
        IOutputResultValidator validator,
        string? temporaryRoot = null)
    {
        _application = application;
        _officeAvailable = capabilityDetector.Detect()
            .Single(item => item.Application == application)
            .IsAvailable;
        _officeWorkerRunner = officeWorkerRunner;
        _doclingWorkerRunner = doclingWorkerRunner;
        _validator = validator;
        _executor = new SafeFileOperationExecutor(validator, temporaryRoot);
    }

    public bool IsAvailable =>
        _officeAvailable
        && _officeWorkerRunner.IsAvailable
        && _doclingWorkerRunner.IsAvailable;

    public string AvailabilityMessage =>
        !_officeAvailable
            ? _application.ToRequiredMessage()
            : !_officeWorkerRunner.IsAvailable
                ? "Компонент преобразования Microsoft Office недоступен."
                : !_doclingWorkerRunner.IsAvailable
                    ? "Компонент Markdown недоступен."
                    : $"{_application.ToDisplayName()} и компонент Markdown доступны.";

    public bool CanConvert(SourceFormat sourceFormat, ConversionTarget target) =>
        target == ConversionTarget.Markdown && GetRequiredOfficeApplication(sourceFormat) == _application;

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
                new ConversionDiagnostic("office_mapping_unsupported")));
        }

        if (!IsAvailable)
        {
            var diagnostic = !_officeAvailable
                ? "office_application_missing"
                : !_officeWorkerRunner.IsAvailable
                    ? "worker_missing"
                    : "docling_worker_missing";

            return Task.FromResult(new ConversionResult(
                operation,
                OperationStatus.EngineUnavailable,
                AvailabilityMessage,
                new ConversionDiagnostic(diagnostic)));
        }

        return _executor.ExecuteAsync(
            operation,
            operation.Target,
            async (temporaryOutput, token) =>
            {
                var (intermediateTarget, intermediateFormat, intermediateExt) = _application switch
                {
                    OfficeApplicationKind.Word => (ConversionTarget.Docx, SourceFormat.Docx, ".docx"),
                    OfficeApplicationKind.Excel => (ConversionTarget.Xlsx, SourceFormat.Xlsx, ".xlsx"),
                    OfficeApplicationKind.PowerPoint => (ConversionTarget.Pptx, SourceFormat.Pptx, ".pptx"),
                    _ => throw new InvalidOperationException($"Unsupported application: {_application}")
                };

                var tempDir = Path.GetDirectoryName(temporaryOutput)!;
                var intermediatePath = Path.Combine(tempDir, $"intermediate_{Guid.NewGuid():N}{intermediateExt}");

                try
                {
                    // Step 1: Legacy Office to modern intermediate OOXML
                    var officeResult = await _officeWorkerRunner.RunAsync(
                        new OfficeWorkerRequest(
                            _application,
                            operation.SourcePath,
                            intermediatePath,
                            intermediateTarget),
                        token);

                    if (!officeResult.Success)
                    {
                        return new TemporaryOutputProductionResult(
                            false,
                            officeResult.ErrorCode,
                            ToOfficeUserMessage(officeResult),
                            officeResult.TimedOut,
                            officeResult.ExitCode,
                            officeResult.HasStandardOutput,
                            officeResult.HasStandardError,
                            officeResult.HResult);
                    }

                    if (!File.Exists(intermediatePath))
                    {
                        return new TemporaryOutputProductionResult(
                            false,
                            "intermediate_output_missing",
                            "Промежуточный файл не был создан.");
                    }

                    var intermediateValidation = _validator.Validate(intermediatePath, intermediateTarget);
                    if (!intermediateValidation.IsValid)
                    {
                        return new TemporaryOutputProductionResult(
                            false,
                            intermediateValidation.ErrorCode,
                            "Промежуточный файл не прошёл проверку.");
                    }

                    // Step 2: Modern intermediate OOXML to Markdown via Docling
                    var doclingResult = await _doclingWorkerRunner.RunAsync(
                        new DoclingWorkerRequest(
                            Guid.NewGuid().ToString("N"),
                            intermediatePath,
                            temporaryOutput,
                            intermediateFormat),
                        token);

                    if (!doclingResult.Success)
                    {
                        return new TemporaryOutputProductionResult(
                            false,
                            doclingResult.ErrorCode,
                            doclingResult.ErrorMessage,
                            doclingResult.TimedOut,
                            doclingResult.ExitCode,
                            doclingResult.HasStandardOutput,
                            doclingResult.HasStandardError);
                    }

                    return new TemporaryOutputProductionResult(true);
                }
                finally
                {
                    try
                    {
                        if (File.Exists(intermediatePath))
                        {
                            File.Delete(intermediatePath);
                        }
                    }
                    catch
                    {
                    }
                }
            },
            "Преобразовано.",
            progress,
            cancellationToken);
    }

    private static OfficeApplicationKind? GetRequiredOfficeApplication(SourceFormat source) =>
        source switch
        {
            SourceFormat.Doc => OfficeApplicationKind.Word,
            SourceFormat.Xls => OfficeApplicationKind.Excel,
            SourceFormat.Ppt => OfficeApplicationKind.PowerPoint,
            _ => null
        };

    private string ToOfficeUserMessage(OfficeWorkerExecutionResult result) =>
        result.ErrorCode switch
        {
            "powerpoint_already_running" =>
                "PowerPoint уже запущен. Закройте его и повторите преобразование.",
            "powerpoint_session_ownership_lost" =>
                "Сеанс PowerPoint изменён пользователем. Текущий файл не преобразован, "
                + "чтобы не закрыть пользовательскую презентацию.",
            "office_com_failure" when result.HResult == unchecked((int)0x80080005) =>
                $"{_application.ToDisplayName()} не запустился через Windows. "
                + $"Откройте {ShortDisplayName()} вручную, устраните ошибку запуска, "
                + "закройте приложение и повторите. (HRESULT 0x80080005)",
            "office_com_failure" =>
                $"{_application.ToDisplayName()} вернул ошибку при открытии или сохранении файла"
                + FormatHResult(result.HResult) + ".",
            _ when result.TimedOut =>
                "Преобразование превысило допустимое время.",
            _ => "Не удалось преобразовать файл в Microsoft Office."
        };

    private static string FormatHResult(int? hResult) => hResult is int value
        ? $" (HRESULT 0x{unchecked((uint)value):X8})"
        : string.Empty;

    private string ShortDisplayName() => _application switch
    {
        OfficeApplicationKind.Word => "Word",
        OfficeApplicationKind.Excel => "Excel",
        OfficeApplicationKind.PowerPoint => "PowerPoint",
        _ => _application.ToDisplayName()
    };
}
