using BriefMe.Application.Abstractions.Messaging;

namespace BriefMe.Application.UseCases.Auth.Logout;

public sealed record LogoutCommand(string RefreshToken = "", bool AllDevices = false, string? IpAddress = null) : ICommand;
