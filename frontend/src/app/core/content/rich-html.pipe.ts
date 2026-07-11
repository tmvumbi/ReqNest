import { inject, Pipe, PipeTransform } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

// Rich content is sanitized server-side with a strict allow-list; bypassing the
// built-in sanitizer here keeps the inline styles (e.g. text color) it allows.
@Pipe({ name: 'richHtml' })
export class RichHtmlPipe implements PipeTransform {
  private readonly sanitizer = inject(DomSanitizer);

  transform(value: string | null | undefined): SafeHtml {
    return this.sanitizer.bypassSecurityTrustHtml(value ?? '');
  }
}
