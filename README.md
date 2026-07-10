# ReqNest

ReqNest is a multi-tenant help desk for managing projects, configurable ticket workflows, secure documents, collaboration, notifications, and operational reports. The interface is available in English and French with light, dark, and system appearance modes.

## Architecture

- `frontend/` — Angular 22 standalone application using PrimeNG/PrimeUI, strict TypeScript, signals, lazy feature routes, and Angular ESLint.
- `backend/src/ReqNest.Api/` — ASP.NET Core Minimal API composition and feature endpoints.
- `backend/src/ReqNest.Core/` — domain entities and application contracts.
- `backend/src/ReqNest.Infrastructure/` — EF Core/PostgreSQL, authentication, private Blob Storage, notifications, and PDF generation.
- `backend/tests/ReqNest.Tests/` — xUnit v3 integration tests against disposable PostgreSQL and Azurite containers.
- `docs/` — the PRD and phase-by-phase implementation evidence.
- `docker-compose.yml` — pinned PostgreSQL and Azurite services for local development.

The backend is a modular monolith. Every tenant-owned query is filtered by the active tenant and authorization is enforced again at each endpoint. Blob names are server-generated and partitioned by tenant/project/ticket.

## Prerequisites

- Node.js `24.18.x` and npm `11.16.x`
- .NET SDK `10.0.9` (the repository `global.json` rolls forward within the pinned feature band)
- Docker Desktop or a compatible Docker engine
- A PrimeUI license key for the frontend

## Local development

Start the local dependencies from the repository root:

```bash
docker compose up -d
docker compose ps
```

The default development endpoints are PostgreSQL on `localhost:5432` and Azurite on `10000`–`10002`. The committed database password and emulator account are local-development defaults only.

Run the API:

```bash
cd backend
dotnet tool restore
dotnet run --project src/ReqNest.Api
```

The API starts on `http://localhost:5055` with status at `/api/status`, general health at `/health`, and dependency readiness at `/health/ready`. Development startup applies committed EF Core migrations.

Configure and run the frontend in another terminal:

```bash
cd frontend
cp .env.local.example .env.local
# Set PRIMEUI_LICENSE_KEY in .env.local.
npm ci
npm start
```

The application starts on `http://localhost:4200`; the Angular development proxy sends `/api` and `/health` requests to the API. `frontend/.env.local` and the generated PrimeUI license module are ignored and must never be committed.

### Alternate local ports

When the default dependency ports are occupied, start Docker with explicit host-port overrides:

```bash
POSTGRES_PORT=5433 \
AZURITE_BLOB_PORT=10010 \
AZURITE_QUEUE_PORT=10011 \
AZURITE_TABLE_PORT=10012 \
docker compose up -d
```

Then override `ConnectionStrings__ReqNest` and `Storage__ConnectionString` for the API. For nonstandard Azurite ports, use the full standard `devstoreaccount1` connection string with explicit Blob, Queue, and Table endpoints rather than `UseDevelopmentStorage=true`.

## Quality gates

Backend:

```bash
cd backend
dotnet restore ReqNest.sln
dotnet build ReqNest.sln --no-restore
dotnet test ReqNest.sln --no-build
dotnet format ReqNest.sln --verify-no-changes
dotnet ef migrations has-pending-model-changes \
  --project src/ReqNest.Infrastructure \
  --startup-project src/ReqNest.Api
dotnet list ReqNest.sln package --vulnerable --include-transitive
dotnet list ReqNest.sln package --deprecated
```

Frontend:

```bash
cd frontend
npm ci
npm test -- --watch=false
npm run lint
npm run build
npx prettier --check "src/**/*.{ts,html,scss}" "*.{json,js}"
npm audit --omit=dev --audit-level=moderate
```

Integration tests use Testcontainers and require Docker. See [the implementation matrix](docs/implementation-plan.md) for browser journeys and current dependency-risk notes.

## Product documentation

- [Product requirements document](docs/product-requirements-document.md)
- [Implementation and verification matrix](docs/implementation-plan.md)
- [Agent guide](AGENTS.md)
