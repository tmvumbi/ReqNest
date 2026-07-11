import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { MultiSelectModule } from 'primeng/multiselect';
import { SelectModule } from 'primeng/select';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { ApiClient } from '../../../core/api/api-client';
import { AppRole, Project } from '../../../core/api/api-models';
import { I18nService } from '../../../core/i18n/i18n.service';

@Component({
  selector: 'app-users-invite-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    ButtonModule,
    InputTextModule,
    MessageModule,
    MultiSelectModule,
    SelectModule,
    ToggleSwitchModule,
  ],
  template: `
    <div class="invite-wrap">
      <a routerLink="/app/admin/users" class="back-link">← {{ i18n.text('nav.users') }}</a>
      <header class="page-heading compact">
        <div>
          <h1>{{ i18n.text('admin.invite') }}</h1>
          <p>{{ i18n.text('admin.usersSummary') }}</p>
        </div>
      </header>
      @if (error()) {
        <p-message severity="error">{{ i18n.text('common.error') }}</p-message>
      }
      <form [formGroup]="form" (ngSubmit)="invite()" class="content-panel invite-form">
        <div class="field">
          <label for="inviteEmail">{{ i18n.text('auth.email') }}</label
          ><input pInputText id="inviteEmail" formControlName="email" type="email" />
        </div>
        <div class="field">
          <label for="inviteName">{{ i18n.text('auth.displayName') }}</label
          ><input pInputText id="inviteName" formControlName="displayName" />
        </div>
        <div class="field">
          <label for="inviteRole">{{ i18n.text('admin.role') }}</label
          ><p-select inputId="inviteRole" formControlName="role" [options]="roles"
            ><ng-template #selectedItem let-role>{{ roleLabel(role) }}</ng-template
            ><ng-template #item let-role>{{ roleLabel(role) }}</ng-template></p-select
          >
        </div>
        <label class="toggle-row" for="allProjects"
          ><p-toggleswitch inputId="allProjects" formControlName="allProjects" />{{
            i18n.text('admin.allProjects')
          }}</label
        >
        @if (!form.controls.allProjects.value) {
          <div class="field">
            <label for="inviteProjects">{{ i18n.text('admin.selectedProjects') }}</label
            ><p-multiselect
              inputId="inviteProjects"
              formControlName="projectIds"
              [options]="projects()"
              optionValue="id"
              display="chip"
              ><ng-template #item let-project>{{
                projectName(project)
              }}</ng-template></p-multiselect
            >
          </div>
        }
        @if (developmentInvitation(); as token) {
          <div class="development-token">
            <strong>Development invitation</strong
            ><a [routerLink]="['/accept-invitation']" [queryParams]="{ token }"
              >Open invitation acceptance</a
            ><code>{{ token }}</code>
          </div>
        }
        <div class="form-actions">
          <a pButton severity="secondary" [outlined]="true" routerLink="/app/admin/users">{{
            i18n.text('common.cancel')
          }}</a
          ><button pButton type="submit" [loading]="submitting()">
            {{ i18n.text('admin.invite') }}
          </button>
        </div>
      </form>
    </div>
  `,
  styles: `
    .invite-wrap {
      max-width: 36rem;
      margin-inline: auto;
    }
    .back-link {
      display: inline-block;
      margin-bottom: 0.6rem;
      color: var(--app-text-muted);
      font-size: 0.8rem;
      text-decoration: none;
    }
    .back-link:hover {
      color: var(--p-primary-color);
    }
    .invite-form,
    .field {
      display: grid;
      gap: 0.35rem;
    }
    .invite-form {
      gap: 0.9rem;
    }
    label {
      color: var(--app-text-muted);
      font-size: 0.76rem;
      font-weight: 650;
      letter-spacing: 0.02em;
    }
    input,
    p-select,
    p-multiselect {
      width: 100%;
    }
    .toggle-row {
      display: flex;
      gap: 0.65rem;
      align-items: center;
      font-size: 0.85rem;
      color: var(--app-text);
    }
    .development-token {
      display: grid;
      gap: 0.5rem;
      padding: 0.75rem;
      border-radius: 0.5rem;
      background: var(--app-sunken);
      overflow-wrap: anywhere;
      font-size: 0.82rem;
    }
    .form-actions {
      display: flex;
      justify-content: flex-end;
      gap: 0.6rem;
      padding-top: 0.25rem;
      border-top: 1px solid var(--app-border);
    }
    p-message {
      display: block;
      margin-bottom: 0.85rem;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UsersInvitePage {
  private readonly api = inject(ApiClient);
  private readonly formBuilder = inject(FormBuilder);
  private readonly router = inject(Router);
  readonly i18n = inject(I18nService);
  readonly projects = signal<Project[]>([]);
  readonly submitting = signal(false);
  readonly error = signal(false);
  readonly developmentInvitation = signal<string | null>(null);
  readonly roles: AppRole[] = ['TenantAdministrator', 'ProjectManager', 'Contributor', 'Observer'];
  readonly form = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    displayName: [''],
    role: ['Contributor' as AppRole],
    allProjects: [true],
    projectIds: [[] as string[]],
  });

  constructor() {
    void this.load();
  }

  roleLabel(role: AppRole): string {
    if (this.i18n.language() !== 'French')
      return {
        TenantAdministrator: 'Tenant administrator',
        ProjectManager: 'Project manager',
        Contributor: 'Contributor',
        Observer: 'Observer',
      }[role];
    return {
      TenantAdministrator: 'Administrateur du tenant',
      ProjectManager: 'Chef de projet',
      Contributor: 'Contributeur',
      Observer: 'Observateur',
    }[role];
  }

  projectName(project: Project): string {
    return project.name;
  }

  async invite(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.submitting()) return;
    const value = this.form.getRawValue();
    if (!value.allProjects && value.projectIds.length === 0) return;
    this.submitting.set(true);
    this.error.set(false);
    try {
      const result = await firstValueFrom(
        this.api.invite({
          email: value.email,
          displayName: value.displayName,
          grants: [
            {
              role: value.role,
              allProjects: value.allProjects,
              projectIds: value.allProjects ? [] : value.projectIds,
            },
          ],
        }),
      );
      this.developmentInvitation.set(result.developmentToken);
      if (!result.developmentToken) await this.router.navigate(['/app/admin/users']);
    } catch {
      this.error.set(true);
    } finally {
      this.submitting.set(false);
    }
  }

  private async load(): Promise<void> {
    this.projects.set(await firstValueFrom(this.api.projects()));
  }
}
