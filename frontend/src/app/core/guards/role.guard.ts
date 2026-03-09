import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthStateService } from '../auth/auth-state.service';
import { AppRole } from '../models/role.model';

export const roleGuard: CanActivateFn = (route) => {
  const requiredRoles = (route.data?.['roles'] ?? []) as AppRole[];
  const authState = inject(AuthStateService);
  const router = inject(Router);

  if (!requiredRoles.length || authState.hasAnyRole(requiredRoles)) {
    return true;
  }

  return router.createUrlTree(['/forbidden']);
};
