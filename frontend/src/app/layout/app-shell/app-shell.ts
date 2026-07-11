import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, firstValueFrom } from 'rxjs';
import { BadgeModule } from 'primeng/badge';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { ToastModule } from 'primeng/toast';
import { ApiClient } from '../../core/api/api-client';
import { AppNotification, ThemePreference } from '../../core/api/api-models';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { I18nService } from '../../core/i18n/i18n.service';
import { SessionStore } from '../../core/session/session-store';
import { ThemeService } from '../../core/theme/theme.service';
import { AssistantDock } from '../../features/assistant/assistant-dock';
import { Icon } from '../icons/icon';

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
    ToastModule,
    AssistantDock,
    Icon,
    LocalizedDatePipe,
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
  readonly userMenuOpen = signal(false);
  readonly notificationsOpen = signal(false);
  readonly adminOpen = signal(this.router.url.startsWith('/app/admin'));
  readonly recentNotifications = signal<AppNotification[]>([]);
  readonly unreadCount = signal(0);
  readonly lightLogoUrl = signal<string | null>(null);
  readonly darkLogoUrl = signal<string | null>(null);
  readonly avatarUrl = signal<string | null>(null);
  searchTerm = '';
  readonly themeOptions: { label: string; value: ThemePreference }[] = [
    { label: this.i18n.text('theme.system'), value: 'System' },
    { label: this.i18n.text('theme.light'), value: 'Light' },
    { label: this.i18n.text('theme.dark'), value: 'Dark' },
  ];

  private readonly currentUrl = signal(this.router.url);

  readonly breadcrumbs = computed<string[]>(() => {
    const url = this.currentUrl().split('?')[0];
    const t = (key: Parameters<I18nService['text']>[0]) => this.i18n.text(key);
    if (url.startsWith('/app/profile')) return [t('profile.title')];
    if (url.startsWith('/app/assistant')) return [t('nav.assistant')];
    if (url.startsWith('/app/tickets/new')) return [t('nav.board'), t('tickets.new')];
    if (/^\/app\/tickets\/.+/.test(url)) return [t('nav.board'), t('tickets.details')];
    if (url.startsWith('/app/tickets')) return [t('nav.board')];
    if (url.startsWith('/app/knowledge/new')) return [t('nav.knowledge'), t('common.create')];
    if (/^\/app\/knowledge\/.+\/edit$/.test(url)) return [t('nav.knowledge'), t('common.edit')];
    if (/^\/app\/knowledge\/.+/.test(url)) return [t('nav.knowledge')];
    if (url.startsWith('/app/notifications')) return [t('notifications.settings')];
    if (url.startsWith('/app/admin/users/invite')) return [t('nav.admin'), t('admin.invite')];
    if (url.startsWith('/app/admin/workflows/')) return [t('nav.admin'), t('nav.workflows')];
    if (url.startsWith('/app/projects')) return [t('nav.projects')];
    if (url.startsWith('/app/reports')) return [t('nav.reports')];
    if (url.startsWith('/app/knowledge')) return [t('nav.knowledge')];
    if (url.startsWith('/app/admin/users')) return [t('nav.admin'), t('nav.users')];
    if (url.startsWith('/app/admin/workflows')) return [t('nav.admin'), t('nav.workflows')];
    if (url.startsWith('/app/admin/operations')) return [t('nav.admin'), t('nav.operations')];
    if (url.startsWith('/app/admin/integrations')) return [t('nav.admin'), t('nav.integrations')];
    if (url.startsWith('/app/admin/settings')) return [t('nav.admin'), t('nav.settings')];
    if (url.startsWith('/app/admin/audit')) return [t('nav.admin'), t('nav.audit')];
    return [t('nav.dashboard')];
  });

  readonly userInitials = computed(() => {
    const name = this.store.session()?.displayName?.trim() || '?';
    const parts = name.split(/\s+/);
    return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase() || name[0].toUpperCase();
  });

  constructor() {
    const session = this.store.session();
    if (session) {
      this.i18n.setLanguage(session.preferredLanguage);
      this.theme.setPreference(session.themePreference);
    }
    void this.refreshNotifications();
    void this.loadBranding();
    void this.loadAvatar();
    const timer = setInterval(() => void this.refreshNotifications(), 10_000);
    this.destroyRef.onDestroy(() => {
      clearInterval(timer);
      if (this.lightLogoUrl()) URL.revokeObjectURL(this.lightLogoUrl()!);
      if (this.darkLogoUrl()) URL.revokeObjectURL(this.darkLogoUrl()!);
      if (this.avatarUrl()) URL.revokeObjectURL(this.avatarUrl()!);
    });
    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe((event) => {
        if (this.currentUrl().startsWith('/app/profile')) {
          void this.loadAvatar();
        }
        this.currentUrl.set(event.urlAfterRedirects);
        this.userMenuOpen.set(false);
        this.notificationsOpen.set(false);
        if (event.urlAfterRedirects.startsWith('/app/admin')) this.adminOpen.set(true);
        document.getElementById('main-content')?.scrollTo({ top: 0, behavior: 'instant' });
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

  submitSearch(): void {
    const search = this.searchTerm.trim();
    void this.router.navigate(['/app/tickets'], search ? { queryParams: { search } } : {});
  }

  async toggleNotifications(): Promise<void> {
    const opening = !this.notificationsOpen();
    this.notificationsOpen.set(opening);
    if (!opening) return;
    try {
      const result = await firstValueFrom(this.api.notifications(false));
      this.recentNotifications.set(result.items.slice(0, 12));
      this.unreadCount.set(result.unreadCount);
    } catch {
      this.recentNotifications.set([]);
    }
  }

  notificationSummary(item: AppNotification): string {
    return item.summary;
  }

  async openNotification(item: AppNotification): Promise<void> {
    this.notificationsOpen.set(false);
    if (!item.readAt) {
      try {
        await firstValueFrom(this.api.setNotificationRead(item, true));
        this.unreadCount.update((count) => Math.max(0, count - 1));
      } catch {
        // Navigation still proceeds; the notification stays unread.
      }
    }
    await this.router.navigateByUrl(item.deepLink);
  }

  async markAllNotificationsRead(): Promise<void> {
    await firstValueFrom(this.api.markAllNotificationsRead());
    this.unreadCount.set(0);
    this.recentNotifications.update((items) =>
      items.map((item) => ({ ...item, readAt: item.readAt ?? new Date().toISOString() })),
    );
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

  private async loadAvatar(): Promise<void> {
    try {
      const profile = await firstValueFrom(this.api.profile());
      if (this.store.session() && profile.displayName !== this.store.session()!.displayName) {
        this.store.setDisplayName(profile.displayName);
      }
      if (this.avatarUrl()) {
        URL.revokeObjectURL(this.avatarUrl()!);
        this.avatarUrl.set(null);
      }
      if (profile.hasAvatar) {
        this.avatarUrl.set(URL.createObjectURL(await firstValueFrom(this.api.avatar())));
      }
    } catch {
      this.avatarUrl.set(null);
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
