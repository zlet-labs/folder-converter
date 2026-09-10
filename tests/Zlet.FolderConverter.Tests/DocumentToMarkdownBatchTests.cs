using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Zlet.FolderConverter.App.Localization;
using Zlet.FolderConverter.App.ViewModels;
using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.Tests;

public sealed class DocumentToMarkdownBatchTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "zlet-batch-md-tests",
        Guid.NewGuid().ToString("N"));

    private readonly string _sourceDir;
    private readonly string _outputDir;

    public DocumentToMarkdownBatchTests()
    {
        _sourceDir = Path.Combine(_rootPath, "source");
        _outputDir = Path.Combine(_rootPath, "output");
        Directory.CreateDirectory(_sourceDir);
        Directory.CreateDirectory(_outputDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    [MarkdownIntegrationFact]
    public async Task Mixed_batch_markdown_conversion_to_folder_and_report_verification()
    {
        var runner = new DoclingWorkerProcessRunner();

        // Populate mixed files
        CopyFixture("F08_structured.docx", "document.docx");
        CopyFixture("F09_slides.pptx", "presentation.pptx");
        CopyFixture("F10_sheets.xlsx", "data.xlsx");
        CopyFixture("F11_structural.html", "webpage.html");
        CopyFixture("F01_simple_text.pdf", "manual.pdf");
        CopyFixture("F06_scanned.pdf", "scanned.pdf");
        File.WriteAllText(Path.Combine(_sourceDir, "notes.txt"), "Important plain text notes\nLine 2");

        var sourceHashes = Directory.EnumerateFiles(_sourceDir, "*.*", SearchOption.AllDirectories)
            .ToDictionary(f => f, Hash);

        var viewModel = CreateViewModel();
        viewModel.SelectedFolder = _sourceDir;
        viewModel.SelectedOutputMode = OutputMode.Folder;
        viewModel.OutputPath = _outputDir;

        await viewModel.ScanAsync();

        // Apply Markdown target to all supported format rules
        foreach (var rule in viewModel.FormatRules)
        {
            var mdOption = rule.Targets.FirstOrDefault(o => o.Target == ConversionTarget.Markdown);
            if (mdOption is not null)
            {
                rule.SelectedTarget = mdOption;
            }
        }

        await viewModel.ConvertAsync();

        // Verify folder outputs
        Assert.True(File.Exists(Path.Combine(_outputDir, "document.md")));
        Assert.True(File.Exists(Path.Combine(_outputDir, "presentation.md")));
        Assert.True(File.Exists(Path.Combine(_outputDir, "data.md")));
        Assert.True(File.Exists(Path.Combine(_outputDir, "webpage.md")));
        Assert.True(File.Exists(Path.Combine(_outputDir, "manual.md")));
        Assert.True(File.Exists(Path.Combine(_outputDir, "notes.md")));

        // Scanned PDF must NOT produce empty markdown
        Assert.False(File.Exists(Path.Combine(_outputDir, "scanned.md")));

        // Verify XLSX markdown table structure
        var xlsxMd = File.ReadAllText(Path.Combine(_outputDir, "data.md"));
        Assert.Contains("## ", xlsxMd);
        Assert.Contains("|", xlsxMd);

        // Verify report file
        var reportPath = Path.Combine(_outputDir, "ZletConverter-report.txt");
        Assert.True(File.Exists(reportPath));
        var report = File.ReadAllText(reportPath);

        Assert.Contains("DOCX → MD", report);
        Assert.Contains("PPTX → MD", report);
        Assert.Contains("XLSX → MD", report);
        Assert.Contains("HTML → MD", report);
        Assert.Contains("PDF → MD", report);
        Assert.Contains("TXT → MD", report);
        Assert.Contains("scanned.pdf", report);
        Assert.Contains("scanned_pdf_unsupported", report);

        // Immutability: source files must remain identical
        foreach (var (path, initialHash) in sourceHashes)
        {
            Assert.Equal(initialHash, Hash(path));
        }
    }

    [MarkdownIntegrationFact]
    public async Task Mixed_batch_markdown_conversion_to_zip_archive()
    {
        var runner = new DoclingWorkerProcessRunner();

        CopyFixture("F11_structural.html", "article.html");
        File.WriteAllText(Path.Combine(_sourceDir, "readme.txt"), "readme plain text");

        var zipPath = Path.Combine(_outputDir, "bundle.zip");
        var viewModel = CreateViewModel();
        viewModel.SelectedFolder = _sourceDir;
        viewModel.SelectedOutputMode = OutputMode.Zip;
        viewModel.OutputPath = zipPath;

        await viewModel.ScanAsync();

        foreach (var rule in viewModel.FormatRules)
        {
            var mdOption = rule.Targets.FirstOrDefault(o => o.Target == ConversionTarget.Markdown);
            if (mdOption is not null)
            {
                rule.SelectedTarget = mdOption;
            }
        }

        await viewModel.ConvertAsync();

        Assert.True(File.Exists(zipPath));
        using var archive = ZipFile.OpenRead(zipPath);

        var entryNames = archive.Entries.Select(e => e.FullName).ToList();
        Assert.Contains("article.md", entryNames);
        Assert.Contains("readme.md", entryNames);
        Assert.Contains("ZletConverter-report.txt", entryNames);
    }

    private void CopyFixture(string fixtureName, string targetFileName)
    {
        var current = AppContext.BaseDirectory;
        string? fixturePath = null;
        while (!string.IsNullOrEmpty(current))
        {
            var candidate = Path.Combine(current, "evaluation", "fixtures", fixtureName);
            if (File.Exists(candidate)) { fixturePath = candidate; break; }
            current = Path.GetDirectoryName(current);
        }
        if (fixturePath is null) throw new FileNotFoundException(fixtureName);
        File.Copy(fixturePath, Path.Combine(_sourceDir, targetFileName), overwrite: true);
    }

    private static string Hash(string path)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    private MainWindowViewModel CreateViewModel()
    {
        var localization = LocalizationService.CreateStandalone("ru-RU");
        var officeDetector = new MicrosoftOfficeCapabilityTests.FakeCapabilityDetector([]);
        var officeRunner = new UnavailableOfficeRunner();
        var doclingRunner = new DoclingWorkerProcessRunner();

        var resolver = new DefaultConversionAdapterResolver(
            officeDetector,
            officeRunner,
            doclingRunner);

        var scanner = new FileSystemFolderScanner();
        var planner = new ConversionPlanner(resolver);
        var processor = new ConversionProcessor(resolver);

        return new MainWindowViewModel(
            scanner,
            planner,
            processor,
            officeDetector,
            localization: localization);
    }

    private sealed class UnavailableOfficeRunner : IMicrosoftOfficeWorkerRunner
    {
        public bool IsAvailable => false;
        public Task<OfficeWorkerExecutionResult> RunAsync(OfficeWorkerRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new OfficeWorkerExecutionResult(false, ErrorCode: "worker_missing"));
    }
}
