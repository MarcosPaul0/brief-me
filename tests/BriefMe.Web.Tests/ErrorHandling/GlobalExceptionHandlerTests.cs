using System.Text.Json;
using BriefMe.Web.ErrorHandling;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

namespace BriefMe.Web.Tests.ErrorHandling;

public class GlobalExceptionHandlerTests
{
    private static GlobalExceptionHandler CreateHandler(string environmentName)
    {
        var environmentMock = new Mock<IHostEnvironment>();
        environmentMock.SetupGet(e => e.EnvironmentName).Returns(environmentName);

        return new GlobalExceptionHandler(environmentMock.Object, new Mock<ILogger<GlobalExceptionHandler>>().Object);
    }

    private static DefaultHttpContext CreateHttpContextWithApiControllerEndpoint()
    {
        var httpContext = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var endpoint = new Endpoint(
            requestDelegate: null,
            metadata: new EndpointMetadataCollection(new ApiControllerAttribute()),
            displayName: "TestApiEndpoint");
        httpContext.SetEndpoint(endpoint);

        return httpContext;
    }

    [Fact]
    public async Task TryHandleAsync_WhenEndpointHasNoApiControllerMetadata_ReturnsFalse()
    {
        // Arrange
        var handler = CreateHandler(Environments.Production);
        var httpContext = new DefaultHttpContext();

        // Act
        var handled = await handler.TryHandleAsync(httpContext, new InvalidOperationException("boom"), CancellationToken.None);

        // Assert
        handled.Should().BeFalse();
    }

    [Fact]
    public async Task TryHandleAsync_WhenEndpointHasApiControllerMetadata_ReturnsTrueAndWrites500ProblemDetails()
    {
        // Arrange
        var handler = CreateHandler(Environments.Production);
        var httpContext = CreateHttpContextWithApiControllerEndpoint();

        // Act
        var handled = await handler.TryHandleAsync(httpContext, new InvalidOperationException("boom"), CancellationToken.None);

        // Assert
        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task TryHandleAsync_WhenEnvironmentIsProduction_HidesExceptionMessageFromDetail()
    {
        // Arrange
        var handler = CreateHandler(Environments.Production);
        var httpContext = CreateHttpContextWithApiControllerEndpoint();

        // Act
        await handler.TryHandleAsync(httpContext, new InvalidOperationException("sensitive internal detail"), CancellationToken.None);

        // Assert
        var problem = await ReadProblemDetailsAsync(httpContext);
        problem!.Detail.Should().NotContain("sensitive internal detail");
    }

    [Fact]
    public async Task TryHandleAsync_WhenEnvironmentIsDevelopment_IncludesExceptionMessageInDetail()
    {
        // Arrange
        var handler = CreateHandler(Environments.Development);
        var httpContext = CreateHttpContextWithApiControllerEndpoint();

        // Act
        await handler.TryHandleAsync(httpContext, new InvalidOperationException("sensitive internal detail"), CancellationToken.None);

        // Assert
        var problem = await ReadProblemDetailsAsync(httpContext);
        problem!.Detail.Should().Contain("sensitive internal detail");
    }

    [Fact]
    public async Task TryHandleAsync_WhenHandled_IncludesTraceIdExtension()
    {
        // Arrange
        var handler = CreateHandler(Environments.Production);
        var httpContext = CreateHttpContextWithApiControllerEndpoint();
        httpContext.TraceIdentifier = "trace-123";

        // Act
        await handler.TryHandleAsync(httpContext, new InvalidOperationException("boom"), CancellationToken.None);

        // Assert
        var problem = await ReadProblemDetailsAsync(httpContext);
        problem!.Extensions["traceId"]!.ToString().Should().Be("trace-123");
    }

    private static async Task<ProblemDetails?> ReadProblemDetailsAsync(DefaultHttpContext httpContext)
    {
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonSerializer.DeserializeAsync<ProblemDetails>(httpContext.Response.Body);
    }
}
