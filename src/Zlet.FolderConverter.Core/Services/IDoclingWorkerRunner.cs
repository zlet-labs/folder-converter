using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public interface IDoclingWorkerRunner
{
    bool IsAvailable { get; }

    string AvailabilityMessage { get; }

    Task BeginBatchAsync(CancellationToken cancellationToken);

    Task EndBatchAsync();

    Task<DoclingWorkerExecutionResult> RunAsync(
        DoclingWorkerRequest request,
        CancellationToken cancellationToken);
}
