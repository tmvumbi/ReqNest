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
  reporterUserId: string;
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
}

export interface TicketComment {
  id: string;
  authorUserId: string;
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
  scanStatus: 'Pending' | 'Clean' | 'Quarantined' | 'Rejected';
  uploadedByUserId: string;
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
}

export interface SavedView {
  id: string;
  name: string;
  projectId: string | null;
  filtersJson: string;
  sortJson: string;
  columnsJson: string;
  groupBy: string | null;
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
