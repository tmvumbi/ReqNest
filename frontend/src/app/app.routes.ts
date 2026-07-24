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
      import('./features/public/product-selector/product-selector-page').then(
        (module) => module.ProductSelectorPage,
      ),
  },
  {
    path: 'support',
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
    path: 'portal/:tenantId',
    loadComponent: () =>
      import('./features/public/requester-portal/requester-portal-page').then(
        (module) => module.RequesterPortalPage,
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
        path: 'assistant',
        loadComponent: () =>
          import('./features/assistant/assistant-list-page').then(
            (module) => module.AssistantListPage,
          ),
      },
      {
        path: 'assistant/:conversationId',
        loadComponent: () =>
          import('./features/assistant/assistant-chat-page').then(
            (module) => module.AssistantChatPage,
          ),
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
        path: 'tickets/:ticketId/edit',
        canActivate: [ticketMaintainerGuard],
        loadComponent: () =>
          import('./features/tickets/ticket-edit-page').then((module) => module.TicketEditPage),
      },
      {
        path: 'profile',
        loadComponent: () =>
          import('./features/profile/profile-page').then((module) => module.ProfilePage),
      },
      {
        path: 'projects',
        loadComponent: () =>
          import('./features/projects/projects-page').then((module) => module.ProjectsPage),
      },
      {
        path: 'projects/:projectId',
        loadComponent: () =>
          import('./features/projects/project-detail-page').then(
            (module) => module.ProjectDetailPage,
          ),
      },
      {
        path: 'reports',
        loadComponent: () =>
          import('./features/reports/reports-page').then((module) => module.ReportsPage),
      },
      {
        path: 'knowledge',
        loadComponent: () =>
          import('./features/knowledge/knowledge-page').then((module) => module.KnowledgePage),
      },
      {
        path: 'knowledge/new',
        canActivate: [projectManagerGuard],
        loadComponent: () =>
          import('./features/knowledge/knowledge-editor-page').then(
            (module) => module.KnowledgeEditorPage,
          ),
      },
      {
        path: 'knowledge/:articleId',
        loadComponent: () =>
          import('./features/knowledge/knowledge-detail-page').then(
            (module) => module.KnowledgeDetailPage,
          ),
      },
      {
        path: 'knowledge/:articleId/edit',
        canActivate: [projectManagerGuard],
        loadComponent: () =>
          import('./features/knowledge/knowledge-editor-page').then(
            (module) => module.KnowledgeEditorPage,
          ),
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
        path: 'admin/users/invite',
        canActivate: [projectManagerGuard],
        loadComponent: () =>
          import('./features/admin/users/users-invite-page').then(
            (module) => module.UsersInvitePage,
          ),
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
        path: 'admin/workflows/new',
        canActivate: [projectManagerGuard],
        loadComponent: () =>
          import('./features/admin/workflows/workflow-editor-page').then(
            (module) => module.WorkflowEditorPage,
          ),
      },
      {
        path: 'admin/workflows/:workflowId/edit',
        canActivate: [projectManagerGuard],
        loadComponent: () =>
          import('./features/admin/workflows/workflow-editor-page').then(
            (module) => module.WorkflowEditorPage,
          ),
      },
      {
        path: 'admin/operations',
        canActivate: [tenantAdministratorGuard],
        loadComponent: () =>
          import('./features/admin/operations/operations-page').then(
            (module) => module.OperationsPage,
          ),
      },
      {
        path: 'admin/integrations',
        canActivate: [tenantAdministratorGuard],
        loadComponent: () =>
          import('./features/admin/integrations/integrations-page').then(
            (module) => module.IntegrationsPage,
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
