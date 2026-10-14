using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.UseCases.Users.ChangePassword;
using BriefMe.Application.UseCases.Users.ChangeUserStatus;
using BriefMe.Application.UseCases.Users.CreateUser;
using BriefMe.Application.UseCases.Users.DeleteUser;
using BriefMe.Application.UseCases.Users.GetUserById;
using BriefMe.Application.UseCases.Users.GetUsersPaged;
using BriefMe.Application.UseCases.Users.UpdateUser;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Web.Contracts.Users;
using BriefMe.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BriefMe.Web.Controllers;

[ApiController]
[Route("users")]
[Authorize]
public sealed class UsersController(ISender sender, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateUserCommand(request.FullName, request.Email, request.Password, request.Role);
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.UserId }, result.Value)
            : result.ToProblem(this, "Falha ao criar usuário");
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> Update(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateUserCommand(id, request.FullName, request.Email, request.Role);
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao atualizar usuário");
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var command = new DeleteUserCommand(id, GetClientIpAddress());
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess ? NoContent() : result.ToProblem(this, "Falha ao excluir usuário");
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> ChangeStatus(Guid id, ChangeUserStatusRequest request, CancellationToken cancellationToken)
    {
        var command = new ChangeUserStatusCommand(id, request.Status, GetClientIpAddress());
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess ? NoContent() : result.ToProblem(this, "Falha ao alterar status do usuário");
    }

    /// <summary>
    /// Every user may change their own password; resetting someone else's is the admin's — the only action
    /// here not gated on the Admin role alone.
    /// </summary>
    [HttpPatch("{id:guid}/password")]
    public async Task<IActionResult> ChangePassword(Guid id, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var isSelf = Guid.TryParse(currentUser.UserId, out var loggedUserId) && loggedUserId == id;
        if (!isSelf && !User.IsInRole(nameof(UserRole.Admin)))
            return Forbid();

        var command = new ChangePasswordCommand(id, request.CurrentPassword, request.NewPassword, GetClientIpAddress());
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess ? NoContent() : result.ToProblem(this, "Falha ao alterar senha");
    }

    [HttpGet]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> GetPaged(
        [FromQuery] string? search,
        [FromQuery] UserStatus? status,
        [FromQuery] UserRole? role,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] UserSortField sortField = UserSortField.FullName,
        [FromQuery] bool sortDescending = false,
        CancellationToken cancellationToken = default)
    {
        var query = new GetUsersPagedQuery(search, status, role, page, pageSize, sortField, sortDescending);
        var result = await sender.Send(query, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao listar usuários");
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetUserByIdQuery(id);
        var result = await sender.Send(query, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao consultar usuário");
    }

    private string? GetClientIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
