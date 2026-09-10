using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.Tests;

public sealed class DocumentFormatDetectorTests
{
    [Theory]
    [InlineData("sample.JSON", SourceFormat.Json)]
    [InlineData("sample.doc", SourceFormat.Doc)]
    [InlineData("sample.XLS", SourceFormat.Xls)]
    [InlineData("sample.PpT", SourceFormat.Ppt)]
    [InlineData("sample.docx", SourceFormat.Docx)]
    [InlineData("sample.xlsx", SourceFormat.Xlsx)]
    [InlineData("sample.pptx", SourceFormat.Pptx)]
    [InlineData("sample.odt", SourceFormat.Odt)]
    [InlineData("sample.ods", SourceFormat.Ods)]
    [InlineData("sample.odp", SourceFormat.Odp)]
    [InlineData("sample.pdf", SourceFormat.Pdf)]
    [InlineData("sample.csv", SourceFormat.Csv)]
    [InlineData("sample.TSV", SourceFormat.Tsv)]
    [InlineData("sample.html", SourceFormat.Html)]
    [InlineData("sample.HTM", SourceFormat.Html)]
    [InlineData("sample.txt", SourceFormat.Txt)]
    [InlineData("sample.TXT", SourceFormat.Txt)]
    [InlineData("sample.epub", SourceFormat.Epub)]
    [InlineData("sample.png", SourceFormat.Image)]
    [InlineData("sample.AVIF", SourceFormat.Image)]
    [InlineData("sample.bmp", SourceFormat.Image)]
    [InlineData("sample.gif", SourceFormat.Image)]
    [InlineData("sample.heic", SourceFormat.Image)]
    [InlineData("sample.heif", SourceFormat.Image)]
    [InlineData("sample.ico", SourceFormat.Image)]
    [InlineData("sample.jp2", SourceFormat.Image)]
    [InlineData("sample.jpe", SourceFormat.Image)]
    [InlineData("sample.jpeg", SourceFormat.Image)]
    [InlineData("sample.jpg", SourceFormat.Image)]
    [InlineData("sample.tif", SourceFormat.Image)]
    [InlineData("sample.tiff", SourceFormat.Image)]
    [InlineData("sample.webp", SourceFormat.Image)]
    [InlineData("sample.svg", SourceFormat.Unknown)]
    [InlineData("sample.zip", SourceFormat.Archive)]
    [InlineData("sample.xyz", SourceFormat.Unknown)]
    public void Detect_classifies_mixed_formats_case_insensitively(
        string fileName,
        SourceFormat expectedFormat)
    {
        Assert.Equal(expectedFormat, DocumentFormatDetector.Detect(fileName));
    }

    [Theory]
    [InlineData(ConversionTarget.Txt, ".txt")]
    [InlineData(ConversionTarget.Markdown, ".md")]
    [InlineData(ConversionTarget.Docx, ".docx")]
    [InlineData(ConversionTarget.Xlsx, ".xlsx")]
    [InlineData(ConversionTarget.Pptx, ".pptx")]
    [InlineData(ConversionTarget.Pdf, ".pdf")]
    [InlineData(ConversionTarget.Csv, ".csv")]
    [InlineData(ConversionTarget.Tsv, ".tsv")]
    public void GetTargetExtension_returns_expected_mapping(
        ConversionTarget target,
        string expectedExtension)
    {
        Assert.Equal(expectedExtension, DocumentFormatDetector.GetTargetExtension(target));
    }
}
