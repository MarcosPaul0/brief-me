using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.UseCases.Users.ChangePassword;
using BriefMe.Application.UseCases.Users.ChangeUserStatus;
using BriefMe.Application.UseCases.Users.CreateUser;
using BriefMe.Application.UseCases.Users.DeleteUser;
using BriefMe.Application.UseCases.Users.GetMyProfile;
using BriefMe.Application.UseCases.Users.GetUserAvatar;
using BriefMe.Application.UseCases.Users.GetUserById;
using BriefMe.Application.UseCases.Users.GetUsersPaged;
using BriefMe.Application.UseCases.Users.RemoveMyAvatar;
using BriefMe.Application.UseCases.Users.UpdateUser;
using BriefMe.Application.UseCases.Users.UploadMyAvatar;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
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
    // Room for the multipart envelope around a maximum-size avatar. Anything bigger is cut off by Kestrel
    // before it is buffered; below it, the validator answers with the friendly size message (AVT-04).
    private const long AvatarRequestSizeLimit = AvatarFormat.MaxSizeBytes + 64 * 1024;

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

    /// <summary>The signed-in user's own profile, for every role (header, profile page).</summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMyProfileQuery(), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao consultar perfil");
    }

    /// <summary>
    /// Replaces the caller's own avatar (AVT-01). Self-service only, so there is no id to authorize — the
    /// handler acts on the authenticated user. Multipart field <c>file</c>.
    /// </summary>
    [HttpPut("me/avatar")]
    [RequestSizeLimit(AvatarRequestSizeLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = AvatarRequestSizeLimit)]
    public async Task<IActionResult> UploadMyAvatar(IFormFile? file, CancellationToken cancellationToken)
    {
        await using var content = file?.OpenReadStream() ?? Stream.Null;
        var command = new UploadMyAvatarCommand(content, file?.Length ?? 0);
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao enviar avatar");
    }

    [HttpDelete("me/avatar")]
    public async Task<IActionResult> RemoveMyAvatar(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RemoveMyAvatarCommand(), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this, "Falha ao remover avatar");
    }

    /// <summary>
    /// Avatar bytes for an <c>&lt;img&gt;</c> (AVT-09). Anonymous because an image request carries no bearer
    /// token; ids are unguessable and avatars are not sensitive. The UI always appends <c>?v={AvatarVersion}</c>,
    /// which changes on every upload, so such a request may be cached as immutable; a bare URL revalidates.
    /// </summary>
    [HttpGet("{id:guid}/avatar")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvatar(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetUserAvatarQuery(id), cancellationToken);
        if (result.IsFailure)
            return result.ToProblem(this, "Avatar indisponível");

        Response.Headers.CacheControl = Request.Query.ContainsKey("v")
            ? "public, max-age=31536000, immutable"
            : "no-cache";
        Response.Headers.XContentTypeOptions = "nosniff";

        return File(result.Value.Content, result.Value.ContentType);
    }

    private string? GetClientIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
