# Architecture

This codebase is built so you can **fix one area without affecting others** and **add features
without breaking what exists**. That property has names, and we follow them deliberately:

- **Separation of Concerns** + **loose coupling / high cohesion** — each piece has one job and
  knows as little as possible about the others.
- **SOLID**, especially:
  - **Open/Closed Principle** — *open for extension, closed for modification*. Add a feature by
    adding a new use-case or a new implementation, **not** by editing existing code.
  - **Dependency-Inversion Principle** — code depends on **abstractions** (interfaces), never on
    concrete implementations.
- **Clean Architecture** — a layered **modular monolith** with the **Dependency Rule**:
  dependencies only point **inward**.

## Layers (dependencies point inward only)

```text
        ┌─────────────────────────────────────────────┐
        │  Api  (HTTP endpoints, middleware, workers)   │  composition root
        │   depends on ▼                                │
        │  Infrastructure  (EF Core, Identity, gateway) │  implements the abstractions
        │   depends on ▼                                │
        │  Application  (use-case handlers, ABSTRACTIONS)│  orchestrates the domain
        │   depends on ▼                                │
        │  Domain  (entities, value objects, events)    │  pure business rules, no deps
        └─────────────────────────────────────────────┘
```

| Project | Responsibility | May depend on |
| --- | --- | --- |
| `TransportPlatform.Domain` | Entities, value objects, domain events, invariants | **nothing** |
| `TransportPlatform.Application` | One handler per use-case; defines abstractions (`IPaymentGateway`, `IApplicationDbContext`, `IClock`, `ICurrentUser`, …) | Domain |
| `TransportPlatform.Infrastructure` | EF Core/Postgres, Identity/JWT, the payment gateway — **implements** Application's abstractions | Application, Domain |
| `TransportPlatform.Api` | Minimal-API endpoints, middleware, background workers; **composition root** (wires DI) | Infrastructure, Application, Domain |

The frontend (`web/customer`) mirrors this: `core/` (cross-cutting: auth, api, interceptors)
is depended on by `features/` (pages); features never import each other.

## Conventions (how to extend safely)

- **One use-case = one handler file** under `Application/<Area>/`. Add a feature by adding a new
  handler + endpoint; don't grow an existing handler to do two jobs.
- **Depend on abstractions.** A handler takes interfaces in its constructor. To swap a provider
  (e.g. a real payment gateway for `SandboxPaymentGateway`), add a new `IPaymentGateway`
  implementation and change one DI line — no handler changes. That's the Open/Closed Principle
  in practice.
- **Inner layers never reference outer ones.** Domain references nothing; Application references
  only Domain. If you need persistence/HTTP/etc. in a handler, depend on an Application
  abstraction and implement it in Infrastructure.
- **The Api is the only composition root** — it's the one place allowed to wire concrete
  Infrastructure types into the DI container.

## This is enforced, not just documented

`tests/TransportPlatform.UnitTests/Architecture/LayeringTests.cs` fails the build if an inner
layer ever takes a real dependency on an outer one (Domain → anything, or Application →
Infrastructure/Api). Combined with solution-wide `TreatWarningsAsErrors` (see
`Directory.Build.props`), an architecture-violating change cannot merge green.

## Strategic / domain docs

Higher-level context lives alongside the repo:

- [../ARCHITECTURE.md](../ARCHITECTURE.md) — module map, bounded contexts, migration plan
- [../SYSTEM_MAP.md](../SYSTEM_MAP.md) — actors, entities, workflows
- [../STACK_RECOMMENDATION.md](../STACK_RECOMMENDATION.md) — why this stack (security rationale)
