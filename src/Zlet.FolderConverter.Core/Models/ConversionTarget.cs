namespace Zlet.FolderConverter.Core.Models;

public enum ConversionTarget
{
    Skip,
    Copy,
    Txt,
    Markdown,
    Docx,
    Xlsx,
    Pptx,
    Pdf,
    Csv,
    Tsv
}

public static class ConversionTargetExtensions
{
    public static string ToDisplayName(this ConversionTarget target) => target switch
    {
        ConversionTarget.Skip => "Пропускаем",
        ConversionTarget.Copy => "Копировать без изменений",
        ConversionTarget.Txt => "TXT",
        ConversionTarget.Markdown => "MD",
        ConversionTarget.Docx => "DOCX",
        ConversionTarget.Xlsx => "XLSX",
        ConversionTarget.Pptx => "PPTX",
        ConversionTarget.Pdf => "PDF",
        ConversionTarget.Csv => "CSV",
        ConversionTarget.Tsv => "TSV",
        _ => "Пропускаем"
    };

    public static string ToExtension(this ConversionTarget target) => target switch
    {
        ConversionTarget.Copy => throw new InvalidOperationException(
            "Copy operations keep the source extension."),
        ConversionTarget.Txt => ".txt",
        ConversionTarget.Markdown => ".md",
        ConversionTarget.Docx => ".docx",
        ConversionTarget.Xlsx => ".xlsx",
        ConversionTarget.Pptx => ".pptx",
        ConversionTarget.Pdf => ".pdf",
        ConversionTarget.Csv => ".csv",
        ConversionTarget.Tsv => ".tsv",
        ConversionTarget.Skip => string.Empty,
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unknown conversion target.")
    };
}
