import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { SessionStore } from '../session/session-store';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const store = inject(SessionStore);
  const session = store.session();
  const tenantId = store.activeTenantId();
  let authenticatedRequest = request;
  if (request.url.startsWith('/api') && session) {
    authenticatedRequest = request.clone({
      setHeaders: {
        Authorization: `Bearer ${session.accessToken}`,
        ...(tenantId ? { 'X-Tenant-Id': tenantId } : {}),
      },
    });
  }

  return next(authenticatedRequest).pipe(
    catchError((error: { status?: number }) => {
      if (error.status === 401 && !request.url.endsWith('/auth/login')) store.clear();
      return throwError(() => error);
    }),
  );
};
