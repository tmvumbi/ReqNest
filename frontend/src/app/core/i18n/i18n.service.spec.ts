import { TestBed } from '@angular/core/testing';
import Aura from '@primeuix/themes/aura';
import { providePrimeNG } from 'primeng/config';
import { I18nService } from './i18n.service';

describe('I18nService', () => {
  it('has a French value for every English interface string and updates the document language', () => {
    TestBed.configureTestingModule({ providers: [providePrimeNG({ theme: { preset: Aura } })] });
    const service = TestBed.inject(I18nService);
    expect(service.catalogsComplete()).toBe(true);

    service.setLanguage('French');
    TestBed.tick();

    expect(document.documentElement.lang).toBe('fr');
    expect(service.text('tickets.new')).toBe('Nouveau ticket');
  });
});
