import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TextareaModule } from 'primeng/textarea';
import { ApiClient } from '../../core/api/api-client';
import {
  Member,
  Project,
  ProjectOverview,
  TicketPriority,
  Workflow,
} from '../../core/api/api-models';
import { I18nService } from '../../core/i18n/i18n.service';
import { SessionStore } from '../../core/session/session-store';

@Component({
  selector: 'app-projects-page',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    DialogModule,
    InputTextModule,
    SelectModule,
    TableModule,
    TagModule,
    TextareaModule,
  ],
  templateUrl: './projects-page.html',
  styleUrl: './projects-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectsPage {
  private readonly api = inject(ApiClient);
  private readonly formBuilder = inject(FormBuilder);
  readonly i18n = inject(I18nService);
  readonly store = inject(SessionStore);
  readonly projects = signal<Project[]>([]);
  readonly workflows = signal<Workflow[]>([]);
  readonly members = signal<Member[]>([]);
  readonly editing = signal<Project | null>(null);
  readonly overview = signal<ProjectOverview | null>(null);
  readonly overviewVisible = signal(false);
  readonly loading = signal(true);
  readonly dialogVisible = signal(false);
  readonly submitting = signal(false);
  readonly priorities: TicketPriority[] = ['Low', 'Normal', 'High', 'Urgent'];
  readonly form = this.formBuilder.nonNullable.group({
    key: ['', [Validators.required, Validators.pattern(/^[A-Za-z0-9_]{2,12}$/)]],
    nameEnglish: ['', Validators.required],
    nameFrench: ['', Validators.required],
    description: [''],
    workflowId: [''],
    defaultPriority: ['Normal' as TicketPriority],
    defaultAssigneeUserId: [''],
  });

  constructor() {
    void this.load();
  }

  openCreate(): void {
    this.editing.set(null);
    this.form.reset({
      key: '',
      nameEnglish: '',
      nameFrench: '',
      description: '',
      workflowId: '',
      defaultPriority: 'Normal',
      defaultAssigneeUserId: '',
    });
    this.form.controls.key.enable();
    this.dialogVisible.set(true);
  }

  openEdit(project: Project): void {
    this.editing.set(project);
    this.form.reset({
      key: project.key,
      nameEnglish: project.nameEnglish,
      nameFrench: project.nameFrench,
      description: project.description ?? '',
      workflowId: project.workflowId,
      defaultPriority: project.defaultPriority,
      defaultAssigneeUserId: project.defaultAssigneeUserId ?? '',
    });
    this.form.controls.key.disable();
    this.dialogVisible.set(true);
  }

  async submit(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.submitting()) return;
    this.submitting.set(true);
    try {
      const value = this.form.getRawValue();
      const editing = this.editing();
      if (editing)
        await firstValueFrom(
          this.api.updateProject(editing.id, {
            nameEnglish: value.nameEnglish,
            nameFrench: value.nameFrench,
            description: value.description || null,
            defaultPriority: value.defaultPriority,
            defaultAssigneeUserId: value.defaultAssigneeUserId || null,
          }),
        );
      else
        await firstValueFrom(
          this.api.createProject({
            ...value,
            workflowId: value.workflowId || null,
            defaultAssigneeUserId: value.defaultAssigneeUserId || null,
          }),
        );
      this.dialogVisible.set(false);
      this.form.controls.key.enable();
      await this.load();
    } finally {
      this.submitting.set(false);
    }
  }

  async showOverview(project: Project): Promise<void> {
    this.overview.set(await firstValueFrom(this.api.projectOverview(project.id)));
    this.overviewVisible.set(true);
  }
  eligibleMembers(project?: Project | null): Member[] {
    return this.members().filter(
      (member) =>
        member.status === 'Active' &&
        (!project ||
          member.grants.some(
            (grant) => grant.allProjects || grant.projectIds.includes(project.id),
          )),
    );
  }

  async toggleArchived(project: Project): Promise<void> {
    await firstValueFrom(this.api.setProjectArchived(project.id, !project.isArchived));
    await this.load();
  }

  name(project: Project): string {
    return this.i18n.language() === 'French' ? project.nameFrench : project.nameEnglish;
  }
  workflowName(id: string): string {
    return this.workflows().find((workflow) => workflow.id === id)?.name ?? '—';
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      const [projects, workflows, members] = await Promise.all([
        firstValueFrom(this.api.projects()),
        firstValueFrom(this.api.workflows()),
        firstValueFrom(this.api.members()).catch(() => []),
      ]);
      this.projects.set(projects);
      this.workflows.set(workflows);
      this.members.set(members);
    } finally {
      this.loading.set(false);
    }
  }
}
