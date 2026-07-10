# ReqNest Help Desk — Product Requirements Document

| Field | Value |
| --- | --- |
| Product | ReqNest Help Desk |
| Document status | Draft for product review |
| Version | 0.1 |
| Date | 2026-07-10 |
| Primary languages | English and French |
| Initial delivery model | Multi-tenant web application |

## 1. Executive summary

ReqNest Help Desk is a multi-tenant help desk and work-tracking system for organizations that need to capture, organize, assign, discuss, and report on tickets across one or many projects. Each company operates in an isolated tenant with its own users, branding, projects, permissions, workflows, and reports.

The product will support English and French as first-class interface languages, light and dark appearance modes, project-scoped access, configurable ticket workflows, secure email/password authentication, rich ticket descriptions, file attachments, internal notifications, audit history, and operational reports with branded PDF export.

The initial product should feel focused and approachable for small teams while retaining the controls needed by larger organizations. Configuration should be powerful without making the default setup difficult: a new company should be able to invite users, create a project, and begin working with the default `TODO → IN PROGRESS → DONE` workflow in minutes.

## 2. Product vision

Give every organization a clear, secure, and adaptable place to manage requests and work, without forcing every team into the same process.

ReqNest should answer five questions quickly:

1. What needs attention?
2. Who owns it?
3. What is its current state?
4. What changed, and why?
5. How well is the team responding and delivering?

## 3. Problem statement

Teams commonly manage support requests through email, chat, spreadsheets, and disconnected project tools. This creates several problems:

- requests are lost or duplicated;
- ownership and priority are unclear;
- teams cannot adapt the workflow to different projects;
- users see information outside their responsibilities;
- decisions and attachments are scattered across tools;
- stakeholders do not have a reliable read-only view;
- management reports require manual spreadsheet work;
- products that support these needs can be too complex for smaller teams.

ReqNest will provide a single source of truth with clear permissions, a useful default workflow, project-level flexibility, and actionable reporting.

## 4. Goals

### 4.1 Product goals

- Allow a company to create and manage one or many projects.
- Make every ticket belong to exactly one project.
- Provide a simple default workflow and customizable tenant/project workflows.
- Support secure collaboration among project managers, contributors, and observers.
- Restrict users to all projects or explicitly selected projects.
- Provide useful in-app notifications without overwhelming users.
- Support English and French throughout the system interface.
- Support light, dark, and system-controlled appearance modes.
- Store ticket documents securely and make attachment activity auditable.
- Provide operational dashboards and exportable PDF reports.
- Allow each company to configure its displayed name, logo, and basic visual identity.
- Enforce strict data isolation between companies.
- Keep the default experience easy enough for a new tenant to use without consulting documentation.

### 4.2 Success outcomes

- A new tenant administrator can configure a company and create the first project in under 10 minutes.
- A contributor can identify their assigned and urgent work within 30 seconds of signing in.
- An observer can follow progress and comment without gaining edit access.
- A project manager can modify a project workflow without technical assistance.
- A manager can generate a filtered, branded PDF report without exporting data to another tool.
- No user can access another tenant’s records or an out-of-scope project through either the interface or API.

## 5. Original Phase 1 non-goals

The following items were intentionally excluded from the first core-help-desk phase. Phase 3 subsequently delivered the requester portal, email ingestion, integration boundaries, opt-in AI assistance, and knowledge-base capabilities described below; the remaining items are still out of scope.

- A public, anonymous customer support portal.
- Email-to-ticket ingestion and two-way email conversation synchronization.
- Live chat, voice support, or call-center routing.
- Billing, subscriptions, or usage metering.
- Native mobile applications.
- Marketplace integrations with third-party tools.
- Fully programmable workflow scripting.
- AI-generated replies, summaries, classification, or prioritization.
- A full knowledge-base authoring and publishing system.
- Cross-tenant ticket sharing.

Items not marked as delivered above remain candidates for later phases and should not be precluded by the design.

## 6. Product principles

- **Secure by default:** private tenant data, private attachments, server-enforced authorization, and no reversible passwords.
- **Useful defaults:** a tenant can work immediately without designing a workflow or permission model.
- **Progressive configuration:** advanced options appear when needed and do not obstruct everyday ticket work.
- **Project-first organization:** tickets, workflow decisions, permissions, and reports have an explicit project context.
- **Explainable changes:** important actions appear in a human-readable activity history.
- **Bilingual parity:** English and French are released together; French is not a secondary or partial experience.
- **Accessible interaction:** keyboard, screen-reader, contrast, focus, and reduced-motion needs are product requirements.
- **Signal over noise:** notifications and dashboards should help users decide what to do next.

## 7. Users and personas

### 7.1 Tenant administrator

Owns company-level setup. Typical responsibilities include company branding, tenant settings, user invitations, role assignments, tenant-wide workflows, retention settings, and global reports.

This role is added because multi-tenancy requires an authority above individual projects. A tenant must always have at least one active tenant administrator.

### 7.2 Project manager

Owns configuration and administration within the projects they can access. Typical responsibilities include project settings, project workflow, membership, ticket types, priorities, assignments, bulk updates, and project reports.

### 7.3 Contributor

Performs day-to-day ticket work. Contributors create and update tickets, change status through allowed transitions, assign tickets when permitted, add comments and attachments, manage watchers, and resolve work.

### 7.4 Observer

Has read access to permitted projects and can add comments, mentions, and optionally attachments to comments. Observers cannot change ticket fields, assignments, priorities, or workflow state.

### 7.5 Requester/reporter

The person who creates or reports a ticket. Reporters may be authenticated tenant users or external requesters using an enabled project portal or verified inbound-email channel. External requesters can access only the ticket associated with their high-entropy private access token and cannot enumerate tenant data.

## 8. Multi-tenant model

### 8.1 Tenant definition

A tenant represents one company or organization. Every tenant-owned record must belong to exactly one tenant, including projects, users’ memberships, roles, workflows, tickets, comments, notifications, reports, branding, and audit events.

A person may use the same normalized email address in more than one tenant. Their membership, permissions, preferences, and notifications are evaluated separately for each tenant.

### 8.2 Tenant selection

- A user with one tenant enters that tenant directly after authentication.
- A user with multiple tenants selects a tenant after authentication and can switch tenants from the application shell.
- The active tenant must always be visually identifiable.
- Switching tenants clears project filters and navigation state that could reveal the prior tenant’s context.

### 8.3 Tenant isolation requirements

- All tenant-owned database records include a non-null tenant identifier.
- Every server-side read and write enforces the active tenant.
- Object storage paths are partitioned by tenant and use server-generated identifiers.
- Authorization is re-evaluated on every API request; hiding a control in the interface is not sufficient.
- Search, notifications, report exports, audit records, and background processing must maintain the same isolation.
- A user must receive a not-found or forbidden response without disclosure when requesting another tenant’s resource.
- Automated tests must attempt cross-tenant reads, writes, attachment access, and identifier substitution.

## 9. Roles and project scope

### 9.1 Role grants

A user may receive one or more role grants within a tenant. Each grant has one of these scopes:

- **All projects:** applies to every current and future project in the tenant.
- **Selected projects:** applies only to the explicitly selected projects.

Effective permissions are the union of valid grants inside the active tenant and project. A role grant never carries into another tenant. The interface must explain why a user has access by showing their role and scope assignments.

### 9.2 Default permission matrix

| Capability | Tenant administrator | Project manager | Contributor | Observer |
| --- | --- | --- | --- | --- |
| View permitted projects and tickets | Yes | Yes | Yes | Yes |
| Create tickets | Yes | Yes | Yes | No by default |
| Edit ticket content and metadata | Yes | Yes | Yes | No |
| Transition tickets | Yes | Yes | Allowed transitions | No |
| Assign/reassign tickets | Yes | Yes | Configurable | No |
| Add comments and mentions | Yes | Yes | Yes | Yes |
| Add comment attachments | Yes | Yes | Yes | Configurable, on by default |
| Delete own recent comment | Yes | Yes | Configurable | Configurable |
| Archive/restore tickets | Yes | Yes | No | No |
| Bulk-update tickets | Yes | Yes | Configurable | No |
| Manage a project’s configuration | Yes | In scope | No | No |
| Manage a project’s workflow | Yes | In scope | No | No |
| Manage project membership | Yes | In scope | No | No |
| View project reports | Yes | In scope | Configurable | Configurable |
| Export reports | Yes | In scope | Configurable | No by default |
| Manage users and tenant-wide roles | Yes | No | No | No |
| Manage company branding/settings | Yes | No | No | No |
| View tenant-wide audit events | Yes | No | No | No |

Role permissions should be centrally defined in the first release. Fine-grained custom roles are a later enhancement, but the authorization model should allow their introduction.

### 9.3 Membership lifecycle

- Tenant administrators invite users by email and assign initial roles/scopes.
- Invitations expire and can be revoked or resent.
- An existing account can accept membership in an additional tenant.
- Deactivated users cannot sign in to the tenant and no longer receive notifications.
- Deactivation preserves historical authorship, comments, assignments, and audit records.
- A user cannot deactivate or demote the tenant’s last active administrator.
- Removing project scope removes access immediately but preserves historical attribution.

## 10. Authentication and account security

### 10.1 Email and password authentication

- Users sign in with an email address and password.
- Email matching is case-insensitive after normalization.
- Passwords are never stored, logged, returned, or transmitted in clear text after initial submission.
- The database stores only a salted, adaptive password hash produced by the approved .NET identity/password-hashing implementation.
- Password verification uses the hashing library’s verification API and supports future rehashing when parameters change.
- Password reset uses a single-use, expiring token sent to the verified email address.
- New accounts must verify their email before accessing tenant data unless an explicit administrator-approved policy says otherwise.
- Repeated failed attempts trigger rate limiting and temporary account protection without revealing whether an email exists.
- Authentication events such as successful sign-in, failed sign-in, password change, password reset, and account deactivation are audited.

### 10.2 Session requirements

- Sessions expire after a configurable period and can be revoked.
- Password reset, password change, and account deactivation revoke existing sessions as appropriate.
- Users can sign out of the current session and all sessions.
- Secure cookie or token storage must follow the selected deployment model and avoid browser-accessible long-lived credentials.
- Privileged actions must be authorized at execution time, not only when a screen loads.

### 10.3 Later security capabilities

- Multi-factor authentication.
- Enterprise single sign-on through OpenID Connect/SAML.
- Tenant-controlled password and session policies.
- Administrator session review.

## 11. Company setup and branding

Tenant administrators can configure:

- company display name;
- short name used in compact navigation;
- logo, with recommended light- and dark-background variants;
- primary accent color selected from an accessible palette;
- default interface language;
- default time zone;
- date/time display format;
- default appearance mode;
- support contact information shown to tenant users;
- optional report footer text.

Branding appears in the application shell, sign-in/tenant-selection experience where applicable, notification emails when introduced, and PDF report exports. Branding must not reduce text/background contrast below accessibility targets. Uploaded logos use the same secure attachment pipeline and have documented file-size and dimension limits.

## 12. Projects

### 12.1 Project fields

Each project includes:

- immutable internal identifier;
- unique tenant-scoped key, such as `IT`, `OPS`, or `WEB`;
- English and French display names where the tenant requires localized configuration;
- description;
- active or archived state;
- project manager(s);
- workflow assignment;
- default priority and ticket type;
- optional default assignee;
- membership and role grants;
- optional SLA policy;
- creation/update timestamps and actor.

### 12.2 Project lifecycle

- Tenant administrators and in-scope project managers can create projects.
- A project starts with the tenant’s default workflow unless another workflow is selected.
- Project keys cannot be reused while historical tickets exist.
- Archiving a project prevents new tickets and routine updates while preserving read/report access.
- Restoring an archived project returns it to active use.
- Permanent project deletion is not available in the initial release.

### 12.3 Project overview

The project home shows:

- ticket counts by workflow state and priority;
- overdue and SLA-risk tickets;
- unassigned tickets;
- recently updated tickets;
- recent activity;
- quick links to saved views and reports;
- actions permitted by the current user’s role.

## 13. Workflows and queues

### 13.1 Default workflow

Every new tenant receives a default workflow:

| Order | Stable category | English label | French label | Terminal |
| --- | --- | --- | --- | --- |
| 1 | To do | TODO | À FAIRE | No |
| 2 | In progress | IN PROGRESS | EN COURS | No |
| 3 | Done | DONE | TERMINÉ | Yes |

By default, project managers and contributors can move tickets forward or backward between adjacent states. Project managers can reopen terminal tickets. These transition permissions can later become configurable by role.

### 13.2 Workflow configuration

A workflow consists of:

- tenant-unique name and description;
- ordered statuses;
- status key, localized labels, stable category, color, and terminal flag;
- allowed transitions between statuses;
- optional transition name and description;
- optional requirement for a transition comment;
- active/inactive state;
- projects using the workflow.

Tenant administrators can create reusable tenant workflows. Project managers can select a tenant workflow or create a project-specific copy. Project-specific edits must not affect other projects.

### 13.3 Workflow safety

- A workflow must contain at least one non-terminal status and one terminal status.
- Status keys are stable after use; display labels can change.
- Circular and backward transitions are allowed when intentionally configured.
- A status cannot be removed while tickets still use it unless the administrator maps those tickets to another status.
- Applying a different workflow to a project requires an explicit old-to-new status mapping and a preview of affected ticket counts.
- Every workflow change and ticket transition is audited.
- Ticket reports group statuses by stable category as well as custom status, enabling comparison across projects.

### 13.4 Queues and views

“Workflow” defines ticket states and transitions. “Queue” describes an actionable filtered list. Built-in queues include:

- My open tickets;
- Unassigned;
- Recently updated;
- Waiting in TODO;
- In progress;
- Overdue;
- SLA at risk;
- Done recently.

Users can save personal views. Project managers can publish project views. A saved view contains filters, sorting, visible columns, and optional grouping, but never bypasses permissions.

## 14. Tickets

### 14.1 Ticket identity

Each ticket receives a human-readable project-scoped key, such as `OPS-1042`. The key is permanent even if the project display name changes.

### 14.2 Core ticket fields

- Ticket key.
- Project.
- Title.
- Rich description.
- Ticket type: Incident, Service Request, Task, or Problem by default.
- Priority: Low, Normal, High, or Urgent by default.
- Workflow status.
- Reporter.
- Primary assignee, which may be unassigned.
- Watchers/followers.
- Labels/tags.
- Optional due date and time.
- Optional SLA target timestamps.
- Resolution summary when completing the ticket, configurable as required.
- Created, updated, first-response, resolved, and closed timestamps.
- Archived state.

The type and priority lists can become configurable later. The initial values should be localized and consistent across reports.

### 14.3 Ticket creation

- Contributors, project managers, and tenant administrators can create tickets in permitted active projects.
- Required fields are project, title, type, priority, and description according to tenant/project configuration.
- The initial status is the workflow’s designated starting status.
- A project’s default assignee and priority are applied when configured.
- The creator becomes the reporter and a watcher unless they opt out.
- Attachment uploads can be completed during ticket creation.
- Duplicate submission protection prevents repeated tickets caused by retries or double clicks.

### 14.4 Ticket editing and assignment

- Authorized users can edit ticket fields without changing the ticket key.
- Concurrent edits must not silently overwrite newer changes; the user is warned and can reload/compare.
- Assignment changes generate activity and notifications.
- Contributors can assign to themselves by default. Assigning other users is configurable and initially reserved for project managers.
- Only active users with access to the project can be assigned or added as watchers.
- Moving a ticket to a terminal status records the resolution timestamp.
- Reopening clears the current resolution timestamp while retaining historical transitions.

### 14.5 Ticket relationships

The first release should support lightweight relationships:

- relates to;
- duplicates/is duplicated by;
- blocks/is blocked by.

Relationship targets must be in a project visible to the current user. Users without access see no details about hidden related tickets. Parent/child ticket hierarchies are a later enhancement.

### 14.6 Bulk actions

Project managers can select permitted tickets from a queue and update status, priority, assignee, labels, or archive state. The interface previews the affected count and validates every ticket. Partial failures must be explained without silently skipping tickets.

## 15. Ticket description formatting

The description and comments support a safe, limited rich-text experience:

- paragraphs and line breaks;
- bold, italic, and strikethrough;
- headings with limited levels;
- ordered and unordered lists;
- inline code and fenced code blocks;
- block quotes;
- links;
- simple tables where accessibility can be preserved.

The editor must produce a well-defined format suitable for safe rendering and future export. Pasted or submitted markup is sanitized on the server. Scripts, embedded frames, unsafe URLs, event attributes, arbitrary styles, and unsupported HTML are removed. Links open safely and show their destination. The plain-text content remains searchable.

The system does not automatically translate user-authored ticket descriptions or comments.

## 16. Attachments

### 16.1 Supported files

The initial allowlist should include common business formats:

- images: PNG, JPEG, GIF, and WebP;
- documents: PDF, DOCX, XLSX, PPTX, ODT, ODS, and ODP;
- text/data: TXT, CSV, JSON, XML, and Markdown;
- archives may be added later after security review.

Default limits are proposed as 25 MB per file, 20 files per ticket/comment operation, and a tenant-level storage quota that can be configured operationally. Exact commercial limits remain an open product decision.

### 16.2 Attachment requirements

- Files are stored in private Azure Blob Storage; PostgreSQL stores metadata and ownership references.
- Blob names are server-generated and never trust the original file name as a path.
- Metadata includes tenant, project, ticket/comment, original name, size, reported and detected content type, uploader, checksum, and timestamps.
- Both extension and content signature are validated.
- Malware scanning is required before a file becomes downloadable in production. Files remain in a pending/quarantined state until approved.
- Downloads are authorized per request or through a narrowly scoped, short-lived URL.
- Image previews and PDF previews may be offered when safe; preview is not required for other document types.
- Upload and deletion appear in the activity history.
- Attachment deletion is soft by default and follows retention policy.
- Reports contain attachment metadata, not embedded file contents, unless explicitly designed later.

## 17. Comments, mentions, and activity

### 17.1 Comments

- All roles can comment on tickets they can view.
- Comments support the same safe basic formatting as descriptions.
- Users can mention other active users who can access the ticket.
- Edited comments display an edited indicator and retain an audit revision.
- Comment deletion follows role and time-window rules and leaves an audit event.
- Project managers can hide inappropriate content without erasing the audit record.
- Internal/private notes separate from observers are not part of the first release because they complicate visibility; they are a candidate enhancement.

### 17.2 Activity timeline

Each ticket has a chronological timeline showing:

- creation;
- field changes with meaningful old/new values;
- status transitions;
- assignment and watcher changes;
- comments and comment edits;
- attachment events;
- ticket relationships;
- archive/restore actions;
- automated SLA events.

Routine technical events should not overwhelm the timeline. Users can filter the timeline by comments, changes, and attachments.

## 18. Internal notification system

### 18.1 Notification center

ReqNest includes a persistent in-app notification center rather than relying only on temporary toast messages. Each notification contains:

- recipient and tenant;
- event type and actor;
- project/ticket reference;
- localized summary rendered in the recipient’s preferred language;
- creation time;
- read/unread state;
- direct link to the authorized resource;
- optional grouping key.

The notification center supports unread count, mark read/unread, mark all read, and filtering by project/type. Notifications remain available according to a retention policy even if the temporary toast is dismissed.

### 18.2 Notification events

Users can be notified when:

- a ticket is assigned or reassigned to them;
- they are mentioned in a description or comment;
- someone comments on a ticket they report, own, or watch;
- a watched ticket changes status or priority;
- a due date is approaching or has passed;
- a ticket enters SLA warning or breach;
- a ticket they reported is resolved or reopened;
- they are added to or removed from a project;
- their role or project scope changes;
- an invitation requires action;
- a requested report export is ready or failed.

### 18.3 Noise controls

- Do not notify a user about their own action unless confirmation has product value.
- Group rapid changes to the same ticket into one notification where appropriate.
- Deduplicate retries using a stable event identifier.
- Assignment, direct mention, security, and role-change events are always delivered in-app.
- Users can configure preferences for comments, watcher updates, due dates, and digests.
- A user can mute routine notifications for a ticket while still receiving direct mentions and security-critical events.
- Project managers can configure project defaults without overriding mandatory events.

### 18.4 Delivery design

The first release requires in-app delivery with near-real-time refresh and a durable database record. Email delivery can follow in a later phase using the same notification event model. Notification generation must be retry-safe and must not block the ticket transaction from completing.

## 19. Search, filtering, and navigation

### 19.1 Global search

Authorized users can search visible tickets by:

- ticket key;
- title;
- plain-text description;
- comment text where permitted;
- labels;
- reporter and assignee names.

Results are always tenant- and project-scoped. Exact ticket-key matches rank first. Search must not reveal counts, snippets, names, or suggestions from inaccessible projects.

### 19.2 Ticket filters

Filters include tenant (when switching is available), project, status/category, type, priority, reporter, assignee, labels, watcher, created/updated/resolved date, due state, SLA state, and free text.

Users can:

- combine filters;
- sort by relevant columns;
- choose visible columns;
- group by project, status, priority, or assignee;
- save personal views;
- share project views when authorized;
- copy a URL that restores a permitted filter state.

## 20. Dashboards and reports

### 20.1 Personal dashboard

The signed-in home page prioritizes action:

- assigned open tickets;
- urgent/high-priority tickets;
- tickets awaiting the user’s response;
- overdue and SLA-risk tickets;
- recent mentions and unread notifications;
- recently viewed tickets;
- project shortcuts.

### 20.2 Standard reports

The first release should provide these reports:

1. **Ticket inventory:** current counts by project, workflow status/category, type, and priority.
2. **Created vs. resolved trend:** tickets created and resolved by day/week/month, including net backlog change.
3. **Ticket aging:** open tickets grouped by age band, status, project, priority, and assignee.
4. **Resolution performance:** median and percentile time to first response and resolution, broken down by project and priority.
5. **Throughput:** tickets completed by period, project, type, and contributor.
6. **Workload:** open and in-progress tickets per assignee, with unassigned work highlighted.
7. **SLA performance:** met, at-risk, breached, and excluded tickets by policy/project/priority.
8. **Workflow flow:** time spent in each workflow status and common transition paths.
9. **Project comparison:** normalized inventory, throughput, aging, and resolution measures across accessible projects.
10. **Activity report:** ticket creation, comments, transitions, assignment changes, and attachment actions by period.

Reports must define every metric in the interface. A “resolved” metric uses entry into a terminal workflow status; reopened tickets are represented consistently and documented.

### 20.3 Report filters

Common filters include date range, project, status/category, priority, ticket type, assignee, reporter, labels, SLA state, and archived state. Reports default to the user’s permitted projects and current tenant time zone.

### 20.4 PDF export

- Authorized users can export the current report and filter state to PDF.
- The PDF includes company name/logo, report title, generation time, time zone, applied filters, metric definitions, page numbers, and the requesting user.
- Labels and generated prose use the requester’s selected English/French locale.
- Charts include readable labels and accompanying tabular values where necessary for accessibility.
- Large exports run asynchronously. The user receives an in-app notification when the file is ready or has failed.
- Generated PDFs are stored privately with an expiry/retention period and require authorization to download.
- Exported figures must match the on-screen report for the same filter snapshot.
- CSV export of report tables is a recommended companion capability, even though PDF is the required format.

## 21. SLA and due-date management

SLA support makes the tool more useful for operational help desks. An SLA policy may define:

- first-response target;
- resolution target;
- priority-specific durations;
- business hours, working days, and tenant holidays;
- pause conditions for selected workflow statuses;
- warning threshold before breach.

Projects may inherit the tenant policy or use a project-specific policy. The ticket records calculated target times and status so historical reports remain explainable if the policy later changes.

The initial implementation may begin with elapsed-clock targets and add business calendars in the next phase, but the UI and reports must clearly state which calculation is active.

## 22. English, French, and localization

### 22.1 Interface language

- Every user-facing system string is available in English and French.
- Users choose their preferred language, which persists in their profile.
- Before sign-in, use the tenant default when known, then browser preference, then English.
- Language can be changed without signing out.
- Validation, errors, notifications, empty states, emails, reports, dates, numbers, and accessibility labels are localized.
- Missing translations fall back predictably and are detectable in automated tests; raw translation keys must never appear to users.

### 22.2 Configurable content

Tenant/project configuration that users see—workflow statuses, project names where needed, ticket types, report labels—supports English and French values. Administrators are prompted when one language is missing.

User-generated descriptions and comments remain in the language entered. Automatic translation is out of scope.

### 22.3 Formatting

- Dates and numbers follow locale conventions.
- Stored timestamps remain absolute/UTC and display in the selected tenant/user time zone.
- Search should handle French accents sensibly without corrupting exact identifiers.

## 23. Appearance and dark mode

- Users can select Light, Dark, or System.
- The preference persists per user; anonymous pages use system preference by default.
- Both modes use the same information hierarchy and feature set.
- Tenant accent colors are adjusted or rejected when they fail contrast requirements.
- Logos can provide light/dark variants, with a safe fallback when only one exists.
- Charts, editors, dialogs, tables, notifications, PDF previews, loading states, and error states must be reviewed in both modes.
- The application avoids flashes of the wrong theme during startup.
- Reduced-motion system preferences are respected.

## 24. Audit and administration

### 24.1 Audit log

The system records security- and administration-relevant events including:

- sign-in and account security actions;
- invitations, activation, deactivation, and role/scope changes;
- tenant and branding changes;
- project creation, configuration, archive, and restore;
- workflow creation and modification;
- ticket creation, edits, transitions, archive, and restore;
- attachment upload, download authorization, quarantine, and deletion;
- report generation;
- data-retention operations.

Audit events include tenant, actor, action, target type/identifier, timestamp, safe change summary, and request correlation identifier. Sensitive values such as passwords, tokens, full attachment contents, and secrets are never recorded.

Tenant administrators can filter and export audit metadata. Audit records are append-only from the application’s perspective.

### 24.2 Administrative safeguards

- Destructive or broad actions show an impact preview and require confirmation.
- Configuration forms validate references before saving.
- Changes that could strand tickets or remove access have explicit migration/reassignment steps.
- Administrators can see when a workflow, role grant, or policy is in use.

## 25. Conceptual data model

The following model is conceptual and does not prescribe exact database tables:

- **User:** global identity, normalized email, password hash, verification/security state.
- **Tenant:** company identity, settings, locale, time zone, appearance defaults.
- **TenantMembership:** user-to-tenant relationship and lifecycle state.
- **RoleGrant:** role, all-project or selected-project scope, grantor, validity.
- **Project:** tenant-owned work boundary and project key.
- **ProjectMembership/Scope:** selected project access where applicable.
- **Workflow:** reusable tenant workflow or project-specific workflow.
- **WorkflowStatus:** localized status metadata, stable category, order, terminal flag.
- **WorkflowTransition:** allowed source/destination and rules.
- **Ticket:** tenant/project-owned request and current workflow state.
- **TicketStatusHistory:** immutable transition records and durations.
- **TicketComment:** formatted collaboration entry and revision state.
- **Attachment:** private blob metadata and scan state.
- **TicketRelationship:** typed relationship between visible tickets.
- **Watcher:** user subscription to a ticket.
- **Notification:** recipient event, localization payload, read state, deep link.
- **SavedView:** owner/project filters, sorting, grouping, and columns.
- **SlaPolicy/SlaSnapshot:** policy definition and ticket-specific calculated targets.
- **ReportExport:** filter snapshot, generation status, private PDF blob, expiry.
- **AuditEvent:** append-only security and business event metadata.

All tenant-owned entities carry a tenant identifier even when it can be inferred through another relationship. This supports defense-in-depth and easier tenant-isolation testing.

## 26. Primary user journeys

### 26.1 Set up a new company

1. The initial administrator creates or accepts a tenant.
2. They configure company name, logo, default language, time zone, and appearance.
3. The system creates the default workflow.
4. The administrator creates the first project.
5. They invite users and assign roles with all-project or selected-project scope.
6. The project is ready for ticket creation.

### 26.2 Create and resolve a ticket

1. A contributor opens an accessible project and selects Create ticket.
2. They enter title, formatted description, type, priority, and attachments.
3. The ticket receives a project key and starts in TODO.
4. A project manager assigns the ticket; the assignee receives a notification.
5. The assignee transitions it to IN PROGRESS and collaborates through comments.
6. The assignee records a resolution summary and transitions it to DONE.
7. The reporter and watchers receive a grouped resolution notification.
8. The transition and resolution duration appear in reports.

### 26.3 Configure a project-specific workflow

1. A project manager opens project settings.
2. They copy the tenant workflow for this project.
3. They add a status with English/French labels and configure transitions.
4. The system validates terminal/start states and previews existing ticket impact.
5. The manager maps old statuses where necessary and confirms.
6. The change applies only to that project and is audited.

### 26.4 Observe and comment

1. An observer signs in and sees only assigned projects.
2. They open a ticket and review its content and activity.
3. Edit, assignment, and transition controls are absent and server-protected.
4. They add a comment and mention an authorized contributor.
5. The contributor receives an in-app notification and follows the deep link.

### 26.5 Produce a management report

1. A manager opens Resolution performance.
2. They select projects, date range, priorities, and SLA state.
3. The dashboard updates with definitions and filter context.
4. They select Export PDF and choose English or French.
5. The export runs asynchronously if needed.
6. A notification links to the authorized branded PDF.

## 27. Functional acceptance criteria for the first release

### 27.1 Tenancy and authorization

- Two tenants can use identical project keys without conflict.
- A user belonging to both tenants sees separate memberships and permissions.
- Identifier substitution cannot access another tenant or unauthorized project.
- All-project scope includes a newly created project automatically.
- Selected-project scope does not include a newly created project.
- An observer can add a comment but cannot update a ticket field or status through the UI or API.

### 27.2 Authentication

- A valid email/password signs in to the correct account.
- The stored credential is a salted hash and never the submitted password.
- Invalid credentials return a generic response.
- Password reset tokens expire and cannot be reused.
- Deactivation prevents new access and revokes relevant sessions.

### 27.3 Projects, workflows, and tickets

- A new tenant has the default three-state workflow.
- A project can use the default workflow or a project-specific workflow.
- Editing a project-specific workflow does not affect another project.
- Existing tickets must be mapped before their status is removed.
- Ticket keys are unique and sequential within the intended project-key strategy.
- Ticket transitions reject paths not present in the active workflow.
- Concurrent edits do not silently lose data.

### 27.4 Collaboration and files

- Safe formatting renders consistently in descriptions and comments.
- Unsafe markup is removed server-side.
- Allowed documents upload and retain correct metadata.
- Disallowed, oversized, or quarantined files cannot be downloaded.
- An authorized user can download; an unauthorized project user cannot.
- Ticket changes, comments, transitions, and attachment events appear in activity history.

### 27.5 Notifications

- Assignment, direct mention, comment, status, and report-ready events create the correct recipient notifications.
- The actor is not notified about their own routine action.
- Duplicate processing does not create duplicate notifications.
- Read/unread state persists.
- A notification deep link still enforces current permission.

### 27.6 Localization and appearance

- A user can switch between complete English and French interfaces without signing out.
- Dates and generated report text follow the selected locale/time zone.
- Light, dark, and system modes persist and render all primary screens.
- Browser and automated accessibility checks find no critical violations in primary journeys.

### 27.7 Reports

- Report figures reconcile with the underlying filtered tickets.
- Reopened tickets follow the documented resolution calculation.
- Project scope applies to on-screen and exported results.
- A PDF includes branding, locale, filter snapshot, time zone, generation time, and metric definitions.
- An expired or unauthorized report URL cannot download the PDF.

## 28. Non-functional requirements

### 28.1 Performance targets

Proposed initial targets under expected operating load:

- authenticated page/API interactions: p95 under 500 ms excluding file transfers and report generation;
- ticket list first page: p95 under 1 second for typical filters;
- exact ticket-key search: p95 under 500 ms;
- notification visibility: normally within 10 seconds of the triggering action;
- ordinary PDF report: generated within 60 seconds;
- large lists are paginated or virtualized and never loaded without bounds.

Actual targets must be validated against expected tenant size and hosting tier.

### 28.2 Availability and resilience

- The API exposes health/readiness signals for PostgreSQL and Blob Storage dependencies.
- Ticket writes and notification/report background work are retry-safe.
- Database and blob operations do not assume a distributed transaction; orphan cleanup is measurable and repeatable.
- Backups, point-in-time recovery expectations, and restoration exercises are defined before production launch.
- User-facing failures provide a correlation identifier without exposing internals.

### 28.3 Accessibility

- Target WCAG 2.2 AA.
- All primary actions are keyboard operable.
- Focus is visible and returns appropriately after dialogs.
- Fields have programmatic labels, descriptions, and errors.
- Status and priority are not represented by color alone.
- Charts include textual/table alternatives.
- Rich-text editing and attachment controls have accessible names and instructions.
- English/French labels are reviewed for screen-reader clarity.

### 28.4 Security and privacy

- TLS is required outside local development.
- Authorization is implemented server-side for every resource/action.
- Sensitive configuration is supplied through managed secrets and never source control.
- Logs exclude passwords, tokens, authorization headers, raw files, and unnecessary personal data.
- Rich content is sanitized; file types are verified and scanned.
- Common abuse paths are rate-limited.
- Dependency and container vulnerability scanning is part of release checks.
- Tenant retention/export/deletion policies are defined before storing production customer data.

### 28.5 Compatibility

- Support current stable desktop versions of Chrome, Edge, Firefox, and Safari.
- Responsive layouts support tablet widths; phone usability is desirable but native/mobile-first workflows are not an MVP requirement.
- The API and frontend use explicit version-compatible contracts.

## 29. Product analytics and success metrics

Product analytics must use tenant-safe, privacy-conscious events. Suggested measures:

- time from tenant creation to first project and first ticket;
- weekly active tenants and active users by role;
- percentage of open tickets with an assignee;
- median time to first response and resolution;
- backlog growth and aging distribution;
- percentage of tickets resolved then reopened;
- notification open rate and mute rate by event type;
- report usage and PDF export completion/failure rate;
- search success proxies such as result click-through;
- attachment scan failure and orphan-cleanup counts;
- English/French and light/dark usage split;
- configuration completion and invitation acceptance rate.

Analytics must not capture ticket descriptions, comment bodies, filenames, passwords, tokens, or other sensitive content.

## 30. Delivery phases

### Phase 1 — Core help desk

- Multi-tenant foundation and tenant switching.
- Email/password authentication, invitation, reset, and secure password hashing.
- Tenant administrator, project manager, contributor, and observer roles.
- All-project and selected-project scope.
- Company name/logo/basic appearance.
- Project management.
- Default and project-specific workflows.
- Ticket create/read/update/transition/archive.
- Safe description/comment formatting.
- Private attachments and scanning integration point.
- Activity timeline, mentions, watchers, and durable in-app notifications.
- Search, filters, built-in queues, and personal saved views.
- English/French and light/dark/system modes.
- Essential dashboards and PDF reports.
- Audit log and tenant-isolation tests.

### Phase 2 — Operational maturity

- Full SLA business calendars and holidays.
- Email notification delivery and digest preferences.
- Configurable ticket types, priorities, and custom fields.
- Published project views and enhanced bulk actions.
- Ticket relationships and parent/child work.
- Report scheduling and CSV exports.
- More granular permissions/custom roles.
- Attachment previews and operational storage quotas.
- Expanded audit export and retention controls.

### Phase 3 — Integrations and external service (delivered)

- Public/requester portal.
- Email-to-ticket and reply synchronization.
- Webhooks and API tokens.
- OIDC single sign-on using authorization code with PKCE, explicit account linking, and one-time application exchanges. Multi-factor authentication is not part of this product.
- Knowledge base and suggested articles.
- Third-party integrations.
- Optional AI assistance subject to privacy, evaluation, and human-review requirements.

## 31. Risks and mitigations

| Risk | Impact | Mitigation |
| --- | --- | --- |
| Tenant filter or authorization defect | Critical data exposure | Defense-in-depth tenant IDs, server policies, cross-tenant tests, security review |
| Workflow customization becomes too complex | Poor adoption and support burden | Strong default, guided editor, validation, preview, templates |
| Notification overload | Users ignore important events | Grouping, deduplication, preferences, mute controls, mandatory-event policy |
| Rich text enables unsafe content | Security and rendering defects | Restricted feature set, server-side sanitization, security tests |
| Attachments carry malware or leak through URLs | Security incident | Private blobs, type/signature validation, quarantine, scanning, short-lived authorization |
| Project workflow changes corrupt reporting | Incorrect metrics | Stable categories, immutable transition history, mandatory status mapping |
| English/French feature drift | Incomplete French experience | Translation keys in definition of done, automated missing-key tests, bilingual QA |
| PDF does not match dashboard | Loss of trust | Shared metric/query definitions and immutable filter snapshot |
| Broad role grants create accidental access | Confidentiality issue | Scope preview, permission explanation, audit, explicit all-project warning |
| Scope expands beyond a usable first release | Delayed delivery | Phase boundaries and explicit non-goals |

## 32. Open product decisions

The following decisions should be resolved before or during detailed feature design:

1. Will tenant creation be self-service, invitation-only, or operated by a platform administrator?
2. Can the same email/password identity join many tenants, or will some customers require isolated identities?
3. Are observers allowed to create tickets, or only view and comment?
4. Are comment attachments always allowed for observers?
5. What are the production per-file, per-ticket, and per-tenant storage limits?
6. Which malware scanning service and quarantine SLA will be used?
7. Are business-hour SLA calculations required in Phase 1 or Phase 2?
8. Which events require future email delivery and which may users disable?
9. How long are notifications, audit events, deleted attachments, and generated reports retained?
10. Should ticket priority/type lists be fixed in Phase 1 or tenant-configurable immediately?
11. Is a primary assignee sufficient, or must a ticket support multiple assignees?
12. Should project managers manage project membership directly or only request changes from tenant administrators?
13. Is sequential project-key numbering sufficient, and are gaps acceptable?
14. Do customers need tenant data export/deletion tools in the first production release?
15. What PDF templates, page sizes, and corporate-brand controls are required?
16. Are public requester access, email ingestion, or API integrations required for the first commercial customer?
17. What expected tenant, project, user, ticket, comment, and attachment volumes should drive performance tests?

## 33. Definition of done for a product feature

A feature is not complete until:

- role and project-scope behavior is defined and server-enforced;
- tenant-isolation behavior is tested;
- English and French strings, errors, and accessibility labels are complete;
- light and dark modes are reviewed;
- keyboard and screen-reader behavior is verified for the primary flow;
- audit and notification impacts are considered;
- empty, loading, success, validation, forbidden, conflict, and failure states are designed;
- API contracts and tests are updated;
- applicable metrics and reports have consistent definitions;
- sensitive data is excluded from logs and analytics;
- documentation and acceptance criteria reflect the shipped behavior.
