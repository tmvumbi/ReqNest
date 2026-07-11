import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { ApiClient } from '../../core/api/api-client';
import { Dashboard, Project } from '../../core/api/api-models';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { I18nService } from '../../core/i18n/i18n.service';
import { SessionStore } from '../../core/session/session-store';
import { Icon, IconName } from '../../layout/icons/icon';

interface Metric {
  label: string;
  value: number;
  icon: IconName;
  tone: 'violet' | 'red' | 'amber' | 'blue' | 'slate';
  link: string;
  alarms: boolean;
}

@Component({
  selector: 'app-dashboard-page',
  imports: [RouterLink, ButtonModule, Icon, LocalizedDatePipe],
  template: `
    <header class="page-heading">
      <div>
        <h1>{{ i18n.text('dashboard.title') }}</h1>
        <p>{{ i18n.text('dashboard.summary') }}</p>
      </div>
      @if (store.canMaintainTickets()) {
        <a pButton routerLink="/app/tickets/new">{{ i18n.text('tickets.new') }}</a>
      }
    </header>
    @if (loading()) {
      <p class="state-message">{{ i18n.text('common.loading') }}</p>
    }
    @if (dashboard(); as data) {
      <section class="metric-grid" aria-label="Dashboard metrics">
        @for (metric of metrics(data); track metric.label) {
          <a
            class="metric-card content-panel"
            [routerLink]="metric.link"
            [class.alarming]="metric.alarms && metric.value > 0"
          >
            <span class="metric-icon" [attr.data-tone]="metric.tone">
              <app-icon [name]="metric.icon" [size]="22" />
            </span>
            <span class="metric-copy">
              <span class="metric-value">{{ metric.value }}</span>
              <span class="metric-label">{{ metric.label }}</span>
            </span>
          </a>
        }
      </section>
      <div class="dashboard-columns">
        <div class="main-column">
          <section class="content-panel">
            <div class="section-heading">
              <h2>{{ i18n.text('dashboard.recent') }}</h2>
              <a pButton [text]="true" size="small" routerLink="/app/tickets">{{
                i18n.text('dashboard.viewAll')
              }}</a>
            </div>
            <div class="recent-list">
              @for (ticket of data.recentlyUpdated; track ticket.id) {
                <a [routerLink]="['/app/tickets', ticket.id]">
                  <span class="ticket-key">{{ ticket.key }}</span>
                  <span class="ticket-title">{{ ticket.title }}</span>
                  <span class="pill" [class]="priorityPill(ticket.priority)">{{
                    i18n.ticketPriority(ticket.priority)
                  }}</span>
                  <time>{{ ticket.updatedAt | localizedDate: 'mediumDate' }}</time>
                </a>
              } @empty {
                <div class="empty-state">
                  <app-icon name="inbox" [size]="26" />
                  <span>{{ i18n.text('common.empty') }}</span>
                </div>
              }
            </div>
          </section>
          <section class="content-panel">
            <div class="section-heading">
              <h2>{{ i18n.text('nav.projects') }}</h2>
              <a pButton [text]="true" size="small" routerLink="/app/projects">{{
                i18n.text('dashboard.viewAll')
              }}</a>
            </div>
            <div class="project-list">
              @for (project of projects(); track project.id) {
                <a routerLink="/app/tickets" [queryParams]="{ projectId: project.id }">
                  <span class="project-key">{{ project.key }}</span>
                  <span class="project-name">{{ projectName(project) }}</span>
                  <span class="pill" [class]="priorityPill(project.defaultPriority)">{{
                    i18n.ticketPriority(project.defaultPriority)
                  }}</span>
                  <span class="pill" [class]="project.isArchived ? 'pill-slate' : 'pill-green'">{{
                    project.isArchived
                      ? i18n.text('projects.archived')
                      : i18n.text('projects.active')
                  }}</span>
                </a>
              } @empty {
                <div class="empty-state">
                  <app-icon name="projects" [size]="26" />
                  <span>{{ i18n.text('common.empty') }}</span>
                </div>
              }
            </div>
          </section>
        </div>
        <section class="content-panel shortcuts">
          <div class="section-heading">
            <h2>{{ i18n.text('dashboard.shortcuts') }}</h2>
          </div>
          @if (store.canMaintainTickets()) {
            <a routerLink="/app/tickets/new"
              ><app-icon name="plus" [size]="16" />{{ i18n.text('tickets.new') }}</a
            >
          }
          <a routerLink="/app/tickets"
            ><app-icon name="tickets" [size]="16" />{{ i18n.text('tickets.myOpen') }}</a
          >
          <a routerLink="/app/knowledge"
            ><app-icon name="knowledge" [size]="16" />{{ i18n.text('nav.knowledge') }}</a
          >
          <a routerLink="/app/reports"
            ><app-icon name="reports" [size]="16" />{{ i18n.text('nav.reports') }}</a
          >
          @if (store.canManageProjects()) {
            <a routerLink="/app/admin/users"
              ><app-icon name="users" [size]="16" />{{ i18n.text('admin.invite') }}</a
            >
          }
        </section>
      </div>
    }
  `,
  styles: `
    .metric-grid {
      display: grid;
      grid-template-columns: repeat(5, minmax(0, 1fr));
      gap: 0.85rem;
      margin-bottom: 1rem;
    }
    .metric-card {
      display: flex;
      align-items: center;
      gap: 0.8rem;
      padding: 0.9rem 1rem;
      color: var(--app-text);
      text-decoration: none;
      transition:
        border-color 0.12s ease,
        transform 0.12s ease;
    }
    .metric-card:hover {
      border-color: var(--p-primary-color);
    }
    .metric-icon {
      display: grid;
      width: 2.5rem;
      height: 2.5rem;
      flex: 0 0 2.5rem;
      place-items: center;
      border-radius: 0.65rem;
    }
    .metric-icon[data-tone='violet'] {
      color: var(--hue-violet);
      background: color-mix(in srgb, var(--hue-violet) 12%, transparent);
    }
    .metric-icon[data-tone='red'] {
      color: var(--hue-red);
      background: color-mix(in srgb, var(--hue-red) 12%, transparent);
    }
    .metric-icon[data-tone='amber'] {
      color: var(--hue-amber);
      background: color-mix(in srgb, var(--hue-amber) 12%, transparent);
    }
    .metric-icon[data-tone='blue'] {
      color: var(--hue-blue);
      background: color-mix(in srgb, var(--hue-blue) 12%, transparent);
    }
    .metric-copy {
      display: grid;
      min-width: 0;
      gap: 0.08rem;
    }
    .metric-value {
      display: block;
      font-size: 1.55rem;
      font-weight: 750;
      letter-spacing: -0.03em;
      line-height: 1.2;
    }
    .metric-card.alarming .metric-value {
      color: var(--hue-red);
    }
    .metric-label {
      display: block;
      color: var(--app-text-muted);
      font-size: 0.78rem;
      font-weight: 550;
    }
    .dashboard-columns {
      display: grid;
      grid-template-columns: minmax(0, 1fr) 17rem;
      gap: 0.85rem;
      align-items: start;
    }
    .main-column {
      display: grid;
      gap: 0.85rem;
      min-width: 0;
    }
    .project-list {
      display: grid;
    }
    .project-list a {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 0.6rem 0.5rem;
      border-radius: 0.5rem;
      color: var(--app-text);
      text-decoration: none;
      font-size: 0.85rem;
    }
    .project-list a:hover {
      background: var(--app-sunken);
    }
    .project-key {
      min-width: 3.2rem;
      color: var(--p-primary-color);
      font-weight: 700;
      font-size: 0.78rem;
      white-space: nowrap;
    }
    .project-name {
      flex: 1;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
      font-weight: 550;
    }
    .recent-list {
      display: grid;
    }
    .recent-list a {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 0.6rem 0.5rem;
      border-radius: 0.5rem;
      color: var(--app-text);
      text-decoration: none;
      font-size: 0.85rem;
    }
    .recent-list a:hover {
      background: var(--app-sunken);
    }
    .ticket-key {
      color: var(--p-primary-color);
      font-weight: 700;
      font-size: 0.78rem;
      white-space: nowrap;
    }
    .ticket-title {
      flex: 1;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
      font-weight: 550;
    }
    .recent-list time {
      color: var(--app-text-subtle);
      font-size: 0.75rem;
      white-space: nowrap;
    }
    .shortcuts {
      display: grid;
      gap: 0.15rem;
    }
    .shortcuts a {
      display: flex;
      align-items: center;
      gap: 0.6rem;
      padding: 0.5rem 0.55rem;
      border-radius: 0.5rem;
      color: var(--app-text);
      text-decoration: none;
      font-size: 0.85rem;
      font-weight: 550;
    }
    .shortcuts a app-icon {
      color: var(--app-text-subtle);
    }
    .shortcuts a:hover {
      background: var(--app-sunken);
    }
    @media (max-width: 1050px) {
      .metric-grid {
        grid-template-columns: repeat(2, 1fr);
      }
      .dashboard-columns {
        grid-template-columns: 1fr;
      }
    }
    @media (max-width: 520px) {
      .metric-grid {
        grid-template-columns: 1fr;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardPage {
  private readonly api = inject(ApiClient);
  readonly i18n = inject(I18nService);
  readonly store = inject(SessionStore);
  readonly dashboard = signal<Dashboard | null>(null);
  readonly projects = signal<Project[]>([]);
  readonly loading = signal(true);

  constructor() {
    void this.load();
  }

  projectName(project: Project): string {
    return project.name;
  }

  metrics(data: Dashboard): Metric[] {
    return [
      {
        label: this.i18n.text('dashboard.assigned'),
        value: data.assignedOpen,
        icon: 'tickets',
        tone: 'violet',
        link: '/app/tickets',
        alarms: false,
      },
      {
        label: this.i18n.text('dashboard.urgent'),
        value: data.urgent,
        icon: 'reports',
        tone: 'red',
        link: '/app/tickets',
        alarms: true,
      },
      {
        label: this.i18n.text('dashboard.overdue'),
        value: data.overdue,
        icon: 'audit',
        tone: 'amber',
        link: '/app/tickets',
        alarms: true,
      },
      {
        label: this.i18n.text('dashboard.slaRisk'),
        value: data.slaRisk,
        icon: 'operations',
        tone: 'amber',
        link: '/app/tickets',
        alarms: true,
      },
      {
        label: this.i18n.text('dashboard.unread'),
        value: data.unreadNotifications,
        icon: 'bell',
        tone: 'blue',
        link: '/app/notifications',
        alarms: false,
      },
    ];
  }

  priorityPill(priority: string): string {
    return (
      {
        Urgent: 'pill-red',
        High: 'pill-amber',
        Normal: 'pill-blue',
        Low: 'pill-slate',
      }[priority] ?? 'pill-slate'
    );
  }

  private async load(): Promise<void> {
    try {
      const [dashboard, projects] = await Promise.all([
        firstValueFrom(this.api.dashboard()),
        firstValueFrom(this.api.projects()),
      ]);
      this.dashboard.set(dashboard);
      this.projects.set(projects);
    } finally {
      this.loading.set(false);
    }
  }
}
