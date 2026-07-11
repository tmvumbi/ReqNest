import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ConfirmationService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { ApiClient } from '../../../core/api/api-client';
import { Project, Workflow, WorkflowStatus } from '../../../core/api/api-models';
import { I18nService } from '../../../core/i18n/i18n.service';
import { Icon } from '../../../layout/icons/icon';

type StatusCategory = 'ToDo' | 'InProgress' | 'Done';
interface StatusDraft {
  key: string;
  label: string;
  category: StatusCategory;
  color: string;
  isInitial: boolean;
  isTerminal: boolean;
}
// Transitions and mappings reference status objects (not key strings) so that
// renaming a status key or label never leaves them pointing at a stale value.
interface TransitionDraft {
  from: StatusDraft;
  to: StatusDraft;
  name: string;
  commentRequired: boolean;
}

@Component({
  selector: 'app-workflow-editor-page',
  imports: [
    DragDropModule,
    FormsModule,
    RouterLink,
    ButtonModule,
    CheckboxModule,
    ConfirmDialogModule,
    InputTextModule,
    MessageModule,
    SelectModule,
    Icon,
  ],
  providers: [ConfirmationService],
  templateUrl: './workflow-editor-page.html',
  styleUrl: './workflow-editor-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WorkflowEditorPage {
  private readonly api = inject(ApiClient);
  private readonly router = inject(Router);
  private readonly confirmation = inject(ConfirmationService);
  readonly i18n = inject(I18nService);
  readonly workflowId = inject(ActivatedRoute).snapshot.paramMap.get('workflowId');
  readonly projects = signal<Project[]>([]);
  readonly editing = signal<Workflow | null>(null);
  readonly saving = signal(false);
  readonly error = signal(false);
  readonly statusUsage = signal<Record<string, number>>({});
  readonly categories: StatusCategory[] = ['ToDo', 'InProgress', 'Done'];
  draftName = '';
  draftDescription = '';
  draftProjectId = '';
  draftActive = true;
  draftStatuses: StatusDraft[] = [];
  draftTransitions: TransitionDraft[] = [];
  statusMappings: Record<string, StatusDraft | undefined> = {};

  constructor() {
    void this.load();
  }

  text(en: string, fr: string): string {
    return this.i18n.language() === 'French' ? fr : en;
  }

  addStatus(): void {
    const index = this.draftStatuses.length + 1;
    this.draftStatuses = [
      ...this.draftStatuses,
      {
        key: `STATUS_${index}`,
        label: `Status ${index}`,
        category: 'InProgress',
        color: '#d0471b',
        isInitial: false,
        isTerminal: false,
      },
    ];
    this.ensureNaturalTransitions();
  }
  statusTicketCount(status: StatusDraft): number {
    return this.statusUsage()[status.key] ?? 0;
  }
  removeStatus(index: number): void {
    const status = this.draftStatuses[index];
    const tickets = this.statusTicketCount(status);
    if (tickets > 0) {
      this.confirmation.confirm({
        header: this.text('Cannot remove status', 'Suppression impossible'),
        message: this.text(
          `"${status.label}" is used by ${tickets} ticket(s) and cannot be removed.`,
          `« ${status.label} » est utilisé par ${tickets} ticket(s) et ne peut pas être supprimé.`,
        ),
        acceptLabel: 'OK',
        rejectVisible: false,
        acceptButtonStyleClass: 'p-button-secondary',
      });
      return;
    }

    this.confirmation.confirm({
      header: this.text('Remove status', 'Supprimer le statut'),
      message: this.text(
        `Remove the status "${status.label}"?`,
        `Supprimer le statut « ${status.label} » ?`,
      ),
      acceptLabel: this.text('Remove', 'Supprimer'),
      rejectLabel: this.i18n.text('common.cancel'),
      acceptButtonStyleClass: 'p-button-danger',
      rejectButtonStyleClass: 'p-button-secondary p-button-outlined',
      accept: () => {
        this.draftStatuses = this.draftStatuses.filter((_, itemIndex) => itemIndex !== index);
        this.draftTransitions = this.draftTransitions.filter(
          (transition) => transition.from !== status && transition.to !== status,
        );
        this.ensureNaturalTransitions();
      },
    });
  }
  dropStatus(event: CdkDragDrop<StatusDraft[]>): void {
    if (event.previousIndex === event.currentIndex) return;
    const statuses = [...this.draftStatuses];
    moveItemInArray(statuses, event.previousIndex, event.currentIndex);
    this.draftStatuses = statuses;
    this.ensureNaturalTransitions();
  }
  moveStatus(index: number, delta: -1 | 1): void {
    const target = index + delta;
    if (target < 0 || target >= this.draftStatuses.length) return;
    const statuses = [...this.draftStatuses];
    [statuses[index], statuses[target]] = [statuses[target], statuses[index]];
    this.draftStatuses = statuses;
    this.ensureNaturalTransitions();
  }
  private ensureNaturalTransitions(): void {
    const additions: TransitionDraft[] = [];
    for (let index = 0; index < this.draftStatuses.length - 1; index++) {
      const from = this.draftStatuses[index];
      const to = this.draftStatuses[index + 1];
      for (const [a, b] of [
        [from, to],
        [to, from],
      ]) {
        const exists = [...this.draftTransitions, ...additions].some(
          (transition) => transition.from === a && transition.to === b,
        );
        if (!exists) {
          additions.push({ from: a, to: b, name: '', commentRequired: false });
        }
      }
    }

    if (additions.length) {
      this.draftTransitions = [...this.draftTransitions, ...additions];
    }
  }
  setInitial(index: number): void {
    this.draftStatuses = this.draftStatuses.map((status, itemIndex) => ({
      ...status,
      isInitial: itemIndex === index,
    }));
  }
  addTransition(): void {
    const first = this.draftStatuses[0];
    if (!first) return;
    const second = this.draftStatuses[1] ?? first;
    this.draftTransitions = [
      ...this.draftTransitions,
      { from: first, to: second, name: '', commentRequired: false },
    ];
  }
  removeTransition(index: number): void {
    const transition = this.draftTransitions[index];
    const from = transition.from.label || transition.from.key;
    const to = transition.to.label || transition.to.key;
    this.confirmation.confirm({
      header: this.text('Remove transition', 'Supprimer la transition'),
      message: this.text(
        `Remove the transition "${from} → ${to}"?`,
        `Supprimer la transition « ${from} → ${to} » ?`,
      ),
      acceptLabel: this.text('Remove', 'Supprimer'),
      rejectLabel: this.i18n.text('common.cancel'),
      acceptButtonStyleClass: 'p-button-danger',
      rejectButtonStyleClass: 'p-button-secondary p-button-outlined',
      accept: () => {
        this.draftTransitions = this.draftTransitions.filter(
          (_, itemIndex) => itemIndex !== index,
        );
      },
    });
  }
  removedStatuses(): WorkflowStatus[] {
    const keys = new Set(this.draftStatuses.map((status) => status.key));
    return this.editing()?.statuses.filter((status) => !keys.has(status.key)) ?? [];
  }
  isExistingKey(key: string): boolean {
    return this.editing()?.statuses.some((status) => status.key === key) ?? false;
  }

  async saveWorkflow(): Promise<void> {
    if (!this.validDraft() || this.saving()) return;
    this.saving.set(true);
    this.error.set(false);
    const statuses = this.draftStatuses.map((status, order) => ({
      ...status,
      key: normalizeKey(status.key),
      order,
    }));
    const transitions = this.draftTransitions.map((transition) => ({
      fromKey: normalizeKey(transition.from.key),
      toKey: normalizeKey(transition.to.key),
      name: transition.name || null,
      commentRequired: transition.commentRequired,
    }));
    const statusMappings: Record<string, string> = {};
    for (const removed of this.removedStatuses()) {
      const target = this.statusMappings[removed.key];
      if (target) statusMappings[removed.key] = normalizeKey(target.key);
    }
    try {
      const editing = this.editing();
      if (editing)
        await firstValueFrom(
          this.api.updateWorkflow(editing.id, {
            name: this.draftName,
            description: this.draftDescription || null,
            isActive: this.draftActive,
            statuses,
            transitions,
            statusMappings,
          }),
        );
      else
        await firstValueFrom(
          this.api.createWorkflow({
            name: this.draftName,
            description: this.draftDescription || null,
            projectId: this.draftProjectId || null,
            statuses,
            transitions,
          }),
        );
      await this.router.navigate(['/app/admin/workflows']);
    } catch {
      this.error.set(true);
    } finally {
      this.saving.set(false);
    }
  }

  validDraft(): boolean {
    const keys = this.draftStatuses.map((status) => normalizeKey(status.key));
    return (
      Boolean(this.draftName.trim()) &&
      keys.every((key) => key.length > 0) &&
      this.draftStatuses.some((status) => status.isInitial && !status.isTerminal) &&
      this.draftStatuses.some((status) => status.isTerminal) &&
      new Set(keys).size === keys.length &&
      this.removedStatuses().every((status) => Boolean(this.statusMappings[status.key]))
    );
  }
  label(status: WorkflowStatus): string {
    return status.label;
  }
  projectName(project: Project): string {
    return project.name;
  }
  categoryLabel(category: StatusCategory): string {
    if (this.i18n.language() !== 'French')
      return { ToDo: 'To do', InProgress: 'In progress', Done: 'Done' }[category];
    return { ToDo: 'À faire', InProgress: 'En cours', Done: 'Terminé' }[category];
  }

  private async load(): Promise<void> {
    const [workflows, projects] = await Promise.all([
      firstValueFrom(this.api.workflows()),
      firstValueFrom(this.api.projects()),
    ]);
    this.projects.set(projects.filter((project) => !project.isArchived));
    if (!this.workflowId) {
      this.initializeNewDraft();
      return;
    }
    const workflow = workflows.find((item) => item.id === this.workflowId);
    if (!workflow) {
      await this.router.navigate(['/app/admin/workflows']);
      return;
    }
    this.draftName = workflow.name;
    this.draftDescription = workflow.description ?? '';
    this.draftProjectId = workflow.projectId ?? '';
    this.draftActive = workflow.isActive;
    this.draftStatuses = [...workflow.statuses]
      .sort((a, b) => a.order - b.order)
      .map((status) => ({
        key: status.key,
        label: status.label,
        category: status.category,
        color: status.color,
        isInitial: status.isInitial,
        isTerminal: status.isTerminal,
      }));
    const draftByStatusId = new Map(
      workflow.statuses.map((status) => [
        status.id,
        this.draftStatuses.find((draft) => draft.key === status.key),
      ]),
    );
    this.draftTransitions = workflow.transitions.flatMap((transition) => {
      const from = draftByStatusId.get(transition.fromStatusId);
      const to = draftByStatusId.get(transition.toStatusId);
      if (!from || !to) return [];
      return [{ from, to, name: transition.name ?? '', commentRequired: transition.commentRequired }];
    });
    this.editing.set(workflow);
    try {
      this.statusUsage.set(await firstValueFrom(this.api.workflowStatusUsage(workflow.id)));
    } catch {
      this.statusUsage.set({});
    }
  }

  private initializeNewDraft(): void {
    this.draftStatuses = [
      {
        key: 'TODO',
        label: 'TODO',
        category: 'ToDo',
        color: '#64748b',
        isInitial: true,
        isTerminal: false,
      },
      {
        key: 'IN_PROGRESS',
        label: 'IN PROGRESS',
        category: 'InProgress',
        color: '#2563eb',
        isInitial: false,
        isTerminal: false,
      },
      {
        key: 'DONE',
        label: 'DONE',
        category: 'Done',
        color: '#16a34a',
        isInitial: false,
        isTerminal: true,
      },
    ];
    this.draftTransitions = [];
    this.ensureNaturalTransitions();
  }
}

function normalizeKey(key: string): string {
  return key.trim().toUpperCase().replaceAll(' ', '_');
}
