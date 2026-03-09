import { Routes } from '@angular/router';
import { AppShellComponent } from './layout/app-shell.component';
import { DashboardPageComponent } from './features/dashboard/dashboard-page.component';
import { PlaceholderPageComponent } from './features/shared/placeholder-page.component';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';
import { APP_ROUTE_REGISTRY } from './core/nav/route-registry';

const childRoutes: Routes = APP_ROUTE_REGISTRY.map((item) => ({
  path: item.path,
  component: item.path === '' ? DashboardPageComponent : PlaceholderPageComponent,
  canActivate: item.roles?.length ? [roleGuard] : undefined,
  data: {
    roles: item.roles,
    title: item.label
  }
}));

export const routes: Routes = [
  { path: 'auth', component: PlaceholderPageComponent, data: { title: 'Auth' } },
  { path: 'forbidden', component: PlaceholderPageComponent, data: { title: 'Forbidden' } },
  {
    path: '',
    component: AppShellComponent,
    canActivate: [authGuard],
    children: childRoutes
  },
  { path: '**', redirectTo: '' }
];
