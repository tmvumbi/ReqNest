import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { ApiClient } from '../../core/api/api-client';
import { AssistantDockService } from '../../core/assistant/assistant-dock.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { Icon } from '../../layout/icons/icon';
import { ChatPanel } from './chat-panel';

// Floating assistant window. Lives in the app shell so it survives navigation.
@Component({
  selector: 'app-assistant-dock',
  imports: [ButtonModule, Icon, ChatPanel],
  template: `
    @if (dock.mode() === 'floating' && dock.conversationId(); as conversationId) {
      <section class="dock" role="dialog" [attr.aria-label]="i18n.text('nav.assistant')">
        <header class="dock-header">
          <app-icon name="sparkles" [size]="15" />
          <span class="dock-title">{{ dock.title() || i18n.text('nav.assistant') }}</span>
          <button
            pButton
            type="button"
            size="small"
            severity="secondary"
            [text]="true"
            class="dock-action"
            (click)="expand()"
            [attr.aria-label]="text('Open full page', 'Ouvrir en pleine page')"
          >
            <app-icon name="expand" [size]="14" />
          </button>
          <button
            pButton
            type="button"
            size="small"
            severity="secondary"
            [text]="true"
            class="dock-action"
            (click)="dock.minimize()"
            [attr.aria-label]="text('Minimize', 'Réduire')"
          >
            <app-icon name="minimize" [size]="14" />
          </button>
          <button
            pButton
            type="button"
            size="small"
            severity="secondary"
            [text]="true"
            class="dock-action"
            (click)="dock.close()"
            [attr.aria-label]="text('Close', 'Fermer')"
          >
            <app-icon name="close" [size]="14" />
          </button>
        </header>
        <div class="dock-body">
          <app-chat-panel
            [conversationId]="conversationId"
            [compact]="true"
            (titleChanged)="dock.title.set($event)"
          />
        </div>
      </section>
    } @else if (dock.mode() === 'minimized') {
      <button type="button" class="dock-pill" (click)="dock.restore()">
        <app-icon name="sparkles" [size]="16" />
        <span>{{ dock.title() || i18n.text('nav.assistant') }}</span>
      </button>
    } @else if (dock.mode() === 'closed') {
      <button
        type="button"
        class="dock-launcher"
        (click)="launch()"
        [attr.aria-label]="i18n.text('nav.assistant')"
      >
        <app-icon name="sparkles" [size]="20" />
      </button>
    }
  `,
  styles: `
    .dock {
      position: fixed;
      right: 1.25rem;
      bottom: 1.25rem;
      width: min(25rem, calc(100vw - 2rem));
      height: min(34rem, calc(100vh - 6rem));
      display: flex;
      flex-direction: column;
      background: var(--app-surface);
      border: 1px solid var(--app-border-strong, var(--app-border));
      border-radius: 1rem;
      box-shadow: 0 18px 50px rgba(0, 0, 0, 0.35);
      overflow: hidden;
      z-index: 1000;
    }
    .dock-header {
      display: flex;
      align-items: center;
      gap: 0.45rem;
      padding: 0.55rem 0.65rem 0.55rem 0.9rem;
      border-bottom: 1px solid var(--app-border);
      color: var(--p-primary-color);
    }
    .dock-title {
      flex: 1;
      color: var(--app-text);
      font-size: 0.85rem;
      font-weight: 650;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .dock-action {
      width: 1.9rem;
      height: 1.9rem;
      justify-content: center;
      padding: 0;
    }
    .dock-body {
      flex: 1;
      min-height: 0;
    }
    .dock-pill,
    .dock-launcher {
      position: fixed;
      right: 1.25rem;
      bottom: 1.25rem;
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      border: 1px solid var(--app-border-strong, var(--app-border));
      background: var(--app-surface);
      color: var(--p-primary-color);
      box-shadow: 0 10px 28px rgba(0, 0, 0, 0.3);
      cursor: pointer;
      z-index: 1000;
    }
    .dock-pill {
      max-width: 18rem;
      padding: 0.55rem 1rem;
      border-radius: 999px;
      font: inherit;
      font-size: 0.85rem;
      font-weight: 650;
    }
    .dock-pill span {
      color: var(--app-text);
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .dock-launcher {
      width: 3rem;
      height: 3rem;
      border-radius: 50%;
      justify-content: center;
    }
    .dock-pill:hover,
    .dock-launcher:hover {
      border-color: var(--p-primary-color);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AssistantDock {
  private readonly api = inject(ApiClient);
  private readonly router = inject(Router);
  readonly dock = inject(AssistantDockService);
  readonly i18n = inject(I18nService);

  text(en: string, fr: string): string {
    return this.i18n.language() === 'French' ? fr : en;
  }

  expand(): void {
    const conversationId = this.dock.conversationId();
    this.dock.close();
    if (conversationId) void this.router.navigate(['/app/assistant', conversationId]);
  }

  async launch(): Promise<void> {
    const conversation = await firstValueFrom(this.api.createAssistantConversation());
    this.dock.openFloating(conversation.id);
  }
}
