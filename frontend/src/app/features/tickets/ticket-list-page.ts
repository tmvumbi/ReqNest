import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ApiClient } from '../../core/api/api-client';
import {
  PagedTickets,
  Project,
  SavedView,
  TicketListItem,
  TicketPriority,
} from '../../core/api/api-models';
import { I18nService } from '../../core/i18n/i18n.service';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { SessionStore } from '../../core/session/session-store';

@Component({
  selector: 'app-ticket-list-page',
  imports: [
    LocalizedDatePipe,
    FormsModule,
    ReactiveFormsModule,
    RouterLink,
    ButtonModule,
    InputTextModule,
    SelectModule,
    TableModule,
    TagModule,
  ],
  templateUrl: './ticket-list-page.html',
  styleUrl: './ticket-list-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TicketListPage {
  private readonly api = inject(ApiClient);
  private readonly formBuilder = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly i18n = inject(I18nService);
  readonly store = inject(SessionStore);
  readonly data = signal<PagedTickets>({ items: [], page: 1, pageSize: 50, total: 0 });
  readonly projects = signal<Project[]>([]);
  readonly loading = signal(true);
  readonly savedViews = signal<SavedView[]>([]);
  selectedTickets: TicketListItem[] = [];
  selectedViewId = '';
  viewName = '';
  bulkPriority: TicketPriority | null = null;
  readonly priorities: TicketPriority[] = ['Low', 'Normal', 'High', 'Urgent'];
  readonly queues = [
    { value: '', labelKey: 'common.all' as const },
    { value: 'my-open', labelKey: 'tickets.myOpen' as const },
    { value: 'unassigned', labelKey: 'tickets.unassigned' as const },
    { value: 'recently-updated', labelKey: 'tickets.recent' as const },
    { value: 'todo', labelKey: 'tickets.todo' as const },
    { value: 'in-progress', labelKey: 'tickets.inProgress' as const },
    { value: 'overdue', labelKey: 'tickets.overdue' as const },
    { value: 'sla-risk', labelKey: 'tickets.slaRisk' as const },
    { value: 'done-recently', labelKey: 'tickets.doneRecent' as const },
  ];
  readonly filterForm = this.formBuilder.nonNullable.group({
    search: [''],
    projectId: [''],
    queue: [''],
  });

  constructor() {
    void this.initialize();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    try {
      const filters = this.filterForm.getRawValue();
      this.data.set(await firstValueFrom(this.api.tickets(filters)));
      await this.router.navigate([], {
        relativeTo: this.route,
        queryParams: filters,
        replaceUrl: true,
      });
    } finally {
      this.loading.set(false);
    }
  }

  projectName(project: Project): string {
    return this.i18n.language() === 'French' ? project.nameFrench : project.nameEnglish;
  }
  ticketProjectName(ticket: PagedTickets['items'][number]): string {
    return this.i18n.language() === 'French' ? ticket.projectNameFrench : ticket.projectNameEnglish;
  }
  statusLabel(ticket: PagedTickets['items'][number]): string {
    return this.i18n.language() === 'French' ? ticket.statusLabelFrench : ticket.statusLabelEnglish;
  }
  prioritySeverity(priority: TicketPriority): 'danger' | 'warn' | 'info' | 'secondary' {
    return priority === 'Urgent'
      ? 'danger'
      : priority === 'High'
        ? 'warn'
        : priority === 'Normal'
          ? 'info'
          : 'secondary';
  }

  async saveCurrentView(): Promise<void> {
    const name = this.viewName.trim();
    if (!name) return;
    await firstValueFrom(
      this.api.saveView(
        name,
        this.filterForm.controls.projectId.value || null,
        this.filterForm.getRawValue(),
      ),
    );
    this.viewName = '';
    await this.loadViews();
  }

  async applyView(): Promise<void> {
    const view = this.savedViews().find((item) => item.id === this.selectedViewId);
    if (!view) return;
    const filters = JSON.parse(view.filtersJson) as {
      search?: string;
      projectId?: string;
      queue?: string;
    };
    this.filterForm.patchValue({
      search: filters.search ?? '',
      projectId: filters.projectId ?? '',
      queue: filters.queue ?? '',
    });
    await this.load();
  }

  async deleteView(): Promise<void> {
    if (!this.selectedViewId) return;
    await firstValueFrom(this.api.deleteSavedView(this.selectedViewId));
    this.selectedViewId = '';
    await this.loadViews();
  }

  async bulkUpdate(request: { priority?: TicketPriority; archived?: boolean }): Promise<void> {
    if (
      !this.selectedTickets.length ||
      !confirm(
        this.i18n.language() === 'French'
          ? `Modifier ${this.selectedTickets.length} ticket(s) ?`
          : `Update ${this.selectedTickets.length} ticket(s)?`,
      )
    )
      return;
    await firstValueFrom(
      this.api.bulkTickets({
        ticketIds: this.selectedTickets.map((ticket) => ticket.id),
        priority: request.priority ?? null,
        assigneeSpecified: false,
        assigneeUserId: null,
        labels: null,
        archived: request.archived ?? null,
      }),
    );
    this.selectedTickets = [];
    this.bulkPriority = null;
    await this.load();
  }

  private async initialize(): Promise<void> {
    const params = this.route.snapshot.queryParamMap;
    this.filterForm.patchValue({
      search: params.get('search') ?? '',
      projectId: params.get('projectId') ?? '',
      queue: params.get('queue') ?? '',
    });
    this.projects.set(await firstValueFrom(this.api.projects()));
    await this.loadViews();
    await this.load();
  }

  private async loadViews(): Promise<void> {
    this.savedViews.set(await firstValueFrom(this.api.savedViews()));
  }
}
