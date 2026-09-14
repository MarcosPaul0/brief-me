using BriefMe.Infrastructure.Auth;
using BriefMe.Web.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BriefMe.Web.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>>((bearerOptions, jwtSettingsOptions) =>
            {
                bearerOptions.TokenValidationParameters = JwtTokenValidationParameters.Build(jwtSettingsOptions.Value);

                bearerOptions.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        context.HttpContext.Items["AuthFailureDetail"] = context.Exception is SecurityTokenExpiredException
                            ? "Token de acesso expirado."
                            : "Token de acesso inválido.";
                        return Task.CompletedTask;
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();

                        var detail = context.HttpContext.Items["AuthFailureDetail"] as string
                            ?? "Token de acesso ausente, inválido ou expirado.";

                        await WriteProblemAsync(context.Response, StatusCodes.Status401Unauthorized, "Não autenticado", detail);
                    },
                    OnForbidden = context => WriteProblemAsync(
                        context.Response,
                        StatusCodes.Status403Forbidden,
                        "Acesso negado",
                        "Você não possui permissão para acessar este recurso.")
                };
            });

        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ComponentAwareAuthorizationMiddlewareResultHandler>();

        return services;
    }

    private static Task WriteProblemAsync(HttpResponse response, int statusCode, string title, string detail)
    {
        response.StatusCode = statusCode;
        response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };

        return response.WriteAsJsonAsync(problem);
    }
}
