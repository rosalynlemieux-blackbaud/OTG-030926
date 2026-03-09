import { AppRole } from '../models/role.model';

export interface AppRouteDefinition {
  path: string;
  label: string;
  roles?: AppRole[];
}

export const APP_ROUTE_REGISTRY: AppRouteDefinition[] = [
  { path: '', label: 'Dashboard' },
  { path: 'ideas', label: 'Ideas' },
  { path: 'ideas/new', label: 'New Idea' },
  { path: 'ideas/:id', label: 'Idea Detail' },
  { path: 'teams', label: 'Teams' },
  { path: 'people', label: 'People' },
  { path: 'winners', label: 'Winners' },
  { path: 'faq', label: 'FAQ & Rules' },
  { path: 'swag', label: 'Swag' },
  { path: 'profile', label: 'Profile' },
  { path: 'analytics', label: 'Analytics', roles: ['admin'] },
  { path: 'admin', label: 'Admin Settings', roles: ['admin'] }
];
