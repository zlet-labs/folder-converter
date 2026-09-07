using System.Text;
using System.Text.Json;
using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.Tests;

public sealed class JsonConversionAdapterTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "zlet-json-adapter-tests",
        Guid.NewGuid().ToString("N"));

    public JsonConversionAdapterTests() => Directory.CreateDirectory(_rootPath);

    [Theory]
    [InlineData(ConversionTarget.Txt, ".txt")]
    [InlineData(ConversionTarget.Markdown, ".md")]
    public async Task ConvertAsync_writes_utf8_and_preserves_source(
        ConversionTarget target,
        string extension)
    {
        const string source = """{"имя":"Анна 😀","nested":{"value":null},"items":[1,true,"```"]}""";
        var sourcePath = Write("nested/source.json", source);
        var operation = CreateOperation(sourcePath, Path.Combine("nested", $"source{extension}"), target);
        var validator = new TrackingValidator();

        var result = await new JsonConversionAdapter(validator)
            .ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Succeeded, result.Status);
        var output = await File.ReadAllTextAsync(operation.TargetPath, Encoding.UTF8);
        Assert.Contains("Анна", output);
        Assert.Equal(source, await File.ReadAllTextAsync(sourcePath));
        Assert.True(validator.CallCount >= 2);
        if (target == ConversionTarget.Markdown)
        {
            Assert.StartsWith("# source.json", output);
            Assert.Contains("````json", output);
        }
        else
        {
            using var parsed = JsonDocument.Parse(output);
            Assert.Equal("Анна 😀", parsed.RootElement.GetProperty("имя").GetString());
        }
    }

    [Fact]
    public async Task ConvertAsync_invalid_json_fails_without_partial_output()
    {
        var sourcePath = Write("broken.json", """{"value": }""");
        var operation = CreateOperation(sourcePath, "broken.txt", ConversionTarget.Txt);

        var result = await new JsonConversionAdapter(new OutputResultValidator())
            .ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Failed, result.Status);
        Assert.StartsWith("Некорректный JSON:", result.Message);
        Assert.False(File.Exists(operation.TargetPath));
        Assert.Equal("""{"value": }""", await File.ReadAllTextAsync(sourcePath));
    }

    [Theory]
    [InlineData("outside")]
    [InlineData("prefix")]
    public async Task ConvertAsync_rejects_target_outside_output_root(string scenario)
    {
        var sourcePath = Write("source.json", "{}");
        var outputRoot = Path.Combine(_rootPath, "_converted");
        var targetPath = scenario == "outside"
            ? Path.Combine(outputRoot, "..", "outside.txt")
            : Path.Combine(_rootPath, "_converted-other", "prefix.txt");
        var operation = CreateOperation(sourcePath, "source.txt", ConversionTarget.Txt) with
        {
            TargetPath = targetPath
        };

        var result = await new JsonConversionAdapter(new OutputResultValidator())
            .ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Failed, result.Status);
        Assert.Equal("Недопустимый путь результата.", result.Message);
        Assert.False(File.Exists(Path.GetFullPath(targetPath)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConvertAsync_protects_file_and_directory_conflicts(bool directoryConflict)
    {
        var sourcePath = Write("source.json", "{}");
        var operation = CreateOperation(sourcePath, "source.txt", ConversionTarget.Txt);
        Directory.CreateDirectory(Path.GetDirectoryName(operation.TargetPath)!);
        if (directoryConflict)
        {
            Directory.CreateDirectory(operation.TargetPath);
        }
        else
        {
            await File.WriteAllTextAsync(operation.TargetPath, "existing");
        }

        var result = await new JsonConversionAdapter(new OutputResultValidator())
            .ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Conflict, result.Status);
        if (!directoryConflict)
        {
            Assert.Equal("existing", await File.ReadAllTextAsync(operation.TargetPath));
        }
    }

    [Fact]
    public async Task Processor_continues_after_invalid_json()
    {
        var bad = CreateOperation(Write("bad.json", "{"), "bad.txt", ConversionTarget.Txt);
        var good = CreateOperation(Write("good.json", """{"ok":true}"""), "good.txt", ConversionTarget.Txt);
        var resolver = new DefaultConversionAdapterResolver();

        var summary = await new ConversionProcessor(resolver)
            .ProcessAsync([bad, good], progress: null, CancellationToken.None);

        Assert.Equal(1, summary.Failed);
        Assert.Equal(1, summary.Succeeded);
        Assert.True(File.Exists(good.TargetPath));
        Assert.False(File.Exists(bad.TargetPath));
    }

    [Fact]
    public async Task Processor_honors_cancellation()
    {
        var operation = CreateOperation(
            Write("source.json", "{}"),
            "source.txt",
            ConversionTarget.Txt);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new ConversionProcessor(new DefaultConversionAdapterResolver())
                .ProcessAsync([operation], progress: null, cancellation.Token));
        Assert.False(File.Exists(operation.TargetPath));
    }

    [Fact]
    public async Task Processor_reports_real_monotonic_pipeline_stages_and_success_at_100()
    {
        var operation = CreateOperation(
            Write("progress.json", "{}"),
            "progress.txt",
            ConversionTarget.Txt);
        var reports = new List<ConversionProgress>();
        var resolver = new DefaultConversionAdapterResolver(
            [new JsonConversionAdapter(new OutputResultValidator())]);

        var summary = await new ConversionProcessor(resolver).ProcessAsync(
            [operation],
            new InlineProgress<ConversionProgress>(reports.Add),
            CancellationToken.None);

        Assert.Equal(1, summary.Succeeded);
        var percentages = reports.Select(report => report.OperationPercent).ToArray();
        Assert.Equal<int?[]>([null, 100], percentages);
        Assert.Equal(OperationStatus.Succeeded, reports[^1].Status);
        Assert.Equal(100, reports[^1].OperationPercent);

        var overall = reports.Select(report =>
            (report.Completed + (report.Status == OperationStatus.Converting
                ? (report.OperationPercent ?? 0) / 100d
                : 0)) * 100d / report.Total).ToArray();
        Assert.True(overall.Zip(overall.Skip(1), (left, right) => left <= right).All(value => value));
    }

    [Fact]
    public async Task Failed_pipeline_reports_only_observed_stages_below_100()
    {
        var operation = CreateOperation(
            Write("progress-broken.json", "{"),
            "progress-broken.txt",
            ConversionTarget.Txt);
        var reports = new List<ConversionProgress>();
        var resolver = new DefaultConversionAdapterResolver(
            [new JsonConversionAdapter(new OutputResultValidator())]);

        var summary = await new ConversionProcessor(resolver).ProcessAsync(
            [operation],
            new InlineProgress<ConversionProgress>(reports.Add),
            CancellationToken.None);

        Assert.Equal(1, summary.Failed);
        Assert.Equal(OperationStatus.Failed, reports[^1].Status);
        Assert.True(reports[^1].OperationPercent is null || reports[^1].OperationPercent < 100);
        Assert.DoesNotContain(reports, report =>
            report.Status == OperationStatus.Failed && report.OperationPercent == 100);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private string Write(string relativePath, string content)
    {
        var path = Path.Combine(_rootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(false));
        return path;
    }

    private PlannedOperation CreateOperation(
        string sourcePath,
        string targetRelativePath,
        ConversionTarget target) =>
        new(
            sourcePath,
            Path.GetRelativePath(_rootPath, sourcePath),
            SourceFormat.Json,
            target,
            target.ToExtension(),
            Path.Combine(_rootPath, "_converted", targetRelativePath),
            true,
            OperationStatus.Ready,
            "ready",
            Path.Combine(_rootPath, "_converted"));

    private sealed class TrackingValidator : IOutputResultValidator
    {
        private readonly OutputResultValidator _inner = new();
        public int CallCount { get; private set; }

        public OutputValidationResult Validate(string targetPath, ConversionTarget target)
        {
            CallCount++;
            return _inner.Validate(targetPath, target);
        }
    }

    private sealed class InlineProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }
}
