import { Pipe, PipeTransform, inject } from '@angular/core';
import { I18nService } from './i18n.service';

@Pipe({
  name: 'localizedDate',
  standalone: true,
  pure: false,
})
export class LocalizedDatePipe implements PipeTransform {
  private readonly i18n = inject(I18nService);

  transform(
    value: string | Date | null | undefined,
    style: 'short' | 'medium' | 'mediumDate' = 'medium',
  ): string {
    if (!value) return '—';
    const date = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(date.getTime())) return '—';

    const locale = this.i18n.language() === 'French' ? 'fr-FR' : 'en-US';
    const options: Intl.DateTimeFormatOptions =
      style === 'short'
        ? { dateStyle: 'short', timeStyle: 'short' }
        : style === 'mediumDate'
          ? { dateStyle: 'medium' }
          : { dateStyle: 'medium', timeStyle: 'short' };
    return new Intl.DateTimeFormat(locale, options).format(date);
  }
}
