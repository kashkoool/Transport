# TPX Transport Platform

A bus trip booking platform: ASP.NET Core (.NET 10) + PostgreSQL backend and an Angular +
Tailwind customer web app. Built as a **Clean Architecture modular monolith** — see
[ARCHITECTURE.md](ARCHITECTURE.md) for the layering and the principles we hold to.

## Layout

```text
src/
  TransportPlatform.Domain          entities, value objects, domain events (no dependencies)
  TransportPlatform.Application     use-case handlers + abstractions
  TransportPlatform.Infrastructure  EF Core/Postgres, Identity/JWT, payment gateway
  TransportPlatform.Api             minimal-API endpoints, middleware, background workers
tests/
  TransportPlatform.UnitTests       domain + application + architecture (layering) tests
  TransportPlatform.IntegrationTests  full API against a throwaway Postgres (Testcontainers)
web/customer                        Angular 20 + Tailwind customer SPA (see its own README)
```

## Prerequisites

- .NET 10 SDK and Docker (the integration tests spin up Postgres via Testcontainers).
- A local PostgreSQL for running the API. The dev connection string is in
  `src/TransportPlatform.Api/appsettings.json`. Quick start with Docker:

  ```sh
  docker run --name tpx-pg -e POSTGRES_DB=transport -e POSTGRES_USER=transport \
    -e POSTGRES_PASSWORD=change_me_local_only -p 5432:5432 -d postgres:17-alpine
  ```

## Build, test, run

```sh
dotnet build                         # 0 warnings (warnings are errors)
dotnet test                          # unit + integration (Docker must be running)

dotnet ef database update \
  --project src/TransportPlatform.Infrastructure \
  --startup-project src/TransportPlatform.Api    # apply migrations

dotnet run --project src/TransportPlatform.Api --launch-profile http   # API on :5278
```

Then run the customer app — see [web/customer/README.md](web/customer/README.md) (`npm install`
&& `npm start`, served on :4200 and proxying `/api` to the API).

> The repo path contains spaces, which breaks the EF CLI. If `dotnet ef` errors, run it from a
> junction without spaces, e.g. `mklink /J C:\tmp\tpx "<repo path>"` then work from `C:\tmp\tpx`.

### Or run the whole stack in Docker

```sh
docker compose up --build   # postgres -> migrations -> API -> web (nginx)
```

Web app on <http://localhost:8080>, API on <http://localhost:5278>. Containers, CI/CD,
production config, and cloud deploy targets are documented in [DEPLOYMENT.md](DEPLOYMENT.md).

## Keeping the architecture clean

The Dependency Rule is enforced automatically by
`tests/TransportPlatform.UnitTests/Architecture/LayeringTests.cs` — the build fails if an inner
layer takes a dependency on an outer one. Add features as new handlers/implementations (Open/
Closed), depend on abstractions, and keep inner layers free of outer ones. Details in
[ARCHITECTURE.md](ARCHITECTURE.md).

## Strategic docs

[../ARCHITECTURE.md](../ARCHITECTURE.md) · [../SYSTEM_MAP.md](../SYSTEM_MAP.md) ·
[../STACK_RECOMMENDATION.md](../STACK_RECOMMENDATION.md)
