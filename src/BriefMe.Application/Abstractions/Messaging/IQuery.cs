using BriefMe.Domain.Common;

namespace BriefMe.Application.Abstractions.Messaging;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>
{
}
