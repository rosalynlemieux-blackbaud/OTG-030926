export type AppRole = 'participant' | 'judge' | 'admin';

export interface AppUser {
  id: string;
  email: string;
  roles: AppRole[];
  banned?: boolean;
}
