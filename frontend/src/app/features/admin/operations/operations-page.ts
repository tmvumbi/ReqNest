import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
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
  TicketSchema,
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
    InputNumberModule,
    InputTextModule,
    MessageModule,
    MultiSelectModule,
    SelectModule,
    TableModule,
    TagModule,
  ],
  templateUrl: './operations-page.html',
  styleUrl: './operations-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OperationsPage {
  private readonly api = inject(ApiClient);
  readonly i18n = inject(I18nService);
  readonly projects = signal<Project[]>([]);
  readonly schema = signal<TicketSchema | null>(null);
  readonly slaPolicies = signal<SlaPolicy[]>([]);
  readonly customRoles = signal<CustomRole[]>([]);
  readonly retention = signal<RetentionSettings | null>(null);
  readonly outbox = signal<EmailOutboxPage['items']>([]);
  readonly success = signal('');
  readonly error = signal('');
  readonly busy = signal(false);

  configProjectId = '';
  typeKey = '';
  typeEnglish = '';
  typeFrench = '';
  priorityKey = '';
  priorityEnglish = '';
  priorityFrench = '';
  priorityColor = '#DC2626';
  fieldKey = '';
  fieldEnglish = '';
  fieldFrench = '';
  fieldKind: 'Text' | 'Number' | 'Date' | 'Boolean' | 'Choice' = 'Text';
  fieldRequired = false;
  fieldOptions = '';
  slaName = '';
  slaTimeZone = 'Africa/Johannesburg';
  slaWarning = 60;
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
          labelEnglish: this.typeEnglish,
          labelFrench: this.typeFrench,
          order: (this.schema()?.types.length ?? 0) + 10,
          isActive: true,
        }),
      );
      this.typeKey = this.typeEnglish = this.typeFrench = '';
      await this.changeScope();
    });
  }

  async addPriority(): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(
        this.api.createTicketPriority({
          projectId: this.configProjectId || null,
          key: this.priorityKey.trim().toUpperCase(),
          labelEnglish: this.priorityEnglish,
          labelFrench: this.priorityFrench,
          color: this.priorityColor,
          weight: 50,
          order: (this.schema()?.priorities.length ?? 0) + 10,
          isActive: true,
        }),
      );
      this.priorityKey = this.priorityEnglish = this.priorityFrench = '';
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
          projectId: this.configProjectId || null,
          key: this.fieldKey.trim().toUpperCase(),
          labelEnglish: this.fieldEnglish,
          labelFrench: this.fieldFrench,
          kind: this.fieldKind,
          isRequired: this.fieldRequired,
          isActive: true,
          order: (this.schema()?.customFields.length ?? 0) + 10,
          options,
        }),
      );
      this.fieldKey = this.fieldEnglish = this.fieldFrench = this.fieldOptions = '';
      await this.changeScope();
    });
  }

  async addSla(): Promise<void> {
    await this.run(async () => {
      const policy = await firstValueFrom(
        this.api.createSlaPolicy({
          projectId: this.configProjectId || null,
          name: this.slaName,
          timeZone: this.slaTimeZone,
          isDefault: !this.configProjectId,
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
      if (this.configProjectId) {
        await firstValueFrom(this.api.assignSlaPolicy(policy.id, this.configProjectId));
      }
      this.slaName = '';
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

  label(item: { labelEnglish: string; labelFrench: string }): string {
    return this.i18n.language() === 'French' ? item.labelFrench : item.labelEnglish;
  }

  projectName(project: Project): string {
    return this.i18n.language() === 'French' ? project.nameFrench : project.nameEnglish;
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
      this.error.set(this.i18n.text('common.error'));
    }
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    this.success.set('');
    try {
      await action();
      this.success.set(
        this.i18n.language() === 'French' ? 'Modifications enregistrées.' : 'Changes saved.',
      );
    } catch {
      this.error.set(this.i18n.text('common.error'));
    } finally {
      this.busy.set(false);
    }
  }
}
