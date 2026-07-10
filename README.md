# ReqNest

ReqNest is an Angular and .NET application for organizing requirements. The repository currently contains the production-oriented application scaffold and a placeholder landing page.

## Stack

- Angular 22 with PrimeNG 22 RC (temporary compatibility exception)
- ASP.NET Core on .NET 10
- EF Core with PostgreSQL 18
- Azure Blob Storage, with Azurite for local development

## Local development

Start PostgreSQL and Azurite from the repository root:

```bash
docker compose up -d
```

Run the API:

```bash
cd backend
dotnet run --project src/ReqNest.Api
```

Run the frontend in another terminal:

```bash
cd frontend
nvm use
npm install
npm start
```

The frontend is available at `http://localhost:4200`, the API at `http://localhost:5055`, and API status at `http://localhost:5055/api/status`.

Copy `.env.example` to `.env` only when local infrastructure defaults need to be overridden. The committed defaults are development-only and must never be reused as production credentials.

### PrimeUI license

PrimeNG 22 requires a PrimeUI license. For local development, copy `frontend/.env.local.example` to `frontend/.env.local` and set `PRIMEUI_LICENSE_KEY`. The local file and generated TypeScript module are ignored by Git. CI and production builds should supply the same variable through their secret-management system.
