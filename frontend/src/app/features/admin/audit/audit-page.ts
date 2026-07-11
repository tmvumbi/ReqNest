import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ApiClient } from '../../../core/api/api-client';
import { AuditPage as AuditData, Member } from '../../../core/api/api-models';
import { I18nService } from '../../../core/i18n/i18n.service';
import { LocalizedDatePipe } from '../../../core/i18n/localized-date.pipe';

@Component({
  selector: 'app-audit-page',
  imports: [
    LocalizedDatePipe,
    FormsModule,
    ButtonModule,
    InputTextModule,
    SelectModule,
    TableModule,
    TagModule,
  ],
  template: `
    <header class="page-heading">
      <div>
        <p class="eyebrow">{{ i18n.text('nav.audit') }}</p>
        <h1>{{ i18n.text('admin.auditTitle') }}</h1>
        <p>{{ i18n.text('admin.auditSummary') }}</p>
      </div>
      <div class="heading-actions">
        <button
          pButton
          type="button"
          severity="secondary"
          [outlined]="true"
          (click)="exportAudit()"
        >
          {{ i18n.language() === 'French' ? 'Exporter JSON' : 'Export JSON' }}
        </button>
        <button pButton type="button" severity="secondary" [outlined]="true" (click)="exportCsv()">
          {{ i18n.language() === 'French' ? 'Exporter CSV' : 'Export CSV' }}
        </button>
        <button pButton type="button" severity="secondary" [outlined]="true" (click)="load()">
          {{ i18n.text('common.refresh') }}
        </button>
      </div>
    </header>
    <section class="content-panel filters-panel">
      <div class="filters">
        <div class="filter-field">
          <label for="auditAction">Action</label>
          <p-select
            inputId="auditAction"
            [(ngModel)]="filterAction"
            [options]="actionOptions()"
            [showClear]="true"
            [filter]="true"
            [placeholder]="i18n.language() === 'French' ? 'Toutes' : 'All'"
          />
        </div>
        <div class="filter-field">
          <label for="auditTarget">{{ i18n.language() === 'French' ? 'Cible' : 'Target' }}</label>
          <p-select
            inputId="auditTarget"
            [(ngModel)]="filterTargetType"
            [options]="targetTypeOptions()"
            [showClear]="true"
            [placeholder]="i18n.language() === 'French' ? 'Toutes' : 'All'"
          />
        </div>
        <div class="filter-field">
          <label for="auditActor">{{
            i18n.language() === 'French' ? 'Utilisateur' : 'User'
          }}</label>
          <p-select
            inputId="auditActor"
            [(ngModel)]="filterActorUserId"
            [options]="members()"
            optionValue="userId"
            optionLabel="displayName"
            [showClear]="true"
            [filter]="true"
            [placeholder]="i18n.language() === 'French' ? 'Tous' : 'All'"
          />
        </div>
        <div class="filter-field">
          <label for="auditFrom">{{ i18n.language() === 'French' ? 'Du' : 'From' }}</label>
          <input pInputText id="auditFrom" type="date" [(ngModel)]="filterFrom" />
        </div>
        <div class="filter-field">
          <label for="auditTo">{{ i18n.language() === 'French' ? 'Au' : 'To' }}</label>
          <input pInputText id="auditTo" type="date" [(ngModel)]="filterTo" />
        </div>
        <div class="filter-actions">
          <button pButton type="button" size="small" (click)="load()" [loading]="loading()">
            {{ i18n.language() === 'French' ? 'Filtrer' : 'Apply' }}
          </button>
          @if (hasFilters()) {
            <button
              pButton
              type="button"
              size="small"
              severity="secondary"
              [text]="true"
              (click)="clearFilters()"
            >
              {{ i18n.language() === 'French' ? 'Réinitialiser' : 'Clear' }}
            </button>
          }
        </div>
      </div>
    </section>
    <section class="content-panel">
      <p-table [value]="data().items" [loading]="loading()" [tableStyle]="{ 'min-width': '64rem' }"
        ><ng-template #header
          ><tr>
            <th>{{ i18n.text('common.updated') }}</th>
            <th>Action</th>
            <th>{{ i18n.language() === 'French' ? 'Cible' : 'Target' }}</th>
            <th>{{ i18n.language() === 'French' ? 'Résumé' : 'Summary' }}</th>
            <th>{{ i18n.language() === 'French' ? 'Corrélation' : 'Correlation' }}</th>
          </tr></ng-template
        ><ng-template #body let-event
          ><tr>
            <td>{{ event.createdAt | localizedDate: 'medium' }}</td>
            <td><p-tag [value]="event.action" severity="secondary" /></td>
            <td>{{ event.targetType }} · {{ event.targetId }}</td>
            <td>{{ event.summary }}</td>
            <td>
              <code>{{ event.correlationId || '—' }}</code>
            </td>
          </tr></ng-template
        ><ng-template #emptymessage
          ><tr>
            <td colspan="5">{{ i18n.text('common.empty') }}</td>
          </tr></ng-template
        ></p-table
      >
    </section>
  `,
  styles: `
    .filters-panel {
      margin-bottom: 1rem;
    }
    .filters {
      display: grid;
      grid-template-columns: repeat(5, minmax(0, 1fr)) auto;
      gap: 0.7rem;
      align-items: end;
    }
    .filter-field {
      display: grid;
      gap: 0.35rem;
    }
    .filter-field label {
      color: var(--app-text-muted);
      font-size: 0.78rem;
      font-weight: 650;
    }
    .filter-field p-select,
    .filter-field input {
      width: 100%;
    }
    .filter-actions {
      display: flex;
      gap: 0.5rem;
      align-items: center;
      padding-bottom: 0.15rem;
    }
    @media (max-width: 1100px) {
      .filters {
        grid-template-columns: repeat(2, minmax(0, 1fr));
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuditPage {
  private readonly api = inject(ApiClient);
  readonly i18n = inject(I18nService);
  readonly loading = signal(false);
  readonly data = signal<AuditData>({ items: [], page: 1, pageSize: 100, total: 0 });
  readonly members = signal<Member[]>([]);
  readonly actionOptions = signal<string[]>([]);
  readonly targetTypeOptions = signal<string[]>([]);
  filterAction = '';
  filterTargetType = '';
  filterActorUserId = '';
  filterFrom = '';
  filterTo = '';
  constructor() {
    void this.load();
    void firstValueFrom(this.api.members()).then((members) =>
      this.members.set(members.filter((member) => member.status !== 'Invited')),
    );
  }
  hasFilters(): boolean {
    return Boolean(
      this.filterAction ||
        this.filterTargetType ||
        this.filterActorUserId ||
        this.filterFrom ||
        this.filterTo,
    );
  }
  async clearFilters(): Promise<void> {
    this.filterAction = '';
    this.filterTargetType = '';
    this.filterActorUserId = '';
    this.filterFrom = '';
    this.filterTo = '';
    await this.load();
  }
  async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.data.set(await firstValueFrom(this.api.audit(this.currentFilters())));
      const items = this.data().items;
      const merge = (current: string[], values: string[]) =>
        [...new Set([...current, ...values])].sort();
      this.actionOptions.set(
        merge(
          this.actionOptions(),
          items.map((item) => item.action),
        ),
      );
      this.targetTypeOptions.set(
        merge(
          this.targetTypeOptions(),
          items.map((item) => item.targetType),
        ),
      );
    } finally {
      this.loading.set(false);
    }
  }
  async exportAudit(): Promise<void> {
    const events = await firstValueFrom(this.api.auditExport(this.currentFilters()));
    const url = URL.createObjectURL(
      new Blob([JSON.stringify(events, null, 2)], { type: 'application/json' }),
    );
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = 'reqnest-audit.json';
    anchor.click();
    URL.revokeObjectURL(url);
  }
  async exportCsv(): Promise<void> {
    const blob = await firstValueFrom(this.api.auditCsv(this.currentFilters()));
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = 'reqnest-audit.csv';
    anchor.click();
    URL.revokeObjectURL(url);
  }
  private currentFilters() {
    return {
      action: this.filterAction,
      targetType: this.filterTargetType,
      actorUserId: this.filterActorUserId,
      from: this.filterFrom ? new Date(this.filterFrom).toISOString() : '',
      to: this.filterTo ? new Date(`${this.filterTo}T23:59:59.999`).toISOString() : '',
    };
  }
}
