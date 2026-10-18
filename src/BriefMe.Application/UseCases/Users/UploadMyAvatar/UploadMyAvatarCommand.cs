using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.Users.UploadMyAvatar;

/// <summary>
/// Replaces the signed-in user's avatar (AVT-01). There is no user id: the avatar is self-service only, so the
/// handler acts on <c>ICurrentUser</c>. <paramref name="Length"/> is the declared size, checked before any
/// byte is read; the handler re-checks the cap while reading.
/// </summary>
public sealed record UploadMyAvatarCommand(Stream Content, long Length) : ICommand<UserDto>;
