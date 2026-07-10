import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { effect, inject, Injectable, PLATFORM_ID, signal } from '@angular/core';
import { AppLanguage } from '../api/api-models';
import { Translation } from 'primeng/api';
import { PrimeNG } from 'primeng/config';

const english = {
  'app.name': 'ReqNest',
  'app.tagline': 'Help desk, clearly organized.',
  'nav.dashboard': 'Dashboard',
  'nav.tickets': 'Tickets',
  'nav.projects': 'Projects',
  'nav.reports': 'Reports',
  'nav.notifications': 'Notifications',
  'nav.users': 'Users & roles',
  'nav.workflows': 'Workflows',
  'nav.settings': 'Company settings',
  'nav.audit': 'Audit log',
  'nav.signOut': 'Sign out',
  'nav.menu': 'Open navigation',
  'nav.language': 'Language',
  'nav.theme': 'Appearance',
  'nav.skip': 'Skip to content',
  'theme.light': 'Light',
  'theme.dark': 'Dark',
  'theme.system': 'System',
  'auth.welcome': 'Welcome back',
  'auth.signInSummary': 'Sign in to manage your projects and support tickets.',
  'auth.email': 'Email address',
  'auth.password': 'Password',
  'auth.signIn': 'Sign in',
  'auth.createCompany': 'Create a company',
  'auth.companyName': 'Company name',
  'auth.shortName': 'Short name',
  'auth.displayName': 'Your name',
  'auth.register': 'Create workspace',
  'auth.forgot': 'Forgot password?',
  'auth.resetTitle': 'Reset your password',
  'auth.resetHelp': 'Enter your email and we will prepare reset instructions.',
  'auth.sendReset': 'Send reset instructions',
  'auth.backToSignIn': 'Back to sign in',
  'auth.error': 'We could not complete that request. Check the details and try again.',
  'auth.required': 'This field is required.',
  'auth.emailInvalid': 'Enter a valid email address.',
  'auth.passwordHelp': 'Use at least 12 characters.',
  'auth.loading': 'Signing in…',
  'dashboard.title': 'Your work at a glance',
  'dashboard.summary': 'Focus on what needs attention across your permitted projects.',
  'dashboard.assigned': 'Assigned open',
  'dashboard.urgent': 'Urgent',
  'dashboard.overdue': 'Overdue',
  'dashboard.slaRisk': 'SLA at risk',
  'dashboard.unread': 'Unread notifications',
  'dashboard.recent': 'Recently updated',
  'common.loading': 'Loading…',
  'common.empty': 'Nothing to show yet.',
  'common.save': 'Save',
  'common.cancel': 'Cancel',
  'common.create': 'Create',
  'common.edit': 'Edit',
  'common.archive': 'Archive',
  'common.restore': 'Restore',
  'common.search': 'Search',
  'common.refresh': 'Refresh',
  'common.all': 'All',
  'common.status': 'Status',
  'common.priority': 'Priority',
  'common.project': 'Project',
  'common.updated': 'Updated',
  'common.actions': 'Actions',
  'common.error': 'Something went wrong. Try again.',
  'tickets.title': 'Tickets',
  'tickets.summary': 'Search, filter, and work through every permitted queue.',
  'tickets.new': 'New ticket',
  'tickets.queue': 'Queue',
  'tickets.myOpen': 'My open tickets',
  'tickets.unassigned': 'Unassigned',
  'tickets.recent': 'Recently updated',
  'tickets.todo': 'Waiting in TODO',
  'tickets.inProgress': 'In progress',
  'tickets.overdue': 'Overdue',
  'tickets.slaRisk': 'SLA at risk',
  'tickets.doneRecent': 'Done recently',
  'tickets.key': 'Key',
  'tickets.titleField': 'Title',
  'tickets.type': 'Type',
  'tickets.assignee': 'Assignee',
  'tickets.reporter': 'Reporter',
  'tickets.description': 'Description',
  'tickets.labels': 'Labels (comma separated)',
  'tickets.due': 'Due date',
  'tickets.create': 'Create ticket',
  'tickets.details': 'Ticket details',
  'tickets.activity': 'Activity',
  'tickets.comments': 'Comments',
  'tickets.addComment': 'Add comment',
  'tickets.commentPlaceholder': 'Share an update or mention a teammate…',
  'tickets.watch': 'Watch ticket',
  'tickets.transition': 'Move ticket',
  'tickets.attachments': 'Attachments',
  'tickets.upload': 'Upload document',
  'tickets.saved': 'Ticket saved.',
  'projects.title': 'Projects',
  'projects.summary': 'Separate work, membership, defaults, and workflows by project.',
  'projects.new': 'New project',
  'projects.key': 'Project key',
  'projects.nameEnglish': 'English name',
  'projects.nameFrench': 'French name',
  'projects.description': 'Description',
  'projects.workflow': 'Workflow',
  'projects.active': 'Active',
  'projects.archived': 'Archived',
  'notifications.title': 'Notifications',
  'notifications.summary':
    'A durable record of assignments, comments, mentions, deadlines, and reports.',
  'notifications.markAll': 'Mark all as read',
  'notifications.unreadOnly': 'Unread only',
  'reports.title': 'Reports',
  'reports.summary':
    'Understand inventory, aging, workload, throughput, resolution, and SLA performance.',
  'reports.choose': 'Report',
  'reports.run': 'Run report',
  'reports.export': 'Export PDF',
  'reports.definitions': 'Metric definitions',
  'reports.inventory': 'Ticket inventory',
  'reports.createdResolved': 'Created vs. resolved',
  'reports.aging': 'Ticket aging',
  'reports.resolution': 'Resolution performance',
  'reports.throughput': 'Throughput',
  'reports.workload': 'Workload',
  'reports.sla': 'SLA performance',
  'reports.workflow': 'Workflow flow',
  'reports.comparison': 'Project comparison',
  'reports.activity': 'Activity report',
  'admin.usersTitle': 'Users and project access',
  'admin.usersSummary':
    'Invite people and explain exactly which role and project scope gives access.',
  'admin.invite': 'Invite user',
  'admin.role': 'Role',
  'admin.scope': 'Scope',
  'admin.allProjects': 'All projects',
  'admin.selectedProjects': 'Selected projects',
  'admin.status': 'Membership status',
  'admin.workflowsTitle': 'Workflows',
  'admin.workflowsSummary': 'Reuse a tenant workflow or keep a project-specific copy.',
  'admin.settingsTitle': 'Company settings',
  'admin.settingsSummary':
    'Set names, language, time zone, appearance, support contact, and report branding.',
  'admin.accent': 'Accent color',
  'admin.timeZone': 'Time zone',
  'admin.support': 'Support contact',
  'admin.footer': 'Report footer',
  'admin.defaultLanguage': 'Default language',
  'admin.defaultTheme': 'Default appearance',
  'admin.auditTitle': 'Audit log',
  'admin.auditSummary': 'Review append-only security and business administration events.',
} as const;

export type TranslationKey = keyof typeof english;

const french: Record<TranslationKey, string> = {
  'app.name': 'ReqNest',
  'app.tagline': "Centre d'assistance, clairement organisé.",
  'nav.dashboard': 'Tableau de bord',
  'nav.tickets': 'Tickets',
  'nav.projects': 'Projets',
  'nav.reports': 'Rapports',
  'nav.notifications': 'Notifications',
  'nav.users': 'Utilisateurs et rôles',
  'nav.workflows': 'Flux de travail',
  'nav.settings': "Paramètres de l'entreprise",
  'nav.audit': "Journal d'audit",
  'nav.signOut': 'Se déconnecter',
  'nav.menu': 'Ouvrir la navigation',
  'nav.language': 'Langue',
  'nav.theme': 'Apparence',
  'nav.skip': 'Aller au contenu',
  'theme.light': 'Clair',
  'theme.dark': 'Sombre',
  'theme.system': 'Système',
  'auth.welcome': 'Bon retour',
  'auth.signInSummary': 'Connectez-vous pour gérer vos projets et tickets de support.',
  'auth.email': 'Adresse e-mail',
  'auth.password': 'Mot de passe',
  'auth.signIn': 'Se connecter',
  'auth.createCompany': 'Créer une entreprise',
  'auth.companyName': "Nom de l'entreprise",
  'auth.shortName': 'Nom court',
  'auth.displayName': 'Votre nom',
  'auth.register': "Créer l'espace de travail",
  'auth.forgot': 'Mot de passe oublié ?',
  'auth.resetTitle': 'Réinitialiser votre mot de passe',
  'auth.resetHelp': 'Saisissez votre e-mail pour préparer les instructions.',
  'auth.sendReset': 'Envoyer les instructions',
  'auth.backToSignIn': 'Retour à la connexion',
  'auth.error': "Impossible d'effectuer cette demande. Vérifiez les informations et réessayez.",
  'auth.required': 'Ce champ est obligatoire.',
  'auth.emailInvalid': 'Saisissez une adresse e-mail valide.',
  'auth.passwordHelp': 'Utilisez au moins 12 caractères.',
  'auth.loading': 'Connexion…',
  'dashboard.title': "Votre travail d'un coup d'œil",
  'dashboard.summary': 'Concentrez-vous sur les éléments qui nécessitent votre attention.',
  'dashboard.assigned': 'Ouverts attribués',
  'dashboard.urgent': 'Urgents',
  'dashboard.overdue': 'En retard',
  'dashboard.slaRisk': 'SLA à risque',
  'dashboard.unread': 'Notifications non lues',
  'dashboard.recent': 'Récemment mis à jour',
  'common.loading': 'Chargement…',
  'common.empty': 'Rien à afficher pour le moment.',
  'common.save': 'Enregistrer',
  'common.cancel': 'Annuler',
  'common.create': 'Créer',
  'common.edit': 'Modifier',
  'common.archive': 'Archiver',
  'common.restore': 'Restaurer',
  'common.search': 'Rechercher',
  'common.refresh': 'Actualiser',
  'common.all': 'Tous',
  'common.status': 'Statut',
  'common.priority': 'Priorité',
  'common.project': 'Projet',
  'common.updated': 'Mis à jour',
  'common.actions': 'Actions',
  'common.error': 'Une erreur est survenue. Réessayez.',
  'tickets.title': 'Tickets',
  'tickets.summary': 'Recherchez, filtrez et traitez chaque file autorisée.',
  'tickets.new': 'Nouveau ticket',
  'tickets.queue': "File d'attente",
  'tickets.myOpen': 'Mes tickets ouverts',
  'tickets.unassigned': 'Non attribués',
  'tickets.recent': 'Récemment mis à jour',
  'tickets.todo': 'En attente dans À FAIRE',
  'tickets.inProgress': 'En cours',
  'tickets.overdue': 'En retard',
  'tickets.slaRisk': 'SLA à risque',
  'tickets.doneRecent': 'Terminés récemment',
  'tickets.key': 'Clé',
  'tickets.titleField': 'Titre',
  'tickets.type': 'Type',
  'tickets.assignee': 'Responsable',
  'tickets.reporter': 'Rapporteur',
  'tickets.description': 'Description',
  'tickets.labels': 'Étiquettes (séparées par des virgules)',
  'tickets.due': "Date d'échéance",
  'tickets.create': 'Créer le ticket',
  'tickets.details': 'Détails du ticket',
  'tickets.activity': 'Activité',
  'tickets.comments': 'Commentaires',
  'tickets.addComment': 'Ajouter le commentaire',
  'tickets.commentPlaceholder': 'Partagez une mise à jour ou mentionnez un collègue…',
  'tickets.watch': 'Suivre le ticket',
  'tickets.transition': 'Déplacer le ticket',
  'tickets.attachments': 'Pièces jointes',
  'tickets.upload': 'Téléverser un document',
  'tickets.saved': 'Ticket enregistré.',
  'projects.title': 'Projets',
  'projects.summary':
    'Séparez le travail, les membres, les valeurs par défaut et les flux par projet.',
  'projects.new': 'Nouveau projet',
  'projects.key': 'Clé du projet',
  'projects.nameEnglish': 'Nom anglais',
  'projects.nameFrench': 'Nom français',
  'projects.description': 'Description',
  'projects.workflow': 'Flux de travail',
  'projects.active': 'Actif',
  'projects.archived': 'Archivé',
  'notifications.title': 'Notifications',
  'notifications.summary':
    'Un historique durable des attributions, commentaires, mentions, échéances et rapports.',
  'notifications.markAll': 'Tout marquer comme lu',
  'notifications.unreadOnly': 'Non lues uniquement',
  'reports.title': 'Rapports',
  'reports.summary':
    "Analysez l'inventaire, l'ancienneté, la charge, le débit, la résolution et les SLA.",
  'reports.choose': 'Rapport',
  'reports.run': 'Exécuter le rapport',
  'reports.export': 'Exporter en PDF',
  'reports.definitions': 'Définitions des mesures',
  'reports.inventory': 'Inventaire des tickets',
  'reports.createdResolved': 'Créés et résolus',
  'reports.aging': 'Ancienneté des tickets',
  'reports.resolution': 'Performance de résolution',
  'reports.throughput': 'Débit de résolution',
  'reports.workload': 'Charge de travail',
  'reports.sla': 'Performance SLA',
  'reports.workflow': 'Flux de travail',
  'reports.comparison': 'Comparaison des projets',
  'reports.activity': "Rapport d'activité",
  'admin.usersTitle': 'Utilisateurs et accès aux projets',
  'admin.usersSummary':
    "Invitez des personnes et expliquez précisément le rôle et l'étendue de leur accès.",
  'admin.invite': 'Inviter un utilisateur',
  'admin.role': 'Rôle',
  'admin.scope': 'Étendue',
  'admin.allProjects': 'Tous les projets',
  'admin.selectedProjects': 'Projets sélectionnés',
  'admin.status': "Statut d'adhésion",
  'admin.workflowsTitle': 'Flux de travail',
  'admin.workflowsSummary':
    "Réutilisez un flux d'entreprise ou conservez une copie propre au projet.",
  'admin.settingsTitle': "Paramètres de l'entreprise",
  'admin.settingsSummary':
    "Configurez les noms, la langue, le fuseau horaire, l'apparence, le contact et les rapports.",
  'admin.accent': "Couleur d'accentuation",
  'admin.timeZone': 'Fuseau horaire',
  'admin.support': 'Contact du support',
  'admin.footer': 'Pied de page des rapports',
  'admin.defaultLanguage': 'Langue par défaut',
  'admin.defaultTheme': 'Apparence par défaut',
  'admin.auditTitle': "Journal d'audit",
  'admin.auditSummary': "Consultez les événements immuables de sécurité et d'administration.",
};

const frenchPrimeNg: Translation = {
  startsWith: 'Commence par',
  contains: 'Contient',
  notContains: 'Ne contient pas',
  endsWith: 'Se termine par',
  equals: 'Égale',
  notEquals: 'Différent de',
  noFilter: 'Aucun filtre',
  lt: 'Inférieur à',
  lte: 'Inférieur ou égal à',
  gt: 'Supérieur à',
  gte: 'Supérieur ou égal à',
  is: 'Est',
  isNot: "N'est pas",
  before: 'Avant',
  after: 'Après',
  clear: 'Effacer',
  apply: 'Appliquer',
  matchAll: 'Toutes les règles',
  matchAny: 'Une règle',
  addRule: 'Ajouter une règle',
  removeRule: 'Supprimer la règle',
  accept: 'Accepter',
  reject: 'Refuser',
  choose: 'Choisir',
  upload: 'Téléverser',
  cancel: 'Annuler',
  today: "Aujourd'hui",
  weekHeader: 'Sem.',
  weak: 'Faible',
  medium: 'Moyen',
  strong: 'Fort',
  passwordPrompt: 'Saisissez un mot de passe',
  emptyMessage: 'Aucun résultat',
  emptyFilterMessage: 'Aucun résultat',
  chooseDate: 'Choisir une date',
  chooseMonth: 'Choisir un mois',
  chooseYear: 'Choisir une année',
  firstDayOfWeek: 1,
  dateFormat: 'dd/mm/yy',
  dayNames: ['dimanche', 'lundi', 'mardi', 'mercredi', 'jeudi', 'vendredi', 'samedi'],
  dayNamesShort: ['dim.', 'lun.', 'mar.', 'mer.', 'jeu.', 'ven.', 'sam.'],
  dayNamesMin: ['Di', 'Lu', 'Ma', 'Me', 'Je', 'Ve', 'Sa'],
  monthNames: [
    'janvier',
    'février',
    'mars',
    'avril',
    'mai',
    'juin',
    'juillet',
    'août',
    'septembre',
    'octobre',
    'novembre',
    'décembre',
  ],
  monthNamesShort: [
    'janv.',
    'févr.',
    'mars',
    'avr.',
    'mai',
    'juin',
    'juil.',
    'août',
    'sept.',
    'oct.',
    'nov.',
    'déc.',
  ],
  aria: {
    close: 'Fermer',
    previous: 'Précédent',
    next: 'Suivant',
    navigation: 'Navigation',
    selectAll: 'Tout sélectionner',
    unselectAll: 'Tout désélectionner',
    selectRow: 'Sélectionner la ligne',
    unselectRow: 'Désélectionner la ligne',
    expandRow: 'Développer la ligne',
    collapseRow: 'Réduire la ligne',
    showFilterMenu: 'Afficher le filtre',
    hideFilterMenu: 'Masquer le filtre',
    filterOperator: 'Opérateur de filtre',
    filterConstraint: 'Contrainte de filtre',
    saveEdit: 'Enregistrer la modification',
    cancelEdit: 'Annuler la modification',
    editRow: 'Modifier la ligne',
    browseFiles: 'Parcourir les fichiers',
  },
};

@Injectable({ providedIn: 'root' })
export class I18nService {
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly document = inject(DOCUMENT);
  private readonly primeNg = inject(PrimeNG);
  private readonly englishPrimeNg = this.primeNg.translation;
  readonly language = signal<AppLanguage>(this.initialLanguage());

  constructor() {
    effect(() => {
      const frenchLanguage = this.language() === 'French';
      this.document.documentElement.setAttribute('lang', frenchLanguage ? 'fr' : 'en');
      this.primeNg.setTranslation(frenchLanguage ? frenchPrimeNg : this.englishPrimeNg);
    });
  }

  text(key: TranslationKey): string {
    return this.language() === 'French' ? french[key] : english[key];
  }

  ticketPriority(priority: string): string {
    if (this.language() !== 'French') return priority;
    return (
      {
        Low: 'Faible',
        Normal: 'Normale',
        High: 'Élevée',
        Urgent: 'Urgente',
      }[priority] ?? priority
    );
  }

  ticketType(type: string): string {
    if (this.language() !== 'French') {
      return type === 'ServiceRequest' ? 'Service request' : type;
    }
    return (
      {
        Incident: 'Incident',
        ServiceRequest: 'Demande de service',
        Task: 'Tâche',
        Problem: 'Problème',
      }[type] ?? type
    );
  }

  ticketActivityAction(action: string): string {
    const catalog: Record<string, readonly [string, string]> = {
      'ticket.created': ['Ticket created', 'Ticket créé'],
      'ticket.updated': ['Ticket details updated', 'Détails du ticket mis à jour'],
      'ticket.transitioned': ['Ticket status changed', 'Statut du ticket modifié'],
      'ticket.archived': ['Ticket archived', 'Ticket archivé'],
      'ticket.restored': ['Ticket restored', 'Ticket restauré'],
      'ticket.bulk_updated': ['Ticket updated by a bulk action', 'Ticket mis à jour en lot'],
      'ticket.commented': ['Comment added', 'Commentaire ajouté'],
      'ticket.comment.edited': ['Comment edited', 'Commentaire modifié'],
      'ticket.comment.deleted': ['Comment deleted', 'Commentaire supprimé'],
      'ticket.comment.hidden': ['Comment hidden', 'Commentaire masqué'],
      'ticket.watcher.updated': ['Watch preference updated', 'Préférence de suivi mise à jour'],
      'ticket.watcher.removed': ['Ticket no longer watched', 'Ticket retiré du suivi'],
      'attachment.uploaded': ['Attachment uploaded', 'Pièce jointe téléversée'],
      'attachment.deleted': ['Attachment deleted', 'Pièce jointe supprimée'],
      'attachment.scan.completed': [
        'Attachment scan completed',
        'Analyse de la pièce jointe terminée',
      ],
    };
    const labels = catalog[action];
    if (!labels) return action;
    return labels[this.language() === 'French' ? 1 : 0];
  }

  notificationType(type: string): string {
    const catalog: Record<string, readonly [string, string]> = {
      TicketAssigned: ['Ticket assigned', 'Ticket attribué'],
      UserMentioned: ['Mention', 'Mention'],
      TicketCommented: ['Comment', 'Commentaire'],
      TicketStatusChanged: ['Status changed', 'Statut modifié'],
      TicketPriorityChanged: ['Priority changed', 'Priorité modifiée'],
      DueDateApproaching: ['Due date approaching', "Échéance à l'approche"],
      DueDatePassed: ['Due date passed', 'Échéance dépassée'],
      SlaAtRisk: ['SLA at risk', 'SLA à risque'],
      SlaBreached: ['SLA breached', 'SLA dépassé'],
      TicketResolved: ['Ticket resolved', 'Ticket résolu'],
      TicketReopened: ['Ticket reopened', 'Ticket rouvert'],
      ProjectMembershipChanged: ['Project access changed', 'Accès au projet modifié'],
      RoleChanged: ['Role changed', 'Rôle modifié'],
      InvitationCreated: ['Invitation created', 'Invitation créée'],
      ReportReady: ['Report ready', 'Rapport prêt'],
      ReportFailed: ['Report failed', 'Échec du rapport'],
    };
    const labels = catalog[type];
    if (!labels) return type;
    return labels[this.language() === 'French' ? 1 : 0];
  }

  setLanguage(language: AppLanguage): void {
    this.language.set(language);
    if (this.browser) localStorage.setItem('reqnest.language', language);
  }

  toggleLanguage(): void {
    this.setLanguage(this.language() === 'English' ? 'French' : 'English');
  }

  catalogsComplete(): boolean {
    return Object.keys(english).every((key) => Boolean(french[key as TranslationKey]));
  }

  private initialLanguage(): AppLanguage {
    if (!this.browser) return 'English';
    const stored = localStorage.getItem('reqnest.language');
    if (stored === 'English' || stored === 'French') return stored;
    return navigator.language.toLowerCase().startsWith('fr') ? 'French' : 'English';
  }
}
