using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public sealed class DefaultConversionAdapterResolver
    : IConversionAdapterResolver, IConversionBatchLifecycle
{
    private readonly IReadOnlyList<IConversionAdapter> _adapters;
    private readonly IMicrosoftOfficeWorkerRunner? _officeWorkerRunner;
    private readonly IDoclingWorkerRunner? _doclingWorkerRunner;

    public DefaultConversionAdapterResolver()
        : this(
            new MicrosoftOfficeCapabilityDetector(),
            new MicrosoftOfficeWorkerProcessRunner(),
            new DoclingWorkerProcessRunner())
    {
    }

    public DefaultConversionAdapterResolver(
        IMicrosoftOfficeCapabilityDetector capabilityDetector,
        IMicrosoftOfficeWorkerRunner workerRunner)
        : this(capabilityDetector, workerRunner, new DoclingWorkerProcessRunner())
    {
    }

    public DefaultConversionAdapterResolver(
        IMicrosoftOfficeCapabilityDetector capabilityDetector,
        IMicrosoftOfficeWorkerRunner officeWorkerRunner,
        IDoclingWorkerRunner doclingWorkerRunner)
        : this(CreateDefaultAdapters(capabilityDetector, officeWorkerRunner, doclingWorkerRunner))
    {
        _officeWorkerRunner = officeWorkerRunner;
        _doclingWorkerRunner = doclingWorkerRunner;
    }

    public DefaultConversionAdapterResolver(IEnumerable<IConversionAdapter> adapters)
    {
        _adapters = adapters.ToArray();
    }

    public IConversionAdapter? Resolve(SourceFormat sourceFormat, ConversionTarget target) =>
        _adapters.FirstOrDefault(adapter => adapter.CanConvert(sourceFormat, target));

    async Task IConversionBatchLifecycle.BeginBatchAsync(CancellationToken cancellationToken)
    {
        if (_officeWorkerRunner is not null)
        {
            await _officeWorkerRunner.BeginBatchAsync(cancellationToken);
        }
        if (_doclingWorkerRunner is not null)
        {
            await _doclingWorkerRunner.BeginBatchAsync(cancellationToken);
        }
    }

    async Task IConversionBatchLifecycle.EndBatchAsync()
    {
        try
        {
            if (_officeWorkerRunner is not null)
            {
                await _officeWorkerRunner.EndBatchAsync();
            }
        }
        finally
        {
            if (_doclingWorkerRunner is not null)
            {
                await _doclingWorkerRunner.EndBatchAsync();
            }
        }
    }

    private static IConversionAdapter[] CreateDefaultAdapters(
        IMicrosoftOfficeCapabilityDetector capabilityDetector,
        IMicrosoftOfficeWorkerRunner officeWorkerRunner,
        IDoclingWorkerRunner doclingWorkerRunner)
    {
        var validator = new OutputResultValidator();
        return
        [
            new JsonConversionAdapter(validator),
            new SafeFileCopyAdapter(validator),
            new MicrosoftOfficeConversionAdapter(
                OfficeApplicationKind.Word,
                capabilityDetector,
                officeWorkerRunner,
                validator,
                temporaryRoot: null),
            new MicrosoftOfficeConversionAdapter(
                OfficeApplicationKind.Excel,
                capabilityDetector,
                officeWorkerRunner,
                validator,
                temporaryRoot: null),
            new MicrosoftOfficeConversionAdapter(
                OfficeApplicationKind.PowerPoint,
                capabilityDetector,
                officeWorkerRunner,
                validator,
                temporaryRoot: null),
            new DoclingConversionAdapter(
                doclingWorkerRunner,
                validator,
                temporaryRoot: null),
            new LegacyOfficeToMarkdownConversionAdapter(
                OfficeApplicationKind.Word,
                capabilityDetector,
                officeWorkerRunner,
                doclingWorkerRunner,
                validator,
                temporaryRoot: null),
            new LegacyOfficeToMarkdownConversionAdapter(
                OfficeApplicationKind.Excel,
                capabilityDetector,
                officeWorkerRunner,
                doclingWorkerRunner,
                validator,
                temporaryRoot: null),
            new LegacyOfficeToMarkdownConversionAdapter(
                OfficeApplicationKind.PowerPoint,
                capabilityDetector,
                officeWorkerRunner,
                doclingWorkerRunner,
                validator,
                temporaryRoot: null)
        ];
    }
}
