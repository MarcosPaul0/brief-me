using BriefMe.Application.Abstractions;
using BriefMe.Domain.Common;
using Microsoft.Extensions.Options;

namespace BriefMe.Infrastructure.Storage;

/// <summary>
/// Enforces the configured size limit and extension allow-list for uploads (BRF-011). Expected
/// rejections are returned as a failure <see cref="Result"/>, never thrown.
/// </summary>
public sealed class FileValidator(IOptions<FileStorageSettings> settings) : IFileValidator
{
    private readonly FileStorageSettings _settings = settings.Value;

    public Result Validate(string fileName, long sizeBytes)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(fileName))
            errors.Add(new ValidationError(nameof(fileName), "File name is required."));

        if (sizeBytes <= 0)
            errors.Add(new ValidationError(nameof(sizeBytes), "File must not be empty."));
        else if (sizeBytes > _settings.MaxSizeBytes)
            errors.Add(new ValidationError(
                nameof(sizeBytes),
                $"File exceeds the maximum allowed size of {_settings.MaxSizeBytes} bytes."));

        if (!string.IsNullOrWhiteSpace(fileName) && _settings.AllowedExtensions.Length > 0)
        {
            var extension = Path.GetExtension(fileName);
            if (!_settings.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                errors.Add(new ValidationError(
                    nameof(fileName),
                    $"File type '{extension}' is not allowed."));
        }

        return errors.Count == 0 ? Result.Success() : Result.Failure(errors);
    }
}
