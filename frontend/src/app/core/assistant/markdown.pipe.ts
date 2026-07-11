import { Pipe, PipeTransform } from '@angular/core';
import { marked } from 'marked';

// Renders assistant markdown to HTML. The output is bound with [innerHTML],
// so Angular's built-in sanitizer still strips anything unsafe.
@Pipe({ name: 'markdown' })
export class MarkdownPipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    if (!value) return '';
    return marked.parse(value, { async: false, gfm: true, breaks: true });
  }
}
