import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { ApiClient } from '../../core/api/api-client';
import { Project, ReportData, ReportExport, ReportSchedule } from '../../core/api/api-models';
import { I18nService, TranslationKey } from '../../core/i18n/i18n.service';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { SessionStore } from '../../core/session/session-store';

@Component({
  selector: 'app-reports-page',
  imports: [
    LocalizedDatePipe,
    FormsModule,
    ButtonModule,
    InputTextModule,
    MessageModule,
    SelectModule,
    TableModule,
  ],
  templateUrl: './reports-page.html',
  styleUrl: './reports-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReportsPage {
  private readonly api = inject(ApiClient);
  readonly i18n = inject(I18nService);
  readonly store = inject(SessionStore);
  readonly projects = signal<Project[]>([]);
  readonly report = signal<ReportData | null>(null);
  readonly loading = signal(false);
  readonly exportReady = signal(false);
  readonly exportFailed = signal(false);
  readonly exporting = signal(false);
  readonly exports = signal<ReportExport[]>([]);
  readonly schedules = signal<ReportSchedule[]>([]);
  selectedReport = 'inventory';
  selectedProject = '';
  scheduleName = '';
  scheduleFrequency: ReportSchedule['frequency'] = 'Weekly';
  scheduleFormat: ReportSchedule['format'] = 'Pdf';
  readonly scheduleFrequencies: ReportSchedule['frequency'][] = ['Daily', 'Weekly', 'Monthly'];
  readonly scheduleFormats: ReportSchedule['format'][] = ['Pdf', 'Csv'];
  readonly reportOptions: { value: string; labelKey: TranslationKey }[] = [
    { value: 'inventory', labelKey: 'reports.inventory' },
    { value: 'created-resolved', labelKey: 'reports.createdResolved' },
    { value: 'aging', labelKey: 'reports.aging' },
    { value: 'resolution', labelKey: 'reports.resolution' },
    { value: 'throughput', labelKey: 'reports.throughput' },
    { value: 'workload', labelKey: 'reports.workload' },
    { value: 'sla', labelKey: 'reports.sla' },
    { value: 'workflow', labelKey: 'reports.workflow' },
    { value: 'project-comparison', labelKey: 'reports.comparison' },
    { value: 'activity', labelKey: 'reports.activity' },
  ];

  constructor() {
    void firstValueFrom(this.api.projects()).then((projects) => this.projects.set(projects));
    void this.run();
    void this.loadExports();
    void this.loadSchedules();
  }

  async run(): Promise<void> {
    this.loading.set(true);
    try {
      this.report.set(
        await firstValueFrom(
          this.api.report(this.selectedReport, this.selectedProject || undefined),
        ),
      );
    } finally {
      this.loading.set(false);
    }
  }
  async exportPdf(): Promise<void> {
    this.exporting.set(true);
    this.exportReady.set(false);
    this.exportFailed.set(false);
    try {
      await firstValueFrom(
        this.api.exportReport(
          this.selectedReport,
          this.i18n.language(),
          this.selectedProject || undefined,
        ),
      );
      this.exportReady.set(true);
      await this.loadExports();
    } catch {
      this.exportFailed.set(true);
    } finally {
      this.exporting.set(false);
    }
  }
  async loadExports(): Promise<void> {
    this.exports.set(await firstValueFrom(this.api.reportExports()));
  }
  async exportCsv(): Promise<void> {
    const blob = await firstValueFrom(
      this.api.reportCsv(
        this.selectedReport,
        this.i18n.language(),
        this.selectedProject || undefined,
      ),
    );
    this.saveBlob(blob, `reqnest-${this.selectedReport}.csv`);
  }
  async loadSchedules(): Promise<void> {
    this.schedules.set(await firstValueFrom(this.api.reportSchedules()));
  }
  async createSchedule(): Promise<void> {
    if (!this.scheduleName) return;
    await firstValueFrom(
      this.api.createReportSchedule({
        projectId: this.selectedProject || null,
        name: this.scheduleName,
        reportType: this.selectedReport,
        filter: {
          projectId: this.selectedProject || null,
          from: null,
          to: null,
          priority: null,
          type: null,
          assigneeUserId: null,
          includeArchived: false,
        },
        language: this.i18n.language(),
        format: this.scheduleFormat,
        frequency: this.scheduleFrequency,
        isActive: true,
        nextRunAt: new Date(Date.now() + 86_400_000).toISOString(),
      }),
    );
    this.scheduleName = '';
    await this.loadSchedules();
  }
  async runSchedule(schedule: ReportSchedule): Promise<void> {
    const blob = await firstValueFrom(this.api.runReportSchedule(schedule.id));
    this.saveBlob(blob, `reqnest-${schedule.reportType}.${schedule.format.toLowerCase()}`);
    await this.loadSchedules();
  }
  async download(reportExport: ReportExport): Promise<void> {
    const blob = await firstValueFrom(this.api.downloadReport(reportExport.id));
    this.saveBlob(blob, `reqnest-${reportExport.reportType}.pdf`);
  }
  private saveBlob(blob: Blob, name: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = name;
    anchor.click();
    URL.revokeObjectURL(url);
  }
  title(report: ReportData): string {
    return this.i18n.language() === 'French' ? report.titleFrench : report.titleEnglish;
  }
  definitions(report: ReportData): string[] {
    return this.i18n.language() === 'French' ? report.definitionsFrench : report.definitionsEnglish;
  }
  projectName(project: Project): string {
    return this.i18n.language() === 'French' ? project.nameFrench : project.nameEnglish;
  }
  columnLabel(column: string): string {
    if (this.i18n.language() !== 'French') return column;
    return frenchColumns[column] ?? column;
  }
  display(value: unknown): string {
    if (value === null || value === undefined) return '—';
    const text = String(value);
    return this.i18n.language() === 'French' ? (frenchValues[text] ?? text) : text;
  }
}

const frenchColumns: Record<string, string> = {
  Project: 'Projet',
  Status: 'Statut',
  Category: 'Catégorie',
  Type: 'Type',
  Priority: 'Priorité',
  Count: 'Nombre',
  Date: 'Date',
  Created: 'Créés',
  Resolved: 'Résolus',
  NetBacklog: 'Variation du stock',
  AgeBand: 'Tranche d’ancienneté',
  Assignee: 'Responsable',
  Tickets: 'Tickets',
  MedianFirstResponseHours: 'Médiane 1re réponse (h)',
  P90ResolutionHours: 'P90 résolution (h)',
  Month: 'Mois',
  Contributor: 'Contributeur',
  Completed: 'Terminés',
  Open: 'Ouverts',
  InProgress: 'En cours',
  Urgent: 'Urgents',
  State: 'État',
  CurrentTickets: 'Tickets actuels',
  Inventory: 'Inventaire',
  MedianResolutionHours: 'Médiane résolution (h)',
  Action: 'Action',
};

const frenchValues: Record<string, string> = {
  ToDo: 'À faire',
  InProgress: 'En cours',
  Done: 'Terminé',
  TODO: 'À FAIRE',
  IN_PROGRESS: 'EN COURS',
  DONE: 'TERMINÉ',
  Low: 'Faible',
  Normal: 'Normale',
  High: 'Élevée',
  Urgent: 'Urgente',
  Incident: 'Incident',
  ServiceRequest: 'Demande de service',
  Task: 'Tâche',
  Problem: 'Problème',
  Unassigned: 'Non attribué',
  None: 'Aucun',
  OnTrack: 'Dans les délais',
  AtRisk: 'À risque',
  Breached: 'Dépassé',
  Met: 'Respecté',
  Ready: 'Prêt',
  Pending: 'En attente',
  Failed: 'Échec',
  Expired: 'Expiré',
};
