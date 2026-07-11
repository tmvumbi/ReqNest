import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { ApiClient } from '../../core/api/api-client';
import { KnowledgeArticle, Project } from '../../core/api/api-models';
import { I18nService } from '../../core/i18n/i18n.service';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { SessionStore } from '../../core/session/session-store';
import { Icon } from '../../layout/icons/icon';

@Component({
  selector: 'app-knowledge-page',
  imports: [
    FormsModule,
    RouterLink,
    ButtonModule,
    MessageModule,
    SelectModule,
    LocalizedDatePipe,
    Icon,
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
  readonly error = signal('');
  search = '';
  projectId = '';

  constructor() {
    void this.load();
  }
  text(en: string, fr: string): string {
    return this.i18n.language() === 'French' ? fr : en;
  }
  title(item: KnowledgeArticle): string {
    return item.title;
  }

  async filter(): Promise<void> {
    this.articles.set(
      await firstValueFrom(this.api.knowledge(this.search, this.projectId || undefined)),
    );
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
}
