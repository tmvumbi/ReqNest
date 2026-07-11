import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { ApiClient } from '../../core/api/api-client';
import { KnowledgeArticle, Project } from '../../core/api/api-models';
import { RichHtmlPipe } from '../../core/content/rich-html.pipe';
import { I18nService } from '../../core/i18n/i18n.service';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { SessionStore } from '../../core/session/session-store';

@Component({
  selector: 'app-knowledge-detail-page',
  imports: [RouterLink, ButtonModule, MessageModule, LocalizedDatePipe, RichHtmlPipe],
  template: `
    <a routerLink="/app/knowledge" class="back-link"
      >← {{ text('Knowledge base', 'Base de connaissances') }}</a
    >
    @if (error()) {
      <p-message severity="error">{{ error() }}</p-message>
    }
    @if (article(); as item) {
      <header class="detail-header content-panel">
        <div>
          <h1>{{ item.title }}</h1>
          <div class="meta-row">
            <span
              class="pill"
              [class]="
                item.status === 'Published'
                  ? 'pill-green'
                  : item.status === 'Archived'
                    ? 'pill-slate'
                    : 'pill-amber'
              "
              >{{ item.status }}</span
            >
            <span class="pill pill-violet">{{ item.visibility }}</span>
            <span class="slug">/{{ item.slug }}</span>
            @if (projectName(); as name) {
              <span class="meta-item">{{ name }}</span>
            }
            <time>{{ item.updatedAt | localizedDate: 'medium' }}</time>
          </div>
        </div>
        @if (store.canManageProjects()) {
          <div class="heading-actions">
            @if (item.status !== 'Published') {
              <button pButton type="button" size="small" [loading]="busy()" (click)="setStatus(item, 'Published')">
                {{ text('Publish', 'Publier') }}
              </button>
            } @else {
              <button
                pButton
                type="button"
                size="small"
                severity="secondary"
                [outlined]="true"
                [loading]="busy()"
                (click)="setStatus(item, 'Archived')"
              >
                {{ text('Archive', 'Archiver') }}
              </button>
            }
            <a pButton size="small" [routerLink]="['/app/knowledge', item.id, 'edit']">{{
              i18n.text('common.edit')
            }}</a>
          </div>
        }
      </header>
      <section class="content-panel">
        <div
          class="rich-content"
          [innerHTML]="item.body | richHtml"
        ></div>
      </section>
    }
  `,
  styles: `
    .back-link {
      display: inline-block;
      margin-bottom: 0.6rem;
      color: var(--app-text-muted);
      font-size: 0.8rem;
      text-decoration: none;
    }
    .back-link:hover {
      color: var(--p-primary-color);
    }
    .detail-header {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 1.25rem;
      margin-bottom: 0.85rem;
    }
    .detail-header h1 {
      margin: 0;
      font-size: 1.25rem;
      font-weight: 700;
      letter-spacing: -0.015em;
      overflow-wrap: anywhere;
    }
    .meta-row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.55rem;
      margin-top: 0.55rem;
    }
    .slug,
    .meta-item,
    time {
      color: var(--app-text-muted);
      font-size: 0.78rem;
    }
    .heading-actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
      flex-shrink: 0;
    }
    .rich-content {
      max-width: 52rem;
      line-height: 1.7;
      font-size: 0.92rem;
      overflow-wrap: anywhere;
    }
    p-message {
      display: block;
      margin-bottom: 0.85rem;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KnowledgeDetailPage {
  private readonly api = inject(ApiClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly i18n = inject(I18nService);
  readonly store = inject(SessionStore);
  readonly article = signal<KnowledgeArticle | null>(null);
  readonly projects = signal<Project[]>([]);
  readonly error = signal('');
  readonly busy = signal(false);

  constructor() {
    void this.load();
  }

  text(en: string, fr: string): string {
    return this.i18n.language() === 'French' ? fr : en;
  }

  projectName(): string | null {
    const projectId = this.article()?.projectId;
    if (!projectId) return null;
    const project = this.projects().find((item) => item.id === projectId);
    if (!project) return null;
    return project.name;
  }

  async setStatus(item: KnowledgeArticle, status: KnowledgeArticle['status']): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    try {
      this.article.set(await firstValueFrom(this.api.setKnowledgeStatus(item.id, status)));
    } catch {
      this.error.set(
        this.text('The change could not be saved.', "La modification n'a pas pu être enregistrée."),
      );
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    const id = this.route.snapshot.paramMap.get('articleId');
    try {
      const [articles, projects] = await Promise.all([
        firstValueFrom(this.api.knowledge()),
        firstValueFrom(this.api.projects()),
      ]);
      this.projects.set(projects);
      const article = articles.find((item) => item.id === id) ?? null;
      if (!article) {
        await this.router.navigate(['/app/knowledge']);
        return;
      }
      this.article.set(article);
    } catch {
      this.error.set(
        this.text('Could not load the article.', "Impossible de charger l'article."),
      );
    }
  }
}
