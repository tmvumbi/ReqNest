import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { ApiClient } from '../../core/api/api-client';
import { Project, ReportData } from '../../core/api/api-models';
import { I18nService, TranslationKey } from '../../core/i18n/i18n.service';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { SessionStore } from '../../core/session/session-store';

@Component({
  selector: 'app-reports-page',
  imports: [LocalizedDatePipe, FormsModule, ButtonModule, MessageModule, SelectModule, TableModule],
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
  readonly exporting = signal(false);
  readonly exportFailed = signal(false);
  selectedReport = 'inventory';
  selectedProject = '';
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
    this.exportFailed.set(false);
    try {
      const blob = await firstValueFrom(
        this.api.reportPdf(
          this.selectedReport,
          this.i18n.language(),
          this.selectedProject || undefined,
        ),
      );
      this.saveBlob(blob, `reqnest-${this.selectedReport}.pdf`);
    } catch {
      this.exportFailed.set(true);
    } finally {
      this.exporting.set(false);
    }
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

  private saveBlob(blob: Blob, name: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = name;
    anchor.click();
    URL.revokeObjectURL(url);
  }

  title(report: ReportData): string {
    return report.title;
  }
  projectName(project: Project): string {
    return project.name;
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
};
