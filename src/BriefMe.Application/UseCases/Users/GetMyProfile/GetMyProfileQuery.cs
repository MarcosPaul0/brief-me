using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.Users.GetMyProfile;

/// <summary>The signed-in user's own profile — what the header and the profile page render, for every role.</summary>
public sealed record GetMyProfileQuery : IQuery<UserDto>;
