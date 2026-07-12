import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import {
  AppLanguage,
  AssistantConversationDetail,
  AssistantConversationSummary,
  AssistantMessage,
  AssistantRealtimeSession,
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
  CustomFieldDefinition,
  CustomRole,
  EmailOutboxPage,
  RetentionSettings,
  SlaPolicy,
  TicketRelationship,
  TicketSchema,
  TicketTypeDefinition,
  TicketPriorityDefinition,
  PortalSettings,
  PublicPortal,
  RequesterTicket,
  ApiTokenItem,
  EmailChannel,
  WebhookItem,
  WebhookDeliveryItem,
  IntegrationConnectionItem,
  KnowledgeArticle,
  AiAssistance,
} from './api-models';

@Injectable({ providedIn: 'root' })
export class ApiClient {
  private readonly http = inject(HttpClient);

  login(email: string, password: string) {
    return this.http.post<AuthenticatedSession>('/api/auth/login', { email, password });
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

  project(projectId: string) {
    return this.http.get<Project>(`/api/projects/${projectId}`);
  }

  createProject(request: {
    key: string;
    name: string;
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
      name: string;
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
      label: string;
      category: string;
      order: number;
      color: string;
      isInitial: boolean;
      isTerminal: boolean;
    }[];
    transitions: {
      fromKey: string;
      toKey: string;
      name: string | null;
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
        label: string;
        category: string;
        order: number;
        color: string;
        isInitial: boolean;
        isTerminal: boolean;
      }[];
      transitions: {
        fromKey: string;
        toKey: string;
        name: string | null;
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
    typeKey?: string;
    priorityKey?: string;
    customFields?: Record<string, unknown>;
    parentTicketId?: string | null;
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
      typeKey?: string;
      priorityKey?: string;
      customFields?: Record<string, unknown>;
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

  previewBulkTickets(request: {
    ticketIds: string[];
    priority: TicketPriority | null;
    assigneeSpecified: boolean;
    assigneeUserId: string | null;
    labels: string[] | null;
    archived: boolean | null;
    priorityKey?: string | null;
    toStatusId?: string | null;
    transitionComment?: string | null;
  }) {
    return this.http.post<{ updated: number; failures: { ticketId: string; code: string }[] }>(
      '/api/tickets/bulk/preview',
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

  report(type: string, projectId?: string) {
    return this.http.get<ReportData>(`/api/reports/${type}`, {
      params: projectId ? { projectId } : {},
    });
  }

  reportPdf(reportType: string, language: AppLanguage, projectId?: string) {
    let params = new HttpParams().set('language', language);
    if (projectId) params = params.set('projectId', projectId);
    return this.http.get(`/api/reports/${reportType}/pdf`, { params, responseType: 'blob' });
  }

  reportCsv(reportType: string, language: AppLanguage, projectId?: string) {
    let params = new HttpParams().set('language', language);
    if (projectId) params = params.set('projectId', projectId);
    return this.http.get(`/api/reports/${reportType}/csv`, { params, responseType: 'blob' });
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

  profile() {
    return this.http.get<{ displayName: string; email: string; hasAvatar: boolean }>(
      '/api/profile',
    );
  }

  updateProfile(displayName: string) {
    return this.http.patch<{ displayName: string; email: string; hasAvatar: boolean }>(
      '/api/profile',
      { displayName },
    );
  }

  changePassword(currentPassword: string, newPassword: string) {
    return this.http.post<void>('/api/auth/change-password', { currentPassword, newPassword });
  }

  avatar() {
    return this.http.get('/api/profile/avatar', { responseType: 'blob' });
  }

  uploadAvatar(file: File) {
    return this.http.post<{ displayName: string; email: string; hasAvatar: boolean }>(
      '/api/profile/avatar',
      file,
      { headers: { 'Content-Type': file.type } },
    );
  }

  deleteAvatar() {
    return this.http.delete<void>('/api/profile/avatar');
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

  deleteLogo(variant: 'light' | 'dark') {
    return this.http.delete<void>(`/api/tenants/current/logos/${variant}`);
  }

  audit(filters?: AuditFilters) {
    return this.http.get<AuditPage>('/api/audit', {
      params: { pageSize: 100, ...compactParams(filters) },
    });
  }

  auditExport(filters?: AuditFilters) {
    return this.http.get<AuditPage['items']>('/api/audit/export', {
      params: compactParams(filters),
    });
  }

  auditCsv(filters?: AuditFilters) {
    return this.http.get('/api/audit/export.csv', {
      responseType: 'blob',
      params: compactParams(filters),
    });
  }

  ticketSchema(projectId?: string) {
    return this.http.get<TicketSchema>('/api/configuration/ticket-schema', {
      params: projectId ? { projectId } : {},
    });
  }

  createTicketType(request: Omit<TicketTypeDefinition, 'id'>) {
    return this.http.post<TicketTypeDefinition>('/api/configuration/ticket-schema/types', request);
  }

  updateTicketType(definitionId: string, request: Omit<TicketTypeDefinition, 'id'>) {
    return this.http.put<TicketTypeDefinition>(
      `/api/configuration/ticket-schema/types/${definitionId}`,
      request,
    );
  }

  deleteTicketType(definitionId: string) {
    return this.http.delete<void>(`/api/configuration/ticket-schema/types/${definitionId}`);
  }

  updateTicketPriority(definitionId: string, request: Omit<TicketPriorityDefinition, 'id'>) {
    return this.http.put<TicketPriorityDefinition>(
      `/api/configuration/ticket-schema/priorities/${definitionId}`,
      request,
    );
  }

  deleteTicketPriority(definitionId: string) {
    return this.http.delete<void>(`/api/configuration/ticket-schema/priorities/${definitionId}`);
  }

  workflowStatusUsage(workflowId: string) {
    return this.http.get<Record<string, number>>(`/api/workflows/${workflowId}/status-usage`);
  }

  createTicketPriority(request: Omit<TicketPriorityDefinition, 'id'>) {
    return this.http.post<TicketPriorityDefinition>(
      '/api/configuration/ticket-schema/priorities',
      request,
    );
  }

  createCustomField(
    request: Omit<CustomFieldDefinition, 'id' | 'optionsJson'> & { options: unknown },
  ) {
    return this.http.post<CustomFieldDefinition>(
      '/api/configuration/ticket-schema/custom-fields',
      request,
    );
  }

  slaPolicies() {
    return this.http.get<SlaPolicy[]>('/api/configuration/sla-policies');
  }

  createSlaPolicy(request: Omit<SlaPolicy, 'id'>) {
    return this.http.post<SlaPolicy>('/api/configuration/sla-policies', request);
  }

  assignSlaPolicy(policyId: string, projectId: string) {
    return this.http.put<void>(
      `/api/configuration/sla-policies/${policyId}/projects/${projectId}`,
      null,
    );
  }

  customRoles() {
    return this.http.get<CustomRole[]>('/api/custom-roles');
  }

  createCustomRole(request: {
    name: string;
    description: string | null;
    permissions: string[];
    isActive: boolean;
  }) {
    return this.http.post<CustomRole>('/api/custom-roles', request);
  }

  updateCustomRoleGrants(
    membershipId: string,
    grants: { customRoleId: string; allProjects: boolean; projectIds: string[] }[],
  ) {
    return this.http.put<void>(`/api/members/${membershipId}/custom-role-grants`, { grants });
  }

  retentionSettings() {
    return this.http.get<RetentionSettings>('/api/operations/retention');
  }

  updateRetentionSettings(request: Omit<RetentionSettings, 'storageUsedBytes'>) {
    return this.http.put<RetentionSettings>('/api/operations/retention', request);
  }

  retentionPreview() {
    return this.http.get<Record<string, number>>('/api/operations/retention/preview');
  }

  runRetention() {
    return this.http.post<Record<string, number>>('/api/operations/retention/run', null);
  }

  emailOutbox() {
    return this.http.get<EmailOutboxPage>('/api/operations/email-outbox');
  }

  retryEmail(messageId: string) {
    return this.http.post<void>(`/api/operations/email-outbox/${messageId}/retry`, null);
  }

  relationships(ticketId: string) {
    return this.http.get<TicketRelationship[]>(`/api/tickets/${ticketId}/relationships`);
  }

  createRelationship(ticketId: string, targetTicketId: string, type: TicketRelationship['type']) {
    return this.http.post<TicketRelationship>(`/api/tickets/${ticketId}/relationships`, {
      targetTicketId,
      type,
    });
  }

  setParent(ticketId: string, parentTicketId: string | null) {
    return this.http.put<void>(`/api/tickets/${ticketId}/parent`, { parentTicketId });
  }

  previewAttachment(attachmentId: string) {
    return this.http.get(`/api/attachments/${attachmentId}/preview`, { responseType: 'blob' });
  }

  publicPortal(tenantId: string) {
    return this.http.get<PublicPortal>(`/api/public/portal/${tenantId}`);
  }

  submitRequesterTicket(tenantId: string, request: object) {
    return this.http.post<{ ticketId: string; key: string; accessToken: string }>(
      `/api/public/portal/${tenantId}/tickets`,
      request,
    );
  }

  requesterTicket(ticketId: string, accessToken: string) {
    return this.http.get<RequesterTicket>(`/api/public/portal/tickets/${ticketId}`, {
      headers: { 'X-Requester-Token': accessToken },
    });
  }

  requesterComment(ticketId: string, accessToken: string, body: string) {
    return this.http.post(
      `/api/public/portal/tickets/${ticketId}/comments`,
      { body },
      { headers: { 'X-Requester-Token': accessToken } },
    );
  }

  portalSettings() {
    return this.http.get<PortalSettings>('/api/portal/settings');
  }
  updatePortalSettings(request: {
    isEnabled: boolean;
    introduction: string;
  }) {
    return this.http.put<void>('/api/portal/settings', request);
  }
  setPortalProject(projectId: string, isEnabled: boolean) {
    return this.http.put<void>(`/api/portal/projects/${projectId}`, { isEnabled });
  }

  apiTokens() {
    return this.http.get<ApiTokenItem[]>('/api/api-tokens');
  }
  createApiToken(request: {
    name: string;
    scopes: string[];
    projectIds: string[];
    expiresAt: string | null;
  }) {
    return this.http.post<{ token: ApiTokenItem; rawToken: string }>('/api/api-tokens', request);
  }
  revokeApiToken(id: string) {
    return this.http.post<void>(`/api/api-tokens/${id}/revoke`, null);
  }

  emailChannels() {
    return this.http.get<EmailChannel[]>('/api/integrations/inbound-email');
  }
  createEmailChannel(request: Omit<EmailChannel, 'id' | 'createdAt'>) {
    return this.http.post<{ channel: EmailChannel; rawSecret: string }>(
      '/api/integrations/inbound-email',
      request,
    );
  }
  webhooks() {
    return this.http.get<WebhookItem[]>('/api/integrations/webhooks');
  }
  createWebhook(request: Omit<WebhookItem, 'id' | 'createdAt'>) {
    return this.http.post<{ webhook: WebhookItem; rawSecret: string }>(
      '/api/integrations/webhooks',
      request,
    );
  }
  testWebhook(id: string) {
    return this.http.post(`/api/integrations/webhooks/${id}/test`, null);
  }
  webhookDeliveries() {
    return this.http.get<WebhookDeliveryItem[]>('/api/integrations/webhooks/deliveries');
  }
  connections() {
    return this.http.get<IntegrationConnectionItem[]>('/api/integrations/connections');
  }
  upsertConnection(request: { provider: string; name: string; configuration: object }) {
    return this.http.post<IntegrationConnectionItem>('/api/integrations/connections', request);
  }
  testConnection(id: string) {
    return this.http.post<IntegrationConnectionItem>(
      `/api/integrations/connections/${id}/test`,
      null,
    );
  }
  aiConfiguration() {
    return this.http.get<{
      isEnabled: boolean;
      provider: string;
      allowedKinds: AiAssistance['kind'][];
      requireHumanReview: boolean;
      allowAttachmentContent: boolean;
      hasCredential: boolean;
      providerDoesNotTrain: boolean;
      evaluationVersion: string;
    }>('/api/integrations/ai');
  }
  updateAi(request: object) {
    return this.http.put<void>('/api/integrations/ai', request);
  }

  knowledge(search?: string, projectId?: string) {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    if (projectId) params = params.set('projectId', projectId);
    return this.http.get<KnowledgeArticle[]>('/api/knowledge', { params });
  }
  publicKnowledge(tenantId: string, search?: string) {
    return this.http.get<KnowledgeArticle[]>(`/api/public/portal/${tenantId}/knowledge`, {
      params: search ? { search } : {},
    });
  }
  saveKnowledge(
    request: Omit<KnowledgeArticle, 'id' | 'status' | 'publishedAt' | 'updatedAt'>,
    id?: string,
  ) {
    return id
      ? this.http.put<KnowledgeArticle>(`/api/knowledge/${id}`, request)
      : this.http.post<KnowledgeArticle>('/api/knowledge', request);
  }
  setKnowledgeStatus(id: string, status: KnowledgeArticle['status']) {
    return this.http.post<KnowledgeArticle>(`/api/knowledge/${id}/status`, { status });
  }
  ticketKnowledge(ticketId: string) {
    return this.http.get<KnowledgeArticle[]>(`/api/knowledge/tickets/${ticketId}`);
  }
  linkKnowledge(ticketId: string, articleId: string) {
    return this.http.post<void>(`/api/knowledge/${articleId}/tickets/${ticketId}`, null);
  }

  assistantConversations() {
    return this.http.get<AssistantConversationSummary[]>('/api/assistant/conversations');
  }
  createAssistantConversation() {
    return this.http.post<AssistantConversationSummary>('/api/assistant/conversations', null);
  }
  assistantConversation(conversationId: string) {
    return this.http.get<AssistantConversationDetail>(`/api/assistant/conversations/${conversationId}`);
  }
  deleteAssistantConversation(conversationId: string) {
    return this.http.delete<void>(`/api/assistant/conversations/${conversationId}`);
  }
  saveAssistantTranscript(conversationId: string, role: 'user' | 'assistant', content: string) {
    return this.http.post<AssistantMessage>(
      `/api/assistant/conversations/${conversationId}/transcripts`,
      { role, content },
    );
  }
  assistantRealtimeSession() {
    return this.http.post<AssistantRealtimeSession>('/api/assistant/realtime/session', null);
  }

  aiAssistance(ticketId: string) {
    return this.http.get<AiAssistance[]>(`/api/tickets/${ticketId}/ai-assistance`);
  }
  createAiAssistance(ticketId: string, kind: AiAssistance['kind']) {
    return this.http.post<AiAssistance>(`/api/tickets/${ticketId}/ai-assistance`, { kind });
  }
  reviewAiAssistance(ticketId: string, id: string, accept: boolean) {
    return this.http.post<AiAssistance>(`/api/tickets/${ticketId}/ai-assistance/${id}/review`, {
      accept,
    });
  }
}

export interface AuditFilters {
  action?: string;
  targetType?: string;
  actorUserId?: string;
  from?: string;
  to?: string;
}

function compactParams(filters?: AuditFilters): Record<string, string> {
  return Object.fromEntries(
    Object.entries(filters ?? {}).filter(([, value]) => Boolean(value)),
  ) as Record<string, string>;
}
