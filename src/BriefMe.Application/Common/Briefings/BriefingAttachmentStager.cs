using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.Briefings;
using BriefMe.Domain.Common;
using BriefMe.Domain.ValueObjects;

namespace BriefMe.Application.Common.Briefings;

/// <summary>
/// Writes the files of a create/edit request to storage, all or nothing (ATT-03..05). Every file is checked
/// first — size and format from its leading bytes — so a single rejected file leaves storage untouched. Only
/// then are they saved; a storage fault midway removes what this call already wrote.
/// </summary>
public sealed class BriefingAttachmentStager(IFileStorage fileStorage)
{
    public async Task<Result<IReadOnlyList<StagedAttachment>>> StageAsync(
        Guid briefingId,
        IReadOnlyList<AttachmentUpload> uploads,
        CancellationToken cancellationToken)
    {
        var formats = new List<AttachmentFormat>(uploads.Count);

        foreach (var upload in uploads)
        {
            // The validator checked the length already; the stager re-checks so it is safe on its own.
            if (upload.Length <= 0)
                return Result.Failure<IReadOnlyList<StagedAttachment>>(BriefingErrors.Validation.AttachmentEmpty(upload.FileName));

            if (upload.Length > AttachmentFormat.MaxSizeBytes)
                return Result.Failure<IReadOnlyList<StagedAttachment>>(
                    BriefingErrors.Validation.AttachmentTooLarge(upload.FileName, AttachmentFormat.MaxSizeBytes));

            var format = await DetectAsync(upload.Content, cancellationToken);
            if (format.IsFailure)
                return Result.Failure<IReadOnlyList<StagedAttachment>>(BriefingErrors.AttachmentRejected(upload.FileName, format.Error!));

            formats.Add(format.Value);
        }

        var staged = new List<StagedAttachment>(uploads.Count);
        try
        {
            for (var index = 0; index < uploads.Count; index++)
            {
                var upload = uploads[index];
                var format = formats[index];
                var key = $"briefings/{briefingId:N}/{Guid.NewGuid():N}{format.Extension}";

                await fileStorage.SaveAsync(key, upload.Content, format.ContentType, cancellationToken);
                staged.Add(new StagedAttachment(upload.FileName, key, format, upload.Length));
            }
        }
        catch
        {
            await DiscardAsync(staged, CancellationToken.None);
            throw;
        }

        return Result.Success<IReadOnlyList<StagedAttachment>>(staged);
    }

    /// <summary>Best-effort removal of files whose briefing change did not commit.</summary>
    public async Task DiscardAsync(IEnumerable<StagedAttachment> staged, CancellationToken cancellationToken)
    {
        foreach (var attachment in staged)
            await fileStorage.DeleteAsync(attachment.StorageKey, cancellationToken);
    }

    /// <summary>Reads the leading bytes, detects the format, and rewinds so the same stream can be saved.</summary>
    private static async Task<Result<AttachmentFormat>> DetectAsync(Stream content, CancellationToken cancellationToken)
    {
        var header = new byte[AttachmentFormat.HeaderLength];
        var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        content.Position = 0;

        return AttachmentFormat.Detect(header.AsSpan(0, read));
    }
}
