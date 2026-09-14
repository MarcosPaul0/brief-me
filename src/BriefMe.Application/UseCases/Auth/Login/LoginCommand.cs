using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Auth;

namespace BriefMe.Application.UseCases.Auth.Login;

public sealed record LoginCommand(string Email, string Password, string? IpAddress = null) : ICommand<AuthenticationDto>;
