import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { EditorInitEvent, EditorModule } from 'primeng/editor';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { ApiClient } from '../../core/api/api-client';
import { Member, TicketDetail, TicketPriority, TicketType } from '../../core/api/api-models';
import { MentionAutocomplete } from '../../core/content/mention-autocomplete';
import { I18nService } from '../../core/i18n/i18n.service';
import { SessionStore } from '../../core/session/session-store';

@Component({
  selector: 'app-ticket-edit-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    ButtonModule,
    EditorModule,
    InputTextModule,
    MentionAutocomplete,
    MessageModule,
    SelectModule,
  ],
  templateUrl: './ticket-edit-page.html',
  styleUrl: './ticket-edit-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TicketEditPage {
  private readonly api = inject(ApiClient);
  private readonly router = inject(Router);
  private readonly formBuilder = inject(FormBuilder);
  private ticketId!: string;
  readonly i18n = inject(I18nService);
  readonly store = inject(SessionStore);
  readonly ticket = signal<TicketDetail | null>(null);
  readonly members = signal<Member[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal(false);
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
  readonly form = this.formBuilder.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(240)]],
    description: ['', Validators.required],
    type: ['Incident' as TicketType, Validators.required],
    priority: ['Normal' as TicketPriority, Validators.required],
    assigneeUserId: [''],
    labels: [''],
    dueAt: [''],
    resolutionSummary: [''],
  });

  constructor() {
    // Re-run on param changes: the router reuses this component when
    // navigating between tickets.
    inject(ActivatedRoute)
      .paramMap.pipe(takeUntilDestroyed())
      .subscribe((params) => {
        this.ticketId = params.get('ticketId')!;
        void this.load();
      });
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

  labelEditor(event: Event, labelId: string): void {
    (event as unknown as EditorInitEvent).editor.root.setAttribute('aria-labelledby', labelId);
  }

  async save(): Promise<void> {
    const ticket = this.ticket();
    this.form.markAllAsTouched();
    if (!ticket || this.form.invalid || this.saving()) return;
    const value = this.form.getRawValue();
    this.saving.set(true);
    this.error.set(false);
    try {
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
          typeKey: ticket.typeKey,
          priorityKey: ticket.priorityKey,
          customFields: ticket.customFields,
        }),
      );
      await this.router.navigate(['/app/tickets', ticket.id]);
    } catch {
      this.error.set(true);
    } finally {
      this.saving.set(false);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(false);
    try {
      const [ticket, members] = await Promise.all([
        firstValueFrom(this.api.ticket(this.ticketId)),
        firstValueFrom(this.api.members()).catch(() => [] as Member[]),
      ]);
      if (!this.store.canMaintainProject(ticket.projectId)) {
        await this.router.navigate(['/app/tickets', ticket.id]);
        return;
      }
      this.form.reset({
        title: ticket.title,
        description: ticket.description,
        type: ticket.type,
        priority: ticket.priority,
        assigneeUserId: ticket.assigneeUserId ?? '',
        labels: ticket.labels.join(', '),
        dueAt: ticket.dueAt ? this.toDateTimeLocal(ticket.dueAt) : '',
        resolutionSummary: ticket.resolutionSummary ?? '',
      });
      this.members.set(members);
      this.ticket.set(ticket);
    } catch {
      this.error.set(true);
    } finally {
      this.loading.set(false);
    }
  }

  // datetime-local inputs hold wall-clock time; render the ISO instant in the user's zone
  private toDateTimeLocal(iso: string): string {
    const date = new Date(iso);
    const pad = (value: number) => String(value).padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
  }
}
