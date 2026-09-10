using System.IO.Compression;
using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.Tests;

public sealed class OutputResultValidatorTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "zlet-folder-converter-output-tests",
        Guid.NewGuid().ToString("N"));
    private readonly OutputResultValidator _validator = new();

    public OutputResultValidatorTests() => Directory.CreateDirectory(_rootPath);

    [Fact]
    public void Validate_rejects_missing_and_empty_output()
    {
        var missing = Path.Combine(_rootPath, "missing.txt");
        var empty = Path.Combine(_rootPath, "empty.txt");
        File.WriteAllBytes(empty, []);

        Assert.False(_validator.Validate(missing, ConversionTarget.Txt).IsValid);
        Assert.False(_validator.Validate(empty, ConversionTarget.Txt).IsValid);
    }

    [Theory]
    [InlineData(ConversionTarget.Docx, "word/document.xml")]
    [InlineData(ConversionTarget.Xlsx, "xl/workbook.xml")]
    [InlineData(ConversionTarget.Pptx, "ppt/presentation.xml")]
    public void Validate_accepts_ooxml_zip_with_required_parts(
        ConversionTarget target,
        string requiredPart)
    {
        var path = Path.Combine(_rootPath, $"valid{target.ToExtension()}");
        CreateZip(path, "[Content_Types].xml", requiredPart);

        Assert.True(_validator.Validate(path, target).IsValid);
    }

    [Fact]
    public void Validate_rejects_wrong_ooxml_structure()
    {
        var path = Path.Combine(_rootPath, "wrong.docx");
        CreateZip(path, "[Content_Types].xml", "xl/workbook.xml");

        var result = _validator.Validate(path, ConversionTarget.Docx);

        Assert.False(result.IsValid);
        Assert.Equal("ooxml_structure_invalid", result.ErrorCode);
    }

    [Theory]
    [InlineData("%PDF-1.7\nsynthetic", true)]
    [InlineData("not a pdf", false)]
    public void Validate_checks_pdf_signature(string content, bool expected)
    {
        var path = Path.Combine(_rootPath, "result.pdf");
        File.WriteAllText(path, content);

        Assert.Equal(expected, _validator.Validate(path, ConversionTarget.Pdf).IsValid);
    }

    [Fact]
    public void Validate_markdown_accepts_valid_utf8_text()
    {
        var path = Path.Combine(_rootPath, "valid.md");
        File.WriteAllText(path, "# Heading\n\nSome UTF-8 text: Привет мир");

        Assert.True(_validator.Validate(path, ConversionTarget.Markdown).IsValid);
    }

    [Fact]
    public void Validate_markdown_source_aware_accepts_empty_when_source_empty()
    {
        var path = Path.Combine(_rootPath, "empty.md");
        File.WriteAllBytes(path, []);

        // When sourceLength == 0, empty markdown is valid
        Assert.True(_validator.Validate(path, ConversionTarget.Markdown, sourceLength: 0).IsValid);

        // When sourceLength > 0, empty markdown is rejected
        var result = _validator.Validate(path, ConversionTarget.Markdown, sourceLength: 100);
        Assert.False(result.IsValid);
        Assert.Equal("output_empty", result.ErrorCode);
    }

    [Fact]
    public void Validate_markdown_rejects_invalid_utf8()
    {
        var path = Path.Combine(_rootPath, "invalid_utf8.md");
        // Write invalid UTF-8 byte sequence (e.g. lone continuation byte or invalid start)
        File.WriteAllBytes(path, [0xFF, 0xFE, 0x00, 0x00]);

        var result = _validator.Validate(path, ConversionTarget.Markdown);
        Assert.False(result.IsValid);
        Assert.Equal("output_invalid_utf8", result.ErrorCode);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    internal static void CreateZip(string path, params string[] entries)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var name in entries)
        {
            using var writer = new StreamWriter(archive.CreateEntry(name).Open());
            writer.Write("<synthetic />");
        }
    }
}
