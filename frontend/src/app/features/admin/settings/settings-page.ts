import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { ApiClient } from '../../../core/api/api-client';
import { AppLanguage, ThemePreference } from '../../../core/api/api-models';
import { I18nService } from '../../../core/i18n/i18n.service';

@Component({
  selector: 'app-settings-page',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    ConfirmDialogModule,
    InputTextModule,
    SelectModule,
    TextareaModule,
  ],
  providers: [ConfirmationService],
  templateUrl: './settings-page.html',
  styleUrl: './settings-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SettingsPage {
  private readonly api = inject(ApiClient);
  private readonly formBuilder = inject(FormBuilder);
  private readonly confirmation = inject(ConfirmationService);
  private readonly messages = inject(MessageService);
  readonly i18n = inject(I18nService);
  readonly saving = signal(false);
  readonly logoUploading = signal(false);
  readonly lightLogoUrl = signal('');
  readonly darkLogoUrl = signal('');
  readonly languages: AppLanguage[] = ['English', 'French'];
  readonly themes: ThemePreference[] = ['System', 'Light', 'Dark'];
  readonly form = this.formBuilder.nonNullable.group({
    name: ['', Validators.required],
    shortName: ['', Validators.required],
    defaultLanguage: ['English' as AppLanguage],
    timeZone: ['UTC', Validators.required],
    defaultTheme: ['System' as ThemePreference],
    primaryColor: ['#4f46e5', [Validators.required, Validators.pattern(/^#[0-9a-fA-F]{6}$/)]],
    supportContact: [''],
    reportFooterText: [''],
  });
  constructor() {
    void this.load();
  }
  async save(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    this.saving.set(true);
    try {
      const value = this.form.getRawValue();
      await firstValueFrom(
        this.api.updateTenantSettings({
          ...value,
          supportContact: value.supportContact || null,
          reportFooterText: value.reportFooterText || null,
        }),
      );
      this.notifySaved(
        this.i18n.language() === 'French' ? 'Paramètres enregistrés.' : 'Settings saved.',
      );
    } catch {
      this.notifyError();
    } finally {
      this.saving.set(false);
    }
  }
  async uploadLogo(event: Event, variant: 'light' | 'dark'): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.logoUploading.set(true);
    try {
      await firstValueFrom(this.api.uploadLogo(variant, file));
      this.setLogoPreview(variant, file);
      this.notifySaved(
        this.i18n.language() === 'French' ? 'Logo mis à jour.' : 'Logo updated.',
      );
    } catch {
      this.notifyError();
    } finally {
      this.logoUploading.set(false);
      input.value = '';
    }
  }
  removeLogo(variant: 'light' | 'dark'): void {
    const french = this.i18n.language() === 'French';
    this.confirmation.confirm({
      header: french ? 'Retirer le logo' : 'Remove logo',
      message: french
        ? `Retirer le logo sur fond ${variant === 'light' ? 'clair' : 'sombre'} ?`
        : `Remove the ${variant} background logo?`,
      acceptLabel: french ? 'Retirer' : 'Remove',
      rejectLabel: this.i18n.text('common.cancel'),
      acceptButtonStyleClass: 'p-button-danger',
      accept: async () => {
        this.logoUploading.set(true);
        try {
          await firstValueFrom(this.api.deleteLogo(variant));
          this.setLogoPreview(variant, null);
          this.notifySaved(french ? 'Logo retiré.' : 'Logo removed.');
        } catch {
          this.notifyError();
        } finally {
          this.logoUploading.set(false);
        }
      },
    });
  }
  private notifySaved(summary: string): void {
    this.messages.add({ severity: 'success', summary, life: 4000 });
  }
  private notifyError(): void {
    this.messages.add({ severity: 'error', summary: this.i18n.text('common.error'), life: 6000 });
  }
  private setLogoPreview(variant: 'light' | 'dark', blob: Blob | null): void {
    const preview = variant === 'light' ? this.lightLogoUrl : this.darkLogoUrl;
    const previous = preview();
    if (previous) URL.revokeObjectURL(previous);
    preview.set(blob ? URL.createObjectURL(blob) : '');
  }
  private async loadLogo(variant: 'light' | 'dark'): Promise<void> {
    try {
      this.setLogoPreview(variant, await firstValueFrom(this.api.logo(variant)));
    } catch {
      this.setLogoPreview(variant, null);
    }
  }
  private async load(): Promise<void> {
    const settings = await firstValueFrom(this.api.tenantSettings());
    this.form.setValue({
      name: settings.name,
      shortName: settings.shortName,
      defaultLanguage: settings.defaultLanguage,
      timeZone: settings.timeZone,
      defaultTheme: settings.defaultTheme,
      primaryColor: settings.primaryColor,
      supportContact: settings.supportContact ?? '',
      reportFooterText: settings.reportFooterText ?? '',
    });
    if (settings.hasLogo) void this.loadLogo('light');
    if (settings.hasDarkLogo) void this.loadLogo('dark');
  }
}
