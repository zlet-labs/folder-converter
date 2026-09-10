using System.Security.Cryptography;
using System.Text;
using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.Tests;

public sealed class DoclingConversionAdapterTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "zlet-docling-adapter-tests",
        Guid.NewGuid().ToString("N"));

    public DoclingConversionAdapterTests() => Directory.CreateDirectory(_rootPath);

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Converts_docx_to_markdown_successfully()
    {
        var fixture = GetFixturePath("F08_structured.docx");
        var sourcePath = CopyFixture(fixture, "report.docx");
        var operation = CreateOperation(sourcePath, "report.md", SourceFormat.Docx);
        var runner = new DoclingWorkerProcessRunner();
        if (!runner.IsAvailable) return;

        var adapter = new DoclingConversionAdapter(runner, new OutputResultValidator());
        var result = await adapter.ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Succeeded, result.Status);
        Assert.True(File.Exists(operation.TargetPath));
        var content = await File.ReadAllTextAsync(operation.TargetPath, Encoding.UTF8);
        Assert.NotEmpty(content);
        Assert.Contains("Architecture Review", content);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Converts_pptx_to_markdown_successfully()
    {
        var fixture = GetFixturePath("F09_slides.pptx");
        var sourcePath = CopyFixture(fixture, "slides.pptx");
        var operation = CreateOperation(sourcePath, "slides.md", SourceFormat.Pptx);
        var runner = new DoclingWorkerProcessRunner();
        if (!runner.IsAvailable) return;

        var adapter = new DoclingConversionAdapter(runner, new OutputResultValidator());
        var result = await adapter.ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Succeeded, result.Status);
        Assert.True(File.Exists(operation.TargetPath));
        var content = await File.ReadAllTextAsync(operation.TargetPath, Encoding.UTF8);
        Assert.NotEmpty(content);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Converts_xlsx_to_markdown_with_sheet_headings_and_tables()
    {
        var fixture = GetFixturePath("F10_sheets.xlsx");
        var sourcePath = CopyFixture(fixture, "sheets.xlsx");
        var operation = CreateOperation(sourcePath, "sheets.md", SourceFormat.Xlsx);
        var runner = new DoclingWorkerProcessRunner();
        if (!runner.IsAvailable) return;

        var adapter = new DoclingConversionAdapter(runner, new OutputResultValidator());
        var result = await adapter.ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Succeeded, result.Status);
        Assert.True(File.Exists(operation.TargetPath));
        var content = await File.ReadAllTextAsync(operation.TargetPath, Encoding.UTF8);
        Assert.NotEmpty(content);
        Assert.Contains("## ", content);
        Assert.Contains("|", content);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Converts_html_to_markdown_successfully()
    {
        var fixture = GetFixturePath("F11_structural.html");
        var sourcePath = CopyFixture(fixture, "page.html");
        var operation = CreateOperation(sourcePath, "page.md", SourceFormat.Html);
        var runner = new DoclingWorkerProcessRunner();
        if (!runner.IsAvailable) return;

        var adapter = new DoclingConversionAdapter(runner, new OutputResultValidator());
        var result = await adapter.ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Succeeded, result.Status);
        Assert.True(File.Exists(operation.TargetPath));
        var content = await File.ReadAllTextAsync(operation.TargetPath, Encoding.UTF8);
        Assert.NotEmpty(content);
        Assert.DoesNotContain("<script>", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<style>", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Converts_txt_to_markdown_preserving_paragraphs()
    {
        var textContent = "First paragraph line 1\nFirst paragraph line 2\n\nSecond paragraph line 1\n";
        var sourcePath = Path.Combine(_rootPath, "sample.txt");
        await File.WriteAllTextAsync(sourcePath, textContent, new UTF8Encoding(false));
        var operation = CreateOperation(sourcePath, "sample.md", SourceFormat.Txt);
        var runner = new DoclingWorkerProcessRunner();
        if (!runner.IsAvailable) return;

        var adapter = new DoclingConversionAdapter(runner, new OutputResultValidator());
        var result = await adapter.ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Succeeded, result.Status);
        Assert.True(File.Exists(operation.TargetPath));
        var content = await File.ReadAllTextAsync(operation.TargetPath, Encoding.UTF8);
        Assert.Equal(textContent.Replace("\r\n", "\n").Trim(), content.Replace("\r\n", "\n").Trim());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Converts_digital_pdf_to_markdown_successfully()
    {
        var fixture = GetFixturePath("F01_simple_text.pdf");
        var sourcePath = CopyFixture(fixture, "document.pdf");
        var operation = CreateOperation(sourcePath, "document.md", SourceFormat.Pdf);
        var runner = new DoclingWorkerProcessRunner();
        if (!runner.IsAvailable) return;

        var adapter = new DoclingConversionAdapter(runner, new OutputResultValidator());
        var result = await adapter.ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Succeeded, result.Status);
        Assert.True(File.Exists(operation.TargetPath));
        var content = await File.ReadAllTextAsync(operation.TargetPath, Encoding.UTF8);
        Assert.NotEmpty(content);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Scanned_pdf_fails_gracefully_with_unsupported_error_code()
    {
        var fixture = GetFixturePath("F06_scanned.pdf");
        var sourcePath = CopyFixture(fixture, "scanned.pdf");
        var operation = CreateOperation(sourcePath, "scanned.md", SourceFormat.Pdf);
        var runner = new DoclingWorkerProcessRunner();
        if (!runner.IsAvailable) return;

        var adapter = new DoclingConversionAdapter(runner, new OutputResultValidator());
        var result = await adapter.ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Failed, result.Status);
        Assert.Equal("scanned_pdf_unsupported", result.Diagnostic?.ErrorCode);
        Assert.False(File.Exists(operation.TargetPath));
    }

    [Fact]
    public async Task Conflict_policy_protects_existing_target_file()
    {
        var sourcePath = Path.Combine(_rootPath, "sample.txt");
        await File.WriteAllTextAsync(sourcePath, "hello world", Encoding.UTF8);
        var operation = CreateOperation(sourcePath, "conflict.md", SourceFormat.Txt);
        Directory.CreateDirectory(Path.GetDirectoryName(operation.TargetPath)!);
        await File.WriteAllTextAsync(operation.TargetPath, "original content", Encoding.UTF8);

        var mockRunner = new FakeDoclingWorkerRunner(new DoclingWorkerExecutionResult(Success: true));
        var adapter = new DoclingConversionAdapter(mockRunner, new OutputResultValidator());
        var result = await adapter.ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Conflict, result.Status);
        Assert.Equal("original content", await File.ReadAllTextAsync(operation.TargetPath, Encoding.UTF8));
    }

    [Fact]
    public async Task Target_directory_conflict_returns_conflict_status()
    {
        var sourcePath = Path.Combine(_rootPath, "sample.txt");
        await File.WriteAllTextAsync(sourcePath, "hello world", Encoding.UTF8);
        var operation = CreateOperation(sourcePath, "conflict_dir.md", SourceFormat.Txt);
        Directory.CreateDirectory(operation.TargetPath);

        var mockRunner = new FakeDoclingWorkerRunner(new DoclingWorkerExecutionResult(Success: true));
        var adapter = new DoclingConversionAdapter(mockRunner, new OutputResultValidator());
        var result = await adapter.ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task Target_outside_output_root_is_rejected()
    {
        var sourcePath = Path.Combine(_rootPath, "sample.txt");
        await File.WriteAllTextAsync(sourcePath, "hello world", Encoding.UTF8);
        var outputRoot = Path.Combine(_rootPath, "_converted");
        var targetPath = Path.Combine(outputRoot, "..", "outside.md");
        var operation = CreateOperation(sourcePath, "sample.md", SourceFormat.Txt) with
        {
            TargetPath = targetPath
        };

        var mockRunner = new FakeDoclingWorkerRunner(new DoclingWorkerExecutionResult(Success: true));
        var adapter = new DoclingConversionAdapter(mockRunner, new OutputResultValidator());
        var result = await adapter.ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Failed, result.Status);
        Assert.False(File.Exists(Path.GetFullPath(targetPath)));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Source_file_hash_is_unchanged_after_conversion()
    {
        var sourcePath = Path.Combine(_rootPath, "sample.txt");
        await File.WriteAllTextAsync(sourcePath, "immutable source content", Encoding.UTF8);
        var hashBefore = ComputeSha256(sourcePath);
        var operation = CreateOperation(sourcePath, "sample.md", SourceFormat.Txt);
        var runner = new DoclingWorkerProcessRunner();
        if (!runner.IsAvailable) return;

        var adapter = new DoclingConversionAdapter(runner, new OutputResultValidator());
        var result = await adapter.ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Succeeded, result.Status);
        var hashAfter = ComputeSha256(sourcePath);
        Assert.Equal(hashBefore, hashAfter);
    }

    [Fact]
    public async Task Mock_runner_error_returns_failed_status_with_diagnostic()
    {
        var sourcePath = Path.Combine(_rootPath, "sample.txt");
        await File.WriteAllTextAsync(sourcePath, "hello", Encoding.UTF8);
        var operation = CreateOperation(sourcePath, "sample.md", SourceFormat.Txt);
        var mockRunner = new FakeDoclingWorkerRunner(new DoclingWorkerExecutionResult(
            Success: false,
            ErrorCode: "docling_conversion_failed",
            ErrorMessage: "Simulated worker error"));

        var adapter = new DoclingConversionAdapter(mockRunner, new OutputResultValidator());
        var result = await adapter.ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Failed, result.Status);
        Assert.Equal("docling_conversion_failed", result.Diagnostic?.ErrorCode);
        Assert.False(File.Exists(operation.TargetPath));
    }

    [Fact]
    public async Task Mock_runner_timeout_returns_timeout_error_code()
    {
        var sourcePath = Path.Combine(_rootPath, "sample.txt");
        await File.WriteAllTextAsync(sourcePath, "hello", Encoding.UTF8);
        var operation = CreateOperation(sourcePath, "sample.md", SourceFormat.Txt);
        var mockRunner = new FakeDoclingWorkerRunner(new DoclingWorkerExecutionResult(
            Success: false,
            ErrorCode: "docling_worker_timeout",
            ErrorMessage: "Timeout exceeded",
            TimedOut: true));

        var adapter = new DoclingConversionAdapter(mockRunner, new OutputResultValidator());
        var result = await adapter.ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Failed, result.Status);
        Assert.Equal("docling_worker_timeout", result.Diagnostic?.ErrorCode);
        Assert.False(File.Exists(operation.TargetPath));
    }

    private string CopyFixture(string fixturePath, string targetName)
    {
        var dest = Path.Combine(_rootPath, targetName);
        File.Copy(fixturePath, dest, overwrite: true);
        return dest;
    }

    private static string GetFixturePath(string fixtureName)
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            var candidate = Path.Combine(current, "evaluation", "fixtures", fixtureName);
            if (File.Exists(candidate)) return candidate;
            current = Path.GetDirectoryName(current);
        }
        throw new FileNotFoundException($"Fixture {fixtureName} not found.");
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

    private static string ComputeSha256(string path)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    private sealed class FakeDoclingWorkerRunner(DoclingWorkerExecutionResult result) : IDoclingWorkerRunner
    {
        public bool IsAvailable => true;
        public string AvailabilityMessage => "";

        public async Task<DoclingWorkerExecutionResult> RunAsync(
            DoclingWorkerRequest request,
            CancellationToken cancellationToken)
        {
            if (result.Success && !File.Exists(request.OutputPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(request.OutputPath)!);
                await File.WriteAllTextAsync(request.OutputPath, "# Generated markdown", Encoding.UTF8);
            }
            return result;
        }

        public Task BeginBatchAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task EndBatchAsync() => Task.CompletedTask;
    }
}
