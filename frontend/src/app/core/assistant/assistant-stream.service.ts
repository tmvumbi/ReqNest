import { Injectable, inject } from '@angular/core';
import { SessionStore } from '../session/session-store';

export interface AssistantStreamHandlers {
  onDelta(text: string): void;
  onTool(name: string): void;
  onDone(payload: { messageId: string; title: string; lastMessageAt: string }): void;
  onError(message: string): void;
}

// HttpClient buffers responses, so the SSE chat stream uses fetch directly.
@Injectable({ providedIn: 'root' })
export class AssistantStreamService {
  private readonly store = inject(SessionStore);

  async send(
    conversationId: string,
    content: string,
    handlers: AssistantStreamHandlers,
    signal?: AbortSignal,
  ): Promise<void> {
    const session = this.store.session();
    const tenantId = this.store.activeTenantId();
    if (!session) {
      handlers.onError('Not signed in.');
      return;
    }

    let response: Response;
    try {
      response = await fetch(`/api/assistant/conversations/${conversationId}/messages`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${session.accessToken}`,
          ...(tenantId ? { 'X-Tenant-Id': tenantId } : {}),
        },
        body: JSON.stringify({ content }),
        signal,
      });
    } catch (error) {
      if ((error as Error).name !== 'AbortError') handlers.onError('The request failed.');
      return;
    }

    if (!response.ok || !response.body) {
      handlers.onError(`The assistant request failed (${response.status}).`);
      return;
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';
    try {
      for (;;) {
        const { done, value } = await reader.read();
        if (done) break;
        buffer += decoder.decode(value, { stream: true });
        let boundary = buffer.indexOf('\n\n');
        while (boundary >= 0) {
          this.dispatch(buffer.slice(0, boundary), handlers);
          buffer = buffer.slice(boundary + 2);
          boundary = buffer.indexOf('\n\n');
        }
      }
    } catch (error) {
      if ((error as Error).name !== 'AbortError') handlers.onError('The stream was interrupted.');
    }
  }

  private dispatch(block: string, handlers: AssistantStreamHandlers): void {
    let eventName = 'message';
    let data = '';
    for (const line of block.split('\n')) {
      if (line.startsWith('event:')) eventName = line.slice(6).trim();
      else if (line.startsWith('data:')) data += line.slice(5).trim();
    }
    if (!data) return;
    try {
      const payload = JSON.parse(data);
      switch (eventName) {
        case 'delta':
          handlers.onDelta(payload.text ?? '');
          break;
        case 'tool':
          handlers.onTool(payload.name ?? '');
          break;
        case 'done':
          handlers.onDone(payload);
          break;
        case 'error':
          handlers.onError(payload.message ?? 'Unknown error.');
          break;
      }
    } catch {
      // Ignore malformed frames.
    }
  }
}
