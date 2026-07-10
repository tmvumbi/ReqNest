import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { ApiClient } from '../../core/api/api-client';
import { Dashboard } from '../../core/api/api-models';
import { I18nService } from '../../core/i18n/i18n.service';
import { SessionStore } from '../../core/session/session-store';

@Component({
  selector: 'app-dashboard-page',
  imports: [RouterLink, ButtonModule, CardModule, TagModule],
  template: `
    <header class="page-heading">
      <div>
        <p class="eyebrow">{{ i18n.text('nav.dashboard') }}</p>
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
          <p-card
            ><span>{{ metric.label }}</span
            ><strong>{{ metric.value }}</strong
            ><small>{{ metric.context }}</small></p-card
          >
        }
      </section>
      <section class="content-panel">
        <div class="section-heading">
          <div>
            <p class="eyebrow">{{ i18n.text('common.updated') }}</p>
            <h2>{{ i18n.text('dashboard.recent') }}</h2>
          </div>
          <a pButton [text]="true" routerLink="/app/tickets">{{ i18n.text('nav.tickets') }}</a>
        </div>
        <div class="recent-list">
          @for (ticket of data.recentlyUpdated; track ticket.id) {
            <a [routerLink]="['/app/tickets', ticket.id]"
              ><span
                ><strong>{{ ticket.key }}</strong
                >{{ ticket.title }}</span
              ><p-tag
                [value]="i18n.ticketPriority(ticket.priority)"
                [severity]="
                  ticket.priority === 'Urgent'
                    ? 'danger'
                    : ticket.priority === 'High'
                      ? 'warn'
                      : 'secondary'
                "
            /></a>
          } @empty {
            <p class="state-message">{{ i18n.text('common.empty') }}</p>
          }
        </div>
      </section>
    }
  `,
  styles: `
    .metric-grid {
      display: grid;
      grid-template-columns: repeat(5, minmax(0, 1fr));
      gap: 1rem;
      margin-bottom: 1.5rem;
    }
    .metric-grid span,
    .metric-grid small {
      color: var(--app-text-muted);
    }
    .metric-grid strong {
      display: block;
      margin: 0.4rem 0;
      font-size: 2.2rem;
      letter-spacing: -0.05em;
    }
    .recent-list {
      display: grid;
    }
    .recent-list a {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
      padding: 0.9rem 0;
      border-bottom: 1px solid var(--app-border);
      color: var(--app-text);
      text-decoration: none;
    }
    .recent-list span {
      display: flex;
      gap: 1rem;
    }
    .recent-list strong {
      color: var(--p-primary-color);
    }
    @media (max-width: 1050px) {
      .metric-grid {
        grid-template-columns: repeat(2, 1fr);
      }
    }
    @media (max-width: 520px) {
      .metric-grid {
        grid-template-columns: 1fr;
      }
      .recent-list span {
        display: grid;
        gap: 0.2rem;
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
  readonly loading = signal(true);

  constructor() {
    void this.load();
  }

  metrics(data: Dashboard) {
    return [
      {
        label: this.i18n.text('dashboard.assigned'),
        value: data.assignedOpen,
        context: this.i18n.text('tickets.myOpen'),
      },
      {
        label: this.i18n.text('dashboard.urgent'),
        value: data.urgent,
        context: this.i18n.text('common.priority'),
      },
      {
        label: this.i18n.text('dashboard.overdue'),
        value: data.overdue,
        context: this.i18n.text('tickets.due'),
      },
      { label: this.i18n.text('dashboard.slaRisk'), value: data.slaRisk, context: 'SLA' },
      {
        label: this.i18n.text('dashboard.unread'),
        value: data.unreadNotifications,
        context: this.i18n.text('nav.notifications'),
      },
    ];
  }

  private async load(): Promise<void> {
    try {
      this.dashboard.set(await firstValueFrom(this.api.dashboard()));
    } finally {
      this.loading.set(false);
    }
  }
}
