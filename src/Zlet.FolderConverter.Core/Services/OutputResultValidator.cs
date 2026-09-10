using System.IO.Compression;
using System.Text;
using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public sealed class OutputResultValidator : IOutputResultValidator
{
    public OutputValidationResult Validate(string targetPath, ConversionTarget target)
        => Validate(targetPath, target, -1);

    public OutputValidationResult Validate(string targetPath, ConversionTarget target, long sourceLength)
    {
        if (string.IsNullOrWhiteSpace(targetPath) || !File.Exists(targetPath))
        {
            return new OutputValidationResult(false, "output_missing");
        }

        try
        {
            var fileLength = new FileInfo(targetPath).Length;
            if (fileLength == 0)
            {
                if (target == ConversionTarget.Markdown && sourceLength == 0)
                {
                    return new OutputValidationResult(true);
                }

                return new OutputValidationResult(false, "output_empty");
            }

            return target switch
            {
                ConversionTarget.Docx => ValidateZip(targetPath, "word/document.xml"),
                ConversionTarget.Xlsx => ValidateZip(targetPath, "xl/workbook.xml"),
                ConversionTarget.Pptx => ValidateZip(targetPath, "ppt/presentation.xml"),
                ConversionTarget.Pdf => ValidatePdf(targetPath),
                ConversionTarget.Markdown => ValidateMarkdown(targetPath, sourceLength),
                ConversionTarget.Txt => new OutputValidationResult(true),
                ConversionTarget.Csv or ConversionTarget.Tsv or ConversionTarget.Copy => new OutputValidationResult(true),
                _ => new OutputValidationResult(false, "unsupported_output_validation")
            };
        }
        catch (Exception exception) when (exception is IOException
                                           or UnauthorizedAccessException
                                           or InvalidDataException)
        {
            return new OutputValidationResult(false, "output_unreadable");
        }
    }

    private static OutputValidationResult ValidateZip(string path, string requiredPart)
    {
        using var archive = ZipFile.OpenRead(path);
        var names = archive.Entries
            .Select(entry => entry.FullName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return names.Contains("[Content_Types].xml") && names.Contains(requiredPart)
            ? new OutputValidationResult(true)
            : new OutputValidationResult(false, "ooxml_structure_invalid");
    }

    private static OutputValidationResult ValidatePdf(string path)
    {
        Span<byte> signature = stackalloc byte[5];
        using var stream = File.OpenRead(path);
        return stream.Read(signature) == signature.Length
               && signature.SequenceEqual(Encoding.ASCII.GetBytes("%PDF-"))
            ? new OutputValidationResult(true)
            : new OutputValidationResult(false, "pdf_signature_invalid");
    }

    private static OutputValidationResult ValidateMarkdown(string path, long sourceLength)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length == 0)
            {
                return sourceLength == 0
                    ? new OutputValidationResult(true)
                    : new OutputValidationResult(false, "output_empty");
            }

            var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            var text = utf8.GetString(bytes);
            if (string.IsNullOrWhiteSpace(text))
            {
                return sourceLength == 0
                    ? new OutputValidationResult(true)
                    : new OutputValidationResult(false, "output_empty");
            }

            return new OutputValidationResult(true);
        }
        catch (DecoderFallbackException)
        {
            return new OutputValidationResult(false, "output_invalid_utf8");
        }
    }
}
