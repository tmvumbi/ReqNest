import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { I18nService } from '../../../core/i18n/i18n.service';
import { Icon } from '../../../layout/icons/icon';

@Component({
  selector: 'app-product-selector-page',
  imports: [RouterLink, Icon],
  templateUrl: './product-selector-page.html',
  styleUrl: './product-selector-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductSelectorPage {
  readonly i18n = inject(I18nService);

  text(english: string, french: string): string {
    return this.i18n.language() === 'French' ? french : english;
  }
}
