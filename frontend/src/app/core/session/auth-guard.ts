import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionStore } from './session-store';

export const authGuard: CanActivateFn = () => {
  const store = inject(SessionStore);
  return store.authenticated() ? true : inject(Router).createUrlTree(['/login']);
};

export const ticketMaintainerGuard: CanActivateFn = () => {
  const store = inject(SessionStore);
  return store.canMaintainTickets() ? true : inject(Router).createUrlTree(['/app/tickets']);
};

export const projectManagerGuard: CanActivateFn = () => {
  const store = inject(SessionStore);
  return store.canManageProjects() ? true : inject(Router).createUrlTree(['/app/dashboard']);
};

export const tenantAdministratorGuard: CanActivateFn = () => {
  const store = inject(SessionStore);
  return store.isAdministrator() ? true : inject(Router).createUrlTree(['/app/dashboard']);
};
