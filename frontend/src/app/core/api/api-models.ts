export type AppLanguage = 'English' | 'French';
export type ThemePreference = 'System' | 'Light' | 'Dark';
export type AppRole = 'TenantAdministrator' | 'ProjectManager' | 'Contributor' | 'Observer';
export type TicketPriority = 'Low' | 'Normal' | 'High' | 'Urgent';
export type TicketType = 'Incident' | 'ServiceRequest' | 'Task' | 'Problem';

export interface TenantAccess {
  tenantId: string;
  tenantName: string;
  tenantShortName: string;
  roles: AppRole[];
  permissions: string[];
  customRoles: string[];
  projectPermissions: Record<string, string[]>;
}

export interface AuthenticatedSession {
  userId: string;
  email: string;
  displayName: string;
  preferredLanguage: AppLanguage;
  themePreference: ThemePreference;
  accessToken: string;
  expiresAt: string;
  tenants: TenantAccess[];
}

export interface Project {
  id: string;
  key: string;
  nameEnglish: string;
  nameFrench: string;
  description: string | null;
  isArchived: boolean;
  workflowId: string;
  defaultPriority: TicketPriority;
  defaultAssigneeUserId: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface ProjectOverview {
  byStatus: {
    statusId: string;
    key: string;
    labelEnglish: string;
    labelFrench: string;
    count: number;
  }[];
  byPriority: { priority: TicketPriority; count: number }[];
  unassigned: number;
  overdue: number;
  recentlyUpdated: {
    id: string;
    key: string;
    title: string;
    priority: TicketPriority;
    updatedAt: string;
  }[];
}

export interface WorkflowStatus {
  id: string;
  key: string;
  labelEnglish: string;
  labelFrench: string;
  category: 'ToDo' | 'InProgress' | 'Done';
  order: number;
  color: string;
  isInitial: boolean;
  isTerminal: boolean;
}

export interface WorkflowTransition {
  id: string;
  fromStatusId: string;
  toStatusId: string;
  nameEnglish: string | null;
  nameFrench: string | null;
  commentRequired: boolean;
}

export interface Workflow {
  id: string;
  name: string;
  description: string | null;
  projectId: string | null;
  isDefault: boolean;
  isActive: boolean;
  statuses: WorkflowStatus[];
  transitions: WorkflowTransition[];
}

export interface TicketListItem {
  id: string;
  key: string;
  projectId: string;
  projectNameEnglish: string;
  projectNameFrench: string;
  title: string;
  type: TicketType;
  priority: TicketPriority;
  statusId: string;
  statusKey: string;
  statusLabelEnglish: string;
  statusLabelFrench: string;
  assigneeUserId: string | null;
  assigneeDisplayName: string | null;
  reporterDisplayName: string;
  dueAt: string | null;
  slaState: 'None' | 'OnTrack' | 'AtRisk' | 'Breached' | 'Met';
  isArchived: boolean;
  updatedAt: string;
  version: number;
  typeKey: string;
  priorityKey: string;
}

export interface PagedTickets {
  items: TicketListItem[];
  page: number;
  pageSize: number;
  total: number;
}

export interface TicketWatcher {
  userId: string;
  displayName: string;
  isMuted: boolean;
}

export interface TicketDetail {
  id: string;
  key: string;
  projectId: string;
  projectKey: string;
  projectNameEnglish: string;
  projectNameFrench: string;
  title: string;
  description: string;
  type: TicketType;
  priority: TicketPriority;
  statusId: string;
  statusKey: string;
  statusLabelEnglish: string;
  statusLabelFrench: string;
  statusCategory: 'ToDo' | 'InProgress' | 'Done';
  reporterUserId: string | null;
  reporterDisplayName: string;
  assigneeUserId: string | null;
  assigneeDisplayName: string | null;
  labels: string[];
  dueAt: string | null;
  firstRespondedAt: string | null;
  resolvedAt: string | null;
  firstResponseTargetAt: string | null;
  resolutionTargetAt: string | null;
  slaState: string;
  resolutionSummary: string | null;
  isArchived: boolean;
  watchers: TicketWatcher[];
  createdAt: string;
  updatedAt: string;
  version: number;
  typeKey: string;
  priorityKey: string;
  slaPolicyName: string | null;
  slaWarningAt: string | null;
  slaPausedAt: string | null;
  slaPausedMinutes: number;
  parentTicketId: string | null;
  customFields: Record<string, unknown>;
}

export interface TicketComment {
  id: string;
  authorUserId: string | null;
  authorDisplayName: string;
  body: string;
  isHidden: boolean;
  isDeleted: boolean;
  editedAt: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface TicketActivity {
  id: string;
  category: string;
  action: string;
  summary: string;
  actorUserId: string | null;
  occurredAt: string;
}

export interface TicketAttachment {
  id: string;
  commentId: string | null;
  fileName: string;
  contentType: string;
  size: number;
  checksumSha256: string;
  scanStatus: 'Pending' | 'Clean' | 'Quarantined' | 'Failed';
  uploadedByUserId: string | null;
  createdAt: string;
}

export interface Dashboard {
  assignedOpen: number;
  urgent: number;
  overdue: number;
  slaRisk: number;
  unreadNotifications: number;
  recentlyUpdated: {
    id: string;
    key: string;
    title: string;
    priority: TicketPriority;
    updatedAt: string;
  }[];
}

export interface AppNotification {
  id: string;
  type: string;
  actorUserId: string | null;
  projectId: string | null;
  ticketId: string | null;
  summaryEnglish: string;
  summaryFrench: string;
  deepLink: string;
  readAt: string | null;
  createdAt: string;
}

export interface PagedNotifications {
  items: AppNotification[];
  page: number;
  pageSize: number;
  total: number;
  unreadCount: number;
}

export interface NotificationPreferences {
  commentsEnabled: boolean;
  watcherUpdatesEnabled: boolean;
  dueDateUpdatesEnabled: boolean;
  digestEnabled: boolean;
  emailEnabled: boolean;
  digestHourLocal: number;
}

export interface SavedView {
  id: string;
  name: string;
  projectId: string | null;
  filtersJson: string;
  sortJson: string;
  columnsJson: string;
  groupBy: string | null;
  isPublished: boolean;
  ownerUserId: string;
}

export interface TenantSettings {
  id: string;
  name: string;
  shortName: string;
  defaultLanguage: AppLanguage;
  timeZone: string;
  defaultTheme: ThemePreference;
  primaryColor: string;
  hasLogo: boolean;
  hasDarkLogo: boolean;
  supportContact: string | null;
  reportFooterText: string | null;
}

export interface RoleGrant {
  id: string;
  role: AppRole;
  allProjects: boolean;
  projectIds: string[];
}

export interface Member {
  membershipId: string;
  userId: string;
  email: string;
  displayName: string;
  status: 'Invited' | 'Active' | 'Deactivated';
  invitationExpiresAt: string | null;
  grants: RoleGrant[];
}

export interface ReportData {
  type: string;
  titleEnglish: string;
  titleFrench: string;
  columns: string[];
  rows: Record<string, unknown>[];
  definitionsEnglish: string[];
  definitionsFrench: string[];
  truncated: boolean;
  generatedAt: string;
}

export interface ReportExport {
  id: string;
  reportType: string;
  language: AppLanguage;
  status: 'Pending' | 'Ready' | 'Failed' | 'Expired';
  expiresAt: string;
  createdAt: string;
}

export interface AuditPage {
  items: {
    id: string;
    actorUserId: string | null;
    action: string;
    targetType: string;
    targetId: string;
    summary: string;
    correlationId: string | null;
    createdAt: string;
  }[];
  page: number;
  pageSize: number;
  total: number;
}

export type CustomFieldKind = 'Text' | 'Number' | 'Date' | 'Boolean' | 'Choice';

export interface TicketTypeDefinition {
  id: string;
  projectId: string | null;
  key: string;
  labelEnglish: string;
  labelFrench: string;
  order: number;
  isActive: boolean;
}

export interface TicketPriorityDefinition extends TicketTypeDefinition {
  color: string;
  weight: number;
}

export interface CustomFieldDefinition extends TicketTypeDefinition {
  kind: CustomFieldKind;
  isRequired: boolean;
  optionsJson: string;
}

export interface TicketSchema {
  types: TicketTypeDefinition[];
  priorities: TicketPriorityDefinition[];
  customFields: CustomFieldDefinition[];
}

export interface SlaPolicy {
  id: string;
  projectId: string | null;
  name: string;
  timeZone: string;
  isDefault: boolean;
  isActive: boolean;
  workingDaysMask: number;
  businessDayStartMinutes: number;
  businessDayEndMinutes: number;
  warningMinutesBefore: number;
  pauseStatusKeys: string[];
  targets: { priorityKey: string; firstResponseMinutes: number; resolutionMinutes: number }[];
  holidays: { date: string; name: string }[];
}

export interface CustomRole {
  id: string;
  name: string;
  description: string | null;
  permissions: string[];
  isActive: boolean;
  grantCount: number;
}

export interface RetentionSettings {
  storageQuotaBytes: number;
  storageUsedBytes: number;
  notificationRetentionDays: number;
  auditRetentionDays: number;
  deletedAttachmentRetentionDays: number;
  reportRetentionDays: number;
}

export interface EmailOutboxPage {
  items: {
    id: string;
    recipientEmail: string;
    subject: string;
    templateKey: string;
    status: 'Pending' | 'Sent' | 'Failed';
    attempts: number;
    nextAttemptAt: string;
    sentAt: string | null;
    lastError: string | null;
    createdAt: string;
  }[];
  page: number;
  pageSize: number;
  total: number;
}

export interface TicketRelationship {
  id: string;
  relatedTicketId: string;
  relatedTicketKey: string;
  relatedTicketTitle: string;
  type: 'RelatesTo' | 'Duplicates' | 'Blocks';
  direction: 'incoming' | 'outgoing';
  createdAt: string;
}

export interface ReportSchedule {
  id: string;
  projectId: string | null;
  name: string;
  reportType: string;
  filterSnapshotJson: string;
  language: AppLanguage;
  format: 'Pdf' | 'Csv';
  frequency: 'Daily' | 'Weekly' | 'Monthly';
  isActive: boolean;
  nextRunAt: string;
  lastRunAt: string | null;
}

export interface PortalProject {
  id: string;
  key: string;
  nameEnglish: string;
  nameFrench: string;
  isEnabled: boolean;
}

export interface PublicPortal {
  tenantId: string;
  companyName: string;
  companyShortName: string;
  primaryColor: string;
  defaultLanguage: AppLanguage;
  introductionEnglish: string | null;
  introductionFrench: string | null;
  projects: PortalProject[];
}

export interface PortalSettings {
  tenantId: string;
  isEnabled: boolean;
  introductionEnglish: string | null;
  introductionFrench: string | null;
  projects: PortalProject[];
}

export interface RequesterTicket {
  ticket: {
    id: string;
    key: string;
    title: string;
    description: string;
    projectNameEnglish: string;
    projectNameFrench: string;
    statusEnglish: string;
    statusFrench: string;
    slaState: string;
    createdAt: string;
    updatedAt: string;
  };
  comments: {
    id: string;
    authorName: string;
    body: string;
    isRequester: boolean;
    createdAt: string;
  }[];
}

export interface ApiTokenItem {
  id: string;
  name: string;
  prefix: string;
  scopes: string[];
  projectIds: string[];
  expiresAt: string | null;
  revokedAt: string | null;
  lastUsedAt: string | null;
  createdAt: string;
}

export interface EmailChannel {
  id: string;
  projectId: string;
  address: string;
  defaultTypeKey: string;
  defaultPriorityKey: string;
  isActive: boolean;
  createdAt: string;
}

export interface WebhookItem {
  id: string;
  name: string;
  url: string;
  eventTypes: string[];
  isActive: boolean;
  createdAt: string;
}

export interface WebhookDeliveryItem {
  id: string;
  subscriptionId: string;
  eventType: string;
  status: 'Pending' | 'Delivered' | 'Failed';
  attempts: number;
  lastStatusCode: number | null;
  lastError: string | null;
  createdAt: string;
}

export interface IntegrationConnectionItem {
  id: string;
  provider: string;
  name: string;
  status: 'Disabled' | 'Connected' | 'Error';
  lastCheckedAt: string | null;
  lastError: string | null;
  retryAttempts: number;
  nextRetryAt: string | null;
  createdAt: string;
}

export interface KnowledgeArticle {
  id: string;
  projectId: string | null;
  slug: string;
  titleEnglish: string;
  titleFrench: string;
  bodyEnglish: string;
  bodyFrench: string;
  status: 'Draft' | 'Published' | 'Archived';
  visibility: 'Internal' | 'Requesters';
  publishedAt: string | null;
  updatedAt: string;
}

export interface AiAssistance {
  id: string;
  kind: 'Summarize' | 'SuggestReply' | 'Classify';
  draftOutput: string;
  status: 'Draft' | 'Accepted' | 'Rejected' | 'Failed';
  evaluationScore: number;
  requestedByUserId: string;
  reviewedByUserId: string | null;
  reviewedAt: string | null;
  createdAt: string;
}
