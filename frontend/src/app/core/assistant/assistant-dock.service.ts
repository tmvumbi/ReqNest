import { Injectable, signal } from '@angular/core';

export type AssistantDockMode = 'closed' | 'floating' | 'minimized';

// Holds the floating assistant state so the chat survives route navigation.
@Injectable({ providedIn: 'root' })
export class AssistantDockService {
  readonly mode = signal<AssistantDockMode>('closed');
  readonly conversationId = signal<string | null>(null);
  readonly title = signal<string>('');

  openFloating(conversationId: string | null, title = ''): void {
    this.conversationId.set(conversationId);
    this.title.set(title);
    this.mode.set('floating');
  }

  minimize(): void {
    if (this.mode() === 'floating') this.mode.set('minimized');
  }

  restore(): void {
    if (this.mode() === 'minimized') this.mode.set('floating');
  }

  close(): void {
    this.mode.set('closed');
    this.conversationId.set(null);
    this.title.set('');
  }
}
