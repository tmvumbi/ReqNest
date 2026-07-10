import { TestBed } from '@angular/core/testing';
import { providePrimeNG } from 'primeng/config';
import Aura from '@primeuix/themes/aura';
import { LandingPage } from './landing-page';
import { PRIMEUI_LICENSE_KEY } from '../../../primeui-license.generated';

describe('LandingPage', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LandingPage],
      providers: [
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
    expect(page.querySelector('h1')?.textContent).toContain('Requirements, organized.');
    expect(page.querySelectorAll('p-card')).toHaveLength(3);
    expect(page.querySelector('[pbutton]')?.textContent).toContain('Explore the foundation');
  });
});
