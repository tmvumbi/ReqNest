import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { PasswordModule } from 'primeng/password';
import { ApiClient } from '../../../core/api/api-client';
import { I18nService } from '../../../core/i18n/i18n.service';

@Component({
  selector: 'app-token-action-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    ButtonModule,
    CardModule,
    InputTextModule,
    MessageModule,
    PasswordModule,
  ],
  template: `
    <main class="token-layout">
      <a routerLink="/" class="brand"><span>R</span>ReqNest</a
      ><p-card
        ><h1>{{ title() }}</h1>
        <p>{{ help() }}</p>
        <form [formGroup]="form" (ngSubmit)="submit()">
          @if (mode === 'invitation') {
            <div>
              <label for="displayName">{{ i18n.text('auth.displayName') }}</label
              ><input pInputText id="displayName" formControlName="displayName" />
            </div>
          }
          <div>
            <label for="tokenPassword">{{ i18n.text('auth.password') }}</label
            ><p-password
              inputId="tokenPassword"
              formControlName="password"
              [toggleMask]="true"
              [feedback]="true"
            />
          </div>
          @if (error()) {
            <p-message severity="error">{{ i18n.text('auth.error') }}</p-message>
          }
          <button pButton type="submit" [loading]="submitting()">
            {{ i18n.text('common.save') }}
          </button>
        </form></p-card
      >
    </main>
  `,
  styles: `
    .token-layout {
      min-height: 100vh;
      display: grid;
      width: min(30rem, calc(100% - 2rem));
      margin: 0 auto;
      align-content: center;
      gap: 1rem;
    }
    .brand {
      display: flex;
      gap: 0.6rem;
      align-items: center;
      color: var(--app-text);
      text-decoration: none;
      font-weight: 800;
    }
    .brand span {
      display: grid;
      width: 2.4rem;
      height: 2.4rem;
      place-items: center;
      border-radius: 0.75rem;
      color: white;
      background: var(--p-primary-color);
    }
    h1 {
      margin-top: 0;
    }
    form,
    form div {
      display: grid;
      gap: 0.5rem;
    }
    form {
      gap: 1rem;
    }
    label {
      font-weight: 700;
    }
    input,
    p-password,
    button {
      width: 100%;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TokenActionPage {
  private readonly api = inject(ApiClient);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly formBuilder = inject(FormBuilder);
  readonly i18n = inject(I18nService);
  readonly mode = this.route.snapshot.data['mode'] as 'invitation' | 'reset';
  readonly token = this.route.snapshot.queryParamMap.get('token') ?? '';
  readonly submitting = signal(false);
  readonly error = signal(false);
  readonly form = this.formBuilder.nonNullable.group({
    displayName: [''],
    password: ['', [Validators.required, Validators.minLength(12)]],
  });
  title(): string {
    return this.mode === 'invitation'
      ? this.i18n.language() === 'French'
        ? "Accepter l'invitation"
        : 'Accept invitation'
      : this.i18n.text('auth.resetTitle');
  }
  help(): string {
    return this.i18n.language() === 'French'
      ? 'Choisissez un mot de passe sécurisé pour continuer.'
      : 'Choose a secure password to continue.';
  }
  async submit(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid || !this.token) {
      this.error.set(true);
      return;
    }
    this.submitting.set(true);
    this.error.set(false);
    try {
      const value = this.form.getRawValue();
      await firstValueFrom(
        this.mode === 'invitation'
          ? this.api.acceptInvitation(this.token, value.displayName, value.password)
          : this.api.resetPassword(this.token, value.password),
      );
      await this.router.navigate(['/login']);
    } catch {
      this.error.set(true);
    } finally {
      this.submitting.set(false);
    }
  }
}
