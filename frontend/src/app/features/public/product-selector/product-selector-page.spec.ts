import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { providePrimeNG } from 'primeng/config';
import Aura from '@primeuix/themes/aura';
import axe from 'axe-core';
import { PRIMEUI_LICENSE_KEY } from '../../../primeui-license.generated';
import { I18nService } from '../../../core/i18n/i18n.service';
import { ProductSelectorPage } from './product-selector-page';

describe('ProductSelectorPage', () => {
  beforeEach(async () => {
    Object.defineProperty(globalThis, 'localStorage', {
      configurable: true,
      value: {
        getItem: () => null,
        setItem: () => undefined,
      },
    });

    await TestBed.configureTestingModule({
      imports: [ProductSelectorPage],
      providers: [
        provideRouter([]),
        providePrimeNG({
          theme: { preset: Aura },
          ...(PRIMEUI_LICENSE_KEY ? { license: PRIMEUI_LICENSE_KEY } : {}),
        }),
      ],
    }).compileComponents();
  });

  it('offers the Support and Projects destinations', () => {
    const fixture = TestBed.createComponent(ProductSelectorPage);
    fixture.detectChanges();

    const page = fixture.nativeElement as HTMLElement;
    const links = Array.from(page.querySelectorAll<HTMLAnchorElement>('.product-card'));

    expect(links).toHaveLength(2);
    expect(links[0]?.getAttribute('href')).toBe('/support');
    expect(links[0]?.textContent).toContain('ReNest Support');
    expect(links[1]?.href).toBe('https://projects.renest-project.online/');
    expect(links[1]?.textContent).toContain('ReNest Projects');
  });

  it('switches the selector copy to French', () => {
    const fixture = TestBed.createComponent(ProductSelectorPage);
    const i18n = TestBed.inject(I18nService);
    i18n.setLanguage('French');
    fixture.detectChanges();

    const page = fixture.nativeElement as HTMLElement;
    expect(page.querySelector('h1')?.textContent).toContain('Où souhaitez-vous aller');
    expect(page.textContent).toContain('Ouvrir Support');
    expect(page.textContent).toContain('Ouvrir Projects');
  });

  it('has no serious or critical automated accessibility violations', async () => {
    const fixture = TestBed.createComponent(ProductSelectorPage);
    fixture.detectChanges();

    const result = await axe.run(fixture.nativeElement as HTMLElement, {
      rules: { 'color-contrast': { enabled: false } },
    });

    expect(
      result.violations.filter(
        (violation) => violation.impact === 'critical' || violation.impact === 'serious',
      ),
    ).toEqual([]);
  });
});
