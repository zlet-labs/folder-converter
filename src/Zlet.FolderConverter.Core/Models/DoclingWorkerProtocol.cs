namespace Zlet.FolderConverter.Core.Models;

public sealed record DoclingWorkerRequest(
    string Id,
    string SourcePath,
    string OutputPath,
    SourceFormat SourceFormat);

public sealed record DoclingWorkerResponse(
    string Id = "",
    bool Success = false,
    string ErrorCode = "",
    string ErrorMessage = "",
    bool Ready = false);

public sealed record DoclingWorkerExecutionResult(
    bool Success,
    string ErrorCode = "",
    string ErrorMessage = "",
    bool TimedOut = false,
    int? ExitCode = null,
    bool HasStandardOutput = false,
    bool HasStandardError = false);

public sealed record DoclingHandshakeResponse(
    bool Ready = false,
    string Version = "",
    string PythonVersion = "",
    string DoclingVersion = "");
