import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { EditorModule } from 'primeng/editor';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { ApiClient } from '../../core/api/api-client';
import {
  CustomFieldDefinition,
  Project,
  TicketPriority,
  TicketSchema,
  TicketType,
  TicketTypeDefinition,
} from '../../core/api/api-models';
import { I18nService } from '../../core/i18n/i18n.service';

@Component({
  selector: 'app-ticket-create-page',
  imports: [
    ReactiveFormsModule,
    FormsModule,
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
  readonly schema = signal<TicketSchema | null>(null);
  readonly submitting = signal(false);
  readonly customFields: Record<string, unknown> = {};
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
    typeKey: this.formBuilder.nonNullable.control('', Validators.required),
    priorityKey: this.formBuilder.nonNullable.control('', Validators.required),
    labels: this.formBuilder.nonNullable.control(''),
    dueAt: this.formBuilder.control<Date | null>(null),
  });

  constructor() {
    void firstValueFrom(this.api.projects()).then((projects) => {
      const active = projects.filter((project) => !project.isArchived);
      this.projects.set(active);
      if (active.length === 1) {
        this.form.controls.projectId.setValue(active[0].id);
        void this.loadSchema(active[0].id);
      }
    });
  }

  async loadSchema(projectId: string): Promise<void> {
    const schema = await firstValueFrom(this.api.ticketSchema(projectId));
    this.schema.set(schema);
    const types = this.activeDefinitions(schema.types, projectId);
    const priorities = this.activeDefinitions(schema.priorities, projectId);
    this.form.controls.typeKey.setValue(types[0]?.key ?? '');
    this.form.controls.priorityKey.setValue(
      priorities.find((item) => item.key === 'Normal')?.key ?? priorities[0]?.key ?? '',
    );
    for (const field of schema.customFields) this.customFields[field.key] = null;
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
          type: this.legacyType(value.typeKey),
          priority: this.legacyPriority(value.priorityKey),
          typeKey: value.typeKey,
          priorityKey: value.priorityKey,
          customFields: this.customFields,
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
    return project.name;
  }

  definitionLabel(definition: { label: string }): string {
    return definition.label;
  }

  activeDefinitions<T extends TicketTypeDefinition>(items: T[], projectId: string): T[] {
    const byKey = new Map<string, T>();
    for (const item of items.filter((item) => item.isActive)) {
      if (!byKey.has(item.key) || item.projectId === projectId) byKey.set(item.key, item);
    }
    return [...byKey.values()].sort((a, b) => a.order - b.order);
  }

  fields(projectId: string): CustomFieldDefinition[] {
    const byKey = new Map<string, CustomFieldDefinition>();
    const relevant = (this.schema()?.customFields ?? []).filter(
      (item) =>
        item.isActive && (item.projectIds.length === 0 || item.projectIds.includes(projectId)),
    );
    for (const item of relevant) {
      if (!byKey.has(item.key) || item.projectIds.includes(projectId)) byKey.set(item.key, item);
    }
    return [...byKey.values()].sort((a, b) => a.order - b.order);
  }

  choices(field: CustomFieldDefinition): string[] {
    try {
      return JSON.parse(field.optionsJson) as string[];
    } catch {
      return [];
    }
  }

  private legacyType(key: string): TicketType {
    return ['Incident', 'ServiceRequest', 'Task', 'Problem'].includes(key)
      ? (key as TicketType)
      : 'Incident';
  }

  private legacyPriority(key: string): TicketPriority {
    return ['Low', 'Normal', 'High', 'Urgent'].includes(key) ? (key as TicketPriority) : 'Normal';
  }
}
