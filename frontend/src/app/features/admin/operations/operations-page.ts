import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ApiClient } from '../../../core/api/api-client';
import {
  CustomRole,
  EmailOutboxPage,
  Project,
  RetentionSettings,
  SlaPolicy,
  TicketPriorityDefinition,
  TicketSchema,
  TicketTypeDefinition,
} from '../../../core/api/api-models';
import { I18nService } from '../../../core/i18n/i18n.service';
import { LocalizedDatePipe } from '../../../core/i18n/localized-date.pipe';

@Component({
  selector: 'app-operations-page',
  imports: [
    DecimalPipe,
    LocalizedDatePipe,
    FormsModule,
    ButtonModule,
    ConfirmDialogModule,
    InputNumberModule,
    InputTextModule,
    MultiSelectModule,
    SelectModule,
    TableModule,
    TagModule,
  ],
  providers: [ConfirmationService],
  templateUrl: './operations-page.html',
  styleUrl: './operations-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OperationsPage {
  private readonly api = inject(ApiClient);
  private readonly confirmation = inject(ConfirmationService);
  private readonly messages = inject(MessageService);
  readonly i18n = inject(I18nService);
  readonly projects = signal<Project[]>([]);
  readonly schema = signal<TicketSchema | null>(null);
  readonly slaPolicies = signal<SlaPolicy[]>([]);
  readonly customRoles = signal<CustomRole[]>([]);
  readonly retention = signal<RetentionSettings | null>(null);
  readonly outbox = signal<EmailOutboxPage['items']>([]);
  readonly busy = signal(false);
  editingDefinitionId = '';
  editingLabel = '';
  editingColor = '#64748b';

  configProjectId = '';
  typeKey = '';
  typeEnglish = '';
  priorityKey = '';
  priorityEnglish = '';
  priorityColor = '#DC2626';
  fieldKey = '';
  fieldEnglish = '';
  fieldKind: 'Text' | 'Number' | 'Date' | 'Boolean' | 'Choice' = 'Text';
  fieldRequired = false;
  fieldOptions = '';
  fieldProjectIds: string[] = [];
  slaName = '';
  slaTimeZone = 'Africa/Johannesburg';
  slaWarning = 60;
  slaProjectIds: string[] = [];
  roleName = '';
  rolePermissions: string[] = [];
  readonly fieldKinds = ['Text', 'Number', 'Date', 'Boolean', 'Choice'];
  readonly permissions = [
    'project.read',
    'project.manage',
    'workflow.manage',
    'ticket.maintain',
    'ticket.archive',
    'ticket.bulk',
    'comment.add',
    'attachment.add',
    'report.view',
    'report.export',
    'user.manage',
    'tenant.settings.manage',
    'audit.view',
  ];

  constructor() {
    void this.load();
  }

  async changeScope(): Promise<void> {
    this.schema.set(await firstValueFrom(this.api.ticketSchema(this.configProjectId || undefined)));
  }

  async addType(): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(
        this.api.createTicketType({
          projectId: this.configProjectId || null,
          key: this.typeKey.trim().toUpperCase(),
          label: this.typeEnglish,
          order: (this.schema()?.types.length ?? 0) + 10,
          isActive: true,
        }),
      );
      this.typeKey = this.typeEnglish = '';
      await this.changeScope();
    });
  }

  startEdit(item: TicketTypeDefinition | TicketPriorityDefinition): void {
    this.editingDefinitionId = item.id;
    this.editingLabel = item.label;
    this.editingColor = 'color' in item ? item.color : '#64748b';
  }

  cancelEdit(): void {
    this.editingDefinitionId = '';
  }

  async saveType(item: TicketTypeDefinition): Promise<void> {
    const label = this.editingLabel.trim();
    if (!label) return;
    await this.run(async () => {
      await firstValueFrom(
        this.api.updateTicketType(item.id, {
          projectId: item.projectId,
          key: item.key,
          label,
          order: item.order,
          isActive: item.isActive,
        }),
      );
      this.cancelEdit();
      await this.changeScope();
    });
  }

  async savePriority(item: TicketPriorityDefinition): Promise<void> {
    const label = this.editingLabel.trim();
    if (!label) return;
    await this.run(async () => {
      await firstValueFrom(
        this.api.updateTicketPriority(item.id, {
          projectId: item.projectId,
          key: item.key,
          label,
          color: this.editingColor,
          weight: item.weight,
          order: item.order,
          isActive: item.isActive,
        }),
      );
      this.cancelEdit();
      await this.changeScope();
    });
  }

  deleteType(item: TicketTypeDefinition): void {
    this.confirmDelete(item.label, async () => {
      await firstValueFrom(this.api.deleteTicketType(item.id));
      await this.changeScope();
    });
  }

  deletePriority(item: TicketPriorityDefinition): void {
    this.confirmDelete(item.label, async () => {
      await firstValueFrom(this.api.deleteTicketPriority(item.id));
      await this.changeScope();
    });
  }

  private confirmDelete(label: string, action: () => Promise<void>): void {
    const french = this.i18n.language() === 'French';
    this.confirmation.confirm({
      header: french ? 'Supprimer la définition' : 'Delete definition',
      message: french ? `Supprimer « ${label} » ?` : `Delete "${label}"?`,
      acceptLabel: french ? 'Supprimer' : 'Delete',
      rejectLabel: this.i18n.text('common.cancel'),
      acceptButtonStyleClass: 'p-button-danger',
      rejectButtonStyleClass: 'p-button-secondary p-button-outlined',
      accept: async () => {
        this.busy.set(true);
        try {
          await action();
          this.notify('success', french ? 'Définition supprimée.' : 'Definition deleted.');
        } catch (errorResponse) {
          this.notify(
            'error',
            errorResponse instanceof HttpErrorResponse && errorResponse.status === 409
              ? french
                ? 'Cette définition est utilisée par des tickets existants et ne peut pas être supprimée.'
                : 'This definition is used by existing tickets and cannot be deleted.'
              : this.i18n.text('common.error'),
          );
        } finally {
          this.busy.set(false);
        }
      },
    });
  }

  async addPriority(): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(
        this.api.createTicketPriority({
          projectId: this.configProjectId || null,
          key: this.priorityKey.trim().toUpperCase(),
          label: this.priorityEnglish,
          color: this.priorityColor,
          weight: 50,
          order: (this.schema()?.priorities.length ?? 0) + 10,
          isActive: true,
        }),
      );
      this.priorityKey = this.priorityEnglish = '';
      await this.changeScope();
    });
  }

  async addField(): Promise<void> {
    await this.run(async () => {
      const options =
        this.fieldKind === 'Choice'
          ? this.fieldOptions
              .split(',')
              .map((item) => item.trim())
              .filter(Boolean)
          : [];
      await firstValueFrom(
        this.api.createCustomField({
          projectIds: this.fieldProjectIds,
          key: this.fieldKey.trim().toUpperCase(),
          label: this.fieldEnglish,
          kind: this.fieldKind,
          isRequired: this.fieldRequired,
          isActive: true,
          order: (this.schema()?.customFields.length ?? 0) + 10,
          options,
        }),
      );
      this.fieldKey = this.fieldEnglish = this.fieldOptions = '';
      this.fieldProjectIds = [];
      await this.changeScope();
    });
  }

  async addSla(): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(
        this.api.createSlaPolicy({
          projectIds: this.slaProjectIds,
          name: this.slaName,
          timeZone: this.slaTimeZone,
          isDefault: this.slaProjectIds.length === 0,
          isActive: true,
          workingDaysMask: 62,
          businessDayStartMinutes: 480,
          businessDayEndMinutes: 1020,
          warningMinutesBefore: this.slaWarning,
          pauseStatusKeys: ['WAITING', 'ON_HOLD'],
          targets: (this.schema()?.priorities ?? [])
            .filter((item) => item.isActive)
            .map((item) => ({
              priorityKey: item.key,
              firstResponseMinutes: item.weight >= 80 ? 30 : 240,
              resolutionMinutes: item.weight >= 80 ? 240 : 1440,
            })),
          holidays: [],
        }),
      );
      this.slaName = '';
      this.slaProjectIds = [];
      this.slaPolicies.set(await firstValueFrom(this.api.slaPolicies()));
    });
  }

  async addRole(): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(
        this.api.createCustomRole({
          name: this.roleName,
          description: null,
          permissions: this.rolePermissions,
          isActive: true,
        }),
      );
      this.roleName = '';
      this.rolePermissions = [];
      this.customRoles.set(await firstValueFrom(this.api.customRoles()));
    });
  }

  async saveRetention(): Promise<void> {
    const item = this.retention();
    if (!item) return;
    await this.run(async () => {
      this.retention.set(await firstValueFrom(this.api.updateRetentionSettings(item)));
    });
  }

  async previewAndRunRetention(): Promise<void> {
    await this.run(async () => {
      const preview = await firstValueFrom(this.api.retentionPreview());
      const count = Object.values(preview).reduce((sum, value) => sum + value, 0);
      if (
        count > 0 &&
        !window.confirm(
          this.i18n.language() === 'French'
            ? `${count} éléments seront supprimés. Continuer ?`
            : `${count} items will be deleted. Continue?`,
        )
      )
        return;
      await firstValueFrom(this.api.runRetention());
      this.retention.set(await firstValueFrom(this.api.retentionSettings()));
    });
  }

  async retry(messageId: string): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.api.retryEmail(messageId));
      this.outbox.set((await firstValueFrom(this.api.emailOutbox())).items);
    });
  }

  label(item: { label: string }): string {
    return item.label;
  }

  projectName(project: Project): string {
    return project.name;
  }

  projectSetLabel(projectIds: string[]): string {
    if (!projectIds.length) {
      return this.i18n.language() === 'French' ? 'Tous les projets' : 'All projects';
    }
    const names = this.projects()
      .filter((project) => projectIds.includes(project.id))
      .map((project) => project.name);
    return names.length ? names.join(', ') : '—';
  }

  private async load(): Promise<void> {
    try {
      const [projects, schema, policies, roles, retention, outbox] = await Promise.all([
        firstValueFrom(this.api.projects()),
        firstValueFrom(this.api.ticketSchema()),
        firstValueFrom(this.api.slaPolicies()),
        firstValueFrom(this.api.customRoles()),
        firstValueFrom(this.api.retentionSettings()),
        firstValueFrom(this.api.emailOutbox()),
      ]);
      this.projects.set(projects);
      this.schema.set(schema);
      this.slaPolicies.set(policies);
      this.customRoles.set(roles);
      this.retention.set(retention);
      this.outbox.set(outbox.items);
    } catch {
      this.notify('error', this.i18n.text('common.error'));
    }
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    try {
      await action();
      this.notify(
        'success',
        this.i18n.language() === 'French' ? 'Modifications enregistrées.' : 'Changes saved.',
      );
    } catch {
      this.notify('error', this.i18n.text('common.error'));
    } finally {
      this.busy.set(false);
    }
  }

  private notify(severity: 'success' | 'error', summary: string): void {
    this.messages.add({ severity, summary, life: severity === 'success' ? 4000 : 6000 });
  }
}
