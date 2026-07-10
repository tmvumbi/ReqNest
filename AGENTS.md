# ReqNest agent guide

## Purpose

This file is the working agreement for coding agents contributing to ReqNest. Follow it for repository-wide changes unless a more specific `AGENTS.md` exists lower in the directory tree.

ReqNest is currently at the repository-bootstrap stage. Keep early choices simple, production-oriented, and easy to revise. Do not invent product requirements, domain rules, authentication flows, or deployment topology when they have not been specified.

## Required stack

- Frontend: the latest stable Angular release, built with the Angular CLI in strict mode.
- UI components: PrimeNG. Prefer PrimeNG components and design tokens over custom replacements.
- Backend: ASP.NET Core on the latest supported .NET LTS release.
- Relational data: PostgreSQL through EF Core and `Npgsql.EntityFrameworkCore.PostgreSQL`.
- Object storage: Azure Blob Storage through `Azure.Storage.Blobs`.
- Local storage emulation: Azurite where practical.
- API style: ASP.NET Core Minimal APIs with OpenAPI, organized by feature.

Do not substitute another framework, component library, database, ORM, or object store without an explicit architectural decision from the user.

## Version policy

“Latest stable” means a generally available release without `alpha`, `beta`, `next`, `preview`, or `rc` in the version. Before scaffolding or performing a major upgrade:

1. Check the official release page or package registry.
2. Verify peer-dependency compatibility across Angular, Angular CDK, PrimeNG, RxJS, and the selected Node.js version.
3. Install exact compatible majors through the package manager and commit the lockfile.
4. Once manifests and lockfiles exist, treat them as the repository source of truth. Do not opportunistically upgrade unrelated dependencies.

Baseline verified on 2026-07-10:

- Angular and Angular CLI: `22.0.6` (stable).
- PrimeNG: `21.1.9` (stable, requiring Angular 21); PrimeNG `22.0.0-rc.2` is pre-release.
- Node.js: `24.18.0` LTS for the Angular toolchain.
- .NET: `10.0.9` LTS.
- PostgreSQL: `18.4`.

There is therefore no all-stable Angular 22 + PrimeNG 22 pairing at this baseline. Do not silently downgrade Angular or install the PrimeNG release candidate. If scaffolding the frontend before stable PrimeNG 22 is available, stop and ask the user to choose between:

- waiting for stable PrimeNG 22,
- temporarily using the PrimeNG 22 release candidate, or
- temporarily using Angular 21 with stable PrimeNG 21.

Re-check this constraint at implementation time; remove the exception once compatible stable majors are available.

Current scaffold exception: the user requires Angular 22 and PrimeNG together, so the frontend temporarily uses PrimeNG `22.0.0-rc.2` and matching pre-release theme packages. Do not expand this exception to unrelated dependencies. Upgrade to stable PrimeNG 22 as soon as it is available and compatible.

## Intended repository layout

Use this layout unless the repository has already established another convention:

```text
frontend/                         Angular application
backend/
  ReqNest.sln                     .NET solution
  src/ReqNest.Api/                ASP.NET Core API
  src/ReqNest.Core/               domain and application contracts
  src/ReqNest.Infrastructure/     EF Core and external integrations
  tests/ReqNest.Tests/            unit and integration tests
docker-compose.yml                local infrastructure only
docs/                             architecture decisions and durable documentation
```

The backend is a modular monolith with one deployable API and Clean Architecture project boundaries. `Core` must not reference `Api` or `Infrastructure`; `Infrastructure` may reference `Core`; `Api` composes both. Organize code inside each project by product feature. Add more projects only when a real deployable or architectural boundary justifies them.

## Frontend conventions

- Use standalone components and functional providers. Do not introduce new NgModules unless a dependency requires one.
- Enable Angular strict template checking and strict TypeScript settings. Do not weaken compiler options to work around errors.
- Use signals for local synchronous UI state and derived state. Use RxJS for asynchronous streams, cancellation, and multi-event composition. Avoid mirroring the same state in both systems.
- Prefer signal-based `input`, `output`, and queries for new code, and use `inject()` for dependency injection.
- Use typed reactive forms for non-trivial forms. Keep validation rules explicit and show accessible error text.
- Use built-in template control flow (`@if`, `@for`, `@switch`) and provide a stable `track` expression for repeated data.
- Keep components focused on presentation and user interaction. Put API access and reusable business behavior outside components.
- Lazy-load feature routes. Do not add global state management until cross-feature state makes it necessary.
- Follow the official Angular style guide: hyphenated file names, colocated tests, one primary concept per file, and feature-oriented folders.

### PrimeNG rules

- PrimeNG is the default component library. Before creating a custom control, confirm that PrimeNG or Angular CDK does not already provide the behavior.
- Import only the PrimeNG components used by a standalone component; avoid broad shared UI modules.
- Configure PrimeNG once in `app.config.ts` with `providePrimeNG`. Start with the Aura preset unless a later design decision replaces it.
- Express product styling through PrimeNG design tokens, semantic CSS, and small component-scoped styles. Avoid brittle selectors into PrimeNG internals, `::ng-deep`, and widespread `!important` rules.
- Do not add Tailwind, another component library, or another icon set without an explicit decision.
- PrimeNG 22 requires a PrimeUI license. Supply it through `PRIMEUI_LICENSE_KEY` or ignored `frontend/.env.local`; never commit the key or the generated license module.
- Preserve keyboard behavior, focus treatment, labels, descriptions, and error associations. Target WCAG 2.2 AA and verify component-specific accessibility guidance.
- Never guess a PrimeNG selector, input, output, template slot, or import path.

For every PrimeNG component addition or material change, use this documentation workflow:

1. Read `https://primeng.dev/llms/llms.txt` as the documentation index.
2. Open the relevant component page; append `.md` to its URL for the Markdown form when useful, for example `https://primeng.dev/button.md`.
3. Confirm the documentation major matches the installed PrimeNG major. The live site may document a release candidate.
4. When documentation and the installed package differ, the installed TypeScript declarations and the documentation for that package tag are authoritative.
5. Check the component’s accessibility section before finishing the UI.

## Backend conventions

- Target the .NET version pinned by `global.json` and the project target framework. Enable nullable reference types and implicit usings.
- Use Minimal APIs grouped into feature-specific endpoint modules. Keep `Program.cs` limited to composition, middleware, and endpoint registration.
- Use dependency injection and options binding. Validate configuration at startup.
- Use asynchronous APIs end to end and pass `CancellationToken` through I/O paths.
- Return explicit HTTP results and consistent RFC Problem Details responses. Do not expose exception details, connection information, or internal entity shapes.
- Keep API request/response contracts separate from EF Core entities. Validate input at the boundary.
- Generate OpenAPI for the public API. Keep frontend contracts synchronized with it; prefer a generated typed client once generation is configured.
- Use structured logging with named properties. Never log secrets, authorization headers, raw file contents, or sensitive personal data.
- Add health checks for dependencies required by the running service.

Favor direct, feature-local code over speculative abstractions. Introduce interfaces at real boundaries such as blob storage, time, external services, or where tests need a controlled substitute—not for every class.

## PostgreSQL and EF Core

- Use the Npgsql EF Core provider whose major matches EF Core/.NET.
- Keep migrations in source control and give them meaningful names. Apply schema changes through migrations; do not use `EnsureCreated` outside disposable tests.
- Use UTC instants consistently. Prefer `DateTimeOffset` at API boundaries and document any domain concepts that are dates without times.
- Configure constraints, indexes, maximum lengths, delete behavior, and concurrency intentionally rather than relying on accidental conventions.
- Avoid lazy loading and unbounded queries. Project read models, paginate collections, and use `AsNoTracking` for read-only queries.
- Use transactions around a complete database consistency boundary. Do not attempt a distributed transaction between PostgreSQL and Blob Storage; design compensating cleanup for partial failures.
- Test PostgreSQL-specific behavior against PostgreSQL, preferably with an ephemeral container. Do not use EF Core’s in-memory provider as proof that relational behavior works.

## Azure Blob Storage

- Access blobs through a small application-owned abstraction so endpoints do not depend directly on SDK types.
- In Azure, authenticate with managed identity via `DefaultAzureCredential`. Do not store account keys or connection strings in source control.
- For local development, use Azurite or developer credentials. Keep local-only settings out of committed production configuration.
- Use private containers by default. If direct client access is required, issue narrowly scoped, short-lived SAS tokens from an authorized backend flow.
- Generate server-controlled blob names. Treat the original file name as untrusted display metadata, not as a storage path.
- Validate allowed content types, file extensions where relevant, and maximum sizes on the server. Do not trust the browser-provided content type alone.
- Store only blob identifiers and required metadata in PostgreSQL; do not store large file payloads in the relational database.
- Stream uploads and downloads rather than buffering whole files in memory. Propagate cancellation and set correct content type and download headers.
- Make database/blob workflows retry-safe and idempotent. Clean up orphaned blobs when a later database operation fails.

## Configuration and secrets

- Use `appsettings.json` only for safe defaults. Use environment variables, .NET user secrets, or Azure-managed configuration for secrets.
- Use ASP.NET Core hierarchical configuration names such as `ConnectionStrings__ReqNest`, `Storage__ServiceUri`, and `Storage__ContainerName` in environment variables.
- Commit `.env.example` or equivalent documentation containing names and safe sample values only—never working credentials.
- Keep development CORS origins explicit. Do not use permissive CORS with credentials.
- Pin local PostgreSQL and Azurite container images rather than using floating `latest` tags.

## Testing and quality gates

Tests should cover behavior, not implementation details.

- Frontend: colocate `*.spec.ts` tests with source. Cover components, validation, accessibility-critical behavior, and service error handling. Add end-to-end tests for core user journeys once those journeys are defined.
- Backend: unit-test domain behavior and integration-test endpoints, persistence, migrations, authorization, and blob workflows. Use real PostgreSQL/Azurite-compatible infrastructure for integration boundaries where practical.
- Every bug fix should include a regression test when reasonably possible.
- Do not delete, skip, or weaken a test merely to make a change pass.

After the relevant projects exist, run the smallest applicable checks during development and the full affected suite before handoff:

```bash
# Frontend
cd frontend
npm ci
npm test -- --watch=false
npm run build

# Backend
cd backend
dotnet restore
dotnet build --no-restore
dotnet test --no-build
dotnet format --verify-no-changes
```

Use the scripts and solution paths actually present in the repository if they differ. Report any check that could not be run and why.

## Agent workflow

1. Read this file, any nested `AGENTS.md`, the README, relevant manifests, and nearby tests before editing.
2. Inspect `git status` and preserve user changes. Never discard unrelated work.
3. For version-sensitive behavior, consult current official documentation and the installed package version; do not rely on memory.
4. Keep changes scoped to the request. Avoid drive-by refactors and new dependencies without a concrete need.
5. Do not hand-edit generated files, lockfiles, or EF migrations. Regenerate them using the owning tool.
6. Update tests and durable documentation when behavior, configuration, API contracts, or architectural decisions change.
7. Run applicable formatters, builds, and tests, then summarize changes, validation, and remaining risks.

## Official references

- Angular releases: https://angular.dev/reference/releases
- Angular style guide: https://angular.dev/style-guide
- PrimeNG: https://primeng.dev/
- PrimeNG installation: https://primeng.dev/installation
- PrimeNG LLM index: https://primeng.dev/llms/llms.txt
- PrimeNG LLM guidance: https://primeng.dev/llms
- .NET support policy: https://dotnet.microsoft.com/platform/support/policy/dotnet-core
- ASP.NET Core API guidance: https://learn.microsoft.com/aspnet/core/fundamentals/apis
- Npgsql EF Core provider: https://www.npgsql.org/efcore/
- PostgreSQL versioning: https://www.postgresql.org/support/versioning/
- Azure Blob Storage for .NET: https://learn.microsoft.com/azure/storage/blobs/storage-quickstart-blobs-dotnet
- Azure SDK authentication: https://learn.microsoft.com/dotnet/azure/sdk/authentication/
- Azurite: https://learn.microsoft.com/azure/storage/common/storage-use-azurite
