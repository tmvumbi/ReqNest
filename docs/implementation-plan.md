# ReqNest implementation and verification matrix

This document turns the product requirements into an executable delivery checklist. A phase is complete only when every row has implementation evidence, automated tests, and (for user-facing behavior) a browser check in English, French, light, and dark modes where applicable.

Status legend: `done`, `in progress`, `planned`.

## Phase 1 — Core help desk

| Capability | Backend evidence | Frontend evidence | Verification | Status |
| --- | --- | --- | --- | --- |
| Tenant bootstrap and switching | Tenant, membership, tenant header boundary, default workflow | Tenant selector and active-tenant shell | Multi-tenant/scope integration journey and identifier substitution tests | done |
| Email/password account security | Registration, normalized email, sign-in/out, lockout, fixed-window rate limit, reset, revocable expiring sessions | Sign-in, registration, invitation acceptance, forgot/reset forms | Hash, generic-error, single-use reset, revocation and 429 tests | done |
| Users, roles and scopes | Invitations, membership lifecycle, four central roles, all/selected-project grants, last-admin safeguard | User/role administration and access explanation | Current/future project scope, multi-tenant membership, last-admin and observer denial tests | done |
| Company branding and preferences | Tenant settings and private logo metadata | Settings, branded shell, locale/theme preference | Image content/dimension validation, branded PDF and persisted browser preference checks | done |
| Project lifecycle | Create, update, archive/restore, overview aggregates | Project list, overview and settings | Endpoint authorization and browser administration journey | done |
| Workflows | Reusable and project-copy workflows, safety validation, status mapping, transition enforcement | Full status/transition editor and project-copy action | Isolation, used-status mapping and invalid-transition integration tests | done |
| Tickets and queues | Paginated CRUD, sequential key, concurrency, transitions, archive, bulk operations, filters and saved views | Queue/table, create/edit/detail, transitions and bulk actions | PostgreSQL concurrency, authorization, idempotency and browser queue tests | done |
| Safe rich content | Server sanitization | Accessible description/comment editors and Angular safe renderer | Unsafe-markup integration regression and accessible editor labeling | done |
| Attachments | Private Blob abstraction, signature/extension/size validation, quarantine/scanning boundary, authorized streaming | Upload, state, download and delete controls | Azurite lifecycle, quarantine, tenant isolation and observer denial tests | done |
| Collaboration and activity | Comments, edit/hide/delete endpoints, mentions, watchers and business timeline | Localized activity stream and watcher/mention controls | Observer browser comment journey plus attribution/visibility integration tests | done |
| In-app notifications | Durable notification events, preferences, mute and read state | Notification center, unread badge, localized filters and deep links | Comment/report recipient generation and permission enforcement tests | done |
| Search and personal views | Tenant/project-safe filters, built-in queues, saved personal views | Search and restorable filter URLs | Scoped project result test and saved-view browser journey | done |
| Dashboard and essential reports | Dashboard aggregates, ten report types, authorized branded PDF export | Personal dashboard, localized report filters/tables/export state | Metric/PDF/logo/locale/access integration and browser export tests | done |
| Audit administration | Append-only audit events, filtering and JSON export | Localized audit log and export | Server authorization and safe audit-summary review | done |
| Localization and appearance | Persisted user/tenant preferences and localized notification/report payloads | Complete English/French catalog; light/dark/system startup behavior | Catalog unit test, localized date/value checks and browser persistence checks | done |
| Operational baseline | Migrations, readiness, correlation-safe errors, bounded queries, Docker local dependencies | Responsive and keyboard-accessible shell with role route guards | Full build/test/lint/format/dependency, Docker smoke, browser denial and zero-console checks | done |

### Phase 1 completion evidence — 2026-07-10

- Backend restore/build completed with zero warnings. The xUnit v3 suite ran **10/10 passing** against disposable PostgreSQL 18.4 and Azurite 3.35.0. The journeys in `AuthenticationEndpointsTests.cs` and `PhaseOneCoreEndpointsTests.cs` cover password hashing and generic errors, authentication throttling, revocable/reset sessions, tenant isolation, all/selected project scopes, last-administrator protection, workflow isolation/mapping, optimistic ticket concurrency, observer restrictions, sanitized content, private/quarantined attachments, branded localized PDF output, and cross-tenant blob/report denial.
- `dotnet format --verify-no-changes` passed. EF Core reported no pending model changes after applying the two committed migrations. NuGet reported no vulnerable or deprecated direct/transitive packages.
- A clean frontend `npm ci` completed using Node 24.18/npm 11.16. Angular tests ran **6/6 passing**, including automated serious/critical axe checks on the public and authentication surfaces. Angular ESLint, Prettier, and the production build passed; the initial production bundle is 486.87 kB, below the configured 500 kB warning budget.
- Browser journeys covered tenant registration/sign-in, English and French labels, light/dark preference persistence, project/ticket/workflow/user administration, saved views, ticket editing and transition, notification preferences, localized report data and PDF export, custom branding, and clean navigation across primary routes.
- A dedicated observer browser journey accepted an invitation, signed in, confirmed create/edit/transition/admin/upload controls and routes were denied, and successfully added a comment. The skip link was regression-checked to focus `main-content` without route navigation. Fresh administrator and observer tabs reported no browser console warnings or errors.
- Local smoke used the documented Docker dependencies with PostgreSQL mapped to 5433 and Azurite mapped to 10010–10012 because the default host ports were already occupied. `/api/status`, `/health`, `/health/ready`, the Angular application, private Blob upload/download, and report export all responded successfully.
- Accepted dependency residuals: npm reports one low-severity Quill 2.0.3 HTML-export advisory in production dependencies and three additional low-severity build/dev advisories. The suggested Quill remediation is a forced downgrade; ReqNest does not use Quill HTML export, sanitizes rich content server-side, and renders through Angular sanitization. No moderate, high, or critical npm advisory remains. Reassess when compatible upstream releases are available.

## Phase 2 — Operational maturity

| Capability | Required evidence | Status |
| --- | --- | --- |
| SLA calendars | Tenant/project policies, business hours, holidays, pause rules, ticket snapshots, warning/breach jobs and reports | done |
| Email and digests | Outbox delivery boundary, localized templates, retry/dedup, per-user preferences and digest scheduling | done |
| Configurable ticket schema | Localized types, priorities and typed custom fields with validation and reporting | done |
| Published views and enhanced bulk | Project views, impact preview, per-ticket outcome reporting and permission enforcement | done |
| Relationships and hierarchy | Symmetric relationships plus parent/child rules and authorized navigation | done |
| Report scheduling and CSV | Schedules, immutable filter snapshots, private artifacts, CSV export and notifications | done |
| Custom roles | Tenant-defined permissions layered onto the central authorization evaluator | done |
| Attachment previews and quotas | Safe image/PDF previews, tenant usage accounting, limits and operational cleanup | done |
| Retention and audit export | Configurable retention, legal-safe purge jobs and complete audit metadata export | done |

### Phase 2 completion evidence — 2026-07-10

- The `OperationalMaturity` and `BackfillOperationalDefaults` migrations apply cleanly to both fresh and existing tenants. Existing tickets receive schema keys, existing tenants receive safe quota/retention defaults, and localized default schema/SLA definitions are backfilled without replacing tenant customization. EF Core reports no pending model changes.
- The backend suite runs **12/12 passing**. `PhaseTwoOperationalMaturityTests.cs` verifies tenant/project ticket schema overrides, typed required fields, business-calendar SLA snapshots, holidays and pause metadata, quota rejection, authorized image preview headers, relationship symmetry and hierarchy-cycle denial, bulk impact preview/partial outcomes, localized CSV, durable schedule execution by the background worker, report notifications, retention settings, complete audit CSV, published project views, custom-role project scopes, and localized email-outbox creation.
- SLA warning/breach processing excludes paused tickets, records state transitions in the audit log, and deduplicates watcher/reporter/assignee notifications. Email delivery uses a durable retrying outbox; digest generation respects each tenant time zone and user-selected local delivery hour. Scheduled reports execute automatically, preserve immutable filters, advance their recurrence, and notify owners on success or failure.
- The PrimeNG frontend exposes a bilingual operations console for ticket types, priorities, custom fields, SLAs, custom roles, quota/retention, and email delivery status. Ticket creation consumes localized schema definitions and validates project fields; details show SLA snapshots, custom values, hierarchy/relationships, and authorized image/PDF previews. Queues support published project views and preflighted bulk changes; reports support CSV and recurring schedules; notification preferences include email, digest, and local hour.
- A clean frontend install completed. Angular unit/component tests run **6/6 passing**; Angular ESLint, Prettier, and the production build pass. The initial production bundle is **493.18 kB**, below the configured 500 kB warning budget. Backend restore/build completes with zero warnings, `dotnet format --verify-no-changes` passes, and NuGet reports no vulnerable or deprecated packages.
- Browser journeys created company/project/schema/ticket data, confirmed a required custom field and SLA resolution target, linked two tickets, published a project queue view, observed bulk impact confirmation before mutation, scheduled a report, enabled digest/email delivery, and verified localized select announcements. English and French plus explicit light and dark modes were checked; the final browser console contained no warnings or errors.
- Accepted dependency residuals remain low severity: Quill 2.0.3's unused HTML-export path, an Angular-build/Babel source-map development advisory, and an esbuild development-server advisory affecting Windows. The application sanitizes rich content server-side, does not use Quill HTML export, and the recommended remaining npm remediations require breaking framework/editor changes. No moderate, high, or critical npm advisory is present.

## Phase 3 — Integrations and external service

| Capability | Required evidence | Status |
| --- | --- | --- |
| Requester portal | Tenant-branded external submission/status/comment experience with strict visibility | planned |
| Email-to-ticket | Verified routing, threading, reply synchronization, attachment validation and loop prevention | planned |
| Webhooks and API tokens | Scoped tokens, rotation/revocation, signed retryable webhooks and delivery logs | planned |
| SSO and MFA | Tenant OIDC configuration, account linking, recovery and step-up controls | planned |
| Knowledge base | Localized articles, permissions, search, ticket linking and requester presentation | planned |
| Third-party integrations | Secure connector boundary, tenant-scoped credentials and observable retry behavior | planned |
| Optional AI assistance | Explicit opt-in, data-minimization, evaluations, human review and non-training guarantees | planned |

## Phase completion gates

For each phase:

1. Regenerate and apply EF Core migrations against disposable PostgreSQL.
2. Run backend restore, build, tests, formatting verification, and dependency/security checks.
3. Run frontend clean install, unit/component tests, production build, lint and end-to-end tests.
4. Start PostgreSQL, Azurite, API and frontend through the documented local-development path.
5. Exercise the phase's primary journeys with the in-app Browser, including denial paths and a browser-console check.
6. Review tenant isolation, bounded queries, accessibility, English/French content, and all appearance modes.
7. Update this matrix with exact test/file evidence, then commit and push the completed phase.
