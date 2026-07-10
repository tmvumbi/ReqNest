import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { ApiClient } from '../../../core/api/api-client';
import { AppRole, CustomRole, Member, Project } from '../../../core/api/api-models';
import { I18nService } from '../../../core/i18n/i18n.service';

@Component({
  selector: 'app-users-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    ButtonModule,
    DialogModule,
    InputTextModule,
    MultiSelectModule,
    SelectModule,
    TableModule,
    TagModule,
    ToggleSwitchModule,
  ],
  templateUrl: './users-page.html',
  styleUrl: './users-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UsersPage {
  private readonly api = inject(ApiClient);
  private readonly formBuilder = inject(FormBuilder);
  readonly i18n = inject(I18nService);
  readonly members = signal<Member[]>([]);
  readonly projects = signal<Project[]>([]);
  readonly customRoles = signal<CustomRole[]>([]);
  readonly visible = signal(false);
  readonly manageVisible = signal(false);
  readonly selectedMember = signal<Member | null>(null);
  readonly submitting = signal(false);
  readonly developmentInvitation = signal<string | null>(null);
  readonly roles: AppRole[] = ['TenantAdministrator', 'ProjectManager', 'Contributor', 'Observer'];
  readonly form = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    displayName: [''],
    role: ['Contributor' as AppRole],
    allProjects: [true],
    projectIds: [[] as string[]],
  });
  readonly manageForm = this.formBuilder.nonNullable.group({
    role: ['Contributor' as AppRole],
    allProjects: [true],
    projectIds: [[] as string[]],
    customRoleIds: [[] as string[]],
  });

  constructor() {
    void this.load();
  }
  async invite(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.submitting()) return;
    const value = this.form.getRawValue();
    if (!value.allProjects && value.projectIds.length === 0) return;
    this.submitting.set(true);
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
      await this.load();
      if (!result.developmentToken) this.visible.set(false);
    } finally {
      this.submitting.set(false);
    }
  }
  grants(member: Member): string {
    return member.grants
      .map(
        (grant) =>
          `${this.roleLabel(grant.role)} · ${grant.allProjects ? this.i18n.text('admin.allProjects') : `${grant.projectIds.length} ${this.i18n.text('nav.projects').toLowerCase()}`}`,
      )
      .join(', ');
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
  statusLabel(status: Member['status']): string {
    if (this.i18n.language() !== 'French') return status;
    return { Invited: 'Invité', Active: 'Actif', Deactivated: 'Désactivé' }[status];
  }
  openManage(member: Member): void {
    const grant = member.grants[0];
    this.selectedMember.set(member);
    this.manageForm.reset({
      role: grant?.role ?? 'Contributor',
      allProjects: grant?.allProjects ?? true,
      projectIds: grant?.projectIds ?? [],
      customRoleIds: [],
    });
    this.manageVisible.set(true);
  }
  async saveRoles(): Promise<void> {
    const member = this.selectedMember();
    if (!member) return;
    const value = this.manageForm.getRawValue();
    if (!value.allProjects && value.projectIds.length === 0) return;
    this.submitting.set(true);
    try {
      await firstValueFrom(
        this.api.updateMemberRoles(member.membershipId, [
          {
            role: value.role,
            allProjects: value.allProjects,
            projectIds: value.allProjects ? [] : value.projectIds,
          },
        ]),
      );
      if (this.customRoles().length) {
        await firstValueFrom(
          this.api.updateCustomRoleGrants(
            member.membershipId,
            value.customRoleIds.map((customRoleId) => ({
              customRoleId,
              allProjects: value.allProjects,
              projectIds: value.allProjects ? [] : value.projectIds,
            })),
          ),
        );
      }
      this.manageVisible.set(false);
      await this.load();
    } finally {
      this.submitting.set(false);
    }
  }
  async resend(member: Member): Promise<void> {
    const result = await firstValueFrom(this.api.resendInvitation(member.membershipId));
    this.developmentInvitation.set(result.developmentToken);
    await this.load();
  }
  async revoke(member: Member): Promise<void> {
    await firstValueFrom(this.api.revokeInvitation(member.membershipId));
    await this.load();
  }
  async toggleActive(member: Member): Promise<void> {
    await firstValueFrom(this.api.setMemberActive(member.membershipId, member.status !== 'Active'));
    await this.load();
  }
  projectName(project: Project): string {
    return this.i18n.language() === 'French' ? project.nameFrench : project.nameEnglish;
  }
  private async load(): Promise<void> {
    const [members, projects, customRoles] = await Promise.all([
      firstValueFrom(this.api.members()),
      firstValueFrom(this.api.projects()),
      firstValueFrom(this.api.customRoles()).catch(() => []),
    ]);
    this.members.set(members);
    this.projects.set(projects);
    this.customRoles.set(customRoles);
  }
}
