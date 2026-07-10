import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import {
  AppLanguage,
  AppNotification,
  AppRole,
  AuditPage,
  AuthenticatedSession,
  Dashboard,
  Member,
  PagedNotifications,
  PagedTickets,
  Project,
  ProjectOverview,
  ReportData,
  ReportExport,
  TenantSettings,
  ThemePreference,
  TicketActivity,
  TicketAttachment,
  TicketComment,
  TicketDetail,
  TicketPriority,
  TicketType,
  Workflow,
  NotificationPreferences,
  SavedView,
} from './api-models';

@Injectable({ providedIn: 'root' })
export class ApiClient {
  private readonly http = inject(HttpClient);

  login(email: string, password: string) {
    return this.http.post<AuthenticatedSession>('/api/auth/login', { email, password });
  }

  register(request: {
    companyName: string;
    companyShortName: string;
    displayName: string;
    email: string;
    password: string;
    language: AppLanguage;
    timeZone: string;
  }) {
    return this.http.post<AuthenticatedSession>('/api/auth/register-tenant', request);
  }

  logout() {
    return this.http.post<void>('/api/auth/logout', null);
  }

  requestPasswordReset(email: string) {
    return this.http.post<{ message: string; developmentToken: string | null }>(
      '/api/auth/request-password-reset',
      { email },
    );
  }

  resetPassword(token: string, newPassword: string) {
    return this.http.post<void>('/api/auth/reset-password', { token, newPassword });
  }

  acceptInvitation(token: string, displayName: string, password: string) {
    return this.http.post<void>('/api/auth/accept-invitation', { token, displayName, password });
  }

  dashboard() {
    return this.http.get<Dashboard>('/api/dashboard');
  }

  projects() {
    return this.http.get<Project[]>('/api/projects');
  }

  createProject(request: {
    key: string;
    nameEnglish: string;
    nameFrench: string;
    description: string;
    workflowId: string | null;
    defaultPriority: TicketPriority;
    defaultAssigneeUserId: string | null;
  }) {
    return this.http.post<Project>('/api/projects', request);
  }

  updateProject(
    projectId: string,
    request: {
      nameEnglish: string;
      nameFrench: string;
      description: string | null;
      defaultPriority: TicketPriority;
      defaultAssigneeUserId: string | null;
    },
  ) {
    return this.http.patch<Project>(`/api/projects/${projectId}`, request);
  }

  projectOverview(projectId: string) {
    return this.http.get<ProjectOverview>(`/api/projects/${projectId}/overview`);
  }

  setProjectArchived(projectId: string, archived: boolean) {
    return this.http.post<Project>(
      `/api/projects/${projectId}/${archived ? 'archive' : 'restore'}`,
      null,
    );
  }

  workflows() {
    return this.http.get<Workflow[]>('/api/workflows');
  }

  copyWorkflow(workflowId: string, projectId: string, name: string) {
    return this.http.post<Workflow>(`/api/workflows/${workflowId}/copy-to-project/${projectId}`, {
      name,
    });
  }

  createWorkflow(request: {
    name: string;
    description: string | null;
    projectId: string | null;
    statuses: {
      key: string;
      labelEnglish: string;
      labelFrench: string;
      category: string;
      order: number;
      color: string;
      isInitial: boolean;
      isTerminal: boolean;
    }[];
    transitions: {
      fromKey: string;
      toKey: string;
      nameEnglish: string | null;
      nameFrench: string | null;
      commentRequired: boolean;
    }[];
  }) {
    return this.http.post<Workflow>('/api/workflows', request);
  }

  updateWorkflow(
    workflowId: string,
    request: {
      name: string;
      description: string | null;
      isActive: boolean;
      statuses: {
        key: string;
        labelEnglish: string;
        labelFrench: string;
        category: string;
        order: number;
        color: string;
        isInitial: boolean;
        isTerminal: boolean;
      }[];
      transitions: {
        fromKey: string;
        toKey: string;
        nameEnglish: string | null;
        nameFrench: string | null;
        commentRequired: boolean;
      }[];
      statusMappings: Record<string, string>;
    },
  ) {
    return this.http.put<Workflow>(`/api/workflows/${workflowId}`, request);
  }

  assignProjectWorkflow(
    projectId: string,
    workflowId: string,
    statusMappings: Record<string, string>,
  ) {
    return this.http.put(`/api/projects/${projectId}/workflow`, { workflowId, statusMappings });
  }

  tickets(filters: { search?: string; projectId?: string; queue?: string; page?: number } = {}) {
    let params = new HttpParams().set('pageSize', 50);
    for (const [key, value] of Object.entries(filters)) {
      if (value !== undefined && value !== '') params = params.set(key, value);
    }
    return this.http.get<PagedTickets>('/api/tickets', { params });
  }

  ticket(ticketId: string) {
    return this.http.get<TicketDetail>(`/api/tickets/${ticketId}`);
  }

  createTicket(request: {
    projectId: string;
    title: string;
    description: string;
    type: TicketType;
    priority: TicketPriority;
    assigneeUserId: string | null;
    labels: string[];
    dueAt: string | null;
  }) {
    return this.http.post<TicketDetail>('/api/tickets', request, {
      headers: { 'Idempotency-Key': crypto.randomUUID() },
    });
  }

  transition(ticketId: string, toStatusId: string, version: number, comment: string | null = null) {
    return this.http.post<TicketDetail>(`/api/tickets/${ticketId}/transition`, {
      toStatusId,
      version,
      comment,
    });
  }

  updateTicket(
    ticket: TicketDetail,
    request: {
      title: string;
      description: string;
      type: TicketType;
      priority: TicketPriority;
      assigneeUserId: string | null;
      labels: string[];
      dueAt: string | null;
      resolutionSummary: string | null;
    },
  ) {
    return this.http.patch<TicketDetail>(`/api/tickets/${ticket.id}`, {
      ...request,
      version: ticket.version,
    });
  }

  setTicketArchived(ticket: TicketDetail, archived: boolean) {
    return this.http.post<TicketDetail>(
      `/api/tickets/${ticket.id}/${archived ? 'archive' : 'restore'}`,
      { version: ticket.version },
    );
  }

  bulkTickets(request: {
    ticketIds: string[];
    priority: TicketPriority | null;
    assigneeSpecified: boolean;
    assigneeUserId: string | null;
    labels: string[] | null;
    archived: boolean | null;
  }) {
    return this.http.post<{ updated: number; failures: { ticketId: string; code: string }[] }>(
      '/api/tickets/bulk',
      request,
    );
  }

  comments(ticketId: string) {
    return this.http.get<TicketComment[]>(`/api/tickets/${ticketId}/comments`);
  }

  addComment(ticketId: string, body: string, mentionUserIds: string[] = []) {
    return this.http.post<TicketComment>(`/api/tickets/${ticketId}/comments`, {
      body,
      mentionUserIds,
    });
  }

  activity(ticketId: string) {
    return this.http.get<TicketActivity[]>(`/api/tickets/${ticketId}/activity`);
  }

  uploadAttachment(ticketId: string, file: File) {
    return this.http.post(`/api/tickets/${ticketId}/attachments`, file, {
      headers: {
        'Content-Type': file.type || 'application/octet-stream',
        'X-File-Name': file.name,
      },
    });
  }

  attachments(ticketId: string) {
    return this.http.get<TicketAttachment[]>(`/api/tickets/${ticketId}/attachments`);
  }

  downloadAttachment(attachmentId: string) {
    return this.http.get(`/api/attachments/${attachmentId}`, { responseType: 'blob' });
  }

  deleteAttachment(attachmentId: string) {
    return this.http.delete<void>(`/api/attachments/${attachmentId}`);
  }

  watchTicket(ticketId: string) {
    return this.http.post<void>(`/api/tickets/${ticketId}/watchers/me`, null);
  }
  unwatchTicket(ticketId: string) {
    return this.http.delete<void>(`/api/tickets/${ticketId}/watchers/me`);
  }
  muteTicket(ticketId: string, muted: boolean) {
    return this.http.patch<void>(`/api/tickets/${ticketId}/watchers/me`, { muted });
  }

  notifications(unreadOnly = false, projectId = '', type = '') {
    let params = new HttpParams();
    if (unreadOnly) params = params.set('unread', true);
    if (projectId) params = params.set('projectId', projectId);
    if (type) params = params.set('type', type);
    return this.http.get<PagedNotifications>('/api/notifications', {
      params,
    });
  }

  setNotificationRead(notification: AppNotification, read: boolean) {
    return this.http.patch<void>(`/api/notifications/${notification.id}`, { read });
  }

  markAllNotificationsRead() {
    return this.http.post<void>('/api/notifications/mark-all-read', null);
  }

  notificationPreferences() {
    return this.http.get<NotificationPreferences>('/api/notifications/preferences');
  }
  updateNotificationPreferences(preferences: NotificationPreferences) {
    return this.http.put<NotificationPreferences>('/api/notifications/preferences', preferences);
  }

  savedViews() {
    return this.http.get<SavedView[]>('/api/saved-views');
  }
  saveView(name: string, projectId: string | null, filters: object) {
    return this.http.post<SavedView>('/api/saved-views', {
      name,
      projectId,
      filters,
      sort: { field: 'updatedAt', direction: 'desc' },
      columns: ['key', 'title', 'project', 'status', 'priority', 'assignee', 'updatedAt'],
      groupBy: null,
    });
  }
  deleteSavedView(viewId: string) {
    return this.http.delete<void>(`/api/saved-views/${viewId}`);
  }

  report(type: string, projectId?: string) {
    return this.http.get<ReportData>(`/api/reports/${type}`, {
      params: projectId ? { projectId } : {},
    });
  }

  exportReport(reportType: string, language: AppLanguage, projectId?: string) {
    return this.http.post<{ id: string }>('/api/reports/exports', {
      reportType,
      language,
      filter: {
        projectId: projectId ?? null,
        from: null,
        to: null,
        priority: null,
        type: null,
        assigneeUserId: null,
        includeArchived: false,
      },
    });
  }

  reportExports() {
    return this.http.get<ReportExport[]>('/api/reports/exports');
  }

  downloadReport(exportId: string) {
    return this.http.get(`/api/reports/exports/${exportId}/download`, { responseType: 'blob' });
  }

  members() {
    return this.http.get<Member[]>('/api/members');
  }

  invite(request: {
    email: string;
    displayName: string;
    grants: { role: AppRole; allProjects: boolean; projectIds: string[] }[];
  }) {
    return this.http.post<{ developmentToken: string | null }>('/api/members/invitations', request);
  }

  updateMemberRoles(
    membershipId: string,
    grants: { role: AppRole; allProjects: boolean; projectIds: string[] }[],
  ) {
    return this.http.put<Member>(`/api/members/${membershipId}/roles`, { grants });
  }
  resendInvitation(membershipId: string) {
    return this.http.post<{ developmentToken: string | null }>(
      `/api/members/${membershipId}/resend`,
      null,
    );
  }
  revokeInvitation(membershipId: string) {
    return this.http.post<void>(`/api/members/${membershipId}/revoke`, null);
  }
  setMemberActive(membershipId: string, active: boolean) {
    return this.http.post<void>(
      `/api/members/${membershipId}/${active ? 'activate' : 'deactivate'}`,
      null,
    );
  }

  tenantSettings() {
    return this.http.get<TenantSettings>('/api/tenants/current');
  }

  updatePreferences(language: AppLanguage, theme: ThemePreference) {
    return this.http.patch('/api/profile/preferences', { language, theme });
  }

  updateTenantSettings(request: {
    name: string;
    shortName: string;
    defaultLanguage: AppLanguage;
    timeZone: string;
    defaultTheme: ThemePreference;
    primaryColor: string;
    supportContact: string | null;
    reportFooterText: string | null;
  }) {
    return this.http.patch<TenantSettings>('/api/tenants/current', request);
  }

  uploadLogo(variant: 'light' | 'dark', file: File) {
    return this.http.post<void>(`/api/tenants/current/logos/${variant}`, file, {
      headers: { 'Content-Type': file.type },
    });
  }

  logo(variant: 'light' | 'dark') {
    return this.http.get(`/api/tenants/current/logos/${variant}`, { responseType: 'blob' });
  }

  audit() {
    return this.http.get<AuditPage>('/api/audit', { params: { pageSize: 100 } });
  }

  auditExport() {
    return this.http.get<AuditPage['items']>('/api/audit/export');
  }
}
