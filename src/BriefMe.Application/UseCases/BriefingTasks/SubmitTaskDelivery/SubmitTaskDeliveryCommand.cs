using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.BriefingTasks.SubmitTaskDelivery;

/// <summary>
/// Attaches a file produced while executing a task (RF022, BRF-031). The endpoint behind it is
/// <c>POST /tasks/{id}/deliveries</c> (BRF-032).
/// </summary>
/// <remarks>
/// The uploader is not part of the command: it is the authenticated user (<c>ICurrentUser</c>), which the
/// handler reads and records as the delivery's author (RF052). Neither is the briefing — a task belongs to
/// exactly one (RF016), so the handler takes it from the loaded task rather than trusting the client.
/// <para>
/// <see cref="Content"/> is a <see cref="Stream"/> because that is the contract <c>IFileStorageService</c>
/// fixed (BRF-011); buffering a 25 MB upload into a <c>byte[]</c> first would buy nothing.
/// <see cref="SizeBytes"/> travels alongside it because <c>IFileValidator</c> is defined over a declared
/// size and the stream may not be seekable. Both are nullable/unchecked here — an absent multipart part is
/// what the validator turns into a <c>400</c> instead of an <c>ArgumentNullException</c>.
/// </para>
/// </remarks>
public sealed record SubmitTaskDeliveryCommand(
    Guid TaskId,
    string? FileName,
    long SizeBytes,
    Stream? Content) : ICommand<AttachmentDto>;
