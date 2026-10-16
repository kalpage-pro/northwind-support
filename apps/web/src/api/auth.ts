import { request } from './client';
import type { AuthUser, LoginRequest, LoginResponse } from '../types/api';

export const authApi = {
  login: (payload: LoginRequest): Promise<LoginResponse> =>
    request<LoginResponse>('/auth/login', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  getMe: (): Promise<AuthUser> => request<AuthUser>('/auth/me'),
};