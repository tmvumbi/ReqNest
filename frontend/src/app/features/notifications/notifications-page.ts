import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { ApiClient } from '../../core/api/api-client';
import { AppNotification, Project } from '../../core/api/api-models';
import { I18nService } from '../../core/i18n/i18n.service';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';

@Component({
  selector: 'app-notifications-page',
  imports: [
    LocalizedDatePipe,
    FormsModule,
    ButtonModule,
    SelectModule,
    ToggleSwitchModule,
  ],
  template: `
    <header class="page-heading">
      <div>
        <p class="eyebrow">{{ i18n.text('nav.notifications') }}</p>
        <h1>{{ i18n.text('notifications.title') }}</h1>
        <p>{{ i18n.text('notifications.summary') }}</p>
      </div>
      <button pButton type="button" severity="secondary" [outlined]="true" (click)="markAllRead()">
        {{ i18n.text('notifications.markAll') }}
      </button>
    </header>
    <div class="notifications-columns">
    <section class="content-panel">
      <div class="notification-filters">
        <label class="toggle-label" for="unreadOnly"
          ><p-toggleswitch
            inputId="unreadOnly"
            [(ngModel)]="unreadOnly"
            (ngModelChange)="load()"
          />{{ i18n.text('notifications.unreadOnly') }}</label
        >
        <p-select
          inputId="notificationProject"
          [(ngModel)]="selectedProject"
          [options]="projects()"
          [optionLabel]="'name'"
          optionValue="id"
          [showClear]="true"
          [placeholder]="i18n.text('common.project') + ' · ' + i18n.text('common.all')"
          [ariaLabel]="i18n.text('common.project')"
          (onChange)="load()"
        />
        <p-select
          inputId="notificationType"
          [(ngModel)]="selectedType"
          [options]="types"
          [showClear]="true"
          [placeholder]="i18n.text('tickets.type') + ' · ' + i18n.text('common.all')"
          [ariaLabel]="i18n.text('tickets.type')"
          (onChange)="load()"
          ><ng-template #selectedItem let-option>{{ i18n.notificationType(option) }}</ng-template>
          <ng-template #item let-option>{{
            i18n.notificationType(option)
          }}</ng-template></p-select
        >
      </div>
      <div class="notification-list">
        @for (notification of notifications(); track notification.id) {
          <button type="button" [class.unread]="!notification.readAt" (click)="open(notification)">
            <span class="notification-icon" aria-hidden="true">{{ icon(notification.type) }}</span
            ><span class="notification-copy"
              ><strong>{{ summary(notification) }}</strong
              ><small>{{ notification.createdAt | localizedDate: 'medium' }}</small></span
            >
            @if (!notification.readAt) {
              <span class="unread-dot" aria-hidden="true"></span>
            }
          </button>
        } @empty {
          <p class="state-message">{{ i18n.text('common.empty') }}</p>
        }
      </div>
    </section>
    <section class="content-panel preferences">
      <div>
        <h2>
          {{
            i18n.language() === 'French'
              ? 'Préférences de notification'
              : 'Notification preferences'
          }}
        </h2>
        <p>
          {{
            i18n.language() === 'French'
              ? 'Les mentions directes, affectations et alertes de sécurité restent toujours actives.'
              : 'Direct mentions, assignments, and security alerts always remain enabled.'
          }}
        </p>
      </div>
      <label for="commentsEnabled"
        ><p-toggleswitch inputId="commentsEnabled" [(ngModel)]="commentsEnabled" />{{
          i18n.language() === 'French' ? 'Commentaires' : 'Comments'
        }}</label
      ><label for="watcherUpdatesEnabled"
        ><p-toggleswitch inputId="watcherUpdatesEnabled" [(ngModel)]="watcherUpdatesEnabled" />{{
          i18n.language() === 'French'
            ? 'Mises à jour des tickets suivis'
            : 'Watched ticket updates'
        }}</label
      ><label for="dueDateUpdatesEnabled"
        ><p-toggleswitch inputId="dueDateUpdatesEnabled" [(ngModel)]="dueDateUpdatesEnabled" />{{
          i18n.language() === 'French' ? 'Échéances et SLA' : 'Due dates and SLA'
        }}</label
      ><label for="digestEnabled"
        ><p-toggleswitch inputId="digestEnabled" [(ngModel)]="digestEnabled" />{{
          i18n.language() === 'French' ? 'Résumé par courriel' : 'Email digest'
        }}</label
      ><label for="emailEnabled"
        ><p-toggleswitch inputId="emailEnabled" [(ngModel)]="emailEnabled" />{{
          i18n.language() === 'French' ? 'Livraison par courriel' : 'Email delivery'
        }}</label
      >
      <div class="field">
        <label for="digestHour">{{
          i18n.language() === 'French' ? 'Heure du résumé' : 'Digest hour'
        }}</label>
        <p-select inputId="digestHour" [(ngModel)]="digestHourLocal" [options]="digestHours" />
      </div>
      <button pButton type="button" (click)="savePreferences()">
        {{ i18n.text('common.save') }}
      </button>
    </section>
    </div>
  `,
  styles: `
    .notifications-columns {
      display: grid;
      grid-template-columns: minmax(0, 1fr) 19rem;
      gap: 0.85rem;
      align-items: start;
    }
    .toggle-label,
    .preferences label {
      display: flex;
      gap: 0.65rem;
      align-items: center;
      font-size: 0.85rem;
      font-weight: 550;
    }
    .notification-filters {
      display: flex;
      flex-wrap: wrap;
      gap: 0.6rem;
      align-items: center;
      margin-bottom: 0.75rem;
      padding-bottom: 0.75rem;
      border-bottom: 1px solid var(--app-border);
    }
    .notification-filters p-select {
      min-width: 11rem;
    }
    .field {
      display: grid;
      gap: 0.35rem;
    }
    .field > label {
      color: var(--app-text-muted);
      font-size: 0.76rem;
      font-weight: 650;
    }
    .preferences {
      display: grid;
      gap: 0.85rem;
    }
    .preferences h2 {
      margin: 0 0 0.3rem;
      font-size: 0.95rem;
      font-weight: 650;
    }
    .preferences p {
      margin: 0;
      color: var(--app-text-muted);
      font-size: 0.78rem;
      line-height: 1.5;
    }
    .preferences button {
      justify-self: stretch;
    }
    .notification-list {
      display: grid;
    }
    .notification-list > button {
      display: grid;
      grid-template-columns: 2.1rem 1fr auto;
      gap: 0.75rem;
      align-items: center;
      width: 100%;
      padding: 0.65rem 0.5rem;
      border: 0;
      border-radius: 0.5rem;
      color: var(--app-text);
      background: transparent;
      text-align: left;
      cursor: pointer;
    }
    .notification-list > button:hover {
      background: var(--app-sunken);
    }
    .notification-list > button.unread strong {
      font-weight: 700;
    }
    .unread-dot {
      width: 0.5rem;
      height: 0.5rem;
      border-radius: 50%;
      background: var(--p-primary-color);
    }
    .notification-icon {
      display: grid;
      width: 2rem;
      height: 2rem;
      place-items: center;
      border-radius: 50%;
      background: var(--app-sunken);
      color: var(--app-text-muted);
      font-size: 0.85rem;
    }
    .notification-copy {
      display: grid;
      gap: 0.1rem;
      min-width: 0;
    }
    .notification-copy strong {
      font-size: 0.85rem;
      font-weight: 550;
    }
    small {
      color: var(--app-text-muted);
      font-size: 0.75rem;
    }
    p-select {
      width: auto;
    }
    .preferences p-select {
      width: 100%;
    }
    @media (max-width: 950px) {
      .notifications-columns {
        grid-template-columns: 1fr;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotificationsPage {
  private readonly api = inject(ApiClient);
  private readonly router = inject(Router);
  readonly i18n = inject(I18nService);
  readonly notifications = signal<AppNotification[]>([]);
  readonly projects = signal<Project[]>([]);
  readonly types = [
    'TicketAssigned',
    'UserMentioned',
    'TicketCommented',
    'TicketStatusChanged',
    'TicketPriorityChanged',
    'DueDateApproaching',
    'DueDatePassed',
    'SlaAtRisk',
    'SlaBreached',
    'TicketResolved',
    'TicketReopened',
    'ProjectMembershipChanged',
    'RoleChanged',
    'InvitationCreated',
    'ReportReady',
    'ReportFailed',
  ];
  unreadOnly = false;
  selectedProject = '';
  selectedType = '';
  commentsEnabled = true;
  watcherUpdatesEnabled = true;
  dueDateUpdatesEnabled = true;
  digestEnabled = false;
  emailEnabled = false;
  digestHourLocal = 8;
  readonly digestHours = Array.from({ length: 24 }, (_, hour) => hour);

  constructor() {
    void this.initialize();
  }
  async load(): Promise<void> {
    this.notifications.set(
      (
        await firstValueFrom(
          this.api.notifications(this.unreadOnly, this.selectedProject, this.selectedType),
        )
      ).items,
    );
  }
  summary(item: AppNotification): string {
    return item.summary;
  }
  icon(type: string): string {
    return type.includes('Comment')
      ? '✦'
      : type.includes('Report')
        ? '▤'
        : type.includes('Due') || type.includes('Sla')
          ? '◷'
          : '●';
  }
  async open(item: AppNotification): Promise<void> {
    if (!item.readAt) await firstValueFrom(this.api.setNotificationRead(item, true));
    await this.router.navigateByUrl(item.deepLink);
  }
  async markAllRead(): Promise<void> {
    await firstValueFrom(this.api.markAllNotificationsRead());
    await this.load();
  }
  async savePreferences(): Promise<void> {
    await firstValueFrom(
      this.api.updateNotificationPreferences({
        commentsEnabled: this.commentsEnabled,
        watcherUpdatesEnabled: this.watcherUpdatesEnabled,
        dueDateUpdatesEnabled: this.dueDateUpdatesEnabled,
        digestEnabled: this.digestEnabled,
        emailEnabled: this.emailEnabled,
        digestHourLocal: this.digestHourLocal,
      }),
    );
  }

  private async initialize(): Promise<void> {
    const [projects, preferences] = await Promise.all([
      firstValueFrom(this.api.projects()),
      firstValueFrom(this.api.notificationPreferences()),
    ]);
    this.projects.set(projects);
    Object.assign(this, preferences);
    await this.load();
  }
}
