import { NavItem } from './nav.model';
import { APP_ROUTE_REGISTRY } from './route-registry';

const defaultRoles = ['participant', 'judge', 'admin'] as const;

export const NAV_ITEMS: NavItem[] = APP_ROUTE_REGISTRY
  .filter((route) => !route.path.includes('/:'))
  .map((route) => ({
    label: route.label,
    route: route.path === '' ? '/' : `/${route.path}`,
    roles: [...(route.roles ?? defaultRoles)]
  }));
