using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Tests.TestDoubles;
using BriefMe.Domain.Common;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace BriefMe.Application.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public async Task AddApplication_WhenCalled_RegistersSenderAndValidationPipeline()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddScoped<IRequestHandler<TestCommand, Result<string>>, TestCommandHandler>();
        var sender = services.BuildServiceProvider().GetRequiredService<ISender>();

        // Act
        var result = await sender.Send(new TestCommand("value"));

        // Assert
        result.Value.Should().Be("handled:value");
    }
}
