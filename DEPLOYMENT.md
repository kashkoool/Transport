# Deployment & DevOps

How this app is built, shipped, and run. Stack: ASP.NET Core (.NET 10) API + Angular SPA +
PostgreSQL. Containerized; CI/CD via GitHub Actions; images published to GHCR.

## 1. Run the whole stack locally (Docker Compose)

```sh
docker compose up --build
```

Brings up, in order: **postgres** → **migrator** (applies EF migrations, then exits) → **api**
(waits for the DB healthy + migrations done) → **web** (nginx, waits for the API healthy).

- Web app: <http://localhost:8080>
- API (direct): <http://localhost:5278>  · liveness `/health` · readiness `/health/ready`

Compose runs the API in **Development** so it's usable immediately (the sandbox
`/api/payments/dev/simulate` endpoint is available; the refresh cookie works over plain HTTP).
Override the Postgres password via a `.env` file (see `.env.example`).

## 2. Images

Two production images, both built multi-stage and run as **non-root**:

| Image | Dockerfile | Base (runtime) | Port |
| --- | --- | --- | --- |
| API | `src/TransportPlatform.Api/Dockerfile` | `mcr.microsoft.com/dotnet/aspnet:10.0` | 8080 |
| Web | `web/customer/Dockerfile` | `nginxinc/nginx-unprivileged:alpine` | 8080 |

The API image also contains a **self-contained EF migrations bundle** (`./efbundle`). It is run
as a **one-off before** the app starts (never from app startup), so new code never meets an old
schema. The same image serves both roles: `dotnet TransportPlatform.Api.dll` (serve) and
`./efbundle --connection "<conn>"` (migrate).

Build context for the API is the **repo root** (it needs the shared `Directory.*.props` +
`.editorconfig`):

```sh
docker build -f src/TransportPlatform.Api/Dockerfile -t tpx-api .
docker build -f web/customer/Dockerfile -t tpx-web web/customer
```

## 3. CI/CD (GitHub Actions)

| Workflow | Trigger | What it does |
| --- | --- | --- |
| `ci.yml` | PR + push to main | Backend build + unit/integration tests (Testcontainers Postgres on the runner), frontend build + ESLint, Docker build smoke (no push), gitleaks secret scan |
| `deploy.yml` | push to main + manual | Builds and pushes `transport-api` / `transport-web` images to **GHCR**, tagged with the commit SHA + `latest` |
| `codeql.yml` | PR + push + weekly | CodeQL SAST for C# and TypeScript |
| `dependency-review.yml` | PR | Blocks PRs adding vulnerable / disallowed-license deps |
| `scorecard.yml` | push + weekly | OpenSSF Scorecard supply-chain score |
| `dependabot.yml` | weekly | Update PRs for NuGet, npm, GitHub Actions, Docker base images |

Published images (GHCR, public):

```
ghcr.io/<owner>/transport-api:<sha>   ghcr.io/<owner>/transport-api:latest
ghcr.io/<owner>/transport-web:<sha>   ghcr.io/<owner>/transport-web:latest
```

CD needs no external cloud account or long-lived secrets — GHCR auth uses the built-in
`GITHUB_TOKEN`.

### Secret-leak defense (layered)

Three independent gates stop a secret before/at GitHub:

1. **Local pre-push hook** — `scripts/git-hooks/pre-push` runs **gitleaks** and blocks the push
   if anything is found. Enable it once per clone:

   ```sh
   git config core.hooksPath scripts/git-hooks
   ```

   It uses a local `gitleaks` binary if present, else runs it via Docker. Config +
   dev/test-placeholder allowlist live in `.gitleaks.toml`. Emergency bypass: `git push --no-verify`.
2. **GitHub push protection + secret scanning** — server-side, blocks a push containing a
   detected secret even if the hook was skipped.
3. **CI `gitleaks` job** — scans every PR/push as the last backstop.

### CodeRabbit (AI PR review)

`.coderabbit.yaml` tunes an AI reviewer that comments on every PR (bugs, security, perf), with
per-area focus on auth/payments/endpoints. One-time setup: install the GitHub App at
<https://github.com/apps/coderabbitai> on the repo (free for public repos).

## 4. Production configuration (required)

The API **fails fast on boot** in Production if config is unsafe (see `Security/StartupGuards.cs`).
Supply these from your platform's secret store (never bake into the image):

| Setting (env var) | Requirement |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__Postgres` | real host; include `SSL Mode=Require` (TLS); non-placeholder password |
| `Jwt__SigningKey` | ≥ 32 chars, not a known dev placeholder |
| `Payments__WebhookSecret` | real secret, not a placeholder |
| `Proxy__TrustedHops` | number of proxies in front (e.g. Cloudflare + LB = 2) — must be > 0 so per-IP rate limits key on the real client IP |
| `AllowedHosts` | explicit host list, never `*` |
| `Cors__AllowedOrigins__0` | the web app's real origin |

In Production the refresh cookie is `Secure` and dev-only endpoints (`/dev/simulate`) are **not
mapped**.

## 5. Deploy targets

The images run anywhere that runs containers. Pick one:

### a) Single host / VPS (simplest)
Run the same compose on a server, but with a Production override: set `ASPNETCORE_ENVIRONMENT=Production`,
inject the secrets above, point Postgres at a managed DB (or a volume-backed container with
backups), and put TLS in front (Caddy/Traefik/nginx, or Cloudflare). Pull images from GHCR
instead of building. Migrations: run `./efbundle` as a one-off (a compose `migrator` service or
`docker run --rm <api-image> ./efbundle --connection ...`) before starting the API.

### b) Managed container platform
- **Azure Container Apps** or **AWS App Runner / ECS Fargate**: deploy the two GHCR images as two
  services; managed Postgres (Azure Database for PostgreSQL / Amazon RDS) with TLS; secrets from
  Key Vault / Secrets Manager; run the migration bundle as a one-off job/task before rolling out
  the API. Point the readiness probe at `/health/ready` and the liveness probe at `/health`.

A CD step to a specific cloud (push image → run migration job → update service → smoke-test) can
be added to `deploy.yml` once the target + credentials (ideally OIDC, not static keys) are
chosen.

## 6. Migrations & rollback

- **Forward:** the migration bundle runs as a one-off and must succeed before the API rolls out.
- **Ship additive migrations** (new nullable columns / new tables) so a rollback is a code-only
  revert. Destructive changes (DROP/RENAME) go in a later release, after the old column is unused
  — the 3-phase pattern.
- **App rollback:** redeploy the previous image tag (SHA). Because migrations are forward-only,
  pair a breaking schema change with a compensating forward migration rather than relying on a
  schema rollback.

## 7. Health probes

- `GET /health` — liveness. Zero dependency checks; 200 if the process is up. Wire the
  orchestrator's **liveness** probe here so a slow DB never causes a restart loop.
- `GET /health/ready` — readiness. Checks DB connectivity. Wire the **readiness** probe here so
  traffic is held until dependencies are reachable.
