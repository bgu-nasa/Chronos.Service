# Chronos Acceptance Tests

This project contains full-stack acceptance tests for the Chronos API. Tests run
through the real ASP.NET Core HTTP pipeline by using `ChronosApiFactory`, with
external infrastructure replaced by deterministic test doubles:

- PostgreSQL is replaced with EF Core InMemory.
- RabbitMQ publishing is replaced with an `IMessagePublisher` substitute.
- LLM calls are replaced with a deterministic Agent adapter.
- Auth tokens are generated with the same issuer/audience/signing-key shape used
  by the application.

Acceptance tests should prove user-visible behavior and business workflows. They
should not become an endpoint inventory.

## Structure

```text
tests/Chronos.Tests.Acceptance/
├── Infrastructure/        # Test host, HTTP helpers, token generator
├── Support/               # AcceptanceContext, Seeder, shared constants
└── Flows/                 # Acceptance flows grouped by product capability
    ├── Agent/
    ├── Appeals/
    ├── Authentication/
    ├── Authorization/
    ├── Constraints/
    ├── Health/
    ├── OrganizationManagement/
    ├── Scheduling/
    └── UserRoleAdmin/
```

`Infrastructure/` should stay plumbing-only. Do not place `[Test]` classes there.

`Flows/<Capability>/` is the home for acceptance tests. Add a new folder when a
new product capability needs a user-facing flow.

## Support Helpers

Use `AcceptanceContext` when a test needs a ready organization and authenticated
administrator:

```csharp
using var ctx = await AcceptanceContext.CreateAsync("My Acceptance Org");
var department = await ctx.Seed.CreateDepartmentAsync("Engineering");
```

`AcceptanceContext` handles:

- booting a fresh API host,
- registering a new organization and admin user,
- attaching the bearer token,
- setting the `x-org-id` header,
- exposing `ctx.Seed` for API-driven setup.

Use `ctx.CreateClientAs(role)` for role-specific authorization scenarios, but do
not add negative RBAC assertions until the role hierarchy bug is fixed.

Use `Seeder` for setup data that is not the behavior under test. Keep seeders
thin and API-driven. Feature-specific seed methods can live in partial files such
as `Seeder.Scheduling.cs` when they are reused by more than one flow.

## When Not To Use AcceptanceContext

Do not use `AcceptanceContext` in tests that are directly proving registration,
login, refresh, or token validation. `AcceptanceContext` depends on registration
working, so auth primitive tests should use `ChronosApiFactory` directly.

This keeps auth tests honest and avoids circular setup.

## Test Quality Bar

Prefer one meaningful workflow over many tiny endpoint checks.

Good acceptance tests usually answer one of these questions:

- Can a user complete a real workflow?
- Does the system expose the resulting state in the places users rely on?
- Is an important invalid action rejected?
- Does a cross-cutting concern, such as tenant isolation, hold end-to-end?

Avoid tests that only say "POST works", "PATCH works", and "DELETE works" unless
that CRUD sequence is itself the user requirement. Low-level validation branches
belong in unit tests.

Keep assertions on durable behavior: status codes, persisted fields, published
messages, and returned DTOs. Avoid asserting human-readable error strings unless
the API has no structured error contract yet.

## Current Scope Decisions

- Agent acceptance uses a deterministic LLM adapter from `ChronosApiFactory`.
  Do not call real LLM providers from acceptance tests.
- Negative authorization/RBAC acceptance tests are deferred until the known role
  hierarchy bug is fixed. In particular, the `Operator` role currently grants too
  much access, so tests for lower-role `403` behavior would be misleading or red.
- The remaining acceptance suite should focus on product flows, not exhaustive
  CRUD coverage. Exhaustive edge cases belong in the relevant unit-test projects.
