import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { definePreset } from '@primeuix/themes';
import Aura from '@primeuix/themes/aura';
import { MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';

// Brand accent (#D0471B) layered over the Aura preset; accent surfaces always
// carry white text in both color schemes.
const ReqNestPreset = definePreset(Aura, {
  semantic: {
    primary: {
      50: '#fdf3ee',
      100: '#fae0d3',
      200: '#f5c0a8',
      300: '#ec9878',
      400: '#e0704a',
      500: '#d0471b',
      600: '#b53d16',
      700: '#933112',
      800: '#71250e',
      900: '#551c0b',
      950: '#361106',
    },
    colorScheme: {
      light: {
        primary: {
          color: '#d0471b',
          contrastColor: '#ffffff',
          hoverColor: '{primary.600}',
          activeColor: '{primary.700}',
        },
      },
      dark: {
        primary: {
          color: '#d0471b',
          contrastColor: '#ffffff',
          hoverColor: '{primary.400}',
          activeColor: '{primary.600}',
        },
      },
    },
  },
});

import { routes } from './app.routes';
import { PRIMEUI_LICENSE_KEY } from './primeui-license.generated';
import { authInterceptor } from './core/api/auth-interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    MessageService,
    providePrimeNG({
      theme: {
        preset: ReqNestPreset,
        options: {
          darkModeSelector: '.reqnest-dark',
        },
      },
      ...(PRIMEUI_LICENSE_KEY ? { license: PRIMEUI_LICENSE_KEY } : {}),
    }),
  ],
};
