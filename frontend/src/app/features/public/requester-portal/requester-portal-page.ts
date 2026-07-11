import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { ApiClient } from '../../../core/api/api-client';
import {
  AppLanguage,
  KnowledgeArticle,
  PublicPortal,
  RequesterTicket,
} from '../../../core/api/api-models';
import { RichHtmlPipe } from '../../../core/content/rich-html.pipe';
import { I18nService } from '../../../core/i18n/i18n.service';
import { LocalizedDatePipe } from '../../../core/i18n/localized-date.pipe';
import { ThemeService } from '../../../core/theme/theme.service';

@Component({
  selector: 'app-requester-portal-page',
  imports: [
    FormsModule,
    ButtonModule,
    CardModule,
    InputTextModule,
    MessageModule,
    SelectModule,
    TextareaModule,
    LocalizedDatePipe,
    RichHtmlPipe,
  ],
  templateUrl: './requester-portal-page.html',
  styleUrl: './requester-portal-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RequesterPortalPage {
  private readonly api = inject(ApiClient);
  private readonly tenantId = inject(ActivatedRoute).snapshot.paramMap.get('tenantId')!;
  readonly i18n = inject(I18nService);
  readonly theme = inject(ThemeService);
  readonly portal = signal<PublicPortal | null>(null);
  readonly ticket = signal<RequesterTicket | null>(null);
  readonly articles = signal<KnowledgeArticle[]>([]);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly createdKey = signal('');
  projectId = '';
  displayName = '';
  email = '';
  title = '';
  description = '';
  language: AppLanguage = 'English';
  lookupTicketId = '';
  lookupToken = '';
  reply = '';
  search = '';

  constructor() {
    void this.load();
  }

  text(en: string, fr: string): string {
    return this.i18n.language() === 'French' ? fr : en;
  }
  projectName(project: PublicPortal['projects'][number]): string {
    return project.name;
  }

  async submit(): Promise<void> {
    if (
      !this.projectId ||
      !this.displayName.trim() ||
      !this.email.trim() ||
      !this.title.trim() ||
      !this.description.trim()
    )
      return;
    await this.run(async () => {
      const created = await firstValueFrom(
        this.api.submitRequesterTicket(this.tenantId, {
          projectId: this.projectId,
          displayName: this.displayName,
          email: this.email,
          language: this.i18n.language(),
          title: this.title,
          description: this.description,
          typeKey: null,
          priorityKey: null,
        }),
      );
      this.lookupTicketId = created.ticketId;
      this.lookupToken = created.accessToken;
      sessionStorage.setItem(`reqnest.requester.${created.ticketId}`, created.accessToken);
      this.createdKey.set(created.key);
      await this.lookup();
    });
  }

  async lookup(): Promise<void> {
    const token =
      this.lookupToken || sessionStorage.getItem(`reqnest.requester.${this.lookupTicketId}`) || '';
    if (!this.lookupTicketId || !token) return;
    await this.run(async () => {
      this.lookupToken = token;
      this.ticket.set(await firstValueFrom(this.api.requesterTicket(this.lookupTicketId, token)));
    });
  }

  async addReply(): Promise<void> {
    if (!this.reply.trim()) return;
    await this.run(async () => {
      await firstValueFrom(
        this.api.requesterComment(this.lookupTicketId, this.lookupToken, this.reply),
      );
      this.reply = '';
      await this.lookup();
    });
  }

  async searchKnowledge(): Promise<void> {
    this.articles.set(await firstValueFrom(this.api.publicKnowledge(this.tenantId, this.search)));
  }

  toggleLanguage(): void {
    this.i18n.setLanguage(this.i18n.language() === 'English' ? 'French' : 'English');
  }
  toggleTheme(): void {
    this.theme.setPreference(this.theme.darkActive() ? 'Light' : 'Dark');
  }

  private async load(): Promise<void> {
    try {
      const portal = await firstValueFrom(this.api.publicPortal(this.tenantId));
      this.portal.set(portal);
      this.projectId = portal.projects[0]?.id ?? '';
      this.i18n.setLanguage(portal.defaultLanguage);
      this.language = portal.defaultLanguage;
      document.documentElement.style.setProperty('--tenant-accent', portal.primaryColor);
      await this.searchKnowledge();
    } catch {
      this.error.set(
        this.text(
          'This requester portal is unavailable.',
          'Ce portail demandeur est indisponible.',
        ),
      );
    } finally {
      this.loading.set(false);
    }
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    try {
      await action();
    } catch {
      this.error.set(
        this.text('The request could not be completed.', "La demande n'a pas pu être traitée."),
      );
    } finally {
      this.busy.set(false);
    }
  }
}
