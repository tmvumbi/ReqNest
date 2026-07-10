import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
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
    InputTextModule,
    MessageModule,
    SelectModule,
    TextareaModule,
  ],
  templateUrl: './settings-page.html',
  styleUrl: './settings-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SettingsPage {
  private readonly api = inject(ApiClient);
  private readonly formBuilder = inject(FormBuilder);
  readonly i18n = inject(I18nService);
  readonly saving = signal(false);
  readonly saved = signal(false);
  readonly logoUploading = signal(false);
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
    this.saved.set(false);
    try {
      const value = this.form.getRawValue();
      await firstValueFrom(
        this.api.updateTenantSettings({
          ...value,
          supportContact: value.supportContact || null,
          reportFooterText: value.reportFooterText || null,
        }),
      );
      this.saved.set(true);
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
      this.saved.set(true);
    } finally {
      this.logoUploading.set(false);
      input.value = '';
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
  }
}
