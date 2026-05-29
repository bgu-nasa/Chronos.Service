# Chronos.Admin

Internal command-line tool for **platform operators** who need a cross-tenant view of the Chronos deployment. It is separate from customer auth in `Chronos.MainApi`: identities live in a dedicated SQLite credential store, and tenant data is read from the shared PostgreSQL database via `Chronos.Data`.

## What it does

Operators use Chronos.Admin to:

- **Authenticate** as a platform admin (bootstrap account from environment, plus additional accounts managed in SQLite).
- **List organizations** across the entire system with administrator email(s) and user counts per tenant.
- **Inspect a single organization** when investigating support or capacity questions.

Customer JWTs, org headers, and org-scoped user records are not used for admin access. That separation keeps platform credentials and tenant data on different trust boundaries.

## Planned commands

| Command | Description |
|---------|-------------|
| `login` | Sign in and persist a session for later commands |
| `logout` | Remove the saved session on this machine |
| `accounts add` / `accounts list` | Manage platform admin accounts |
| `orgs list` | All organizations (id, name, admin emails, user count) |
| `orgs show <id>` | Detail for one organization |

Table output is the default; `--json` is supported for scripting. See [docs/Chronos.Admin.md](docs/Chronos.Admin.md) for exit codes, configuration, and security expectations.

## Quick start

```bash
# From the Chronos.Service repository root
export AdminConfiguration__DefaultEmail=admin@internal.example
export AdminConfiguration__DefaultPassword='<strong-password>'
export AdminConfiguration__SecretKey='<signing-key-at-least-32-chars>'

dotnet run --project src/Chronos.Admin -- login --email admin@internal.example --password '<password>'
dotnet run --project src/Chronos.Admin -- accounts list
# Session is stored at %USERPROFILE%\.chronos-admin\session — later commands reuse it until logout or expiry.
dotnet run --project src/Chronos.Admin -- logout
dotnet run --project src/Chronos.Admin -- accounts add ops@internal.example --first-name Ops --last-name User --password OpsPass12
dotnet run --project src/Chronos.Admin -- --help
```

## Project layout

| Path | Purpose |
|------|---------|
| `docs/` | Architecture, CLI design, and configuration reference |
| `Auth/` | Platform admin login and account management |
| `Organizations/` | Cross-tenant organization queries |
| `Configuration/` | `AdminConfiguration` (cred store path, bootstrap admin, token settings) |
| `AppSettings/` | JSON configuration (overridden by environment variables) |

## Configuration

Settings live in `AppSettings/appsettings.json`. In deployment, prefer environment variables:

- `ConnectionStrings__DefaultConnection` — PostgreSQL (tenant data)
- `AdminConfiguration__DefaultEmail` / `DefaultPassword` — first-run bootstrap admin
- `AdminConfiguration__DefaultFirstName` / `DefaultLastName` — bootstrap profile names
- `AdminConfiguration__CredStorePath` — SQLite file for admin accounts (default `./data/admin-creds.db`)
- `AdminConfiguration__SecretKey` — signing key for admin sessions (min 32 characters)
- `AdminConfiguration__Issuer` / `Audience` — JWT issuer and audience (defaults `ChronosAdmin` / `ChronosAdminCli`)

Full reference: [docs/Chronos.Admin.md](docs/Chronos.Admin.md#configuration-reference).
