using System.Runtime.InteropServices;
using Zlet.FolderConverter.App.Localization;
using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.App.Settings;

public static class DiagnosticsText
{
    // Deliberate allowlist: no paths, environment variables, document data or logs.
    public static string Create(LocalizationService localization, IReadOnlyList<OfficeApplicationAvailability> office, bool? doclingAvailable = null)
    {
        var lines = new List<string>
        {
            $"{ProductIdentity.Name} {ProductIdentity.Version}",
            localization.Format("DiagnosticsWindows", Environment.OSVersion.Version),
            localization.Format("DiagnosticsOsArch", RuntimeInformation.OSArchitecture),
            localization.Format("DiagnosticsAppArch", RuntimeInformation.ProcessArchitecture),
            localization.Format("DiagnosticsLanguage", localization.Language)
        };
        lines.AddRange(Enum.GetValues<OfficeApplicationKind>().Select(kind =>
            $"{kind}: {localization.Get(office.Any(item => item.Application == kind && item.IsAvailable) ? "OfficeAvailable" : "OfficeUnavailable")}"));
        if (doclingAvailable.HasValue)
        {
            lines.Add($"Docling: {localization.Get(doclingAvailable.Value ? "DoclingAvailable" : "DoclingUnavailable")}");
        }
        return string.Join(Environment.NewLine, lines);
    }
}
