using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Auth;

namespace BriefMe.Application.UseCases.Auth.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken, string? IpAddress = null) : ICommand<AuthenticationDto>;
