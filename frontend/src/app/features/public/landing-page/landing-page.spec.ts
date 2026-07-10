import { TestBed } from '@angular/core/testing';
import { providePrimeNG } from 'primeng/config';
import Aura from '@primeuix/themes/aura';
import { LandingPage } from './landing-page';
import { PRIMEUI_LICENSE_KEY } from '../../../primeui-license.generated';
import { provideRouter } from '@angular/router';
import axe from 'axe-core';

describe('LandingPage', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LandingPage],
      providers: [
        provideRouter([]),
        providePrimeNG({
          theme: { preset: Aura },
          ...(PRIMEUI_LICENSE_KEY ? { license: PRIMEUI_LICENSE_KEY } : {}),
        }),
      ],
    }).compileComponents();
  });

  it('introduces ReqNest and its technical foundation', () => {
    const fixture = TestBed.createComponent(LandingPage);
    fixture.detectChanges();

    const page = fixture.nativeElement as HTMLElement;
    expect(page.querySelector('h1')?.textContent).toContain('Help desk, untangled.');
    expect(page.querySelectorAll('p-card')).toHaveLength(3);
    expect(page.querySelectorAll('[pbutton]')).toHaveLength(3);
  });

  it('has no serious or critical automated accessibility violations', async () => {
    const fixture = TestBed.createComponent(LandingPage);
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
