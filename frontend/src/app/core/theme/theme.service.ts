import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { effect, inject, Injectable, PLATFORM_ID, signal } from '@angular/core';
import { ThemePreference } from '../api/api-models';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));
  readonly preference = signal<ThemePreference>(this.initialPreference());
  readonly darkActive = signal(false);

  constructor() {
    effect((cleanup) => {
      const preference = this.preference();
      if (!this.browser) return;
      const media =
        typeof window.matchMedia === 'function'
          ? window.matchMedia('(prefers-color-scheme: dark)')
          : ({
              matches: false,
              addEventListener: () => undefined,
              removeEventListener: () => undefined,
            } as Pick<MediaQueryList, 'matches' | 'addEventListener' | 'removeEventListener'>);
      const apply = () => {
        const dark = preference === 'Dark' || (preference === 'System' && media.matches);
        this.darkActive.set(dark);
        this.document.documentElement.classList.toggle('reqnest-dark', dark);
        this.document.documentElement.style.colorScheme = dark ? 'dark' : 'light';
      };
      apply();
      media.addEventListener('change', apply);
      cleanup(() => media.removeEventListener('change', apply));
    });
  }

  setPreference(preference: ThemePreference): void {
    this.preference.set(preference);
    if (this.browser) localStorage.setItem('reqnest.theme', preference);
  }

  private initialPreference(): ThemePreference {
    if (!this.browser) return 'System';
    const stored = localStorage.getItem('reqnest.theme');
    return stored === 'Light' || stored === 'Dark' || stored === 'System' ? stored : 'System';
  }
}
