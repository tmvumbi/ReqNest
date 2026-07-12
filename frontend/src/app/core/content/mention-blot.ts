import Quill from 'quill';

const Inline = Quill.import('blots/inline') as typeof import('parchment').InlineBlot;

// Inline format producing <a class="mention" data-user-id="…">@Name</a>; the
// backend reads data-user-id to know who to notify, so the attribute is the
// contract — not the visible text.
export class MentionBlot extends Inline {
  static override blotName = 'mention';
  static override tagName = 'A';
  static override className = 'mention';

  static override create(value?: unknown): HTMLElement {
    const node = super.create(value) as HTMLElement;
    if (typeof value === 'string') node.setAttribute('data-user-id', value);
    return node;
  }

  static override formats(domNode: HTMLElement): string | boolean {
    return domNode.getAttribute('data-user-id') ?? true;
  }
}

let registered = false;

export function registerMentionBlot(): void {
  if (registered) return;
  registered = true;
  Quill.register(MentionBlot);
}
