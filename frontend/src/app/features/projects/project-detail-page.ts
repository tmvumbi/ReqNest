import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { MessageModule } from 'primeng/message';
import { ApiClient } from '../../core/api/api-client';
import { Member, Project, ProjectOverview, Workflow } from '../../core/api/api-models';
import { I18nService } from '../../core/i18n/i18n.service';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { SessionStore } from '../../core/session/session-store';

@Component({
  selector: 'app-project-detail-page',
  imports: [LocalizedDatePipe, RouterLink, MessageModule],
  templateUrl: './project-detail-page.html',
  styleUrl: './project-detail-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectDetailPage {
  private readonly api = inject(ApiClient);
  private projectId!: string;
  readonly i18n = inject(I18nService);
  readonly store = inject(SessionStore);
  readonly project = signal<Project | null>(null);
  readonly overview = signal<ProjectOverview | null>(null);
  readonly workflows = signal<Workflow[]>([]);
  readonly members = signal<Member[]>([]);
  readonly loading = signal(true);
  readonly error = signal(false);

  constructor() {
    // Re-run on param changes: the router reuses this component when
    // navigating between projects (e.g. assistant links).
    inject(ActivatedRoute)
      .paramMap.pipe(takeUntilDestroyed())
      .subscribe((params) => {
        this.projectId = params.get('projectId')!;
        void this.load();
      });
  }

  workflowName(id: string): string {
    return this.workflows().find((workflow) => workflow.id === id)?.name ?? '—';
  }

  memberName(userId: string | null): string {
    if (!userId) return '—';
    return this.members().find((member) => member.userId === userId)?.displayName ?? '—';
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(false);
    try {
      const [project, overview, workflows, members] = await Promise.all([
        firstValueFrom(this.api.project(this.projectId)),
        firstValueFrom(this.api.projectOverview(this.projectId)),
        firstValueFrom(this.api.workflows()).catch(() => [] as Workflow[]),
        firstValueFrom(this.api.members()).catch(() => [] as Member[]),
      ]);
      this.project.set(project);
      this.overview.set(overview);
      this.workflows.set(workflows);
      this.members.set(members);
    } catch {
      this.error.set(true);
    } finally {
      this.loading.set(false);
    }
  }
}
