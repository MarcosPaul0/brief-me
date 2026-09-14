using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.UseCases.Users.ChangePassword;
using BriefMe.Application.UseCases.Users.ChangeUserStatus;
using BriefMe.Application.UseCases.Users.CreateUser;
using BriefMe.Application.UseCases.Users.DeleteUser;
using BriefMe.Application.UseCases.Users.GetUserById;
using BriefMe.Application.UseCases.Users.GetUsersPaged;
using BriefMe.Application.UseCases.Users.UpdateUser;
using BriefMe.Application.UseCases.Users.UpdateUserRoles;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Web.Authorization;
using BriefMe.Web.Contracts.Users;
using BriefMe.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BriefMe.Web.Controllers;

[ApiController]
[Route("users")]
[Authorize]
public sealed class UsersController(
    ISender sender,
    IAuthorizationService authorizationService,
    ICurrentUser currentUser) : ControllerBase
{
    private const string ResetPasswordPermission = "users.reset-password";

    [HttpPost]
    [HasPermission("users.create")]
    public async Task<IActionResult> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateUserCommand(request.FullName, request.Email, request.Password, request.RoleIds);
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.UserId }, result.Value)
            : result.ToProblem(this, "Falha ao criar usuário");
    }

    [HttpPut("{id:guid}")]
    [HasPermission("users.update")]
    public async Task<IActionResult> Update(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateUserCommand(id, request.FullName, request.Email, request.RoleIds);
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao atualizar usuário");
    }

    [HttpPut("{id:guid}/roles")]
    [HasPermission("roles.manage")]
    public async Task<IActionResult> UpdateRoles(Guid id, UpdateUserRolesRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateUserRolesCommand(id, request.RoleIds);
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao atualizar roles do usuário");
    }

    [HttpDelete("{id:guid}")]
    [HasPermission("users.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var command = new DeleteUserCommand(id, GetClientIpAddress());
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess ? NoContent() : result.ToProblem(this, "Falha ao excluir usuário");
    }

    [HttpPatch("{id:guid}/status")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> ChangeStatus(Guid id, ChangeUserStatusRequest request, CancellationToken cancellationToken)
    {
        var command = new ChangeUserStatusCommand(id, request.Status, GetClientIpAddress());
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess ? NoContent() : result.ToProblem(this, "Falha ao alterar status do usuário");
    }

    [HttpPatch("{id:guid}/password")]
    public async Task<IActionResult> ChangePassword(Guid id, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var isSelf = Guid.TryParse(currentUser.UserId, out var loggedUserId) && loggedUserId == id;
        if (!isSelf)
        {
            var authorization = await authorizationService.AuthorizeAsync(User, ResetPasswordPermission);
            if (!authorization.Succeeded)
                return Forbid();
        }

        var command = new ChangePasswordCommand(id, request.CurrentPassword, request.NewPassword, GetClientIpAddress());
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess ? NoContent() : result.ToProblem(this, "Falha ao alterar senha");
    }

    [HttpGet]
    [HasPermission("users.view")]
    public async Task<IActionResult> GetPaged(
        [FromQuery] string? search,
        [FromQuery] UserStatus? status,
        [FromQuery] Guid? roleId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] UserSortField sortField = UserSortField.FullName,
        [FromQuery] bool sortDescending = false,
        CancellationToken cancellationToken = default)
    {
        var query = new GetUsersPagedQuery(search, status, roleId, page, pageSize, sortField, sortDescending);
        var result = await sender.Send(query, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao listar usuários");
    }

    [HttpGet("{id:guid}")]
    [HasPermission("users.view")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetUserByIdQuery(id);
        var result = await sender.Send(query, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao consultar usuário");
    }

    private string? GetClientIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
