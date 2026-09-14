using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;

namespace BriefMe.Application.UseCases.Sla.ConfigureSlaRule;

/// <summary>
/// Writes the SLA policy for one scope (RF027, BRF-042). Identifies the rule by its scope: creates it when
/// the scope has no rule yet, and adjusts the deadline of the existing one when it has — so configuring the
/// same scope twice ends in the same state instead of colliding with the unique index behind
/// <c>SlaRules</c>.
/// </summary>
/// <remarks>
/// There is <b>no</b> <c>ICurrentUser</c> here: a rule is house policy, not someone's record, and who may
/// configure it is decided by the <c>sla.configure</c> policy on the endpoint (RF027 — "the Administrator").
/// The trace of who changed what is written by the audit interceptor (RF038), as for any
/// <c>IAuditableEntity</c>.
/// </remarks>
public sealed class ConfigureSlaRuleCommandHandler(
    ISlaRuleRepository slaRuleRepository,
    ICategoryRepository categoryRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<ConfigureSlaRuleCommand, SlaRuleDto>
{
    public async Task<Result<SlaRuleDto>> Handle(ConfigureSlaRuleCommand request, CancellationToken cancellationToken)
    {
        // An empty id is no category at all, not a category nothing can match — the normalization
        // SlaRule.Create applies, done before the lookup so both agree on which scope is being configured.
        var categoryId = request.CategoryId == Guid.Empty ? null : request.CategoryId;

        // Re-checked here even though the validator already ran: the handler must stand on its own, and the
        // aggregate answers an invalid deadline with a DomainException — an exception is for a programming
        // error, not for user input (CLAUDE.md — Result over exceptions).
        var deadline = SlaDeadline.Create(request.DeadlineHours);
        if (deadline.IsFailure)
            return Result.Failure<SlaRuleDto>(deadline.Error!);

        // A rule aimed at no category applies to all of them, so there is nothing to look up (RF040).
        if (categoryId is not null &&
            !await categoryRepository.ExistsActiveAsync(categoryId.Value, cancellationToken))
        {
            return Result.NotFound<SlaRuleDto>(CategoryErrors.NotFoundOrInactive);
        }

        var rule = await slaRuleRepository.GetByScopeAsync(categoryId, request.Priority, cancellationToken);

        if (rule is null)
        {
            rule = SlaRule.Create(categoryId, request.Priority, request.DeadlineHours);
            await slaRuleRepository.AddAsync(rule, cancellationToken);
        }
        else
        {
            rule.ChangeDeadline(request.DeadlineHours);
            slaRuleRepository.Update(rule);
        }

        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(SlaRuleDto.FromSlaRule(rule));
    }
}
