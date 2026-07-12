import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnDestroy,
  inject,
  input,
  signal,
} from '@angular/core';
import type Quill from 'quill';
import { Member } from '../api/api-models';
import { registerMentionBlot } from './mention-blot';

// Inline "@" autocomplete for a Quill editor: wire it up with
// (onEditorInit)="menu.attach($event)". Typing "@" plus at least one character
// opens a people picker; selecting inserts a mention blot the backend uses to
// notify the mentioned user.
@Component({
  selector: 'app-mention-autocomplete',
  template: `
    @if (open()) {
      <ul class="mention-menu" role="listbox" [style.top.px]="top()" [style.left.px]="left()">
        @for (member of options(); track member.userId; let index = $index) {
          <li
            role="option"
            [attr.aria-selected]="index === activeIndex()"
            [class.active]="index === activeIndex()"
            (mousedown)="$event.preventDefault(); select(member)"
            (mousemove)="activeIndex.set(index)"
          >
            <span class="mention-option-avatar" aria-hidden="true">{{
              initials(member.displayName)
            }}</span>
            <span class="mention-option-text">
              <span class="mention-option-name">{{ member.displayName }}</span>
              <span class="mention-option-email">{{ member.email }}</span>
            </span>
          </li>
        }
      </ul>
    }
  `,
  styles: `
    .mention-menu {
      position: fixed;
      z-index: 1200;
      min-width: 240px;
      max-width: 300px;
      margin: 0;
      padding: 0.3rem;
      list-style: none;
      border: 1px solid var(--p-content-border-color, #e2e8f0);
      border-radius: 0.75rem;
      background: var(--p-content-background, #fff);
      box-shadow: 0 12px 32px rgba(15, 23, 42, 0.16);
    }
    .mention-menu li {
      display: flex;
      align-items: center;
      gap: 0.55rem;
      padding: 0.4rem 0.55rem;
      border-radius: 0.55rem;
      cursor: pointer;
    }
    .mention-menu li.active {
      background: color-mix(in srgb, var(--p-primary-color) 12%, transparent);
    }
    .mention-option-avatar {
      display: inline-flex;
      flex: none;
      align-items: center;
      justify-content: center;
      width: 1.7rem;
      height: 1.7rem;
      border-radius: 50%;
      background: var(--p-primary-color);
      color: var(--p-primary-contrast-color, #fff);
      font-size: 0.7rem;
      font-weight: 700;
    }
    .mention-option-text {
      display: flex;
      flex-direction: column;
      min-width: 0;
    }
    .mention-option-name {
      font-size: 0.9rem;
      font-weight: 600;
    }
    .mention-option-email {
      overflow: hidden;
      font-size: 0.75rem;
      color: var(--app-text-muted, #64748b);
      text-overflow: ellipsis;
      white-space: nowrap;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MentionAutocomplete implements OnDestroy {
  readonly members = input.required<Member[]>();
  protected readonly open = signal(false);
  protected readonly options = signal<Member[]>([]);
  protected readonly activeIndex = signal(0);
  protected readonly top = signal(0);
  protected readonly left = signal(0);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private quill: Quill | null = null;
  private atIndex = 0;
  private query = '';

  constructor() {
    registerMentionBlot();
  }

  // PrimeNG types onEditorInit's payload as Event even though it emits
  // an EditorInitEvent carrying the Quill instance.
  attach(event: Event): void {
    this.detach();
    this.quill = (event as unknown as { editor: Quill }).editor;
    this.quill.on('text-change', this.onTextChange);
    this.quill.on('selection-change', this.onSelectionChange);
    this.quill.container.addEventListener('keydown', this.onKeydown, true);
    document.addEventListener('mousedown', this.onDocumentMousedown);
    window.addEventListener('scroll', this.onViewportChange, true);
    window.addEventListener('resize', this.onViewportChange);
  }

  ngOnDestroy(): void {
    this.detach();
  }

  protected initials(name: string): string {
    const parts = name.trim().split(/\s+/);
    return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase() || '?';
  }

  protected select(member: Member): void {
    const quill = this.quill;
    if (!quill) return;
    const name = member.displayName;
    quill.deleteText(this.atIndex, this.query.length + 1, 'user');
    quill.insertText(this.atIndex, `@${name}`, 'mention', member.userId, 'user');
    quill.insertText(this.atIndex + name.length + 1, ' ', 'mention', false, 'user');
    quill.setSelection(this.atIndex + name.length + 2, 0, 'silent');
    this.close();
  }

  private detach(): void {
    if (this.quill) {
      this.quill.off('text-change', this.onTextChange);
      this.quill.off('selection-change', this.onSelectionChange);
      this.quill.container.removeEventListener('keydown', this.onKeydown, true);
      this.quill = null;
    }
    document.removeEventListener('mousedown', this.onDocumentMousedown);
    window.removeEventListener('scroll', this.onViewportChange, true);
    window.removeEventListener('resize', this.onViewportChange);
    this.close();
  }

  private close(): void {
    this.open.set(false);
  }

  private evaluate(): void {
    const quill = this.quill;
    if (!quill) return;
    const range = quill.getSelection();
    if (!range || range.length > 0) {
      this.close();
      return;
    }
    const lookback = Math.min(range.index, 80);
    const before = quill.getText(range.index - lookback, lookback);
    const match = /(?:^|[\s(])@([\p{L}\p{N}][^@\n]{0,40})$/u.exec(before);
    if (!match) {
      this.close();
      return;
    }
    const query = match[1];
    const atIndex = range.index - query.length - 1;
    const formats = quill.getFormat(atIndex, 1);
    if (formats['mention'] || formats['code-block'] || formats['code']) {
      this.close();
      return;
    }
    const needle = query.toLowerCase();
    const matches = this.members()
      .filter(
        (member) =>
          member.displayName.toLowerCase().includes(needle) ||
          member.email.toLowerCase().includes(needle),
      )
      .slice(0, 8);
    const bounds = quill.getBounds(range.index);
    if (!matches.length || !bounds) {
      this.close();
      return;
    }
    const containerRect = quill.container.getBoundingClientRect();
    this.atIndex = atIndex;
    this.query = query;
    this.options.set(matches);
    this.activeIndex.set(0);
    this.top.set(containerRect.top + bounds.bottom + 4);
    this.left.set(Math.max(8, Math.min(containerRect.left + bounds.left, window.innerWidth - 308)));
    this.open.set(true);
  }

  private readonly onTextChange = (_delta: unknown, _previous: unknown, source: string): void => {
    if (source === 'user') this.evaluate();
  };

  private readonly onSelectionChange = (): void => {
    if (this.open()) this.evaluate();
  };

  private readonly onKeydown = (event: KeyboardEvent): void => {
    if (!this.open()) return;
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      const count = this.options().length;
      const delta = event.key === 'ArrowDown' ? 1 : -1;
      this.activeIndex.set((this.activeIndex() + delta + count) % count);
    } else if (event.key === 'Enter' || event.key === 'Tab') {
      this.select(this.options()[this.activeIndex()]);
    } else if (event.key === 'Escape') {
      this.close();
    } else {
      return;
    }
    event.preventDefault();
    event.stopPropagation();
  };

  private readonly onDocumentMousedown = (event: MouseEvent): void => {
    if (!this.open()) return;
    const target = event.target as Node;
    if (this.host.nativeElement.contains(target) || this.quill?.root.contains(target)) return;
    this.close();
  };

  private readonly onViewportChange = (): void => {
    if (this.open()) this.evaluate();
  };
}
