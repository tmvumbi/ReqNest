import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { ApiClient } from '../../../core/api/api-client';
import { Project, Workflow, WorkflowStatus } from '../../../core/api/api-models';
import { I18nService } from '../../../core/i18n/i18n.service';

type StatusCategory = 'ToDo' | 'InProgress' | 'Done';
interface StatusDraft {
  key: string;
  labelEnglish: string;
  labelFrench: string;
  category: StatusCategory;
  color: string;
  isInitial: boolean;
  isTerminal: boolean;
}
interface TransitionDraft {
  fromKey: string;
  toKey: string;
  nameEnglish: string;
  nameFrench: string;
  commentRequired: boolean;
}

@Component({
  selector: 'app-workflows-page',
  imports: [
    FormsModule,
    ButtonModule,
    CardModule,
    CheckboxModule,
    DialogModule,
    InputTextModule,
    SelectModule,
    TagModule,
  ],
  templateUrl: './workflows-page.html',
  styleUrl: './workflows-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WorkflowsPage {
  private readonly api = inject(ApiClient);
  readonly i18n = inject(I18nService);
  readonly workflows = signal<Workflow[]>([]);
  readonly projects = signal<Project[]>([]);
  readonly copyVisible = signal(false);
  readonly editorVisible = signal(false);
  readonly source = signal<Workflow | null>(null);
  readonly editing = signal<Workflow | null>(null);
  readonly saving = signal(false);
  readonly categories: StatusCategory[] = ['ToDo', 'InProgress', 'Done'];
  selectedProject = '';
  copyName = '';
  draftName = '';
  draftDescription = '';
  draftProjectId = '';
  draftActive = true;
  draftStatuses: StatusDraft[] = [];
  draftTransitions: TransitionDraft[] = [];
  statusMappings: Record<string, string> = {};

  constructor() {
    void this.load();
  }

  openCopy(workflow: Workflow): void {
    this.source.set(workflow);
    this.copyName = `${workflow.name} copy`;
    this.selectedProject = '';
    this.copyVisible.set(true);
  }

  openCreate(): void {
    this.editing.set(null);
    this.draftName = '';
    this.draftDescription = '';
    this.draftProjectId = '';
    this.draftActive = true;
    this.draftStatuses = [
      {
        key: 'TODO',
        labelEnglish: 'TODO',
        labelFrench: 'À FAIRE',
        category: 'ToDo',
        color: '#64748b',
        isInitial: true,
        isTerminal: false,
      },
      {
        key: 'IN_PROGRESS',
        labelEnglish: 'IN PROGRESS',
        labelFrench: 'EN COURS',
        category: 'InProgress',
        color: '#2563eb',
        isInitial: false,
        isTerminal: false,
      },
      {
        key: 'DONE',
        labelEnglish: 'DONE',
        labelFrench: 'TERMINÉ',
        category: 'Done',
        color: '#16a34a',
        isInitial: false,
        isTerminal: true,
      },
    ];
    this.draftTransitions = this.adjacentTransitions(this.draftStatuses);
    this.statusMappings = {};
    this.editorVisible.set(true);
  }

  openEdit(workflow: Workflow): void {
    this.editing.set(workflow);
    this.draftName = workflow.name;
    this.draftDescription = workflow.description ?? '';
    this.draftProjectId = workflow.projectId ?? '';
    this.draftActive = workflow.isActive;
    this.draftStatuses = workflow.statuses.map((status) => ({
      key: status.key,
      labelEnglish: status.labelEnglish,
      labelFrench: status.labelFrench,
      category: status.category,
      color: status.color,
      isInitial: status.isInitial,
      isTerminal: status.isTerminal,
    }));
    this.draftTransitions = workflow.transitions.map((transition) => {
      const from =
        workflow.statuses.find((status) => status.id === transition.fromStatusId)?.key ?? '';
      const to = workflow.statuses.find((status) => status.id === transition.toStatusId)?.key ?? '';
      return {
        fromKey: from,
        toKey: to,
        nameEnglish: transition.nameEnglish ?? '',
        nameFrench: transition.nameFrench ?? '',
        commentRequired: transition.commentRequired,
      };
    });
    this.statusMappings = {};
    this.editorVisible.set(true);
  }

  addStatus(): void {
    const index = this.draftStatuses.length + 1;
    this.draftStatuses = [
      ...this.draftStatuses,
      {
        key: `STATUS_${index}`,
        labelEnglish: `Status ${index}`,
        labelFrench: `Statut ${index}`,
        category: 'InProgress',
        color: '#7c3aed',
        isInitial: false,
        isTerminal: false,
      },
    ];
  }
  removeStatus(index: number): void {
    this.draftStatuses = this.draftStatuses.filter((_, itemIndex) => itemIndex !== index);
  }
  setInitial(index: number): void {
    this.draftStatuses = this.draftStatuses.map((status, itemIndex) => ({
      ...status,
      isInitial: itemIndex === index,
    }));
  }
  addTransition(): void {
    const first = this.draftStatuses[0]?.key ?? '';
    const second = this.draftStatuses[1]?.key ?? first;
    this.draftTransitions = [
      ...this.draftTransitions,
      { fromKey: first, toKey: second, nameEnglish: '', nameFrench: '', commentRequired: false },
    ];
  }
  removeTransition(index: number): void {
    this.draftTransitions = this.draftTransitions.filter((_, itemIndex) => itemIndex !== index);
  }
  removedStatuses(): WorkflowStatus[] {
    const keys = new Set(this.draftStatuses.map((status) => status.key));
    return this.editing()?.statuses.filter((status) => !keys.has(status.key)) ?? [];
  }
  isExistingKey(key: string): boolean {
    return this.editing()?.statuses.some((status) => status.key === key) ?? false;
  }

  async copy(): Promise<void> {
    const source = this.source();
    if (!source || !this.selectedProject) return;
    this.saving.set(true);
    try {
      await firstValueFrom(this.api.copyWorkflow(source.id, this.selectedProject, this.copyName));
      this.copyVisible.set(false);
      await this.load();
    } finally {
      this.saving.set(false);
    }
  }

  async saveWorkflow(): Promise<void> {
    if (!this.validDraft() || this.saving()) return;
    this.saving.set(true);
    const statuses = this.draftStatuses.map((status, order) => ({
      ...status,
      key: status.key.trim().toUpperCase().replaceAll(' ', '_'),
      order,
    }));
    const transitions = this.draftTransitions.map((transition) => ({
      ...transition,
      nameEnglish: transition.nameEnglish || null,
      nameFrench: transition.nameFrench || null,
    }));
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
            statusMappings: this.statusMappings,
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
      this.editorVisible.set(false);
      await this.load();
    } finally {
      this.saving.set(false);
    }
  }

  validDraft(): boolean {
    const keys = this.draftStatuses.map((status) =>
      status.key.trim().toUpperCase().replaceAll(' ', '_'),
    );
    return (
      Boolean(this.draftName.trim()) &&
      this.draftStatuses.some((status) => status.isInitial && !status.isTerminal) &&
      this.draftStatuses.some((status) => status.isTerminal) &&
      new Set(keys).size === keys.length &&
      this.removedStatuses().every((status) => Boolean(this.statusMappings[status.key]))
    );
  }
  label(status: WorkflowStatus): string {
    return this.i18n.language() === 'French' ? status.labelFrench : status.labelEnglish;
  }
  projectName(project: Project): string {
    return this.i18n.language() === 'French' ? project.nameFrench : project.nameEnglish;
  }
  categoryLabel(category: StatusCategory): string {
    if (this.i18n.language() !== 'French')
      return { ToDo: 'To do', InProgress: 'In progress', Done: 'Done' }[category];
    return { ToDo: 'À faire', InProgress: 'En cours', Done: 'Terminé' }[category];
  }

  private adjacentTransitions(statuses: StatusDraft[]): TransitionDraft[] {
    const transitions: TransitionDraft[] = [];
    for (let index = 0; index < statuses.length - 1; index++) {
      transitions.push({
        fromKey: statuses[index].key,
        toKey: statuses[index + 1].key,
        nameEnglish: '',
        nameFrench: '',
        commentRequired: false,
      });
      transitions.push({
        fromKey: statuses[index + 1].key,
        toKey: statuses[index].key,
        nameEnglish: '',
        nameFrench: '',
        commentRequired: false,
      });
    }
    return transitions;
  }

  private async load(): Promise<void> {
    const [workflows, projects] = await Promise.all([
      firstValueFrom(this.api.workflows()),
      firstValueFrom(this.api.projects()),
    ]);
    this.workflows.set(workflows);
    this.projects.set(projects.filter((project) => !project.isArchived));
  }
}
