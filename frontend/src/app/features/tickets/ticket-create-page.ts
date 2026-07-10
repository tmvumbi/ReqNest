import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { EditorModule } from 'primeng/editor';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { ApiClient } from '../../core/api/api-client';
import { Project, TicketPriority, TicketType } from '../../core/api/api-models';
import { I18nService } from '../../core/i18n/i18n.service';

@Component({
  selector: 'app-ticket-create-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    ButtonModule,
    DatePickerModule,
    EditorModule,
    InputTextModule,
    SelectModule,
  ],
  templateUrl: './ticket-create-page.html',
  styleUrl: './ticket-create-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TicketCreatePage {
  private readonly api = inject(ApiClient);
  private readonly router = inject(Router);
  private readonly formBuilder = inject(FormBuilder);
  readonly i18n = inject(I18nService);
  readonly projects = signal<Project[]>([]);
  readonly submitting = signal(false);
  readonly priorities: TicketPriority[] = ['Low', 'Normal', 'High', 'Urgent'];
  readonly types: TicketType[] = ['Incident', 'ServiceRequest', 'Task', 'Problem'];
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
  readonly form = this.formBuilder.group({
    projectId: this.formBuilder.nonNullable.control('', Validators.required),
    title: this.formBuilder.nonNullable.control('', [
      Validators.required,
      Validators.maxLength(300),
    ]),
    description: this.formBuilder.nonNullable.control('', Validators.required),
    type: this.formBuilder.nonNullable.control<TicketType>('Incident'),
    priority: this.formBuilder.nonNullable.control<TicketPriority>('Normal'),
    labels: this.formBuilder.nonNullable.control(''),
    dueAt: this.formBuilder.control<Date | null>(null),
  });

  constructor() {
    void firstValueFrom(this.api.projects()).then((projects) =>
      this.projects.set(projects.filter((project) => !project.isArchived)),
    );
  }

  async create(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.submitting()) return;
    this.submitting.set(true);
    try {
      const value = this.form.getRawValue();
      const ticket = await firstValueFrom(
        this.api.createTicket({
          projectId: value.projectId,
          title: value.title,
          description: value.description,
          type: value.type,
          priority: value.priority,
          assigneeUserId: null,
          labels: value.labels
            .split(',')
            .map((label) => label.trim())
            .filter(Boolean),
          dueAt: value.dueAt?.toISOString() ?? null,
        }),
      );
      await this.router.navigate(['/app/tickets', ticket.id]);
    } finally {
      this.submitting.set(false);
    }
  }

  projectName(project: Project): string {
    return this.i18n.language() === 'French' ? project.nameFrench : project.nameEnglish;
  }
}
