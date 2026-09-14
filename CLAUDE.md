# BriefMe

.NET 10 Blazor Server app following Clean Architecture.

## Project Structure

```
src/
├── BriefMe.Domain/         # Entities, value objects, domain events, repository interfaces
├── BriefMe.Application/    # Use cases, DTOs, service interfaces, CQRS handlers
├── BriefMe.Infrastructure/ # EF Core, external APIs, repository implementations
└── BriefMe.Web/            # Blazor Server UI, DI composition root
tests/
```

## Dependency Rules (strict — never violate)

```
Domain ← Application ← Infrastructure
                ↑
              Web (composition root)
```

- **Domain**: no project references, no framework dependencies
- **Application**: references Domain only
- **Infrastructure**: references Application (implements its interfaces)
- **Web**: references Application + Infrastructure; wires DI; never contains business logic

## Layer Responsibilities

### Domain (`BriefMe.Domain`)
- Entities with private setters; state changes via methods
- Value objects (immutable, equality by value)
- Domain events
- Repository interfaces (`IXRepository`)
- Domain exceptions
- No `using Microsoft.*` except `System.*`

### Application (`BriefMe.Application`)
- One use case per file (command/query handler or service)
- DTOs for input/output — never expose domain entities to outer layers
- Output DTOs live flat under `Common/Dtos/` (namespace `BriefMe.Application.Common.Dtos`), one type per file — never inside a use-case folder
- Service interfaces for external concerns (email, storage, etc.)
- Validation logic lives here, not in Web
- Failure messages live in the catalogs under `Common/Errors/` — never inline literals or per-handler consts
- Pagination envelope (`PagedResult<T>`) lives in `Common/Pagination/` — assembled by the handler, never returned by a repository
- No EF Core, no HTTP clients

### Infrastructure (`BriefMe.Infrastructure`)
- EF Core `DbContext` and configurations
- Repository implementations
- External API clients (HTTP, messaging)
- Migrations

### Web (`BriefMe.Web`)
- Routable pages (`@page`) in `Pages/` — one page = one screen/route
- Reusable UI parts in `Components/` (`Auth/`, `Layout/`, `Profiles/`, `Users/`) — building blocks composed into pages
- App root infra (`App.razor`, `Routes.razor`) stays in `Components/`
- Typed REST API clients in `ApiClients/` (namespace `BriefMe.Web.ApiClients`) — thin clients pages/components call to reach the API; base HTTP infra (`AuthenticatedApiClient`, extensions) stays in `Http/`
- Shared `@using` directives in root `_Imports.razor`
- DI registration (`Program.cs`)
- No business logic in components — call Application use cases only
- `appsettings.json` / `appsettings.Development.json` for config

## Commands

```bash
dotnet build                          # build solution
dotnet run --project src/BriefMe.Web  # run dev server
dotnet test                           # run all tests
dotnet ef migrations add <Name> --project src/BriefMe.Infrastructure --startup-project src/BriefMe.Web
dotnet ef database update --project src/BriefMe.Infrastructure --startup-project src/BriefMe.Web
```

## Conventions

- Nullable reference types enabled everywhere — no `!` suppression without justification
- `Result<T>` / discriminated unions over exceptions for expected failures
- Repository pattern: interfaces in Domain, implementations in Infrastructure
- Repositories return entities or plain collections (`IReadOnlyList<T>`) — never a paging envelope. A paginated read is two methods: `GetXAsync(..., page, pageSize)` for the slice and `CountXAsync(...)` for the total behind the same filter; the handler combines them into `PagedResult<T>` over DTOs
- CQRS: commands mutate state, queries return data — keep them separate
- Blazor components are thin; extract logic to Application layer services
- No static classes or global state in Domain or Application
- DTO mapping: static `From<Source>` factory on the DTO — see [DTO Mapping Rules](.claude/rules/dto-mapping.md)
- Error messages: one catalog per feature in `Application/Common/Errors/` — see [Error Message Rules](.claude/rules/error-messages.md)

## Testing

See [Unit Testing Rules](.claude/rules/unit-tests.md) — AAA pattern, naming convention, mocking guidelines, project organization.
