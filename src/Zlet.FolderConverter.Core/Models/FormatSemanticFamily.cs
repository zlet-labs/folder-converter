namespace Zlet.FolderConverter.Core.Models;

public enum FormatSemanticFamily
{
    Document,
    Spreadsheet,
    Presentation,
    Pdf,
    DataCode,
    TextData,
    Image,
    Ebook,
    Generic
}

public static class FormatSemanticFamilyExtensions
{
    public static FormatSemanticFamily GetSemanticFamily(this SourceFormat format) => format switch
    {
        SourceFormat.Doc or SourceFormat.Docx or SourceFormat.Odt => FormatSemanticFamily.Document,
        SourceFormat.Xls or SourceFormat.Xlsx or SourceFormat.Ods => FormatSemanticFamily.Spreadsheet,
        SourceFormat.Ppt or SourceFormat.Pptx or SourceFormat.Odp => FormatSemanticFamily.Presentation,
        SourceFormat.Pdf => FormatSemanticFamily.Pdf,
        SourceFormat.Json or SourceFormat.Html => FormatSemanticFamily.DataCode,
        SourceFormat.Txt or SourceFormat.Csv or SourceFormat.Tsv => FormatSemanticFamily.TextData,
        SourceFormat.Image => FormatSemanticFamily.Image,
        SourceFormat.Epub => FormatSemanticFamily.Ebook,
        _ => FormatSemanticFamily.Generic
    };
}
