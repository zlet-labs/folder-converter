namespace Zlet.FolderConverter.Core.Models;

public enum SourceFormat
{
    Json,
    Doc,
    Xls,
    Ppt,
    Docx,
    Xlsx,
    Pptx,
    Odt,
    Ods,
    Odp,
    Pdf,
    Csv,
    Tsv,
    Epub,
    Image,
    Archive,
    Html,
    Txt,
    Unknown
}

public static class SourceFormatExtensions
{
    public static string ToDisplayName(this SourceFormat format) => format switch
    {
        SourceFormat.Json => "JSON",
        SourceFormat.Doc => "DOC",
        SourceFormat.Xls => "XLS",
        SourceFormat.Ppt => "PPT",
        SourceFormat.Docx => "DOCX",
        SourceFormat.Xlsx => "XLSX",
        SourceFormat.Pptx => "PPTX",
        SourceFormat.Odt => "ODT",
        SourceFormat.Ods => "ODS",
        SourceFormat.Odp => "ODP",
        SourceFormat.Pdf => "PDF",
        SourceFormat.Csv => "CSV",
        SourceFormat.Tsv => "TSV",
        SourceFormat.Epub => "EPUB",
        SourceFormat.Image => "Изображения",
        SourceFormat.Archive => "Архивы",
        SourceFormat.Html => "HTML",
        SourceFormat.Txt => "TXT",
        SourceFormat.Unknown => "Другие",
        _ => "Другие"
    };
}
