import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnDestroy,
  effect,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { ApiClient } from '../../core/api/api-client';
import { AssistantMessage } from '../../core/api/api-models';
import { AssistantStreamService } from '../../core/assistant/assistant-stream.service';
import { MarkdownPipe } from '../../core/assistant/markdown.pipe';
import { VoiceSession } from '../../core/assistant/voice-session';
import { I18nService } from '../../core/i18n/i18n.service';
import { Icon } from '../../layout/icons/icon';

interface DisplayMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  isVoice: boolean;
  streaming?: boolean;
}

@Component({
  selector: 'app-chat-panel',
  imports: [FormsModule, ButtonModule, MarkdownPipe, Icon],
  template: `
    <div class="chat" [class.compact]="compact()">
      <!-- Click delegation for anchors only; links stay natively keyboard-accessible. -->
      <!-- eslint-disable-next-line @angular-eslint/template/click-events-have-key-events, @angular-eslint/template/interactive-supports-focus -->
      <div class="messages" #scroller (click)="onContentClick($event)">
        @if (!messages().length && !loading()) {
          <div class="empty">
            <app-icon name="sparkles" [size]="26" />
            <p>{{ text('Ask about your tickets and projects, or ask me to do something —', 'Posez des questions sur vos tickets et projets, ou demandez-moi une action —') }}</p>
            <ul>
              <li>“{{ text('What are my open tickets?', 'Quels sont mes tickets ouverts ?') }}”</li>
              <li>“{{ text('Create a ticket for the printer outage', "Créer un ticket pour la panne d'imprimante") }}”</li>
              <li>“{{ text('Move HELP-2 to Done', 'Passer HELP-2 à Terminé') }}”</li>
            </ul>
          </div>
        }
        @for (message of messages(); track message.id) {
          <div class="message" [class.user]="message.role === 'user'">
            <div class="bubble">
              @if (message.isVoice) {
                <span class="voice-tag"><app-icon name="mic" [size]="11" />{{ text('Voice', 'Voix') }}</span>
              }
              @if (message.role === 'assistant') {
                <div class="md" [innerHTML]="message.content | markdown"></div>
                @if (message.streaming) {
                  <span class="caret" aria-hidden="true"></span>
                }
              } @else {
                <p class="plain">{{ message.content }}</p>
              }
            </div>
          </div>
        }
        @if (toolActivity(); as tool) {
          <div class="tool-activity">
            <span class="spinner" aria-hidden="true"></span>{{ toolLabel(tool) }}
          </div>
        }
        @if (voiceState() === 'live' && voicePartial()) {
          <div class="message">
            <div class="bubble"><p class="plain live">{{ voicePartial() }}</p></div>
          </div>
        }
      </div>
      @if (voiceState() !== 'stopped') {
        <div class="voice-bar" [class.error]="voiceState() === 'error'">
          @if (voiceState() === 'connecting') {
            <span class="spinner" aria-hidden="true"></span>{{ text('Connecting voice…', 'Connexion vocale…') }}
          } @else if (voiceState() === 'live') {
            <span class="pulse" aria-hidden="true"></span>{{ text('Live voice conversation — transcripts appear above.', 'Conversation vocale en direct — les transcriptions s’affichent ci-dessus.') }}
          } @else {
            {{ voiceError() }}
          }
          @if (voiceState() !== 'error') {
            <button pButton type="button" size="small" severity="danger" [text]="true" (click)="stopVoice()">
              {{ text('End', 'Terminer') }}
            </button>
          } @else {
            <button pButton type="button" size="small" severity="secondary" [text]="true" (click)="dismissVoiceError()">OK</button>
          }
        </div>
      }
      <form class="composer" (ngSubmit)="send()">
        <textarea
          #inputBox
          [(ngModel)]="draft"
          name="draft"
          rows="1"
          [placeholder]="text('Message the assistant…', 'Écrivez à l’assistant…')"
          (keydown.enter)="onEnter($any($event))"
          [disabled]="busy()"
        ></textarea>
        <button
          pButton
          type="button"
          severity="secondary"
          [text]="true"
          class="icon-button"
          [disabled]="busy() || voiceState() === 'connecting'"
          [attr.aria-label]="text('Voice conversation', 'Conversation vocale')"
          (click)="voiceState() === 'live' ? stopVoice() : startVoice()"
        >
          <app-icon [name]="voiceState() === 'live' ? 'mic-off' : 'mic'" [size]="17" />
        </button>
        <button
          pButton
          type="submit"
          class="icon-button"
          [disabled]="!draft.trim() || busy()"
          [attr.aria-label]="text('Send', 'Envoyer')"
        >
          <app-icon name="send" [size]="16" />
        </button>
      </form>
    </div>
  `,
  styles: `
    :host {
      display: block;
      height: 100%;
      min-height: 0;
    }
    .chat {
      display: flex;
      flex-direction: column;
      height: 100%;
      min-height: 0;
    }
    .messages {
      flex: 1;
      min-height: 0;
      overflow-y: auto;
      display: flex;
      flex-direction: column;
      gap: 0.65rem;
      padding: 1rem;
    }
    .empty {
      margin: auto;
      max-width: 26rem;
      text-align: center;
      color: var(--app-text-muted);
      display: grid;
      gap: 0.5rem;
      justify-items: center;
      font-size: 0.88rem;
    }
    .empty ul {
      list-style: none;
      padding: 0;
      margin: 0;
      display: grid;
      gap: 0.3rem;
      font-size: 0.82rem;
    }
    .message {
      display: flex;
    }
    .message.user {
      justify-content: flex-end;
    }
    .bubble {
      max-width: 85%;
      padding: 0.55rem 0.8rem;
      border-radius: 0.85rem;
      background: var(--app-surface-raised, var(--app-surface));
      border: 1px solid var(--app-border);
      font-size: 0.88rem;
      line-height: 1.55;
      overflow-wrap: anywhere;
    }
    .message.user .bubble {
      background: color-mix(in srgb, var(--p-primary-color) 14%, var(--app-surface));
      border-color: color-mix(in srgb, var(--p-primary-color) 30%, var(--app-border));
    }
    .plain {
      margin: 0;
      white-space: pre-wrap;
    }
    .plain.live {
      color: var(--app-text-muted);
      font-style: italic;
    }
    .voice-tag {
      display: inline-flex;
      align-items: center;
      gap: 0.25rem;
      font-size: 0.68rem;
      font-weight: 650;
      color: var(--p-primary-color);
      text-transform: uppercase;
      letter-spacing: 0.04em;
      margin-bottom: 0.2rem;
    }
    .md :first-child { margin-top: 0; }
    .md :last-child { margin-bottom: 0; }
    .md p { margin: 0.4rem 0; }
    .md h1, .md h2, .md h3 { font-size: 1rem; margin: 0.7rem 0 0.3rem; }
    .md ul, .md ol { margin: 0.35rem 0; padding-left: 1.2rem; }
    .md li { margin: 0.15rem 0; }
    .md code {
      font-size: 0.8rem;
      background: color-mix(in srgb, var(--app-text) 8%, transparent);
      border-radius: 0.3rem;
      padding: 0.1rem 0.3rem;
    }
    .md pre {
      overflow-x: auto;
      background: color-mix(in srgb, var(--app-text) 7%, transparent);
      border-radius: 0.5rem;
      padding: 0.6rem 0.75rem;
    }
    .md pre code { background: none; padding: 0; }
    .md a {
      color: var(--p-primary-color);
      font-weight: 600;
      text-decoration: none;
    }
    .md a:hover { text-decoration: underline; }
    .md table { border-collapse: collapse; margin: 0.4rem 0; }
    .md th, .md td { border: 1px solid var(--app-border); padding: 0.25rem 0.55rem; font-size: 0.82rem; }
    .md blockquote {
      margin: 0.4rem 0;
      padding-left: 0.7rem;
      border-left: 3px solid var(--app-border-strong, var(--app-border));
      color: var(--app-text-muted);
    }
    .caret {
      display: inline-block;
      width: 7px;
      height: 15px;
      margin-left: 2px;
      background: var(--p-primary-color);
      animation: blink 1s steps(1) infinite;
      vertical-align: text-bottom;
    }
    @keyframes blink { 50% { opacity: 0; } }
    .tool-activity {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      align-self: flex-start;
      color: var(--app-text-muted);
      font-size: 0.8rem;
      padding: 0.3rem 0.2rem;
    }
    .spinner {
      width: 0.85rem;
      height: 0.85rem;
      border: 2px solid var(--app-border-strong, var(--app-border));
      border-top-color: var(--p-primary-color);
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
    }
    @keyframes spin { to { transform: rotate(360deg); } }
    .voice-bar {
      display: flex;
      align-items: center;
      gap: 0.55rem;
      margin: 0 1rem;
      padding: 0.45rem 0.7rem;
      border-radius: 0.6rem;
      border: 1px solid color-mix(in srgb, var(--p-primary-color) 35%, var(--app-border));
      background: color-mix(in srgb, var(--p-primary-color) 8%, var(--app-surface));
      font-size: 0.8rem;
    }
    .voice-bar.error {
      border-color: color-mix(in srgb, #ef4444 45%, var(--app-border));
      background: color-mix(in srgb, #ef4444 8%, var(--app-surface));
    }
    .voice-bar button { margin-left: auto; }
    .pulse {
      width: 0.6rem;
      height: 0.6rem;
      border-radius: 50%;
      background: #ef4444;
      animation: pulse 1.4s ease-in-out infinite;
    }
    @keyframes pulse { 50% { opacity: 0.35; } }
    .composer {
      display: flex;
      align-items: flex-end;
      gap: 0.4rem;
      padding: 0.75rem 1rem 1rem;
    }
    .composer textarea {
      flex: 1;
      resize: none;
      max-height: 9rem;
      min-height: 2.6rem;
      padding: 0.6rem 0.75rem;
      border-radius: 0.7rem;
      border: 1px solid var(--app-border);
      background: var(--app-surface);
      color: var(--app-text);
      font: inherit;
      font-size: 0.88rem;
      line-height: 1.45;
    }
    .composer textarea:focus-visible {
      outline: none;
      border-color: var(--p-primary-color);
    }
    .icon-button {
      width: 2.5rem;
      height: 2.5rem;
      justify-content: center;
    }
    .compact .messages { padding: 0.75rem; }
    .compact .composer { padding: 0.6rem 0.75rem 0.75rem; }
    .compact .bubble { max-width: 92%; font-size: 0.85rem; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChatPanel implements OnDestroy {
  private readonly api = inject(ApiClient);
  private readonly streamService = inject(AssistantStreamService);
  private readonly router = inject(Router);
  readonly i18n = inject(I18nService);

  readonly conversationId = input.required<string>();
  readonly compact = input(false);
  readonly titleChanged = output<string>();

  readonly messages = signal<DisplayMessage[]>([]);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly toolActivity = signal<string>('');
  readonly voiceState = signal<'stopped' | 'connecting' | 'live' | 'error'>('stopped');
  readonly voiceError = signal('');
  readonly voicePartial = signal('');
  draft = '';

  private readonly scroller = viewChild<ElementRef<HTMLDivElement>>('scroller');
  private abortController: AbortController | null = null;
  private voiceSession: VoiceSession | null = null;

  constructor() {
    effect(() => {
      const id = this.conversationId();
      void this.load(id);
    });
  }

  ngOnDestroy(): void {
    this.abortController?.abort();
    this.voiceSession?.stop();
  }

  text(en: string, fr: string): string {
    return this.i18n.language() === 'French' ? fr : en;
  }

  toolLabel(tool: string): string {
    const labels: Record<string, [string, string]> = {
      list_projects: ['Looking up projects…', 'Consultation des projets…'],
      search_tickets: ['Searching tickets…', 'Recherche de tickets…'],
      get_ticket: ['Reading the ticket…', 'Lecture du ticket…'],
      create_ticket: ['Creating the ticket…', 'Création du ticket…'],
      update_ticket: ['Updating the ticket…', 'Mise à jour du ticket…'],
      add_comment: ['Adding the comment…', 'Ajout du commentaire…'],
      transition_ticket: ['Changing the status…', 'Changement de statut…'],
      search_knowledge: ['Searching the knowledge base…', 'Recherche dans la base de connaissances…'],
      list_members: ['Looking up members…', 'Consultation des membres…'],
      get_ticket_schema: ['Checking the ticket schema…', 'Vérification du schéma…'],
    };
    const label = labels[tool];
    return label ? this.text(label[0], label[1]) : this.text('Working…', 'En cours…');
  }

  onEnter(event: KeyboardEvent): void {
    if (event.shiftKey) return;
    event.preventDefault();
    void this.send();
  }

  async send(): Promise<void> {
    const content = this.draft.trim();
    if (!content || this.busy()) return;
    this.draft = '';
    this.busy.set(true);
    this.appendMessage({ id: crypto.randomUUID(), role: 'user', content, isVoice: false });
    const streamingId = crypto.randomUUID();
    this.appendMessage({ id: streamingId, role: 'assistant', content: '', isVoice: false, streaming: true });
    this.abortController = new AbortController();
    await this.streamService.send(
      this.conversationId(),
      content,
      {
        onDelta: (textDelta) => {
          this.toolActivity.set('');
          this.patchMessage(streamingId, (message) => ({
            ...message,
            content: message.content + textDelta,
          }));
          this.scrollToBottom();
        },
        onTool: (name) => {
          this.toolActivity.set(name);
          this.scrollToBottom();
        },
        onDone: (payload) => {
          this.toolActivity.set('');
          this.patchMessage(streamingId, (message) => ({ ...message, streaming: false }));
          this.titleChanged.emit(payload.title);
        },
        onError: (message) => {
          this.toolActivity.set('');
          this.patchMessage(streamingId, (current) => ({
            ...current,
            streaming: false,
            content: current.content || `⚠️ ${message}`,
          }));
        },
      },
      this.abortController.signal,
    );
    this.busy.set(false);
    this.scrollToBottom();
  }

  async startVoice(): Promise<void> {
    this.voiceError.set('');
    let session;
    try {
      session = await firstValueFrom(this.api.assistantRealtimeSession());
    } catch (error) {
      const problem = (error as { error?: { title?: string; detail?: string } }).error;
      this.voiceError.set(
        problem?.detail ??
          problem?.title ??
          this.text('Voice is not available.', 'La voix n’est pas disponible.'),
      );
      this.voiceState.set('error');
      return;
    }

    this.voiceSession = new VoiceSession({
      onUserTranscript: (transcript) => this.addVoiceMessage('user', transcript),
      onAssistantDelta: (delta) => this.voicePartial.set(this.voicePartial() + delta),
      onAssistantTranscript: (transcript) => {
        this.voicePartial.set('');
        this.addVoiceMessage('assistant', transcript);
      },
      onStateChange: (state, detail) => {
        if (state === 'error') {
          this.voiceError.set(detail ?? this.text('Voice failed.', 'La voix a échoué.'));
          this.voiceState.set('error');
        } else if (state === 'stopped') {
          this.voiceState.set('stopped');
          this.voicePartial.set('');
        } else {
          this.voiceState.set(state);
        }
      },
    });
    await this.voiceSession.start(session.clientSecret, session.model);
  }

  stopVoice(): void {
    this.voiceSession?.stop();
    this.voiceSession = null;
  }

  dismissVoiceError(): void {
    this.voiceState.set('stopped');
    this.voiceError.set('');
  }

  onContentClick(event: MouseEvent): void {
    const anchor = (event.target as HTMLElement).closest('a');
    if (!anchor) return;
    const href = anchor.getAttribute('href');
    if (href?.startsWith('/')) {
      event.preventDefault();
      void this.router.navigateByUrl(href);
    } else if (href) {
      anchor.setAttribute('target', '_blank');
      anchor.setAttribute('rel', 'noopener noreferrer');
    }
  }

  private addVoiceMessage(role: 'user' | 'assistant', content: string): void {
    this.appendMessage({ id: crypto.randomUUID(), role, content, isVoice: true });
    void firstValueFrom(this.api.saveAssistantTranscript(this.conversationId(), role, content)).catch(
      () => undefined,
    );
  }

  private async load(conversationId: string): Promise<void> {
    this.loading.set(true);
    this.messages.set([]);
    try {
      const detail = await firstValueFrom(this.api.assistantConversation(conversationId));
      this.messages.set(
        detail.messages.map((message: AssistantMessage) => ({
          id: message.id,
          role: message.role,
          content: message.content,
          isVoice: message.isVoice,
        })),
      );
      this.titleChanged.emit(detail.title);
    } finally {
      this.loading.set(false);
      this.scrollToBottom();
    }
  }

  private appendMessage(message: DisplayMessage): void {
    this.messages.set([...this.messages(), message]);
    this.scrollToBottom();
  }

  private patchMessage(id: string, update: (message: DisplayMessage) => DisplayMessage): void {
    this.messages.set(
      this.messages().map((message) => (message.id === id ? update(message) : message)),
    );
  }

  private scrollToBottom(): void {
    setTimeout(() => {
      const element = this.scroller()?.nativeElement;
      if (element) element.scrollTop = element.scrollHeight;
    });
  }
}
