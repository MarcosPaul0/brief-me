using System.Collections.Concurrent;
using BriefMe.Application.Abstractions.Messaging;

namespace BriefMe.Application.Messaging;

public sealed class Sender : ISender
{
    private static readonly ConcurrentDictionary<(Type RequestType, Type ResponseType), RequestHandlerWrapperBase> Wrappers = new();

    private readonly IServiceProvider _serviceProvider;

    public Sender(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        var wrapper = Wrappers.GetOrAdd((request.GetType(), typeof(TResponse)), CreateWrapper);

        var result = await wrapper.Handle(request, _serviceProvider, cancellationToken);
        return (TResponse)result!;
    }

    private static RequestHandlerWrapperBase CreateWrapper((Type RequestType, Type ResponseType) key)
    {
        var wrapperType = typeof(RequestHandlerWrapper<,>).MakeGenericType(key.RequestType, key.ResponseType);
        return (RequestHandlerWrapperBase)Activator.CreateInstance(wrapperType)!;
    }
}
