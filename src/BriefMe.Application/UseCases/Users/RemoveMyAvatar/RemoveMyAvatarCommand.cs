using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.Users.RemoveMyAvatar;

/// <summary>Clears the signed-in user's avatar so the UI falls back to the initial (AVT-12). Idempotent.</summary>
public sealed record RemoveMyAvatarCommand : ICommand<UserDto>;
