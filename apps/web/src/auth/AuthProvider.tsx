import { useEffect, useState, type ReactNode } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { authApi } from '../api/auth';
import { setUnauthorizedHandler } from '../api/client';
import { clearSession, getSession, saveSession, type Session } from './session';
import { AuthContext } from './AuthContext';
import type { AuthUser } from '../types/api';

export function AuthProvider({ children }: { children: ReactNode }) {
  const navigate = useNavigate();
  const location = useLocation();
  const [user, setUser] = useState<AuthUser | undefined>(() => getSession()?.user);

  useEffect(() => {
    const handleUnauthorized = () => {
      setUser(undefined);
      navigate('/login', { replace: true, state: { from: location } });
    };

    setUnauthorizedHandler(handleUnauthorized);
    return () => setUnauthorizedHandler(undefined);
  }, [location, navigate]);

  const login = async (email: string, password: string) => {
    const response = await authApi.login({ email, password });
    const session: Session = {
      token: response.token,
      expiresAtUtc: response.expiresAtUtc,
      user: response.user,
    };
    saveSession(session);
    setUser(response.user);
  };

  const logout = () => {
    clearSession();
    setUser(undefined);
    navigate('/login', { replace: true });
  };

  return (
    <AuthContext.Provider value={{ user, isAuthenticated: user !== undefined, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}