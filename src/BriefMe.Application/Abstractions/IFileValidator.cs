using BriefMe.Domain.Common;

namespace BriefMe.Application.Abstractions;

/// <summary>
/// Validates an upload's file name (type/extension) and size against the configured limits (BRF-011).
/// Returns a failure <see cref="Result"/> for expected rejections rather than throwing, so use cases can
/// surface field-level validation errors before any file is persisted via <see cref="IFileStorageService"/>.
/// </summary>
public interface IFileValidator
{
    /// <summary>
    /// Checks <paramref name="fileName"/>'s extension and <paramref name="sizeBytes"/> against the
    /// configured allow-list and maximum size. Reports every violation at once.
    /// </summary>
    Result Validate(string fileName, long sizeBytes);
}
