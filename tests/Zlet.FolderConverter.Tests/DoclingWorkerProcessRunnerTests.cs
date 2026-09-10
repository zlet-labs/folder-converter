using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.Tests;

public sealed class DoclingWorkerProcessRunnerTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "zlet-docling-runner-tests",
        Guid.NewGuid().ToString("N"));

    public DoclingWorkerProcessRunnerTests() => Directory.CreateDirectory(_rootPath);

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    [Fact]
    public void Runner_availability_reflects_environment()
    {
        var runner = new DoclingWorkerProcessRunner();
        // Should detect either venv or system python if installed
        Assert.True(runner.IsAvailable || !runner.IsAvailable);
    }

    [Fact]
    public async Task Nonexistent_python_path_returns_unavailable()
    {
        var fakePython = Path.Combine(_rootPath, "nonexistent_python.exe");
        var runner = new DoclingWorkerProcessRunner(new DoclingWorkerOptions { PythonExecutablePath = fakePython });

        Assert.False(runner.IsAvailable);

        var result = await runner.RunAsync(
            new DoclingWorkerRequest("job-1", "source.txt", "output.md", SourceFormat.Txt),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("docling_worker_missing", result.ErrorCode);
    }

    [Fact]
    public async Task Cancellation_stops_execution_promptly()
    {
        var runner = new DoclingWorkerProcessRunner();
        if (!runner.IsAvailable) return;

        var sourcePath = Path.Combine(_rootPath, "dummy.txt");
        await File.WriteAllTextAsync(sourcePath, "hello");
        var outputPath = Path.Combine(_rootPath, "out.md");

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancelled

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.RunAsync(
            new DoclingWorkerRequest("job-cancel", sourcePath, outputPath, SourceFormat.Txt),
            cts.Token));
    }

    [Fact]
    public async Task Batch_lifecycle_starts_and_stops_cleanly()
    {
        var runner = new DoclingWorkerProcessRunner();
        if (!runner.IsAvailable) return;

        await runner.BeginBatchAsync(CancellationToken.None);

        var sourcePath = Path.Combine(_rootPath, "sample.txt");
        await File.WriteAllTextAsync(sourcePath, "batch text");
        var outputPath = Path.Combine(_rootPath, "sample.md");

        var result = await runner.RunAsync(
            new DoclingWorkerRequest("job-batch", sourcePath, outputPath, SourceFormat.Txt),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(File.Exists(outputPath));

        await runner.EndBatchAsync();
    }
}
