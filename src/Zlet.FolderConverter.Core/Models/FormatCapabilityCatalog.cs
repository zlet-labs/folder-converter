namespace Zlet.FolderConverter.Core.Models;

public static class FormatCapabilityCatalog
{
    private static readonly IReadOnlyDictionary<SourceFormat, FormatCapability> Capabilities =
        new Dictionary<SourceFormat, FormatCapability>
        {
            [SourceFormat.Json] = Capability(SourceFormat.Json, ConversionTarget.Txt, ConversionTarget.Txt, ConversionTarget.Markdown, ConversionTarget.Skip),
            [SourceFormat.Doc] = Capability(SourceFormat.Doc, ConversionTarget.Docx, ConversionTarget.Docx, ConversionTarget.Markdown, ConversionTarget.Skip),
            [SourceFormat.Xls] = Capability(SourceFormat.Xls, ConversionTarget.Xlsx, ConversionTarget.Xlsx, ConversionTarget.Markdown, ConversionTarget.Csv, ConversionTarget.Tsv, ConversionTarget.Skip),
            [SourceFormat.Ppt] = Capability(SourceFormat.Ppt, ConversionTarget.Pptx, ConversionTarget.Pptx, ConversionTarget.Markdown, ConversionTarget.Skip),
            [SourceFormat.Docx] = Capability(SourceFormat.Docx, ConversionTarget.Copy, ConversionTarget.Copy, ConversionTarget.Markdown, ConversionTarget.Skip),
            [SourceFormat.Xlsx] = Capability(SourceFormat.Xlsx, ConversionTarget.Copy, ConversionTarget.Copy, ConversionTarget.Markdown, ConversionTarget.Csv, ConversionTarget.Tsv, ConversionTarget.Skip),
            [SourceFormat.Pptx] = Capability(SourceFormat.Pptx, ConversionTarget.Copy, ConversionTarget.Copy, ConversionTarget.Markdown, ConversionTarget.Skip),
            [SourceFormat.Odt] = Capability(SourceFormat.Odt, ConversionTarget.Skip, ConversionTarget.Skip),
            [SourceFormat.Ods] = Capability(SourceFormat.Ods, ConversionTarget.Skip, ConversionTarget.Skip),
            [SourceFormat.Odp] = Capability(SourceFormat.Odp, ConversionTarget.Skip, ConversionTarget.Skip),
            [SourceFormat.Pdf] = Capability(SourceFormat.Pdf, ConversionTarget.Copy, ConversionTarget.Copy, ConversionTarget.Markdown, ConversionTarget.Skip),
            [SourceFormat.Csv] = Capability(SourceFormat.Csv, ConversionTarget.Copy, ConversionTarget.Copy, ConversionTarget.Skip),
            [SourceFormat.Tsv] = Capability(SourceFormat.Tsv, ConversionTarget.Copy, ConversionTarget.Copy, ConversionTarget.Skip),
            [SourceFormat.Epub] = Capability(SourceFormat.Epub, ConversionTarget.Copy, ConversionTarget.Copy, ConversionTarget.Skip),
            [SourceFormat.Image] = Capability(SourceFormat.Image, ConversionTarget.Copy, ConversionTarget.Copy, ConversionTarget.Skip),
            [SourceFormat.Archive] = Capability(SourceFormat.Archive, ConversionTarget.Skip, ConversionTarget.Skip),
            [SourceFormat.Html] = Capability(SourceFormat.Html, ConversionTarget.Copy, ConversionTarget.Copy, ConversionTarget.Markdown, ConversionTarget.Skip),
            [SourceFormat.Txt] = Capability(SourceFormat.Txt, ConversionTarget.Copy, ConversionTarget.Copy, ConversionTarget.Markdown, ConversionTarget.Skip),
            [SourceFormat.Unknown] = Capability(SourceFormat.Unknown, ConversionTarget.Skip, ConversionTarget.Skip)
        };

    public static IReadOnlyCollection<FormatCapability> All => Capabilities.Values.ToArray();

    public static FormatCapability Get(SourceFormat format) => Capabilities[format];

    public static OfficeApplicationKind? RequiredOfficeApplication(
        SourceFormat source,
        ConversionTarget target) =>
        (source, target) switch
        {
            (SourceFormat.Doc, ConversionTarget.Docx) => OfficeApplicationKind.Word,
            (SourceFormat.Xls, ConversionTarget.Xlsx) => OfficeApplicationKind.Excel,
            (SourceFormat.Xls or SourceFormat.Xlsx, ConversionTarget.Csv or ConversionTarget.Tsv) => OfficeApplicationKind.Excel,
            (SourceFormat.Ppt, ConversionTarget.Pptx) => OfficeApplicationKind.PowerPoint,
            _ => null
        };

    public static bool IsSafeCopy(SourceFormat source, ConversionTarget target) =>
        target == ConversionTarget.Copy
        && source is SourceFormat.Docx
            or SourceFormat.Xlsx
            or SourceFormat.Pptx
            or SourceFormat.Pdf
            or SourceFormat.Csv
            or SourceFormat.Tsv
            or SourceFormat.Epub
            or SourceFormat.Image
            or SourceFormat.Html
            or SourceFormat.Txt;

    private static FormatCapability Capability(
        SourceFormat source,
        ConversionTarget defaultTarget,
        params ConversionTarget[] allowedTargets) =>
        new(source, allowedTargets, defaultTarget);
}
