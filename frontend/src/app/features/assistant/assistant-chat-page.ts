import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { AssistantDockService } from '../../core/assistant/assistant-dock.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { Icon } from '../../layout/icons/icon';
import { ChatPanel } from './chat-panel';

@Component({
  selector: 'app-assistant-chat-page',
  imports: [RouterLink, ButtonModule, Icon, ChatPanel],
  template: `
    <div class="chat-page">
      <header class="chat-header">
        <a routerLink="/app/assistant" class="back-link">← {{ i18n.text('nav.assistant') }}</a>
        <h1>{{ title() }}</h1>
        <button
          pButton
          type="button"
          size="small"
          severity="secondary"
          [text]="true"
          (click)="float()"
          [attr.aria-label]="text('Float this conversation', 'Faire flotter la conversation')"
        >
          <app-icon name="pip" [size]="16" />
          {{ text('Float', 'Flotter') }}
        </button>
      </header>
      <div class="chat-body content-panel">
        <app-chat-panel [conversationId]="conversationId()" (titleChanged)="title.set($event)" />
      </div>
    </div>
  `,
  styles: `
    :host {
      display: block;
      height: 100%;
    }
    .chat-page {
      display: flex;
      flex-direction: column;
      height: 100%;
      min-height: 0;
      max-width: 56rem;
      margin: 0 auto;
    }
    .chat-header {
      display: grid;
      grid-template-columns: 1fr auto;
      align-items: center;
      column-gap: 0.75rem;
      margin-bottom: 0.75rem;
    }
    .back-link {
      grid-column: 1 / -1;
      color: var(--app-text-muted);
      font-size: 0.8rem;
      text-decoration: none;
      margin-bottom: 0.35rem;
    }
    .back-link:hover { color: var(--p-primary-color); }
    .chat-header h1 {
      margin: 0;
      font-size: 1.15rem;
      font-weight: 700;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .chat-body {
      flex: 1;
      min-height: 24rem;
      padding: 0;
      overflow: hidden;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AssistantChatPage {
  private readonly dock = inject(AssistantDockService);
  private readonly router = inject(Router);
  readonly i18n = inject(I18nService);
  // Signal so the chat panel reloads when navigating between conversations
  // (the router reuses this component when only the param changes).
  readonly conversationId = toSignal(
    inject(ActivatedRoute).paramMap.pipe(map((params) => params.get('conversationId')!)),
    { requireSync: true },
  );
  readonly title = signal('');

  text(en: string, fr: string): string {
    return this.i18n.language() === 'French' ? fr : en;
  }

  float(): void {
    this.dock.openFloating(this.conversationId(), this.title());
    void this.router.navigate(['/app/assistant']);
  }
}
