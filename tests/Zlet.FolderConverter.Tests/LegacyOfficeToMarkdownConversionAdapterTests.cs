using System.Text;
using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.Tests;

public sealed class LegacyOfficeToMarkdownConversionAdapterTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "zlet-legacy-office-md-tests",
        Guid.NewGuid().ToString("N"));

    public LegacyOfficeToMarkdownConversionAdapterTests() => Directory.CreateDirectory(_rootPath);

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    [Theory]
    [InlineData(OfficeApplicationKind.Word, SourceFormat.Doc, "doc.doc", "doc.md")]
    [InlineData(OfficeApplicationKind.Excel, SourceFormat.Xls, "sheet.xls", "sheet.md")]
    [InlineData(OfficeApplicationKind.PowerPoint, SourceFormat.Ppt, "slides.ppt", "slides.md")]
    public async Task Two_stage_conversion_succeeds_and_deletes_intermediate_file(
        OfficeApplicationKind application,
        SourceFormat sourceFormat,
        string sourceName,
        string targetName)
    {
        var sourcePath = Path.Combine(_rootPath, sourceName);
        await File.WriteAllTextAsync(sourcePath, "legacy binary content", Encoding.UTF8);
        var operation = CreateOperation(sourcePath, targetName, sourceFormat);

        string? generatedIntermediatePath = null;

        var fakeOfficeRunner = new ActionOfficeWorkerRunner(req =>
        {
            generatedIntermediatePath = req.OutputPath;
            var requiredPart = application switch
            {
                OfficeApplicationKind.Word => "word/document.xml",
                OfficeApplicationKind.Excel => "xl/workbook.xml",
                OfficeApplicationKind.PowerPoint => "ppt/presentation.xml",
                _ => "word/document.xml"
            };
            OutputResultValidatorTests.CreateZip(req.OutputPath, "[Content_Types].xml", requiredPart);
            return Task.FromResult(new OfficeWorkerExecutionResult(Success: true));
        });

        var fakeDoclingRunner = new ActionDoclingWorkerRunner(async req =>
        {
            // Verify intermediate file exists while Docling is executing
            Assert.True(File.Exists(req.SourcePath));
            await File.WriteAllTextAsync(req.OutputPath, "# Markdown from intermediate", Encoding.UTF8);
            return new DoclingWorkerExecutionResult(Success: true);
        });

        var adapter = new LegacyOfficeToMarkdownConversionAdapter(
            application,
            new MicrosoftOfficeCapabilityTests.FakeCapabilityDetector([application]),
            fakeOfficeRunner,
            fakeDoclingRunner,
            new OutputResultValidator(),
            Path.Combine(_rootPath, "temp_work"));

        var result = await adapter.ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Succeeded, result.Status);
        Assert.True(File.Exists(operation.TargetPath));
        var markdown = await File.ReadAllTextAsync(operation.TargetPath, Encoding.UTF8);
        Assert.Equal("# Markdown from intermediate", markdown);

        // Ensure intermediate temporary file was deleted!
        Assert.NotNull(generatedIntermediatePath);
        Assert.False(File.Exists(generatedIntermediatePath));
    }

    [Fact]
    public async Task Intermediate_file_is_deleted_when_docling_fails()
    {
        var sourcePath = Path.Combine(_rootPath, "doc.doc");
        await File.WriteAllTextAsync(sourcePath, "legacy binary content", Encoding.UTF8);
        var operation = CreateOperation(sourcePath, "doc.md", SourceFormat.Doc);

        string? generatedIntermediatePath = null;

        var fakeOfficeRunner = new ActionOfficeWorkerRunner(req =>
        {
            generatedIntermediatePath = req.OutputPath;
            OutputResultValidatorTests.CreateZip(req.OutputPath, "[Content_Types].xml", "word/document.xml");
            return Task.FromResult(new OfficeWorkerExecutionResult(Success: true));
        });

        var fakeDoclingRunner = new ActionDoclingWorkerRunner(req =>
        {
            return Task.FromResult(new DoclingWorkerExecutionResult(
                Success: false,
                ErrorCode: "docling_conversion_failed",
                ErrorMessage: "Failed to parse document"));
        });

        var adapter = new LegacyOfficeToMarkdownConversionAdapter(
            OfficeApplicationKind.Word,
            new MicrosoftOfficeCapabilityTests.FakeCapabilityDetector([OfficeApplicationKind.Word]),
            fakeOfficeRunner,
            fakeDoclingRunner,
            new OutputResultValidator(),
            Path.Combine(_rootPath, "temp_work"));

        var result = await adapter.ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Failed, result.Status);
        Assert.Equal("docling_conversion_failed", result.Diagnostic?.ErrorCode);
        Assert.False(File.Exists(operation.TargetPath));

        // Ensure intermediate file is cleaned up despite failure!
        Assert.NotNull(generatedIntermediatePath);
        Assert.False(File.Exists(generatedIntermediatePath));
    }

    [Fact]
    public async Task Intermediate_file_is_deleted_when_cancelled()
    {
        var sourcePath = Path.Combine(_rootPath, "doc.doc");
        await File.WriteAllTextAsync(sourcePath, "legacy binary content", Encoding.UTF8);
        var operation = CreateOperation(sourcePath, "doc.md", SourceFormat.Doc);

        using var cts = new CancellationTokenSource();
        string? generatedIntermediatePath = null;

        var fakeOfficeRunner = new ActionOfficeWorkerRunner(req =>
        {
            generatedIntermediatePath = req.OutputPath;
            OutputResultValidatorTests.CreateZip(req.OutputPath, "[Content_Types].xml", "word/document.xml");
            return Task.FromResult(new OfficeWorkerExecutionResult(Success: true));
        });

        var fakeDoclingRunner = new ActionDoclingWorkerRunner(req =>
        {
            cts.Cancel();
            cts.Token.ThrowIfCancellationRequested();
            return Task.FromResult(new DoclingWorkerExecutionResult(Success: true));
        });

        var adapter = new LegacyOfficeToMarkdownConversionAdapter(
            OfficeApplicationKind.Word,
            new MicrosoftOfficeCapabilityTests.FakeCapabilityDetector([OfficeApplicationKind.Word]),
            fakeOfficeRunner,
            fakeDoclingRunner,
            new OutputResultValidator(),
            Path.Combine(_rootPath, "temp_work"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            adapter.ConvertAsync(operation, cts.Token));

        Assert.False(File.Exists(operation.TargetPath));
        Assert.NotNull(generatedIntermediatePath);
        Assert.False(File.Exists(generatedIntermediatePath));
    }

    [Fact]
    public async Task Fails_with_diagnostic_when_office_is_unavailable()
    {
        var sourcePath = Path.Combine(_rootPath, "doc.doc");
        await File.WriteAllTextAsync(sourcePath, "legacy binary content", Encoding.UTF8);
        var operation = CreateOperation(sourcePath, "doc.md", SourceFormat.Doc);

        var adapter = new LegacyOfficeToMarkdownConversionAdapter(
            OfficeApplicationKind.Word,
            new MicrosoftOfficeCapabilityTests.FakeCapabilityDetector([]),
            new ActionOfficeWorkerRunner(_ => Task.FromResult(new OfficeWorkerExecutionResult(true))),
            new ActionDoclingWorkerRunner(_ => Task.FromResult(new DoclingWorkerExecutionResult(true))),
            new OutputResultValidator());

        var result = await adapter.ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.EngineUnavailable, result.Status);
        Assert.Equal("office_application_missing", result.Diagnostic?.ErrorCode);
    }

    [Fact]
    public async Task Fails_with_diagnostic_when_docling_is_unavailable()
    {
        var sourcePath = Path.Combine(_rootPath, "doc.doc");
        await File.WriteAllTextAsync(sourcePath, "legacy binary content", Encoding.UTF8);
        var operation = CreateOperation(sourcePath, "doc.md", SourceFormat.Doc);

        var adapter = new LegacyOfficeToMarkdownConversionAdapter(
            OfficeApplicationKind.Word,
            new MicrosoftOfficeCapabilityTests.FakeCapabilityDetector([OfficeApplicationKind.Word]),
            new ActionOfficeWorkerRunner(_ => Task.FromResult(new OfficeWorkerExecutionResult(true))),
            new UnavailableDoclingWorkerRunner(),
            new OutputResultValidator());

        var result = await adapter.ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.EngineUnavailable, result.Status);
        Assert.Equal("docling_worker_missing", result.Diagnostic?.ErrorCode);
    }

    [Fact]
    public async Task Protects_against_target_collision_before_running_office()
    {
        var sourcePath = Path.Combine(_rootPath, "doc.doc");
        await File.WriteAllTextAsync(sourcePath, "legacy binary content", Encoding.UTF8);
        var operation = CreateOperation(sourcePath, "doc.md", SourceFormat.Doc);
        Directory.CreateDirectory(Path.GetDirectoryName(operation.TargetPath)!);
        await File.WriteAllTextAsync(operation.TargetPath, "# existing target", Encoding.UTF8);

        var officeCalls = 0;
        var fakeOfficeRunner = new ActionOfficeWorkerRunner(_ =>
        {
            officeCalls++;
            return Task.FromResult(new OfficeWorkerExecutionResult(true));
        });

        var adapter = new LegacyOfficeToMarkdownConversionAdapter(
            OfficeApplicationKind.Word,
            new MicrosoftOfficeCapabilityTests.FakeCapabilityDetector([OfficeApplicationKind.Word]),
            fakeOfficeRunner,
            new ActionDoclingWorkerRunner(_ => Task.FromResult(new DoclingWorkerExecutionResult(true))),
            new OutputResultValidator());

        var result = await adapter.ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Conflict, result.Status);
        Assert.Equal(0, officeCalls);
        Assert.Equal("# existing target", await File.ReadAllTextAsync(operation.TargetPath, Encoding.UTF8));
    }

    private PlannedOperation CreateOperation(
        string sourcePath,
        string targetRelativePath,
        SourceFormat format) =>
        new(
            sourcePath,
            Path.GetRelativePath(_rootPath, sourcePath),
            format,
            ConversionTarget.Markdown,
            ".md",
            Path.Combine(_rootPath, "_converted", targetRelativePath),
            true,
            OperationStatus.Ready,
            "ready",
            Path.Combine(_rootPath, "_converted"));

    private sealed class ActionOfficeWorkerRunner(Func<OfficeWorkerRequest, Task<OfficeWorkerExecutionResult>> handler)
        : IMicrosoftOfficeWorkerRunner
    {
        public bool IsAvailable => true;

        public Task<OfficeWorkerExecutionResult> RunAsync(OfficeWorkerRequest request, CancellationToken cancellationToken) =>
            handler(request);

        public Task BeginBatchAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task EndBatchAsync() => Task.CompletedTask;
    }

    private sealed class ActionDoclingWorkerRunner(Func<DoclingWorkerRequest, Task<DoclingWorkerExecutionResult>> handler)
        : IDoclingWorkerRunner
    {
        public bool IsAvailable => true;
        public string AvailabilityMessage => "";

        public Task<DoclingWorkerExecutionResult> RunAsync(DoclingWorkerRequest request, CancellationToken cancellationToken) =>
            handler(request);

        public Task BeginBatchAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task EndBatchAsync() => Task.CompletedTask;
    }

    private sealed class UnavailableDoclingWorkerRunner : IDoclingWorkerRunner
    {
        public bool IsAvailable => false;
        public string AvailabilityMessage => "Docling unavailable";

        public Task<DoclingWorkerExecutionResult> RunAsync(DoclingWorkerRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new DoclingWorkerExecutionResult(false, ErrorCode: "docling_worker_missing"));

        public Task BeginBatchAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task EndBatchAsync() => Task.CompletedTask;
    }
}
