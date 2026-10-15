using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.Users.GetUserAvatar;

/// <summary>Any user's avatar bytes, for the anonymous image endpoint (AVT-09).</summary>
public sealed record GetUserAvatarQuery(Guid UserId) : IQuery<AvatarFileDto>;
