import { Routes } from '@angular/router';
import {
  authGuard,
  projectManagerGuard,
  tenantAdministratorGuard,
  ticketMaintainerGuard,
} from './core/session/auth-guard';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./features/public/landing-page/landing-page').then((module) => module.LandingPage),
  },
  {
    path: 'login',
    data: { mode: 'login' },
    loadComponent: () =>
      import('./features/auth/auth-page/auth-page').then((module) => module.AuthPage),
  },
  {
    path: 'register',
    data: { mode: 'register' },
    loadComponent: () =>
      import('./features/auth/auth-page/auth-page').then((module) => module.AuthPage),
  },
  {
    path: 'forgot-password',
    data: { mode: 'reset' },
    loadComponent: () =>
      import('./features/auth/auth-page/auth-page').then((module) => module.AuthPage),
  },
  {
    path: 'reset-password',
    data: { mode: 'reset' },
    loadComponent: () =>
      import('./features/auth/token-action-page/token-action-page').then(
        (module) => module.TokenActionPage,
      ),
  },
  {
    path: 'accept-invitation',
    data: { mode: 'invitation' },
    loadComponent: () =>
      import('./features/auth/token-action-page/token-action-page').then(
        (module) => module.TokenActionPage,
      ),
  },
  {
    path: 'app',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/app-shell/app-shell').then((module) => module.AppShell),
    children: [
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./features/dashboard/dashboard-page').then((module) => module.DashboardPage),
      },
      {
        path: 'tickets',
        loadComponent: () =>
          import('./features/tickets/ticket-list-page').then((module) => module.TicketListPage),
      },
      {
        path: 'tickets/new',
        canActivate: [ticketMaintainerGuard],
        loadComponent: () =>
          import('./features/tickets/ticket-create-page').then((module) => module.TicketCreatePage),
      },
      {
        path: 'tickets/:ticketId',
        loadComponent: () =>
          import('./features/tickets/ticket-detail-page').then((module) => module.TicketDetailPage),
      },
      {
        path: 'projects',
        loadComponent: () =>
          import('./features/projects/projects-page').then((module) => module.ProjectsPage),
      },
      {
        path: 'reports',
        loadComponent: () =>
          import('./features/reports/reports-page').then((module) => module.ReportsPage),
      },
      {
        path: 'notifications',
        loadComponent: () =>
          import('./features/notifications/notifications-page').then(
            (module) => module.NotificationsPage,
          ),
      },
      {
        path: 'admin/users',
        canActivate: [projectManagerGuard],
        loadComponent: () =>
          import('./features/admin/users/users-page').then((module) => module.UsersPage),
      },
      {
        path: 'admin/workflows',
        canActivate: [projectManagerGuard],
        loadComponent: () =>
          import('./features/admin/workflows/workflows-page').then(
            (module) => module.WorkflowsPage,
          ),
      },
      {
        path: 'admin/settings',
        canActivate: [tenantAdministratorGuard],
        loadComponent: () =>
          import('./features/admin/settings/settings-page').then((module) => module.SettingsPage),
      },
      {
        path: 'admin/audit',
        canActivate: [tenantAdministratorGuard],
        loadComponent: () =>
          import('./features/admin/audit/audit-page').then((module) => module.AuditPage),
      },
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
    ],
  },
  {
    path: '**',
    redirectTo: '',
  },
];
