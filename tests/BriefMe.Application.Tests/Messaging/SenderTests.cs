using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Messaging;
using BriefMe.Application.Tests.TestDoubles;
using BriefMe.Domain.Common;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace BriefMe.Application.Tests.Messaging;

public class SenderTests
{
    [Fact]
    public async Task Send_WhenHandlerIsRegistered_ReturnsHandlerResult()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<ISender, Sender>();
        services.AddScoped<IRequestHandler<TestCommand, Result<string>>, TestCommandHandler>();
        var sender = services.BuildServiceProvider().GetRequiredService<ISender>();

        // Act
        var result = await sender.Send(new TestCommand("value"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("handled:value");
    }

    [Fact]
    public async Task Send_WhenMultipleBehaviorsAreRegistered_ExecutesThemInRegistrationOrder()
    {
        // Arrange
        var executionOrder = new List<string>();
        var services = new ServiceCollection();
        services.AddScoped<ISender, Sender>();
        services.AddScoped<IRequestHandler<TestCommand, Result<string>>, TestCommandHandler>();
        services.AddSingleton<IPipelineBehavior<TestCommand, Result<string>>>(new RecordingBehavior("first", executionOrder));
        services.AddSingleton<IPipelineBehavior<TestCommand, Result<string>>>(new RecordingBehavior("second", executionOrder));
        var sender = services.BuildServiceProvider().GetRequiredService<ISender>();

        // Act
        await sender.Send(new TestCommand("value"));

        // Assert
        executionOrder.Should().Equal("first", "second");
    }

    [Fact]
    public async Task Send_WhenBehaviorDoesNotCallNext_HandlerIsNotExecuted()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<ISender, Sender>();
        services.AddScoped<IRequestHandler<TestCommand, Result<string>>, TestCommandHandler>();
        services.AddSingleton<IPipelineBehavior<TestCommand, Result<string>>>(new ShortCircuitBehavior());
        var sender = services.BuildServiceProvider().GetRequiredService<ISender>();

        // Act
        var result = await sender.Send(new TestCommand("value"));

        // Assert
        result.Value.Should().Be("short-circuited");
    }
}
