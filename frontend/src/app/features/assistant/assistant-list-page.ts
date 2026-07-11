import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ConfirmationService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ApiClient } from '../../core/api/api-client';
import { AssistantConversationSummary } from '../../core/api/api-models';
import { I18nService } from '../../core/i18n/i18n.service';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { Icon } from '../../layout/icons/icon';

@Component({
  selector: 'app-assistant-list-page',
  imports: [ButtonModule, ConfirmDialogModule, LocalizedDatePipe, Icon],
  providers: [ConfirmationService],
  template: `
    <p-confirmdialog [style]="{ width: '26rem' }" />
    <header class="page-heading">
      <div>
        <p class="eyebrow">{{ i18n.text('nav.assistant') }}</p>
        <h1>{{ text('Conversations', 'Conversations') }}</h1>
        <p>
          {{
            text(
              'Ask about your tickets, projects, and knowledge base — or ask the assistant to act for you.',
              'Interrogez vos tickets, projets et base de connaissances — ou demandez à l’assistant d’agir pour vous.'
            )
          }}
        </p>
      </div>
      <div class="heading-actions">
        <button pButton type="button" (click)="startConversation()" [loading]="creating()">
          <app-icon name="sparkles" [size]="16" />
          {{ text('New conversation', 'Nouvelle conversation') }}
        </button>
      </div>
    </header>
    <section class="content-panel">
      @if (!conversations().length && !loading()) {
        <div class="empty">
          <app-icon name="sparkles" [size]="28" />
          <p>{{ text('No conversations yet. Start one!', 'Aucune conversation. Lancez-vous !') }}</p>
        </div>
      }
      <ul class="conversation-list">
        @for (conversation of conversations(); track conversation.id) {
          <li>
            <button type="button" class="conversation" (click)="open(conversation)">
              <app-icon name="sparkles" [size]="16" />
              <span class="title">{{ conversation.title }}</span>
              <time>{{ conversation.lastMessageAt | localizedDate: 'medium' }}</time>
            </button>
            <button
              pButton
              type="button"
              size="small"
              severity="danger"
              [text]="true"
              class="delete"
              (click)="remove(conversation)"
            >
              {{ text('Delete', 'Supprimer') }}
            </button>
          </li>
        }
      </ul>
    </section>
  `,
  styles: `
    .empty {
      display: grid;
      justify-items: center;
      gap: 0.5rem;
      padding: 2.5rem 1rem;
      color: var(--app-text-muted);
    }
    .conversation-list {
      list-style: none;
      margin: 0;
      padding: 0;
      display: grid;
      gap: 0.5rem;
    }
    .conversation-list li {
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }
    .conversation {
      flex: 1;
      display: flex;
      align-items: center;
      gap: 0.65rem;
      padding: 0.75rem 0.9rem;
      border: 1px solid var(--app-border);
      border-radius: 0.7rem;
      background: transparent;
      color: var(--app-text);
      font: inherit;
      text-align: left;
      cursor: pointer;
      min-width: 0;
    }
    .conversation:hover {
      border-color: var(--p-primary-color);
      background: color-mix(in srgb, var(--p-primary-color) 6%, transparent);
    }
    .conversation app-icon { color: var(--p-primary-color); }
    .title {
      flex: 1;
      font-weight: 600;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .conversation time {
      color: var(--app-text-muted);
      font-size: 0.78rem;
      flex-shrink: 0;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AssistantListPage {
  private readonly api = inject(ApiClient);
  private readonly router = inject(Router);
  private readonly confirmation = inject(ConfirmationService);
  readonly i18n = inject(I18nService);
  readonly conversations = signal<AssistantConversationSummary[]>([]);
  readonly loading = signal(true);
  readonly creating = signal(false);

  constructor() {
    void this.load();
  }

  text(en: string, fr: string): string {
    return this.i18n.language() === 'French' ? fr : en;
  }

  async startConversation(): Promise<void> {
    this.creating.set(true);
    try {
      const conversation = await firstValueFrom(this.api.createAssistantConversation());
      await this.router.navigate(['/app/assistant', conversation.id]);
    } finally {
      this.creating.set(false);
    }
  }

  open(conversation: AssistantConversationSummary): void {
    void this.router.navigate(['/app/assistant', conversation.id]);
  }

  remove(conversation: AssistantConversationSummary): void {
    const french = this.i18n.language() === 'French';
    this.confirmation.confirm({
      header: french ? 'Supprimer la conversation' : 'Delete conversation',
      message: french ? `Supprimer « ${conversation.title} » ?` : `Delete "${conversation.title}"?`,
      acceptLabel: french ? 'Supprimer' : 'Delete',
      rejectLabel: this.i18n.text('common.cancel'),
      acceptButtonStyleClass: 'p-button-danger',
      rejectButtonStyleClass: 'p-button-secondary p-button-outlined',
      accept: async () => {
        await firstValueFrom(this.api.deleteAssistantConversation(conversation.id));
        await this.load();
      },
    });
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.conversations.set(await firstValueFrom(this.api.assistantConversations()));
    } finally {
      this.loading.set(false);
    }
  }
}
