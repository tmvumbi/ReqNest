# ReqNest UI/UX redesign plan

Status: approved direction — "ClickUp-like" productivity-tool UI. This document records the audit
of the previous UI, the target design language, and the per-page rework so future changes stay
consistent with it.

## 1. Audit of the previous UI (what was wrong)

Navigated every route as a tenant administrator (2026-07-11).

### Structural problems

- **The whole document scrolled.** The sidebar and topbar scrolled away with the content and the
  viewport filled with empty background on long pages (ticket detail). A work tool needs a fixed
  shell where only the content pane scrolls.
- **Hero typography inside a work tool.** Every page opened with an eyebrow label plus a
  `clamp(2.2rem, 5vw, 4rem)` display title ("Your work at a glance", "Operations and
  configuration"), pushing actual content below the fold and repeating what the sidebar already
  said.
- **Identity repeated three times** (sidebar brand, sidebar tenant card, topbar tenant name) while
  the topbar offered no search, no breadcrumbs, and no notification entry point.
- **Navigation without icons or hierarchy.** Flat text links; the Notifications badge was the only
  visual differentiator; admin links barely separated; no global create action.
- **Inconsistent accents.** Landing page used indigo, the app used Aura's default emerald, links
  rendered violet, and the tenant accent color (`--tenant-accent`) was fetched but never used.
- **Low density.** Enormous paddings, 2-row tables floating in dark space, forms stretching inputs
  to ~1500px wide.

### Page-level problems

| Page | Problems |
| --- | --- |
| Dashboard | Five identical KPI cards with equal weight, no icons/color; one short list; no guidance when counts are zero. |
| Tickets | "Saved views" occupied the prime slot even when empty; search required a button press; checkbox column with invisible purpose; columns clipped at the right edge; plain grey status tags. |
| Ticket detail | Multi-line 5rem title; Watch/Edit/Archive buttons wrapped; "Move ticket" was a card with a single shouting button; six stacked cards made a very long page; activity showed raw event keys (`requester.ticket.commented`). |
| New ticket | Subtitle copy-pasted from the list page ("Search, filter, and work through every permitted queue."); full-width inputs. |
| Projects | Text-button actions; "Overview" opened a dialog with broken spacing ("2Unassigned"). |
| Reports | Run bar, schedule bar, and results stacked with no hierarchy. |
| Knowledge | Permanent authoring form beside the list; a second, visually different rich-text editor (Quill default toolbar vs. the tickets editor). |
| Notifications | Preferences and inbox merged into one column; notifications not linked to their subject. |
| Users & roles | Action buttons clipped by horizontal overflow; header cell said "Your name" for all users. |
| Workflows | Raw enum keys duplicated next to their labels. |
| Operations | Seven unrelated config areas stacked on one page with no internal navigation. |
| Portal & integrations | Seven numbered sections, a full-width green "Save portal" button, inconsistent control styles. |
| Auth | Dark-only split hero with 5rem "Welcome back"; language toggle floating in a corner. |

## 2. Design language (ClickUp-inspired)

The goal is a crisp, light-first, dense productivity tool: compact left rail, breadcrumbed
toolbar, small type, colorful-but-restrained status accents.

### Tokens (defined in `src/styles.scss`)

- **Primary:** violet — PrimeNG preset remapped to the `violet` palette
  (`#7c5cf4` region, ClickUp-adjacent). Tenant accent (`--tenant-accent`) still layers on top via
  company settings.
- **Neutrals (light):** background `#f7f8fa`, surface `#ffffff`, sunken `#f1f2f6`, border
  `#e4e7ee`, strong text `#1f2430`, muted `#6b7280`, subtle `#9aa1ad`.
- **Neutrals (dark, class `reqnest-dark`):** background `#101218`, surface `#181b23`, sunken
  `#13151c`, border `#2a2e3a`, text `#e9ebf1`.
- **Status hues:** todo `slate`, in-progress `blue`, done `green`, urgent `red`, high `amber`,
  normal `blue`, low `slate`. Exposed as `.pill` variants used across list, detail, dashboard.
- **Type scale:** base 14px equivalent (`html { font-size: 15px }` for global density plus
  explicit sizes), page title `1.25rem/600/-0.01em`, section title `0.95rem/650`, body `0.9rem`,
  meta `0.8rem`. Inter stays.
- **Radii:** controls `8px`, cards `12px`. **Shadows:** 1px borders + soft ambient shadow on
  raised surfaces only (menus, dialogs).
- **Spacing:** page gutter `1.5rem`, card padding `1rem–1.25rem`, table row height ~`2.5rem`.

### Shell

- `100dvh` fixed flex layout; **only `<main>` scrolls** (`overflow-y: auto`).
- **Sidebar (15rem):** brand + tenant name row (single identity block, doubles as tenant
  switcher when multiple tenants), primary "New ticket" button, icon + label nav (inline SVG, no
  new deps), `WORKSPACE` / `ADMIN` section labels, notification count badge, collapsible to icons
  on tablet, drawer on mobile.
- **Topbar (3.25rem):** breadcrumb trail derived from the route, global ticket search box
  (Enter routes to `/app/tickets?search=`), bell → notifications (badge), language toggle, theme
  cycle button (System→Light→Dark), avatar-initial menu with display name + sign out.
- Content max-width `1200px` except tables pages which stretch to `1400px`.

### Shared patterns (global classes)

- `.page-header`: compact 1-row header — `h1` at 1.25rem + optional description at 0.85rem muted +
  right-aligned action buttons. No eyebrows.
- `.content-panel`: white card, 12px radius, 1px border, no heavy shadow.
- `.toolbar`: single-row filter/search bar with inline compact fields (labels become
  placeholders/aria-labels), used by tickets, knowledge, notifications, reports.
- `.pill`: colored dot + label token for statuses and priorities.
- `.empty-state`: icon + one-line explanation + optional CTA replacing bare "Nothing to show yet."
- Tables: sticky header row, 0.85rem cells, hover row background, right-aligned numeric/meta,
  actions in a trailing cell that never clips (`min-width` on the panel, internal scroll).

## 3. Per-page rework

- **Dashboard:** compact header ("Dashboard" + greeting subtitle). KPI strip of 5 stat cards
  (icon chip + value + label, color-coded, urgent/overdue turn red only when non-zero, links to
  filtered ticket views). Below: two-column grid — "Recently updated" ticket list (key, title,
  priority pill, relative time) and a "Shortcuts" card (new ticket, knowledge base, reports,
  invite user for admins).
- **Tickets:** one toolbar row: search field (searches on Enter), project select, queue select,
  saved-view select + save/delete icon actions pushed right. Table: key (violet mono), title
  (medium weight link), project, status pill, priority pill, assignee, updated; hover row;
  selection checkboxes only for bulk-capable users; contextual bulk bar slides above the table.
  Count in the panel footer.
- **Ticket detail:** breadcrumb (Tickets / HELP-1). Header card: key + title (1.5rem), status
  pill + transition menu ("Move to …"), priority pill, watch/edit/archive compact buttons.
  Main column: description card, comments (avatar initials, author, relative time, editor at
  bottom). Right rail (sticky, 20rem): details (project, type, reporter, due, SLA), relationships,
  attachments, linked knowledge, AI drafts collapsed by default, activity feed humanized.
- **New ticket:** correct subtitle; centered 720px card; project+type+priority+due in two-column
  rows; labels field with hint; sticky footer actions.
- **Projects:** header + table with proper hover actions (Overview/Edit/Archive as small
  buttons); overview dialog re-laid out (stat row, status list, recent tickets with links).
- **Reports:** "Run a report" panel (report + project + Run/Export buttons in one row), result
  panel with table + metric definitions, "Schedules" panel, "Recent exports" table.
- **Knowledge:** two tabs — Articles (search toolbar + article cards with status pill,
  slug, edit/archive) and New article (authoring form).
- **Notifications:** inbox list with icon per type, unread dot, filters in toolbar; preferences
  moved to a side card.
- **Users & roles:** fixed table layout (name+email stacked in one cell, role · scope, status
  pill, actions that never clip).
- **Workflows:** card per workflow: name, default badge, state chips with arrows in workflow
  order; edit/copy actions.
- **Operations:** PrimeNG tabs — Ticket schema (types, priorities, custom fields), SLA policies,
  Roles, Storage & retention, Email outbox. Same forms, organized.
- **Portal & integrations:** accordion of integration cards (portal, inbound email, API tokens,
  webhooks, SSO, connectors, AI assistance) with normal-sized actions.
- **Company settings:** sectioned cards (Identity, Localization & appearance, Branding, Support
  contact, Report footer) with one save bar.
- **Audit log:** toolbar + table treatment consistent with the rest.
- **Auth pages:** light-first centered card on soft violet-tinted background, brand top-left,
  compact 1.75rem heading, language toggle in the card footer; same layout for login, register,
  forgot/reset, SSO.
- **Landing:** align accent with app violet; tighten hero scale; reuse the same neutrals.
- **Requester portal:** same card language as the app (light, violet accent, compact).

## 4. Non-goals / constraints

- No dependency additions (icons are inline SVG; PrimeNG + Aura preset stay).
- No API or behavioral changes: every existing control keeps its function; only presentation,
  copy, and layout change. i18n: every new string exists in English and French.
- Accessibility preserved: skip link, focus rings, aria labels, keyboard operability, reduced
  motion support; contrast ≥ 4.5:1 in both themes.
