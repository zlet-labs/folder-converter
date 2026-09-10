using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public sealed record DoclingWorkerOptions
{
    public string? PythonExecutablePath { get; init; }
    public string? WorkerScriptPath { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(10);
    public TimeSpan ShutdownTimeout { get; init; } = TimeSpan.FromSeconds(2);
}

internal sealed class DoclingVersionIncompatibleException(string message) : Exception(message);

public sealed class StderrBuffer(int maxCapacity = 32 * 1024)
{
    private readonly StringBuilder _buffer = new();
    private readonly object _lock = new();

    public void Append(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        lock (_lock)
        {
            _buffer.AppendLine(text);
            if (_buffer.Length > maxCapacity)
            {
                _buffer.Remove(0, _buffer.Length - maxCapacity);
            }
        }
    }

    public string GetContent()
    {
        lock (_lock)
        {
            return _buffer.ToString();
        }
    }
}

public sealed class DoclingWorkerProcessRunner : IDoclingWorkerRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly DoclingWorkerOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string? _resolvedPythonPath;
    private readonly string? _resolvedScriptPath;
    private WorkerSession? _session;
    private bool _batchActive;

    public DoclingWorkerProcessRunner(DoclingWorkerOptions? options = null)
    {
        _options = options ?? new DoclingWorkerOptions();
        _resolvedPythonPath = ResolvePythonPath(_options.PythonExecutablePath);
        _resolvedScriptPath = ResolveScriptPath(_options.WorkerScriptPath);
    }

    public bool IsAvailable =>
        !string.IsNullOrWhiteSpace(_resolvedPythonPath)
        && File.Exists(_resolvedPythonPath)
        && !string.IsNullOrWhiteSpace(_resolvedScriptPath)
        && File.Exists(_resolvedScriptPath);

    public string AvailabilityMessage => IsAvailable
        ? "Компонент Markdown доступен."
        : "Компонент Markdown недоступен.";

    public async Task BeginBatchAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            _batchActive = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task EndBatchAsync()
    {
        await _gate.WaitAsync();
        try
        {
            _batchActive = false;
            await ShutdownSessionAsync(force: false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<DoclingWorkerExecutionResult> RunAsync(
        DoclingWorkerRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsAvailable)
        {
            return new(false, "docling_worker_missing", AvailabilityMessage);
        }

        await _gate.WaitAsync(cancellationToken);
        var closeAfterRequest = !_batchActive;
        try
        {
            if (_session is not null && _session.Process.HasExited)
            {
                await ShutdownSessionAsync(force: true);
            }

            if (_session is null)
            {
                try
                {
                    _session = StartSession();
                }
                catch (DoclingVersionIncompatibleException)
                {
                    return new(false, "docling_version_incompatible", "Версия компонента Markdown несовместима с текущим приложением.");
                }
                catch
                {
                    return new(false, "docling_worker_start_failure", "Не удалось запустить процесс Markdown.");
                }
            }

            var result = await ExecuteAsync(_session, request, cancellationToken);
            if (_session is not null && !_session.Process.HasExited && _session.Process.WorkingSet64 > 1887436800L) // > 1.75 GB RSS watchdog at safe boundary
            {
                await ShutdownSessionAsync(force: false);
            }
            return result;
        }
        finally
        {
            if (closeAfterRequest)
            {
                await ShutdownSessionAsync(force: false);
            }
            _gate.Release();
        }
    }

    private WorkerSession StartSession()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _resolvedPythonPath!,
            Arguments = $"\"{_resolvedScriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        startInfo.Environment["HF_HUB_OFFLINE"] = "1";
        startInfo.Environment["TRANSFORMERS_OFFLINE"] = "1";
        startInfo.Environment["HF_HUB_DISABLE_SYMLINKS_WARNING"] = "1";
        startInfo.Environment["PYTHONIOENCODING"] = "utf-8";
        startInfo.Environment["PYTHONUTF8"] = "1";

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to launch Markdown worker process.");

        var stderrBuffer = new StderrBuffer(32 * 1024);
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderrBuffer.Append(e.Data);
            }
        };
        process.BeginErrorReadLine();

        // Read and validate startup handshake
        var readyLine = process.StandardOutput.ReadLine();
        if (string.IsNullOrWhiteSpace(readyLine))
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new InvalidOperationException("Markdown worker failed to report ready: empty response.");
        }

        try
        {
            var handshake = JsonSerializer.Deserialize<DoclingHandshakeResponse>(readyLine, JsonOptions);
            if (handshake is null || !handshake.Ready)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                if (handshake?.ErrorCode == "docling_version_incompatible")
                {
                    throw new DoclingVersionIncompatibleException(handshake.ErrorMessage);
                }
                throw new InvalidOperationException("Markdown worker reported not ready.");
            }

            if (handshake.Version != "1.0.0")
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                throw new DoclingVersionIncompatibleException($"Markdown worker protocol version mismatch: {handshake.Version}");
            }

            if (!string.IsNullOrWhiteSpace(handshake.PythonVersion))
            {
                var parts = handshake.PythonVersion.Split('.');
                if (parts.Length >= 2
                    && int.TryParse(parts[0], out var major)
                    && int.TryParse(parts[1], out var minor))
                {
                    if (major != 3 || minor < 10 || minor > 12)
                    {
                        try { process.Kill(entireProcessTree: true); } catch { }
                        throw new DoclingVersionIncompatibleException($"Incompatible Python version: {handshake.PythonVersion} (expected 3.10-3.12)");
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(handshake.DoclingVersion)
                && !handshake.DoclingVersion.StartsWith("2."))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                throw new DoclingVersionIncompatibleException($"Incompatible Docling version: {handshake.DoclingVersion} (expected 2.x)");
            }
        }
        catch (JsonException ex)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new InvalidOperationException($"Markdown worker invalid handshake: {ex.Message}");
        }

        return new WorkerSession(process, stderrBuffer);
    }

    private async Task<DoclingWorkerExecutionResult> ExecuteAsync(
        WorkerSession session,
        DoclingWorkerRequest request,
        CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(_options.Timeout);

        try
        {
            var json = JsonSerializer.Serialize(new
            {
                id = request.Id,
                sourcePath = request.SourcePath,
                outputPath = request.OutputPath,
                sourceFormat = request.SourceFormat.ToString()
            }, JsonOptions);

            await session.Process.StandardInput.WriteLineAsync(json.AsMemory(), linkedCts.Token);
            await session.Process.StandardInput.FlushAsync(linkedCts.Token);

            var responseLine = await session.Process.StandardOutput.ReadLineAsync(linkedCts.Token);
            if (string.IsNullOrWhiteSpace(responseLine))
            {
                await ShutdownSessionAsync(force: true);
                return new(false, "docling_worker_missing_response", "Процесс Markdown завершился без ответа.");
            }

            var response = JsonSerializer.Deserialize<DoclingWorkerResponse>(responseLine, JsonOptions);
            if (response is null)
            {
                return new(false, "docling_protocol_error", "Некорректный ответ процесса Markdown.");
            }

            return new(response.Success, response.ErrorCode, response.ErrorMessage);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await ShutdownSessionAsync(force: true);
            throw;
        }
        catch (OperationCanceledException)
        {
            await ShutdownSessionAsync(force: true);
            return new(false, "docling_worker_timeout", "Преобразование превысило допустимое время.", TimedOut: true);
        }
        catch (Exception ex)
        {
            await ShutdownSessionAsync(force: true);
            return new(false, "docling_worker_failure", ex.Message);
        }
    }

    private async Task ShutdownSessionAsync(bool force)
    {
        if (_session is null) return;
        var session = _session;
        _session = null;

        try
        {
            if (!session.Process.HasExited)
            {
                if (!force)
                {
                    try
                    {
                        await session.Process.StandardInput.WriteLineAsync(
                            JsonSerializer.Serialize(new { command = "shutdown" }, JsonOptions));
                        await session.Process.StandardInput.FlushAsync();
                        using var timeoutCts = new CancellationTokenSource(_options.ShutdownTimeout);
                        await session.Process.WaitForExitAsync(timeoutCts.Token);
                    }
                    catch
                    {
                        force = true;
                    }
                }

                if (force && !session.Process.HasExited)
                {
                    session.Process.Kill(entireProcessTree: true);
                }
            }
        }
        catch
        {
        }
        finally
        {
            session.Dispose();
        }
    }

    private static string? ResolvePythonPath(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
            return File.Exists(explicitPath) ? Path.GetFullPath(explicitPath) : null;

        var env = Environment.GetEnvironmentVariable("ZLET_DOCLING_PYTHON");
        if (!string.IsNullOrWhiteSpace(env))
            return File.Exists(env) ? Path.GetFullPath(env) : null;

        // 1. App-owned runtime directory in LocalAppData
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            var appOwnedDir = Path.Combine(localAppData, "Zlet Converter", "runtimes", "markdown");
            var candidate = Path.Combine(appOwnedDir, "python.exe");
            if (File.Exists(candidate)) return candidate;
            var scriptCandidate = Path.Combine(appOwnedDir, "Scripts", "python.exe");
            if (File.Exists(scriptCandidate)) return scriptCandidate;
        }

        // 2. Installation directory subfolder
        var installRuntimeDir = Path.Combine(AppContext.BaseDirectory, "runtimes", "markdown");
        var installCandidate = Path.Combine(installRuntimeDir, "python.exe");
        if (File.Exists(installCandidate)) return installCandidate;
        var installScriptCandidate = Path.Combine(installRuntimeDir, "Scripts", "python.exe");
        if (File.Exists(installScriptCandidate)) return installScriptCandidate;

        // 3. Developer repository virtual environment (only if explicitly opted-in via env var)
        if (Environment.GetEnvironmentVariable("ZLET_ALLOW_DEV_RUNTIME") == "1")
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            {
                var p = Path.Combine(dir.FullName, "evaluation", "venv", "Scripts", "python.exe");
                if (File.Exists(p)) return p;
            }
        }

        return null;
    }

    private static string? ResolveScriptPath(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
            return File.Exists(explicitPath) ? Path.GetFullPath(explicitPath) : null;

        var env = Environment.GetEnvironmentVariable("ZLET_DOCLING_SCRIPT");
        if (!string.IsNullOrWhiteSpace(env))
            return File.Exists(env) ? Path.GetFullPath(env) : null;

        // 1. App-owned runtime directory in LocalAppData
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            var appOwnedScript = Path.Combine(localAppData, "Zlet Converter", "runtimes", "markdown", "zlet_docling_worker.py");
            if (File.Exists(appOwnedScript)) return appOwnedScript;
        }

        // 2. Installation directory subfolder
        var installCandidate = Path.Combine(AppContext.BaseDirectory, "runtimes", "markdown", "zlet_docling_worker.py");
        if (File.Exists(installCandidate)) return installCandidate;

        var appBaseCandidate = Path.Combine(AppContext.BaseDirectory, "zlet_docling_worker.py");
        if (File.Exists(appBaseCandidate)) return appBaseCandidate;

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var p = Path.Combine(dir.FullName, "src", "Zlet.FolderConverter.DoclingWorker", "zlet_docling_worker.py");
            if (File.Exists(p)) return p;
        }

        return null;
    }

    private sealed class WorkerSession(Process process, StderrBuffer stderr) : IDisposable
    {
        public Process Process { get; } = process;
        public StderrBuffer Stderr { get; } = stderr;

        public void Dispose()
        {
            Process.Dispose();
        }
    }
}
