import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { ApiClient } from '../../../core/api/api-client';
import { I18nService } from '../../../core/i18n/i18n.service';
import { SessionStore } from '../../../core/session/session-store';
import { ThemeService } from '../../../core/theme/theme.service';

@Component({
  selector: 'app-sso-page',
  imports: [FormsModule, RouterLink, ButtonModule, CardModule, InputTextModule, MessageModule],
  templateUrl: './sso-page.html',
  styleUrl: '../auth-page/auth-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SsoPage {
  private readonly api = inject(ApiClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly store = inject(SessionStore);
  private readonly theme = inject(ThemeService);
  readonly i18n = inject(I18nService);
  readonly busy = signal(false);
  readonly error = signal('');
  tenantId = this.route.snapshot.queryParamMap.get('tenantId') ?? '';
  constructor() {
    const code = this.route.snapshot.queryParamMap.get('ssoCode');
    if (code) void this.exchange(code);
    else if (this.route.snapshot.queryParamMap.get('error'))
      this.error.set(
        this.text('Single sign-on was not completed.', "L'authentification unique n'a pas abouti."),
      );
  }
  text(en: string, fr: string): string {
    return this.i18n.language() === 'French' ? fr : en;
  }
  async start(): Promise<void> {
    if (!this.tenantId) return;
    this.busy.set(true);
    this.error.set('');
    try {
      const result = await firstValueFrom(this.api.startSso(this.tenantId));
      window.location.assign(result.authorizationUrl);
    } catch {
      this.error.set(
        this.text(
          'SSO is not available for this company.',
          "Le SSO n'est pas disponible pour cette entreprise.",
        ),
      );
      this.busy.set(false);
    }
  }
  private async exchange(code: string): Promise<void> {
    this.busy.set(true);
    try {
      const session = await firstValueFrom(this.api.exchangeSso(code));
      this.store.setSession(session);
      this.i18n.setLanguage(session.preferredLanguage);
      this.theme.setPreference(session.themePreference);
      await this.router.navigate(['/app/dashboard']);
    } catch {
      this.error.set(
        this.text(
          'The SSO sign-in link is invalid or expired.',
          'Le lien de connexion SSO est invalide ou expiré.',
        ),
      );
    } finally {
      this.busy.set(false);
    }
  }
}
