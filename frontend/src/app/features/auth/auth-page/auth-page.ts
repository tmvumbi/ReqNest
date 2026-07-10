import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { PasswordModule } from 'primeng/password';
import { SelectModule } from 'primeng/select';
import { ApiClient } from '../../../core/api/api-client';
import { AppLanguage } from '../../../core/api/api-models';
import { I18nService } from '../../../core/i18n/i18n.service';
import { SessionStore } from '../../../core/session/session-store';
import { ThemeService } from '../../../core/theme/theme.service';

@Component({
  selector: 'app-auth-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    ButtonModule,
    CardModule,
    InputTextModule,
    MessageModule,
    PasswordModule,
    SelectModule,
  ],
  templateUrl: './auth-page.html',
  styleUrl: './auth-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuthPage {
  private readonly formBuilder = inject(FormBuilder);
  private readonly api = inject(ApiClient);
  private readonly router = inject(Router);
  private readonly sessionStore = inject(SessionStore);
  private readonly theme = inject(ThemeService);
  readonly i18n = inject(I18nService);
  readonly mode = inject(ActivatedRoute).snapshot.data['mode'] as 'login' | 'register' | 'reset';
  readonly submitting = signal(false);
  readonly error = signal(false);
  readonly successMessage = signal<string | null>(null);
  readonly developmentToken = signal<string | null>(null);
  readonly languages = [
    { label: 'English', value: 'English' as AppLanguage },
    { label: 'Français', value: 'French' as AppLanguage },
  ];
  readonly form = this.formBuilder.nonNullable.group({
    companyName: [''],
    companyShortName: [''],
    displayName: [''],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(12)]],
    language: ['English' as AppLanguage],
  });

  constructor() {
    if (this.mode === 'register') {
      this.form.controls.companyName.addValidators(Validators.required);
      this.form.controls.companyShortName.addValidators(Validators.required);
      this.form.controls.displayName.addValidators(Validators.required);
    }
  }

  async submit(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.submitting()) return;
    this.submitting.set(true);
    this.error.set(false);
    this.successMessage.set(null);
    const value = this.form.getRawValue();
    try {
      if (this.mode === 'reset') {
        const result = await firstValueFrom(this.api.requestPasswordReset(value.email));
        this.successMessage.set(result.message);
        this.developmentToken.set(result.developmentToken);
        return;
      }

      const session = await firstValueFrom(
        this.mode === 'register'
          ? this.api.register({
              companyName: value.companyName,
              companyShortName: value.companyShortName,
              displayName: value.displayName,
              email: value.email,
              password: value.password,
              language: value.language,
              timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC',
            })
          : this.api.login(value.email, value.password),
      );
      this.sessionStore.setSession(session);
      this.i18n.setLanguage(session.preferredLanguage);
      this.theme.setPreference(session.themePreference);
      await this.router.navigate(['/app/dashboard']);
    } catch {
      this.error.set(true);
    } finally {
      this.submitting.set(false);
    }
  }
}
