using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.Categories.ChangeCategoryStatus;

/// <summary>Activates or deactivates a category. An inactive one leaves the requester's picker; briefings keep it.</summary>
public sealed record ChangeCategoryStatusCommand(Guid CategoryId, bool IsActive) : ICommand<CategoryDto>;
