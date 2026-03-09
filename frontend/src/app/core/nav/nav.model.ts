import { AppRole } from '../models/role.model';

export interface NavItem {
  label: string;
  route: string;
  roles: AppRole[];
}
