import { provideHttpClient } from '@angular/common/http';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { providePrimeNG } from 'primeng/config';
import Aura from '@primeuix/themes/aura';
import { AuthPage } from './auth-page';
import { PRIMEUI_LICENSE_KEY } from '../../../primeui-license.generated';
import axe from 'axe-core';

describe('AuthPage', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AuthPage],
      providers: [
        provideHttpClient(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { data: { mode: 'register' } } } },
        providePrimeNG({
          theme: { preset: Aura },
          ...(PRIMEUI_LICENSE_KEY ? { license: PRIMEUI_LICENSE_KEY } : {}),
        }),
      ],
    }).compileComponents();
  });

  it('requires company identity, a valid email, and a strong password', () => {
    const fixture = TestBed.createComponent(AuthPage);
    const page = fixture.componentInstance;

    page.form.setValue({
      companyName: '',
      companyShortName: '',
      displayName: '',
      email: 'invalid',
      password: 'short',
      language: 'English',
    });
    fixture.detectChanges();

    expect(page.form.invalid).toBe(true);
    expect(page.form.controls.email.hasError('email')).toBe(true);
    expect(page.form.controls.password.hasError('minlength')).toBe(true);
  });

  it('has no serious or critical automated accessibility violations', async () => {
    const fixture = TestBed.createComponent(AuthPage);
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
