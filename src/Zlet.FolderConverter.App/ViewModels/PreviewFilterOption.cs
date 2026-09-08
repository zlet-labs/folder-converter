using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.App.ViewModels;

public enum PreviewFilter
{
    All,
    Format,
    Convert,
    Skip,
    Unavailable,
    Conflicts,
    Errors
}

public sealed record PreviewFilterOption(
    PreviewFilter Filter,
    string Label,
    SourceFormat? Format = null);
