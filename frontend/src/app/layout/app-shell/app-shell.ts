import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { BadgeModule } from 'primeng/badge';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { ApiClient } from '../../core/api/api-client';
import { ThemePreference } from '../../core/api/api-models';
import { I18nService } from '../../core/i18n/i18n.service';
import { SessionStore } from '../../core/session/session-store';
import { ThemeService } from '../../core/theme/theme.service';

@Component({
  selector: 'app-shell',
  imports: [
    FormsModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    BadgeModule,
    ButtonModule,
    SelectModule,
  ],
  templateUrl: './app-shell.html',
  styleUrl: './app-shell.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppShell {
  private readonly api = inject(ApiClient);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  readonly store = inject(SessionStore);
  readonly i18n = inject(I18nService);
  readonly theme = inject(ThemeService);
  readonly navigationOpen = signal(false);
  readonly unreadCount = signal(0);
  readonly lightLogoUrl = signal<string | null>(null);
  readonly darkLogoUrl = signal<string | null>(null);
  readonly themeOptions: { label: string; value: ThemePreference }[] = [
    { label: this.i18n.text('theme.system'), value: 'System' },
    { label: this.i18n.text('theme.light'), value: 'Light' },
    { label: this.i18n.text('theme.dark'), value: 'Dark' },
  ];

  constructor() {
    const session = this.store.session();
    if (session) {
      this.i18n.setLanguage(session.preferredLanguage);
      this.theme.setPreference(session.themePreference);
    }
    void this.refreshNotifications();
    void this.loadBranding();
    const timer = setInterval(() => void this.refreshNotifications(), 10_000);
    this.destroyRef.onDestroy(() => {
      clearInterval(timer);
      if (this.lightLogoUrl()) URL.revokeObjectURL(this.lightLogoUrl()!);
      if (this.darkLogoUrl()) URL.revokeObjectURL(this.darkLogoUrl()!);
    });
  }

  async signOut(): Promise<void> {
    try {
      await firstValueFrom(this.api.logout());
    } finally {
      this.store.clear();
      await this.router.navigate(['/login']);
    }
  }

  switchTenant(tenantId: string): void {
    if (tenantId === this.store.activeTenantId()) return;
    this.store.switchTenant(tenantId);
    window.location.assign('/app/dashboard');
  }

  setLanguage(): void {
    this.i18n.toggleLanguage();
    this.store.setPreferences(this.i18n.language(), this.theme.preference());
    void firstValueFrom(this.api.updatePreferences(this.i18n.language(), this.theme.preference()));
  }

  setTheme(preference: ThemePreference): void {
    this.theme.setPreference(preference);
    this.store.setPreferences(this.i18n.language(), preference);
    void firstValueFrom(this.api.updatePreferences(this.i18n.language(), preference));
  }

  closeNavigation(): void {
    this.navigationOpen.set(false);
  }

  skipToContent(event: Event): void {
    event.preventDefault();
    document.getElementById('main-content')?.focus();
  }

  private async refreshNotifications(): Promise<void> {
    try {
      this.unreadCount.set((await firstValueFrom(this.api.notifications(true))).unreadCount);
    } catch {
      // The active view owns user-facing errors; the shell retries quietly.
    }
  }

  private async loadBranding(): Promise<void> {
    try {
      const settings = await firstValueFrom(this.api.tenantSettings());
      document.documentElement.style.setProperty('--tenant-accent', settings.primaryColor);
      if (settings.hasLogo)
        this.lightLogoUrl.set(URL.createObjectURL(await firstValueFrom(this.api.logo('light'))));
      if (settings.hasDarkLogo)
        this.darkLogoUrl.set(URL.createObjectURL(await firstValueFrom(this.api.logo('dark'))));
    } catch {
      // The text brand remains available if custom branding is absent.
    }
  }
}
