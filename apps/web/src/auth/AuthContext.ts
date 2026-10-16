import { createContext } from 'react';
import type { AuthUser } from '../types/api';

export interface AuthContextValue {
  user: AuthUser | undefined;
  isAuthenticated: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined);
