import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import Aura from '@primeuix/themes/aura';
import { providePrimeNG } from 'primeng/config';

import { routes } from './app.routes';
import { PRIMEUI_LICENSE_KEY } from './primeui-license.generated';
import { authInterceptor } from './core/api/auth-interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    providePrimeNG({
      theme: {
        preset: Aura,
        options: {
          darkModeSelector: '.reqnest-dark',
        },
      },
      ...(PRIMEUI_LICENSE_KEY ? { license: PRIMEUI_LICENSE_KEY } : {}),
    }),
  ],
};
