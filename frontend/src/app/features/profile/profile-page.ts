import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { PasswordModule } from 'primeng/password';
import { ApiClient } from '../../core/api/api-client';
import { I18nService } from '../../core/i18n/i18n.service';
import { SessionStore } from '../../core/session/session-store';

@Component({
  selector: 'app-profile-page',
  imports: [FormsModule, ButtonModule, InputTextModule, MessageModule, PasswordModule],
  template: `
    <div class="profile-wrap">
      <header class="page-heading compact">
        <div>
          <h1>{{ i18n.text('profile.title') }}</h1>
          <p>{{ i18n.text('profile.summary') }}</p>
        </div>
      </header>
      @if (error()) {
        <p-message severity="error">{{ error() }}</p-message>
      }

      <section class="content-panel">
        <div class="section-heading">
          <h2>{{ text('Profile', 'Profil') }}</h2>
        </div>
        <div class="avatar-row">
          @if (avatarUrl(); as url) {
            <img class="avatar-preview" [src]="url" alt="" />
          } @else {
            <span class="avatar-preview initials">{{ initials() }}</span>
          }
          <div class="avatar-actions">
            <label class="file-button">
              <input
                type="file"
                accept="image/png,image/jpeg,image/webp"
                (change)="uploadAvatar($event)"
                [disabled]="busy()"
              />
              {{ text('Upload image', 'Téléverser une image') }}
            </label>
            @if (avatarUrl()) {
              <button
                pButton
                type="button"
                size="small"
                severity="danger"
                [text]="true"
                [disabled]="busy()"
                (click)="removeAvatar()"
              >
                {{ text('Remove', 'Retirer') }}
              </button>
            }
            <small>{{ text('PNG, JPEG, or WebP · up to 10 MB', 'PNG, JPEG ou WebP · 10 Mo max') }}</small>
          </div>
        </div>
        <form (ngSubmit)="saveProfile()" class="profile-form">
          <div class="field">
            <label for="profileName">{{ i18n.text('auth.displayName') }}</label>
            <input pInputText id="profileName" [(ngModel)]="displayName" name="displayName" required />
          </div>
          <div class="field">
            <label for="profileEmail">{{ i18n.text('auth.email') }}</label>
            <input pInputText id="profileEmail" [ngModel]="email()" name="email" disabled />
          </div>
          <div class="form-actions">
            <button pButton type="submit" [loading]="busy()" [disabled]="!displayName.trim()">
              {{ i18n.text('common.save') }}
            </button>
          </div>
        </form>
      </section>

      <section class="content-panel">
        <div class="section-heading">
          <h2>{{ text('Change password', 'Changer le mot de passe') }}</h2>
        </div>
        <form (ngSubmit)="changePassword()" class="profile-form">
          <div class="field">
            <label for="currentPassword">{{
              text('Current password', 'Mot de passe actuel')
            }}</label>
            <p-password
              inputId="currentPassword"
              [(ngModel)]="currentPassword"
              name="currentPassword"
              [toggleMask]="true"
              [feedback]="false"
              autocomplete="current-password"
            />
          </div>
          <div class="field">
            <label for="newPassword">{{ text('New password', 'Nouveau mot de passe') }}</label>
            <p-password
              inputId="newPassword"
              [(ngModel)]="newPassword"
              name="newPassword"
              [toggleMask]="true"
              [feedback]="true"
              autocomplete="new-password"
            />
            <small>{{ i18n.text('auth.passwordHelp') }}</small>
          </div>
          <div class="form-actions">
            <button
              pButton
              type="submit"
              [loading]="busy()"
              [disabled]="!currentPassword || newPassword.length < 12"
            >
              {{ text('Update password', 'Mettre à jour le mot de passe') }}
            </button>
          </div>
        </form>
      </section>
    </div>
  `,
  styles: `
    .profile-wrap {
      display: grid;
      gap: 0.85rem;
      max-width: 36rem;
      margin-inline: auto;
    }
    .avatar-row {
      display: flex;
      align-items: center;
      gap: 1rem;
      margin-bottom: 1rem;
      padding-bottom: 1rem;
      border-bottom: 1px solid var(--app-border);
    }
    .avatar-preview {
      width: 4.25rem;
      height: 4.25rem;
      flex-shrink: 0;
      border-radius: 50%;
      object-fit: cover;
      border: 1px solid var(--app-border);
    }
    .avatar-preview.initials {
      display: grid;
      place-items: center;
      background: color-mix(in srgb, var(--p-primary-color) 15%, transparent);
      color: var(--p-primary-color);
      font-size: 1.3rem;
      font-weight: 750;
    }
    .avatar-actions {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.5rem;
    }
    .avatar-actions small {
      flex-basis: 100%;
      color: var(--app-text-subtle);
      font-size: 0.75rem;
    }
    .file-button {
      display: inline-block;
      padding: 0.45rem 0.9rem;
      border: 1px solid var(--app-border-strong);
      border-radius: 0.5rem;
      color: var(--app-text);
      font-size: 0.85rem;
      font-weight: 600;
      cursor: pointer;
    }
    .file-button:hover {
      border-color: var(--p-primary-color);
      color: var(--p-primary-color);
    }
    .file-button input {
      position: absolute;
      width: 1px;
      height: 1px;
      overflow: hidden;
      clip: rect(0 0 0 0);
    }
    .profile-form,
    .field {
      display: grid;
      gap: 0.35rem;
    }
    .profile-form {
      gap: 0.9rem;
    }
    label {
      color: var(--app-text-muted);
      font-size: 0.76rem;
      font-weight: 650;
      letter-spacing: 0.02em;
    }
    input,
    p-password {
      width: 100%;
    }
    small {
      color: var(--app-text-subtle);
      font-size: 0.75rem;
    }
    .form-actions {
      justify-content: flex-end;
    }
    p-message {
      display: block;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfilePage {
  private readonly api = inject(ApiClient);
  private readonly messages = inject(MessageService);
  private readonly store = inject(SessionStore);
  private readonly destroyRef = inject(DestroyRef);
  readonly i18n = inject(I18nService);
  readonly email = signal('');
  readonly avatarUrl = signal<string | null>(null);
  readonly busy = signal(false);
  readonly error = signal('');
  displayName = '';
  currentPassword = '';
  newPassword = '';

  constructor() {
    void this.load();
    this.destroyRef.onDestroy(() => this.revokeAvatarUrl());
  }

  text(en: string, fr: string): string {
    return this.i18n.language() === 'French' ? fr : en;
  }

  initials(): string {
    const parts = (this.displayName || '?').trim().split(/\s+/);
    return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase() || '?';
  }

  async saveProfile(): Promise<void> {
    await this.run(async () => {
      const profile = await firstValueFrom(this.api.updateProfile(this.displayName.trim()));
      this.displayName = profile.displayName;
      this.store.setDisplayName(profile.displayName);
    });
  }

  async changePassword(): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.api.changePassword(this.currentPassword, this.newPassword));
      this.currentPassword = '';
      this.newPassword = '';
    });
  }

  async uploadAvatar(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    await this.run(async () => {
      await firstValueFrom(this.api.uploadAvatar(file));
      await this.refreshAvatar();
    });
    input.value = '';
  }

  async removeAvatar(): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.api.deleteAvatar());
      this.revokeAvatarUrl();
      this.avatarUrl.set(null);
    });
  }

  private async load(): Promise<void> {
    try {
      const profile = await firstValueFrom(this.api.profile());
      this.displayName = profile.displayName;
      this.email.set(profile.email);
      if (profile.hasAvatar) await this.refreshAvatar();
    } catch {
      this.error.set(this.i18n.text('common.error'));
    }
  }

  private async refreshAvatar(): Promise<void> {
    try {
      const blob = await firstValueFrom(this.api.avatar());
      this.revokeAvatarUrl();
      this.avatarUrl.set(URL.createObjectURL(blob));
    } catch {
      this.avatarUrl.set(null);
    }
  }

  private revokeAvatarUrl(): void {
    const url = this.avatarUrl();
    if (url) URL.revokeObjectURL(url);
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    try {
      await action();
      this.messages.add({
        severity: 'success',
        summary: this.text('Profile updated.', 'Profil mis à jour.'),
        life: 4000,
      });
    } catch {
      this.error.set(this.i18n.text('common.error'));
    } finally {
      this.busy.set(false);
    }
  }
}
