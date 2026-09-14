using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Roles.CreateRole;

public sealed class CreateRoleCommandHandler(
    IRoleRepository roleRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<CreateRoleCommand, RoleDto>
{
    public async Task<Result<RoleDto>> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        if (await roleRepository.NameExistsAsync(name, cancellationToken: cancellationToken))
            return Result.Conflict<RoleDto>(RoleErrors.DuplicateName);

        var role = Role.Create(name, request.Description);

        await roleRepository.AddAsync(role, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(RoleDto.FromRole(role));
    }
}
