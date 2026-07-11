import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { ApiClient } from '../../../core/api/api-client';
import { Project, Workflow, WorkflowStatus } from '../../../core/api/api-models';
import { I18nService } from '../../../core/i18n/i18n.service';

@Component({
  selector: 'app-workflows-page',
  imports: [
    FormsModule,
    RouterLink,
    ButtonModule,
    CardModule,
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
  readonly source = signal<Workflow | null>(null);
  readonly saving = signal(false);
  selectedProject = '';
  copyName = '';

  constructor() {
    void this.load();
  }

  openCopy(workflow: Workflow): void {
    this.source.set(workflow);
    this.copyName = `${workflow.name} copy`;
    this.selectedProject = '';
    this.copyVisible.set(true);
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

  label(status: WorkflowStatus): string {
    return status.label;
  }
  projectName(project: Project): string {
    return project.name;
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
