# Build from the REPO ROOT so the shared props + every project are in context:
#   docker build -f src/TransportPlatform.Api/Dockerfile -t tpx-api .
# syntax=docker/dockerfile:1

# ── Stage 1: build, publish, and produce a migrations bundle ─────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore against just the manifests first, so the (slow) restore layer is cached until a
# .csproj or the central package list actually changes. .editorconfig must be present too — it
# carries the analyzer config (e.g. CA1861 is suppressed for EF-generated migrations), without
# which the Release build fails on generated code.
COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/TransportPlatform.Domain/TransportPlatform.Domain.csproj src/TransportPlatform.Domain/
COPY src/TransportPlatform.Application/TransportPlatform.Application.csproj src/TransportPlatform.Application/
COPY src/TransportPlatform.Infrastructure/TransportPlatform.Infrastructure.csproj src/TransportPlatform.Infrastructure/
COPY src/TransportPlatform.Api/TransportPlatform.Api.csproj src/TransportPlatform.Api/
RUN dotnet restore src/TransportPlatform.Api/TransportPlatform.Api.csproj

# Now the source, then publish.
COPY src/ src/
RUN dotnet publish src/TransportPlatform.Api/TransportPlatform.Api.csproj \
    -c Release -o /app/publish --no-restore

# A self-contained migrations bundle: a single executable that applies EF Core migrations as a
# one-off at deploy time (never from app startup). Self-contained so it runs even on a minimal
# base with no SDK.
RUN dotnet tool install --global dotnet-ef --version 10.0.0
ENV PATH="$PATH:/root/.dotnet/tools"
RUN dotnet ef migrations bundle --self-contained -r linux-x64 \
    --project src/TransportPlatform.Infrastructure/TransportPlatform.Infrastructure.csproj \
    --startup-project src/TransportPlatform.Api/TransportPlatform.Api.csproj \
    --configuration Release -o /app/efbundle

# ── Stage 2: minimal runtime, non-root ───────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080

# curl for the container HEALTHCHECK (the runtime image ships neither curl nor wget).
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./
COPY --from=build /app/efbundle ./efbundle

# The .NET 8+ runtime images ship a non-root 'app' user — run as it.
USER app
EXPOSE 8080

# Liveness probe (dependency-free /health). startPeriod gives the app time to JIT/boot.
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "TransportPlatform.Api.dll"]
