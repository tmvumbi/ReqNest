import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { EditorInitEvent, EditorModule } from 'primeng/editor';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { MultiSelectModule } from 'primeng/multiselect';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { ApiClient } from '../../core/api/api-client';
import {
  Member,
  TicketActivity,
  TicketAttachment,
  TicketComment,
  TicketDetail,
  TicketPriority,
  TicketType,
  Workflow,
  WorkflowStatus,
} from '../../core/api/api-models';
import { I18nService } from '../../core/i18n/i18n.service';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { SessionStore } from '../../core/session/session-store';

@Component({
  selector: 'app-ticket-detail-page',
  imports: [
    DecimalPipe,
    LocalizedDatePipe,
    ReactiveFormsModule,
    RouterLink,
    ButtonModule,
    DialogModule,
    EditorModule,
    InputTextModule,
    MessageModule,
    MultiSelectModule,
    SelectModule,
    TagModule,
  ],
  templateUrl: './ticket-detail-page.html',
  styleUrl: './ticket-detail-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TicketDetailPage {
  private readonly api = inject(ApiClient);
  private readonly formBuilder = inject(FormBuilder);
  private readonly ticketId = inject(ActivatedRoute).snapshot.paramMap.get('ticketId')!;
  readonly i18n = inject(I18nService);
  readonly store = inject(SessionStore);
  readonly ticket = signal<TicketDetail | null>(null);
  readonly workflow = signal<Workflow | null>(null);
  readonly comments = signal<TicketComment[]>([]);
  readonly activity = signal<TicketActivity[]>([]);
  readonly attachments = signal<TicketAttachment[]>([]);
  readonly members = signal<Member[]>([]);
  readonly editVisible = signal(false);
  readonly saving = signal(false);
  readonly loading = signal(true);
  readonly submittingComment = signal(false);
  readonly uploading = signal(false);
  readonly error = signal(false);
  readonly commentForm = this.formBuilder.nonNullable.group({
    body: ['', Validators.required],
    mentionUserIds: [[] as string[]],
  });
  readonly editForm = this.formBuilder.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(240)]],
    description: ['', Validators.required],
    type: ['Incident' as TicketType, Validators.required],
    priority: ['Normal' as TicketPriority, Validators.required],
    assigneeUserId: [''],
    labels: [''],
    dueAt: [''],
    resolutionSummary: [''],
  });
  readonly editorFormats = [
    'header',
    'bold',
    'italic',
    'strike',
    'list',
    'blockquote',
    'code-block',
    'link',
  ];
  readonly types: TicketType[] = ['Incident', 'ServiceRequest', 'Task', 'Problem'];
  readonly priorities: TicketPriority[] = ['Low', 'Normal', 'High', 'Urgent'];

  constructor() {
    void this.load();
  }

  allowedStatuses(): WorkflowStatus[] {
    const workflow = this.workflow();
    const ticket = this.ticket();
    if (!workflow || !ticket) return [];
    const targets = workflow.transitions
      .filter((transition) => transition.fromStatusId === ticket.statusId)
      .map((transition) => transition.toStatusId);
    return workflow.statuses.filter((status) => targets.includes(status.id));
  }

  async transition(status: WorkflowStatus): Promise<void> {
    const ticket = this.ticket();
    if (!ticket) return;
    try {
      this.ticket.set(
        await firstValueFrom(this.api.transition(ticket.id, status.id, ticket.version)),
      );
      await this.refreshActivity();
    } catch {
      this.error.set(true);
    }
  }

  async addComment(): Promise<void> {
    this.commentForm.markAllAsTouched();
    if (this.commentForm.invalid || this.submittingComment()) return;
    this.submittingComment.set(true);
    try {
      await firstValueFrom(
        this.api.addComment(
          this.ticketId,
          this.commentForm.controls.body.value,
          this.commentForm.controls.mentionUserIds.value,
        ),
      );
      this.commentForm.reset({ body: '', mentionUserIds: [] });
      await Promise.all([this.refreshComments(), this.refreshActivity()]);
    } finally {
      this.submittingComment.set(false);
    }
  }

  async upload(event: Event): Promise<void> {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    this.uploading.set(true);
    this.error.set(false);
    try {
      await firstValueFrom(this.api.uploadAttachment(this.ticketId, file));
      await Promise.all([this.refreshAttachments(), this.refreshActivity()]);
    } catch {
      this.error.set(true);
    } finally {
      this.uploading.set(false);
      (event.target as HTMLInputElement).value = '';
    }
  }

  statusLabel(status: WorkflowStatus): string {
    return this.i18n.language() === 'French' ? status.labelFrench : status.labelEnglish;
  }
  displayScanStatus(status: TicketAttachment['scanStatus']): string {
    if (this.i18n.language() !== 'French') return status;
    return {
      Pending: 'En attente',
      Clean: 'Sain',
      Quarantined: 'En quarantaine',
      Rejected: 'Rejeté',
    }[status];
  }

  activitySummary(event: TicketActivity): string {
    const action = this.i18n.ticketActivityAction(event.action);
    if (!event.summary || event.category === 'change') return action;
    const container = document.createElement('div');
    container.innerHTML = event.summary;
    const detail = (container.textContent ?? '').trim();
    return detail ? `${action}: ${detail}` : action;
  }

  labelEditor(event: Event, labelId: string): void {
    (event as unknown as EditorInitEvent).editor.root.setAttribute('aria-labelledby', labelId);
  }

  openEdit(): void {
    const ticket = this.ticket();
    if (!ticket) return;
    this.editForm.reset({
      title: ticket.title,
      description: ticket.description,
      type: ticket.type,
      priority: ticket.priority,
      assigneeUserId: ticket.assigneeUserId ?? '',
      labels: ticket.labels.join(', '),
      dueAt: ticket.dueAt ? ticket.dueAt.slice(0, 16) : '',
      resolutionSummary: ticket.resolutionSummary ?? '',
    });
    this.editVisible.set(true);
  }

  async saveEdit(): Promise<void> {
    const ticket = this.ticket();
    this.editForm.markAllAsTouched();
    if (!ticket || this.editForm.invalid || this.saving()) return;
    const value = this.editForm.getRawValue();
    this.saving.set(true);
    this.error.set(false);
    try {
      this.ticket.set(
        await firstValueFrom(
          this.api.updateTicket(ticket, {
            title: value.title,
            description: value.description,
            type: value.type,
            priority: value.priority,
            assigneeUserId: value.assigneeUserId || null,
            labels: value.labels
              .split(',')
              .map((label) => label.trim())
              .filter(Boolean),
            dueAt: value.dueAt ? new Date(value.dueAt).toISOString() : null,
            resolutionSummary: value.resolutionSummary || null,
          }),
        ),
      );
      this.editVisible.set(false);
      await this.refreshActivity();
    } catch {
      this.error.set(true);
    } finally {
      this.saving.set(false);
    }
  }

  async setArchived(archived: boolean): Promise<void> {
    const ticket = this.ticket();
    if (!ticket) return;
    this.ticket.set(await firstValueFrom(this.api.setTicketArchived(ticket, archived)));
    await this.refreshActivity();
  }

  async downloadAttachment(attachment: TicketAttachment): Promise<void> {
    const blob = await firstValueFrom(this.api.downloadAttachment(attachment.id));
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = attachment.fileName;
    anchor.click();
    URL.revokeObjectURL(url);
  }

  async deleteAttachment(attachment: TicketAttachment): Promise<void> {
    await firstValueFrom(this.api.deleteAttachment(attachment.id));
    await Promise.all([this.refreshAttachments(), this.refreshActivity()]);
  }

  isWatching(item: TicketDetail): boolean {
    return item.watchers.some((watcher) => watcher.userId === this.store.session()?.userId);
  }
  isMuted(item: TicketDetail): boolean {
    return (
      item.watchers.find((watcher) => watcher.userId === this.store.session()?.userId)?.isMuted ??
      false
    );
  }
  async toggleWatching(item: TicketDetail): Promise<void> {
    if (this.isWatching(item)) await firstValueFrom(this.api.unwatchTicket(item.id));
    else await firstValueFrom(this.api.watchTicket(item.id));
    await this.refreshTicket();
  }
  async toggleMute(item: TicketDetail): Promise<void> {
    await firstValueFrom(this.api.muteTicket(item.id, !this.isMuted(item)));
    await this.refreshTicket();
  }

  eligibleMembers(item: TicketDetail): Member[] {
    return this.members().filter(
      (member) =>
        member.status === 'Active' &&
        member.grants.some(
          (grant) => grant.allProjects || grant.projectIds.includes(item.projectId),
        ),
    );
  }

  private async load(): Promise<void> {
    try {
      const [ticket, workflows, comments, activity, attachments, members] = await Promise.all([
        firstValueFrom(this.api.ticket(this.ticketId)),
        firstValueFrom(this.api.workflows()),
        firstValueFrom(this.api.comments(this.ticketId)),
        firstValueFrom(this.api.activity(this.ticketId)),
        firstValueFrom(this.api.attachments(this.ticketId)),
        firstValueFrom(this.api.members()).catch(() => []),
      ]);
      this.ticket.set(ticket);
      this.workflow.set(
        workflows.find((item) => item.statuses.some((status) => status.id === ticket.statusId)) ??
          null,
      );
      this.comments.set(comments);
      this.activity.set(activity);
      this.attachments.set(attachments);
      this.members.set(members);
    } catch {
      this.error.set(true);
    } finally {
      this.loading.set(false);
    }
  }

  private async refreshComments(): Promise<void> {
    this.comments.set(await firstValueFrom(this.api.comments(this.ticketId)));
  }
  private async refreshActivity(): Promise<void> {
    this.activity.set(await firstValueFrom(this.api.activity(this.ticketId)));
  }
  private async refreshAttachments(): Promise<void> {
    this.attachments.set(await firstValueFrom(this.api.attachments(this.ticketId)));
  }
  private async refreshTicket(): Promise<void> {
    this.ticket.set(await firstValueFrom(this.api.ticket(this.ticketId)));
  }
}
