using BriefMe.Domain.ValueObjects;

namespace BriefMe.Application.Common.Briefings;

/// <summary>
/// A file already written to storage but not yet recorded on the briefing — the hand-off between
/// <see cref="BriefingAttachmentStager"/> and <c>Briefing.AddAttachment</c>.
/// </summary>
public sealed record StagedAttachment(string? FileName, string StorageKey, AttachmentFormat Format, long SizeBytes);
