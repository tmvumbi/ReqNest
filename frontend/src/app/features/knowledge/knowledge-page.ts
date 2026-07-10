import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { EditorModule } from 'primeng/editor';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { ApiClient } from '../../core/api/api-client';
import { KnowledgeArticle, Project } from '../../core/api/api-models';
import { I18nService } from '../../core/i18n/i18n.service';
import { SessionStore } from '../../core/session/session-store';

@Component({
  selector: 'app-knowledge-page',
  imports: [
    FormsModule,
    ButtonModule,
    EditorModule,
    InputTextModule,
    MessageModule,
    SelectModule,
    TagModule,
  ],
  templateUrl: './knowledge-page.html',
  styleUrl: './knowledge-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KnowledgePage {
  private readonly api = inject(ApiClient);
  readonly i18n = inject(I18nService);
  readonly store = inject(SessionStore);
  readonly articles = signal<KnowledgeArticle[]>([]);
  readonly projects = signal<Project[]>([]);
  readonly selected = signal<KnowledgeArticle | null>(null);
  readonly error = signal('');
  readonly success = signal('');
  readonly busy = signal(false);
  search = '';
  projectId = '';
  slug = '';
  titleEnglish = '';
  titleFrench = '';
  bodyEnglish = '';
  bodyFrench = '';
  visibility: KnowledgeArticle['visibility'] = 'Internal';
  readonly visibilities: KnowledgeArticle['visibility'][] = ['Internal', 'Requesters'];

  constructor() {
    void this.load();
  }
  text(en: string, fr: string): string {
    return this.i18n.language() === 'French' ? fr : en;
  }
  title(item: KnowledgeArticle): string {
    return this.text(item.titleEnglish, item.titleFrench);
  }
  edit(item: KnowledgeArticle): void {
    this.selected.set(item);
    this.projectId = item.projectId ?? '';
    this.slug = item.slug;
    this.titleEnglish = item.titleEnglish;
    this.titleFrench = item.titleFrench;
    this.bodyEnglish = item.bodyEnglish;
    this.bodyFrench = item.bodyFrench;
    this.visibility = item.visibility;
  }
  clear(): void {
    this.selected.set(null);
    this.projectId =
      this.slug =
      this.titleEnglish =
      this.titleFrench =
      this.bodyEnglish =
      this.bodyFrench =
        '';
    this.visibility = 'Internal';
  }

  async filter(): Promise<void> {
    this.articles.set(
      await firstValueFrom(this.api.knowledge(this.search, this.projectId || undefined)),
    );
  }
  async save(): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(
        this.api.saveKnowledge(
          {
            projectId: this.projectId || null,
            slug: this.slug,
            titleEnglish: this.titleEnglish,
            titleFrench: this.titleFrench,
            bodyEnglish: this.bodyEnglish,
            bodyFrench: this.bodyFrench,
            visibility: this.visibility,
          },
          this.selected()?.id,
        ),
      );
      this.clear();
      await this.filter();
    });
  }
  async status(item: KnowledgeArticle, status: KnowledgeArticle['status']): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.api.setKnowledgeStatus(item.id, status));
      await this.filter();
    });
  }
  private async load(): Promise<void> {
    try {
      const [articles, projects] = await Promise.all([
        firstValueFrom(this.api.knowledge()),
        firstValueFrom(this.api.projects()),
      ]);
      this.articles.set(articles);
      this.projects.set(projects);
    } catch {
      this.error.set(
        this.text('Could not load knowledge articles.', 'Impossible de charger les articles.'),
      );
    }
  }
  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    this.success.set('');
    try {
      await action();
      this.success.set(this.text('Knowledge base updated.', 'Base de connaissances mise à jour.'));
    } catch {
      this.error.set(
        this.text('The change could not be saved.', "La modification n'a pas pu être enregistrée."),
      );
    } finally {
      this.busy.set(false);
    }
  }
}
