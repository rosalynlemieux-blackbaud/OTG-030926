import { Injectable, computed, signal } from '@angular/core';
import { AppRole, AppUser } from '../models/role.model';

@Injectable({ providedIn: 'root' })
export class AuthStateService {
  private readonly _user = signal<AppUser | null>(null);

  readonly user = computed(() => this._user());
  readonly isAuthenticated = computed(() => !!this._user());

  setUser(user: AppUser | null): void {
    this._user.set(user);
  }

  hasAnyRole(roles: AppRole[]): boolean {
    const user = this._user();
    return !!user && roles.some((role) => user.roles.includes(role));
  }
}
