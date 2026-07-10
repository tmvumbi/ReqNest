import { isPlatformBrowser } from '@angular/common';
import { computed, effect, inject, Injectable, PLATFORM_ID, signal } from '@angular/core';
import { AuthenticatedSession, TenantAccess } from '../api/api-models';
import { AppLanguage, ThemePreference } from '../api/api-models';

const sessionKey = 'reqnest.session';
const tenantKey = 'reqnest.tenant';

@Injectable({ providedIn: 'root' })
export class SessionStore {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly browser = isPlatformBrowser(this.platformId);
  private readonly sessionState = signal<AuthenticatedSession | null>(this.readSession());
  private readonly tenantIdState = signal<string | null>(this.readTenantId());

  readonly session = this.sessionState.asReadonly();
  readonly activeTenantId = this.tenantIdState.asReadonly();
  readonly authenticated = computed(() => {
    const current = this.sessionState();
    return current !== null && new Date(current.expiresAt).getTime() > Date.now();
  });
  readonly activeTenant = computed<TenantAccess | null>(() => {
    const current = this.sessionState();
    if (!current) return null;
    return (
      current.tenants.find((tenant) => tenant.tenantId === this.tenantIdState()) ??
      current.tenants[0] ??
      null
    );
  });
  readonly roles = computed(() => this.activeTenant()?.roles ?? []);
  readonly permissions = computed(() => this.activeTenant()?.permissions ?? []);
  readonly projectPermissions = computed(() => this.activeTenant()?.projectPermissions ?? {});
  readonly isAdministrator = computed(() => this.roles().includes('TenantAdministrator'));
  readonly canManageProjects = computed(
    () =>
      this.isAdministrator() ||
      this.roles().includes('ProjectManager') ||
      this.permissions().includes('project.manage'),
  );
  readonly canMaintainTickets = computed(
    () =>
      this.canManageProjects() ||
      this.roles().includes('Contributor') ||
      this.permissions().includes('ticket.maintain') ||
      Object.values(this.projectPermissions()).some((permissions) =>
        permissions.includes('ticket.maintain'),
      ),
  );
  readonly canBulkTickets = computed(
    () =>
      this.canManageProjects() ||
      this.permissions().includes('ticket.bulk') ||
      Object.values(this.projectPermissions()).some((permissions) =>
        permissions.includes('ticket.bulk'),
      ),
  );

  constructor() {
    effect(() => {
      const active = this.activeTenant();
      if (active && active.tenantId !== this.tenantIdState()) {
        this.tenantIdState.set(active.tenantId);
      }
    });
  }

  setSession(session: AuthenticatedSession): void {
    this.sessionState.set(session);
    const tenantId = session.tenants.some((tenant) => tenant.tenantId === this.tenantIdState())
      ? this.tenantIdState()
      : (session.tenants[0]?.tenantId ?? null);
    this.tenantIdState.set(tenantId);
    if (this.browser) {
      sessionStorage.setItem(sessionKey, JSON.stringify(session));
      if (tenantId) sessionStorage.setItem(tenantKey, tenantId);
    }
  }

  canMaintainProject(projectId: string): boolean {
    return (
      this.canManageProjects() ||
      this.roles().includes('Contributor') ||
      this.permissions().includes('ticket.maintain') ||
      (this.projectPermissions()[projectId] ?? []).includes('ticket.maintain')
    );
  }

  canManageProject(projectId: string): boolean {
    return (
      this.canManageProjects() ||
      this.permissions().includes('project.manage') ||
      (this.projectPermissions()[projectId] ?? []).includes('project.manage')
    );
  }

  canArchiveProject(projectId: string): boolean {
    return (
      this.canManageProjects() ||
      this.permissions().includes('ticket.archive') ||
      (this.projectPermissions()[projectId] ?? []).includes('ticket.archive')
    );
  }

  switchTenant(tenantId: string): void {
    if (!this.sessionState()?.tenants.some((tenant) => tenant.tenantId === tenantId)) return;
    this.tenantIdState.set(tenantId);
    if (this.browser) sessionStorage.setItem(tenantKey, tenantId);
  }

  setPreferences(language: AppLanguage, theme: ThemePreference): void {
    const current = this.sessionState();
    if (!current) return;
    this.setSession({ ...current, preferredLanguage: language, themePreference: theme });
  }

  clear(): void {
    this.sessionState.set(null);
    this.tenantIdState.set(null);
    if (this.browser) {
      sessionStorage.removeItem(sessionKey);
      sessionStorage.removeItem(tenantKey);
    }
  }

  private readSession(): AuthenticatedSession | null {
    if (!this.browser) return null;
    try {
      return JSON.parse(
        sessionStorage.getItem(sessionKey) ?? 'null',
      ) as AuthenticatedSession | null;
    } catch {
      sessionStorage.removeItem(sessionKey);
      return null;
    }
  }

  private readTenantId(): string | null {
    return this.browser ? sessionStorage.getItem(tenantKey) : null;
  }
}
