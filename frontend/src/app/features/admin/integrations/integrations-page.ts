import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { MultiSelectModule } from 'primeng/multiselect';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { TextareaModule } from 'primeng/textarea';
import { ApiClient } from '../../../core/api/api-client';
import {
  ApiTokenItem,
  EmailChannel,
  IntegrationConnectionItem,
  PortalSettings,
  Project,
  WebhookDeliveryItem,
  WebhookItem,
} from '../../../core/api/api-models';
import { I18nService } from '../../../core/i18n/i18n.service';

@Component({
  selector: 'app-integrations-page',
  imports: [
    FormsModule,
    ButtonModule,
    CheckboxModule,
    InputTextModule,
    MessageModule,
    MultiSelectModule,
    SelectModule,
    TagModule,
    TextareaModule,
  ],
  templateUrl: './integrations-page.html',
  styleUrl: './integrations-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class IntegrationsPage {
  private readonly api = inject(ApiClient);
  readonly i18n = inject(I18nService);
  readonly projects = signal<Project[]>([]);
  readonly portal = signal<PortalSettings | null>(null);
  readonly tokens = signal<ApiTokenItem[]>([]);
  readonly channels = signal<EmailChannel[]>([]);
  readonly webhooks = signal<WebhookItem[]>([]);
  readonly deliveries = signal<WebhookDeliveryItem[]>([]);
  readonly connections = signal<IntegrationConnectionItem[]>([]);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly success = signal('');
  readonly oneTimeSecret = signal('');
  tokenName = '';
  tokenScopes: string[] = ['ticket.maintain'];
  tokenProjects: string[] = [];
  tokenExpiry = '';
  channelProject = '';
  channelAddress = '';
  channelType = 'ServiceRequest';
  channelPriority = 'Normal';
  webhookName = '';
  webhookUrl = '';
  webhookEvents: string[] = ['ticket.created'];
  connectionProvider = 'GenericHttp';
  connectionName = '';
  connectionUrl = '';
  connectionBearer = '';
  sso = {
    authority: '',
    clientId: '',
    clientSecret: '',
    domains: '',
    isEnabled: false,
    requireSso: false,
  };
  ai = {
    isEnabled: false,
    provider: 'ReqNestSafeDraft',
    credential: '',
    allowedKinds: ['Summarize', 'SuggestReply'] as string[],
    requireHumanReview: true,
    allowAttachmentContent: false,
    providerDoesNotTrain: false,
  };
  readonly scopes = [
    'project.read',
    'project.manage',
    'ticket.maintain',
    'ticket.archive',
    'ticket.bulk',
    'comment.add',
    'attachment.add',
    'report.view',
    'report.export',
    'audit.view',
  ];
  readonly events = [
    'ticket.created',
    'ticket.updated',
    'ticket.transitioned',
    'ticket.commented',
    'attachment.created',
  ];
  readonly aiKinds = ['Summarize', 'SuggestReply', 'Classify'];
  constructor() {
    void this.load();
  }
  text(en: string, fr: string): string {
    return this.i18n.language() === 'French' ? fr : en;
  }
  async savePortal(): Promise<void> {
    const p = this.portal();
    if (!p) return;
    await this.run(async () => {
      await firstValueFrom(
        this.api.updatePortalSettings({
          isEnabled: p.isEnabled,
          introductionEnglish: p.introductionEnglish ?? '',
          introductionFrench: p.introductionFrench ?? '',
        }),
      );
      for (const project of p.projects)
        await firstValueFrom(this.api.setPortalProject(project.id, project.isEnabled));
      this.portal.set(await firstValueFrom(this.api.portalSettings()));
    });
  }
  async createToken(): Promise<void> {
    await this.run(async () => {
      const result = await firstValueFrom(
        this.api.createApiToken({
          name: this.tokenName,
          scopes: this.tokenScopes,
          projectIds: this.tokenProjects,
          expiresAt: this.tokenExpiry ? new Date(this.tokenExpiry).toISOString() : null,
        }),
      );
      this.oneTimeSecret.set(result.rawToken);
      this.tokenName = '';
      this.tokens.set(await firstValueFrom(this.api.apiTokens()));
    });
  }
  async revokeToken(id: string): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.api.revokeApiToken(id));
      this.tokens.set(await firstValueFrom(this.api.apiTokens()));
    });
  }
  async createChannel(): Promise<void> {
    await this.run(async () => {
      const result = await firstValueFrom(
        this.api.createEmailChannel({
          projectId: this.channelProject,
          address: this.channelAddress,
          defaultTypeKey: this.channelType,
          defaultPriorityKey: this.channelPriority,
          isActive: true,
        }),
      );
      this.oneTimeSecret.set(result.rawSecret);
      this.channelAddress = '';
      this.channels.set(await firstValueFrom(this.api.emailChannels()));
    });
  }
  async createWebhook(): Promise<void> {
    await this.run(async () => {
      const result = await firstValueFrom(
        this.api.createWebhook({
          name: this.webhookName,
          url: this.webhookUrl,
          eventTypes: this.webhookEvents,
          isActive: true,
        }),
      );
      this.oneTimeSecret.set(result.rawSecret);
      this.webhookName = this.webhookUrl = '';
      this.webhooks.set(await firstValueFrom(this.api.webhooks()));
    });
  }
  async testWebhook(id: string): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.api.testWebhook(id));
      this.deliveries.set(await firstValueFrom(this.api.webhookDeliveries()));
    });
  }
  async saveConnection(): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(
        this.api.upsertConnection({
          provider: this.connectionProvider,
          name: this.connectionName,
          configuration: { healthCheckUrl: this.connectionUrl, bearerToken: this.connectionBearer },
        }),
      );
      this.connectionName = this.connectionUrl = this.connectionBearer = '';
      this.connections.set(await firstValueFrom(this.api.connections()));
    });
  }
  async testConnection(id: string): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.api.testConnection(id));
      this.connections.set(await firstValueFrom(this.api.connections()));
    });
  }
  async saveSso(): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(
        this.api.updateSso({
          authority: this.sso.authority,
          clientId: this.sso.clientId,
          clientSecret: this.sso.clientSecret || null,
          allowedEmailDomains: this.sso.domains
            .split(',')
            .map((x) => x.trim())
            .filter(Boolean),
          isEnabled: this.sso.isEnabled,
          requireSso: this.sso.requireSso,
        }),
      );
    });
  }
  async testSso(): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.api.testSso());
    });
  }
  async saveAi(): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.api.updateAi(this.ai));
    });
  }
  private async load(): Promise<void> {
    try {
      const [projects, portal, tokens, channels, webhooks, deliveries, connections, sso, ai] =
        await Promise.all([
          firstValueFrom(this.api.projects()),
          firstValueFrom(this.api.portalSettings()),
          firstValueFrom(this.api.apiTokens()),
          firstValueFrom(this.api.emailChannels()),
          firstValueFrom(this.api.webhooks()),
          firstValueFrom(this.api.webhookDeliveries()),
          firstValueFrom(this.api.connections()),
          firstValueFrom(this.api.ssoConfiguration()),
          firstValueFrom(this.api.aiConfiguration()),
        ]);
      this.projects.set(projects);
      this.portal.set(portal);
      this.tokens.set(tokens);
      this.channels.set(channels);
      this.webhooks.set(webhooks);
      this.deliveries.set(deliveries);
      this.connections.set(connections);
      this.channelProject = projects[0]?.id ?? '';
      this.sso = {
        authority: sso.authority,
        clientId: sso.clientId,
        clientSecret: '',
        domains: sso.allowedEmailDomains.join(', '),
        isEnabled: sso.isEnabled,
        requireSso: sso.requireSso,
      };
      this.ai = {
        isEnabled: ai.isEnabled,
        provider: ai.provider,
        credential: '',
        allowedKinds: ai.allowedKinds,
        requireHumanReview: true,
        allowAttachmentContent: ai.allowAttachmentContent,
        providerDoesNotTrain: ai.providerDoesNotTrain,
      };
    } catch {
      this.error.set(
        this.text(
          'Could not load integration settings.',
          "Impossible de charger les paramètres d'intégration.",
        ),
      );
    }
  }
  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    this.success.set('');
    try {
      await action();
      this.success.set(this.text('Changes saved.', 'Modifications enregistrées.'));
    } catch {
      this.error.set(
        this.text(
          'The operation could not be completed.',
          "L'opération n'a pas pu être effectuée.",
        ),
      );
    } finally {
      this.busy.set(false);
    }
  }
}
