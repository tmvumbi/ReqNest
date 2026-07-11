import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export type IconName =
  | 'dashboard'
  | 'tickets'
  | 'projects'
  | 'reports'
  | 'knowledge'
  | 'bell'
  | 'users'
  | 'workflows'
  | 'operations'
  | 'integrations'
  | 'settings'
  | 'audit'
  | 'search'
  | 'plus'
  | 'globe'
  | 'theme'
  | 'sign-out'
  | 'chevron-down'
  | 'inbox'
  | 'sparkles'
  | 'mic'
  | 'mic-off'
  | 'send'
  | 'expand'
  | 'minimize'
  | 'close'
  | 'pip';

@Component({
  selector: 'app-icon',
  template: `
    <svg
      [attr.width]="size()"
      [attr.height]="size()"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      stroke-width="1.8"
      stroke-linecap="round"
      stroke-linejoin="round"
      aria-hidden="true"
    >
      @switch (name()) {
        @case ('dashboard') {
          <rect x="3" y="3" width="7" height="9" rx="1.5" />
          <rect x="14" y="3" width="7" height="5" rx="1.5" />
          <rect x="14" y="12" width="7" height="9" rx="1.5" />
          <rect x="3" y="16" width="7" height="5" rx="1.5" />
        }
        @case ('tickets') {
          <path
            d="M4 8a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2v1.5a2.5 2.5 0 0 0 0 5V16a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2v-1.5a2.5 2.5 0 0 0 0-5Z"
          />
          <path d="M14 6v12" stroke-dasharray="2.5 2.5" />
        }
        @case ('projects') {
          <path d="M3.5 7a2 2 0 0 1 2-2h4l2 2.5h7a2 2 0 0 1 2 2V17a2 2 0 0 1-2 2h-13a2 2 0 0 1-2-2Z" />
        }
        @case ('reports') {
          <path d="M4 20V10" />
          <path d="M10 20V4" />
          <path d="M16 20v-7" />
          <path d="M22 20H2" />
        }
        @case ('knowledge') {
          <path d="M12 6.5c-1.6-1.5-3.8-2-6.5-2v13c2.7 0 4.9.5 6.5 2 1.6-1.5 3.8-2 6.5-2v-13c-2.7 0-4.9.5-6.5 2Z" />
          <path d="M12 6.5v13" />
        }
        @case ('bell') {
          <path d="M18 9a6 6 0 1 0-12 0c0 5-2 6-2 6h16s-2-1-2-6" />
          <path d="M10 19a2.2 2.2 0 0 0 4 0" />
        }
        @case ('users') {
          <circle cx="9" cy="8.5" r="3.2" />
          <path d="M3.5 19.5c.6-3 2.8-4.5 5.5-4.5s4.9 1.5 5.5 4.5" />
          <path d="M16 5.6a3.2 3.2 0 0 1 0 5.8" />
          <path d="M17.8 15.4c1.6.7 2.5 2 2.9 4.1" />
        }
        @case ('workflows') {
          <circle cx="5.5" cy="6" r="2.5" />
          <circle cx="18.5" cy="18" r="2.5" />
          <path d="M8 6h6.5a3 3 0 0 1 3 3v2" />
          <path d="M16 18H9.5a3 3 0 0 1-3-3v-2" />
        }
        @case ('operations') {
          <path d="M4 8h10" />
          <path d="M18 8h2" />
          <circle cx="16" cy="8" r="2" />
          <path d="M4 16h2" />
          <path d="M10 16h10" />
          <circle cx="8" cy="16" r="2" />
        }
        @case ('integrations') {
          <path d="M9 7V3.5" />
          <path d="M15 7V3.5" />
          <path d="M7 7h10v4a5 5 0 0 1-10 0Z" />
          <path d="M12 16v4.5" />
        }
        @case ('settings') {
          <circle cx="12" cy="12" r="3" />
          <path
            d="M19.4 15a1.7 1.7 0 0 0 .34 1.88l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.7 1.7 0 0 0-1.88-.34 1.7 1.7 0 0 0-1 1.55V21a2 2 0 1 1-4 0v-.09a1.7 1.7 0 0 0-1-1.55 1.7 1.7 0 0 0-1.88.34l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.7 1.7 0 0 0 .34-1.88 1.7 1.7 0 0 0-1.55-1H3a2 2 0 1 1 0-4h.09a1.7 1.7 0 0 0 1.55-1 1.7 1.7 0 0 0-.34-1.88l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.7 1.7 0 0 0 1.88.34h.01a1.7 1.7 0 0 0 1-1.55V3a2 2 0 1 1 4 0v.09a1.7 1.7 0 0 0 1 1.55 1.7 1.7 0 0 0 1.88-.34l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.7 1.7 0 0 0-.34 1.88v.01a1.7 1.7 0 0 0 1.55 1H21a2 2 0 1 1 0 4h-.09a1.7 1.7 0 0 0-1.55 1Z"
          />
        }
        @case ('audit') {
          <path d="M12 3 5 6v5c0 4.5 3 8.3 7 10 4-1.7 7-5.5 7-10V6Z" />
          <path d="m9.5 12 2 2 3.5-4" />
        }
        @case ('search') {
          <circle cx="11" cy="11" r="6.5" />
          <path d="m20 20-4.4-4.4" />
        }
        @case ('plus') {
          <path d="M12 5v14" />
          <path d="M5 12h14" />
        }
        @case ('globe') {
          <circle cx="12" cy="12" r="9" />
          <path d="M3 12h18" />
          <path d="M12 3a13.5 13.5 0 0 1 0 18 13.5 13.5 0 0 1 0-18Z" />
        }
        @case ('theme') {
          <circle cx="12" cy="12" r="9" />
          <path d="M12 3a9 9 0 0 0 0 18Z" fill="currentColor" stroke="none" />
        }
        @case ('sign-out') {
          <path d="M9 4H6a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h3" />
          <path d="m15 8 4 4-4 4" />
          <path d="M19 12H9" />
        }
        @case ('chevron-down') {
          <path d="m6 9 6 6 6-6" />
        }
        @case ('inbox') {
          <path d="M4 5h16v14H4Z" />
          <path d="M4 13h4.5l1.5 2.5h4L15.5 13H20" />
        }
        @case ('sparkles') {
          <path d="M12 4.5 13.8 9l4.5 1.8-4.5 1.8L12 17l-1.8-4.4L5.7 10.8 10.2 9Z" />
          <path d="M19 3.5v3" />
          <path d="M17.5 5h3" />
          <path d="M5.5 16.5v3" />
          <path d="M4 18h3" />
        }
        @case ('mic') {
          <rect x="9" y="3.5" width="6" height="11" rx="3" />
          <path d="M5.5 11.5a6.5 6.5 0 0 0 13 0" />
          <path d="M12 18v3" />
        }
        @case ('mic-off') {
          <rect x="9" y="3.5" width="6" height="11" rx="3" />
          <path d="M5.5 11.5a6.5 6.5 0 0 0 13 0" />
          <path d="M12 18v3" />
          <path d="m4 4 16 16" />
        }
        @case ('send') {
          <path d="M4.5 12 20 4.5 14.5 20l-2.6-5.8Z" />
          <path d="M11.9 14.2 20 4.5" />
        }
        @case ('expand') {
          <path d="M14 4h6v6" />
          <path d="M20 4 13 11" />
          <path d="M10 20H4v-6" />
          <path d="m4 20 7-7" />
        }
        @case ('minimize') {
          <path d="M5 12h14" />
        }
        @case ('close') {
          <path d="m6 6 12 12" />
          <path d="M18 6 6 18" />
        }
        @case ('pip') {
          <rect x="3" y="4.5" width="18" height="15" rx="2" />
          <rect x="11.5" y="11.5" width="7" height="5.5" rx="1" />
        }
      }
    </svg>
  `,
  styles: `
    :host {
      display: inline-flex;
      flex-shrink: 0;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Icon {
  readonly name = input.required<IconName>();
  readonly size = input(18);
}
