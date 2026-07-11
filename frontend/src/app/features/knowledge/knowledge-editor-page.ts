import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { EditorModule } from 'primeng/editor';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { ApiClient } from '../../core/api/api-client';
import { KnowledgeArticle, Project } from '../../core/api/api-models';
import { I18nService } from '../../core/i18n/i18n.service';

@Component({
  selector: 'app-knowledge-editor-page',
  imports: [FormsModule, RouterLink, ButtonModule, EditorModule, InputTextModule, MessageModule, SelectModule],
  template: `
    <div class="editor-wrap">
      <a [routerLink]="backLink()" class="back-link"
        >← {{ text('Knowledge base', 'Base de connaissances') }}</a
      >
      <header class="page-heading compact">
        <div>
          <h1>
            {{
              articleId
                ? text('Edit article', "Modifier l'article")
                : text('New article', 'Nouvel article')
            }}
          </h1>
          <p>
            {{
              text(
                'Write the answer and choose who can see it.',
                'Rédigez la réponse et choisissez qui peut la voir.'
              )
            }}
          </p>
        </div>
      </header>
      @if (error()) {
        <p-message severity="error">{{ error() }}</p-message>
      }
      <form class="content-panel editor-form" (ngSubmit)="save()">
        <div class="field">
          <label for="knowledgeProject">{{ text('Project (optional)', 'Projet (facultatif)') }}</label>
          <p-select
            inputId="knowledgeProject"
            [(ngModel)]="projectId"
            name="project"
            [options]="projects()"
            optionValue="id"
            [optionLabel]="'name'"
            [showClear]="true"
          />
        </div>
        <div class="field">
          <label for="knowledgeSlug">Slug</label>
          <input pInputText id="knowledgeSlug" [(ngModel)]="slug" name="slug" required />
        </div>
        <div class="field">
          <label for="knowledgeTitle">{{ text('Title', 'Titre') }}</label>
          <input pInputText id="knowledgeTitle" [(ngModel)]="title" name="title" required />
        </div>
        <div class="field">
          <span class="field-label">{{ text('Content', 'Contenu') }}</span>
          <p-editor
            [ariaLabel]="text('Content', 'Contenu')"
            [(ngModel)]="body"
            name="body"
            [style]="{ height: '16rem' }"
          />
        </div>
        <div class="field">
          <label for="knowledgeVisibility">{{ text('Visibility', 'Visibilité') }}</label>
          <p-select
            inputId="knowledgeVisibility"
            [(ngModel)]="visibility"
            name="visibility"
            [options]="visibilities"
          />
        </div>
        <div class="form-actions">
          <a pButton severity="secondary" [outlined]="true" [routerLink]="backLink()">{{
            i18n.text('common.cancel')
          }}</a>
          <button pButton type="submit" [loading]="busy()">
            {{ articleId ? i18n.text('common.save') : text('Save draft', 'Enregistrer le brouillon') }}
          </button>
        </div>
      </form>
    </div>
  `,
  styles: `
    .editor-wrap {
      max-width: 52rem;
      margin-inline: auto;
    }
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
    .editor-form,
    .field {
      display: grid;
      gap: 0.35rem;
    }
    .editor-form {
      gap: 0.9rem;
    }
    label,
    .field-label {
      color: var(--app-text-muted);
      font-size: 0.76rem;
      font-weight: 650;
      letter-spacing: 0.02em;
    }
    .split {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 0.7rem;
    }
    input,
    p-select,
    p-editor {
      width: 100%;
    }
    .form-actions {
      display: flex;
      justify-content: flex-end;
      gap: 0.6rem;
      padding-top: 0.25rem;
      border-top: 1px solid var(--app-border);
    }
    p-message {
      display: block;
      margin-bottom: 0.85rem;
    }
    @media (max-width: 720px) {
      .split {
        grid-template-columns: 1fr;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KnowledgeEditorPage {
  private readonly api = inject(ApiClient);
  private readonly router = inject(Router);
  readonly i18n = inject(I18nService);
  readonly projects = signal<Project[]>([]);
  readonly error = signal('');
  readonly busy = signal(false);
  readonly articleId = inject(ActivatedRoute).snapshot.paramMap.get('articleId');
  projectId = '';
  slug = '';
  title = '';
  body = '';
  visibility: KnowledgeArticle['visibility'] = 'Internal';
  readonly visibilities: KnowledgeArticle['visibility'][] = ['Internal', 'Requesters'];

  constructor() {
    void this.load();
  }

  text(en: string, fr: string): string {
    return this.i18n.language() === 'French' ? fr : en;
  }

  backLink(): string[] {
    return this.articleId ? ['/app/knowledge', this.articleId] : ['/app/knowledge'];
  }

  async save(): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    try {
      const saved = await firstValueFrom(
        this.api.saveKnowledge(
          {
            projectId: this.projectId || null,
            slug: this.slug,
            title: this.title,
            body: this.body,
            visibility: this.visibility,
          },
          this.articleId ?? undefined,
        ),
      );
      await this.router.navigate(['/app/knowledge', saved.id]);
    } catch {
      this.error.set(
        this.text('The article could not be saved.', "L'article n'a pas pu être enregistré."),
      );
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    try {
      const [articles, projects] = await Promise.all([
        this.articleId ? firstValueFrom(this.api.knowledge()) : Promise.resolve([]),
        firstValueFrom(this.api.projects()),
      ]);
      this.projects.set(projects);
      if (!this.articleId) return;
      const article = articles.find((item) => item.id === this.articleId);
      if (!article) {
        await this.router.navigate(['/app/knowledge']);
        return;
      }
      this.projectId = article.projectId ?? '';
      this.slug = article.slug;
      this.title = article.title;
      this.body = article.body;
      this.visibility = article.visibility;
    } catch {
      this.error.set(this.text('Could not load the article.', "Impossible de charger l'article."));
    }
  }
}
