import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ApiClient } from '../../../core/api/api-client';
import { AuditPage as AuditData } from '../../../core/api/api-models';
import { I18nService } from '../../../core/i18n/i18n.service';
import { LocalizedDatePipe } from '../../../core/i18n/localized-date.pipe';

@Component({
  selector: 'app-audit-page',
  imports: [LocalizedDatePipe, ButtonModule, TableModule, TagModule],
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
        <button pButton type="button" severity="secondary" [outlined]="true" (click)="load()">
          {{ i18n.text('common.refresh') }}
        </button>
      </div>
    </header>
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
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuditPage {
  private readonly api = inject(ApiClient);
  readonly i18n = inject(I18nService);
  readonly loading = signal(false);
  readonly data = signal<AuditData>({ items: [], page: 1, pageSize: 100, total: 0 });
  constructor() {
    void this.load();
  }
  async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.data.set(await firstValueFrom(this.api.audit()));
    } finally {
      this.loading.set(false);
    }
  }
  async exportAudit(): Promise<void> {
    const events = await firstValueFrom(this.api.auditExport());
    const url = URL.createObjectURL(
      new Blob([JSON.stringify(events, null, 2)], { type: 'application/json' }),
    );
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = 'reqnest-audit.json';
    anchor.click();
    URL.revokeObjectURL(url);
  }
}
