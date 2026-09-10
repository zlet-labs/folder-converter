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
    public void Runner_availability_is_deterministic_based_on_paths()
    {
        var dummyExe = Path.Combine(_rootPath, "python.exe");
        var dummyScript = Path.Combine(_rootPath, "worker.py");
        File.WriteAllBytes(dummyExe, Array.Empty<byte>());
        File.WriteAllBytes(dummyScript, Array.Empty<byte>());

        var availableRunner = new DoclingWorkerProcessRunner(new DoclingWorkerOptions
        {
            PythonExecutablePath = dummyExe,
            WorkerScriptPath = dummyScript
        });
        Assert.True(availableRunner.IsAvailable);
        Assert.Equal("Компонент Markdown доступен.", availableRunner.AvailabilityMessage);

        var missingExeRunner = new DoclingWorkerProcessRunner(new DoclingWorkerOptions
        {
            PythonExecutablePath = Path.Combine(_rootPath, "missing.exe"),
            WorkerScriptPath = dummyScript
        });
        Assert.False(missingExeRunner.IsAvailable);
        Assert.Equal("Компонент Markdown недоступен.", missingExeRunner.AvailabilityMessage);

        var missingScriptRunner = new DoclingWorkerProcessRunner(new DoclingWorkerOptions
        {
            PythonExecutablePath = dummyExe,
            WorkerScriptPath = Path.Combine(_rootPath, "missing.py")
        });
        Assert.False(missingScriptRunner.IsAvailable);
        Assert.Equal("Компонент Markdown недоступен.", missingScriptRunner.AvailabilityMessage);
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
    public void StderrBuffer_drains_large_volume_and_bounds_memory_to_limit()
    {
        var buffer = new StderrBuffer(32 * 1024);
        var chunk = new string('A', 1024);
        for (var i = 0; i < 100; i++)
        {
            buffer.Append(chunk);
        }

        var content = buffer.GetContent();
        Assert.NotNull(content);
        Assert.True(content.Length <= 33 * 1024, $"Expected <= 33KB, got {content.Length}");
        Assert.NotEmpty(content);
    }

    [MarkdownIntegrationFact]
    public async Task Worker_drains_100kb_stderr_without_pipe_deadlock()
    {
        var noisyScript = Path.Combine(_rootPath, "noisy_worker.py");
        await File.WriteAllTextAsync(noisyScript, @"
import sys, json
sys.stdout.write(json.dumps({'ready': True, 'version': '1.0.0', 'pythonVersion': '3.11.0', 'doclingVersion': '2.126.0'}) + '\n')
sys.stdout.flush()
for line in sys.stdin:
    req = json.loads(line)
    if req.get('command') == 'shutdown':
        break
    sys.stderr.write('E' * 102400 + '\n')
    sys.stderr.flush()
    with open(req['outputPath'], 'w', encoding='utf-8') as f:
        f.write('finished')
    sys.stdout.write(json.dumps({'id': req['id'], 'success': True}) + '\n')
    sys.stdout.flush()
");
        var runner = new DoclingWorkerProcessRunner(new DoclingWorkerOptions
        {
            WorkerScriptPath = noisyScript
        });

        var sourcePath = Path.Combine(_rootPath, "in.txt");
        await File.WriteAllTextAsync(sourcePath, "test");
        var outputPath = Path.Combine(_rootPath, "out.md");

        var result = await runner.RunAsync(
            new DoclingWorkerRequest("job-stderr", sourcePath, outputPath, SourceFormat.Txt),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(File.Exists(outputPath));
        Assert.Equal("finished", await File.ReadAllTextAsync(outputPath));
    }

    [MarkdownIntegrationFact]
    public async Task Incompatible_protocol_version_returns_version_incompatible_error()
    {
        var mockScript = Path.Combine(_rootPath, "incompatible_worker.py");
        await File.WriteAllTextAsync(mockScript, @"
import sys, json
sys.stdout.write(json.dumps({'ready': True, 'version': '2.0.0', 'pythonVersion': '3.11.0', 'doclingVersion': '2.126.0'}) + '\n')
sys.stdout.flush()
for line in sys.stdin:
    break
");
        var runner = new DoclingWorkerProcessRunner(new DoclingWorkerOptions
        {
            WorkerScriptPath = mockScript
        });

        var result = await runner.RunAsync(
            new DoclingWorkerRequest("job-ver", "dummy.txt", "out.md", SourceFormat.Txt),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("docling_version_incompatible", result.ErrorCode);
    }

    [MarkdownIntegrationFact]
    public async Task Cancellation_stops_execution_promptly()
    {
        var runner = new DoclingWorkerProcessRunner();

        var sourcePath = Path.Combine(_rootPath, "dummy.txt");
        await File.WriteAllTextAsync(sourcePath, "hello");
        var outputPath = Path.Combine(_rootPath, "out.md");

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancelled

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.RunAsync(
            new DoclingWorkerRequest("job-cancel", sourcePath, outputPath, SourceFormat.Txt),
            cts.Token));
    }

    [MarkdownIntegrationFact]
    public async Task Batch_lifecycle_starts_and_stops_cleanly()
    {
        var runner = new DoclingWorkerProcessRunner();

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
