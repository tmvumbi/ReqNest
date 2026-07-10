import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./features/public/landing-page/landing-page').then((module) => module.LandingPage),
  },
  {
    path: '**',
    redirectTo: '',
  },
];
