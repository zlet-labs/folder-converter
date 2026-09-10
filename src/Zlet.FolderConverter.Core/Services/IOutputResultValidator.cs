namespace Zlet.FolderConverter.Core.Services;

public interface IOutputResultValidator
{
    Models.OutputValidationResult Validate(string targetPath, Models.ConversionTarget target);

    Models.OutputValidationResult Validate(string targetPath, Models.ConversionTarget target, long sourceLength)
        => Validate(targetPath, target);
}
