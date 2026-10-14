using BriefMe.Application;
using BriefMe.Application.Abstractions;
using BriefMe.Infrastructure;
using BriefMe.Infrastructure.Persistence.Seeding;
using BriefMe.Web.Auth;
using BriefMe.Web.Components;
using BriefMe.Web.Http;
using BriefMe.Web.ApiClients;
using BriefMe.Web.ErrorHandling;
using BriefMe.Web.Extensions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtAuthentication();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Overrides the Infrastructure default (SystemCurrentUser) with the HTTP-request-scoped identity.
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

// Blazor Server auth state: reflects the JWT issued by AuthController inside the circuit (USR-038).
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<ProtectedSessionStorage>();
builder.Services.AddScoped<IAuthTokenStorage, ProtectedBrowserAuthTokenStorage>();
builder.Services.AddScoped<JwtClaimsPrincipalFactory>();
builder.Services.AddScoped<AuthSessionStore>();
builder.Services.AddScoped<AuthenticationStateProvider, JwtAuthenticationStateProvider>();

var apiBaseAddress = new Uri(builder.Configuration["Api:BaseAddress"]
    ?? throw new InvalidOperationException("Configuration key 'Api:BaseAddress' is required."));

// Login/refresh/logout (USR-039): a dedicated client with no auth handlers attached, so a failed
// refresh call can never trigger the "Api" client's own refresh-on-401 handler recursively.
builder.Services.AddHttpClient<IAuthService, AuthService>(client => client.BaseAddress = apiBaseAddress);

// Raw "Api" client — no auth handlers. IHttpClientFactory message handlers run outside the Blazor
// circuit scope, so they saw an empty AuthSessionStore and an uninitialized NavigationManager
// (USR-039 regression). Bearer + refresh-on-401 live in the circuit-scoped AuthenticatedApiClient instead.
builder.Services.AddHttpClient("Api", client => client.BaseAddress = apiBaseAddress);

// Circuit-scoped auth wrapper the typed API clients send through.
builder.Services.AddScoped<AuthenticatedApiClient>();

// Blazor data client for the user-listing page (USR-042): runs over the "Api" pipeline above.
builder.Services.AddScoped<IUsersApiClient, UsersApiClient>();

// Blazor data client for the briefing create/edit form (BRF-013): same "Api" pipeline.
builder.Services.AddScoped<IBriefingsApiClient, BriefingsApiClient>();



var app = builder.Build();

// Apply pending migrations and seed the permission catalog / native data on startup (USR-046+).
await app.Services.MigrateAndSeedAsync();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
// Innermost exception handler: catches API-controller exceptions first and returns ProblemDetails JSON.
// Non-API (Blazor) exceptions fall through unhandled to the "/Error" handler registered above.
app.UseExceptionHandler();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
// Skip HTTPS redirect in Development: the app calls its own API over http loopback, and redirecting
// to the self-signed https endpoint fails the HttpClient cert chain (UntrustedRoot).
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
