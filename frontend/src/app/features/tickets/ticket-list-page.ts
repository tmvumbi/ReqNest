import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import {
  CdkDrag,
  CdkDragDrop,
  CdkDropList,
  DragDropModule,
  transferArrayItem,
} from '@angular/cdk/drag-drop';
import { FormBuilder, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { ApiClient } from '../../core/api/api-client';
import {
  PagedTickets,
  Project,
  TicketListItem,
  TicketPriority,
  Workflow,
  WorkflowStatus,
} from '../../core/api/api-models';
import { I18nService } from '../../core/i18n/i18n.service';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { SessionStore } from '../../core/session/session-store';
import { Icon } from '../../layout/icons/icon';

interface BoardColumn {
  key: string;
  label: string;
  category: WorkflowStatus['category'];
  color: string;
  order: number;
  tickets: TicketListItem[];
}

@Component({
  selector: 'app-ticket-list-page',
  imports: [
    LocalizedDatePipe,
    DragDropModule,
    FormsModule,
    ReactiveFormsModule,
    RouterLink,
    ButtonModule,
    InputTextModule,
    SelectModule,
    TableModule,
    Icon,
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
  readonly workflows = signal<Workflow[]>([]);
  readonly columns = signal<BoardColumn[]>([]);
  readonly view = signal<'board' | 'list'>('board');
  readonly loading = signal(true);
  selectedTickets: TicketListItem[] = [];
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
      this.buildColumns();
      await this.router.navigate([], {
        relativeTo: this.route,
        queryParams: { ...filters, view: this.view() },
        replaceUrl: true,
      });
    } finally {
      this.loading.set(false);
    }
  }

  setView(view: 'board' | 'list'): void {
    if (this.view() === view) return;
    this.view.set(view);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { view },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  columnLabel(column: BoardColumn): string {
    return column.label;
  }

  readonly boardEnterPredicate = (drag: CdkDrag, drop: CdkDropList): boolean => {
    const ticket = drag.data as TicketListItem;
    const column = drop.data as BoardColumn;
    return this.targetTransition(ticket, column) !== null;
  };

  async onBoardDrop(event: CdkDragDrop<BoardColumn>): Promise<void> {
    const ticket = event.item.data as TicketListItem;
    const source = event.previousContainer.data;
    const target = event.container.data;
    if (source.key === target.key) return;
    const resolved = this.targetTransition(ticket, target);
    if (!resolved) return;
    let comment: string | null = null;
    if (resolved.commentRequired) {
      comment = prompt(this.i18n.text('tickets.commentRequired'));
      if (!comment?.trim()) return;
    }
    transferArrayItem(source.tickets, target.tickets, event.previousIndex, event.currentIndex);
    ticket.statusId = resolved.status.id;
    ticket.statusKey = resolved.status.key;
    ticket.statusLabel = resolved.status.label;
    this.columns.update((columns) => [...columns]);
    try {
      const updated = await firstValueFrom(
        this.api.transition(ticket.id, resolved.status.id, ticket.version, comment),
      );
      ticket.version = updated.version;
    } catch {
      await this.load();
    }
  }

  private targetTransition(
    ticket: TicketListItem,
    column: BoardColumn,
  ): { status: WorkflowStatus; commentRequired: boolean } | null {
    if (!this.store.canMaintainProject(ticket.projectId)) return null;
    const workflow = this.workflowForProject(ticket.projectId);
    const status = workflow?.statuses.find((item) => item.key === column.key);
    if (!workflow || !status || status.id === ticket.statusId) return null;
    const transition = workflow.transitions.find(
      (item) => item.fromStatusId === ticket.statusId && item.toStatusId === status.id,
    );
    return transition ? { status, commentRequired: transition.commentRequired } : null;
  }

  private workflowForProject(projectId: string): Workflow | undefined {
    const workflowId = this.projects().find((project) => project.id === projectId)?.workflowId;
    return this.workflows().find((workflow) => workflow.id === workflowId);
  }

  private buildColumns(): void {
    const projectId = this.filterForm.controls.projectId.value;
    const projectIds = projectId
      ? [projectId]
      : [...new Set(this.data().items.map((ticket) => ticket.projectId))];
    let statuses = projectIds
      .flatMap((id) => this.workflowForProject(id)?.statuses ?? [])
      .slice()
      .sort((a, b) => a.order - b.order);
    if (!statuses.length)
      statuses = this.workflows()
        .flatMap((workflow) => workflow.statuses)
        .sort((a, b) => a.order - b.order);
    const columns = new Map<string, BoardColumn>();
    for (const status of statuses) {
      if (!columns.has(status.key))
        columns.set(status.key, {
          key: status.key,
          label: status.label,
          category: status.category,
          color: status.color,
          order: status.order,
          tickets: [],
        });
    }
    for (const ticket of this.data().items) {
      const column = columns.get(ticket.statusKey);
      if (column) column.tickets.push(ticket);
    }
    const categoryRank = { ToDo: 0, InProgress: 1, Done: 2 } as const;
    this.columns.set(
      [...columns.values()].sort(
        (a, b) => categoryRank[a.category] - categoryRank[b.category] || a.order - b.order,
      ),
    );
  }

  projectName(project: Project): string {
    return project.name;
  }
  ticketProjectName(ticket: PagedTickets['items'][number]): string {
    return ticket.projectName;
  }
  statusLabel(ticket: PagedTickets['items'][number]): string {
    return ticket.statusLabel;
  }
  priorityPill(priority: TicketPriority): string {
    return { Urgent: 'pill-red', High: 'pill-amber', Normal: 'pill-blue', Low: 'pill-slate' }[
      priority
    ];
  }
  statusPill(ticket: PagedTickets['items'][number]): string {
    const key = ticket.statusKey.toUpperCase();
    if (key.includes('DONE') || key.includes('RESOLVED') || key.includes('CLOSED'))
      return 'pill-green';
    if (key.includes('PROGRESS') || key.includes('REVIEW')) return 'pill-blue';
    return 'pill-slate';
  }
  initials(name: string): string {
    const parts = name.trim().split(/\s+/);
    return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase() || '?';
  }

  async bulkUpdate(request: { priority?: TicketPriority; archived?: boolean }): Promise<void> {
    if (!this.selectedTickets.length) return;
    const payload = {
      ticketIds: this.selectedTickets.map((ticket) => ticket.id),
      priority: request.priority ?? null,
      assigneeSpecified: false,
      assigneeUserId: null,
      labels: null,
      archived: request.archived ?? null,
    };
    const preview = await firstValueFrom(this.api.previewBulkTickets(payload));
    if (
      !confirm(
        this.i18n.language() === 'French'
          ? `Modifier ${preview.updated} ticket(s) ? ${preview.failures.length} échec(s) prévu(s).`
          : `Update ${preview.updated} ticket(s)? ${preview.failures.length} expected failure(s).`,
      )
    )
      return;
    await firstValueFrom(this.api.bulkTickets(payload));
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
    if (params.get('view') === 'list') this.view.set('list');
    const [projects, workflows] = await Promise.all([
      firstValueFrom(this.api.projects()),
      firstValueFrom(this.api.workflows()),
    ]);
    this.projects.set(projects);
    this.workflows.set(workflows);
    await this.load();
  }
}
