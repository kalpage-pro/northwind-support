import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { authApi } from '../api/auth';
import { AuthProvider } from '../auth/AuthProvider';
import { ProtectedRoute } from '../auth/ProtectedRoute';
import { ApiError } from '../api/client';
import { clearSession } from '../auth/session';
import { LoginPage } from './LoginPage';

const user = { id: 7, fullName: 'Alex Turner', email: 'alex@example.com' };
const loginResponse = {
  token: 'token-123',
  expiresAtUtc: '2099-01-01T00:00:00.000Z',
  user,
};

function LocationText() {
  const location = useLocation();
  return <p data-testid="location">{location.pathname}</p>;
}

describe('LoginPage', () => {
  beforeEach(() => {
    clearSession();
    vi.spyOn(authApi, 'login').mockResolvedValue(loginResponse);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('redirects an unauthenticated visitor from a protected route to login', async () => {
    render(
      <MemoryRouter initialEntries={['/tickets']}>
        <AuthProvider>
          <Routes>
            <Route element={<ProtectedRoute />}>
              <Route path="/tickets" element={<p>Tickets</p>} />
            </Route>
            <Route path="/login" element={<LocationText />} />
          </Routes>
        </AuthProvider>
      </MemoryRouter>,
    );

    expect(await screen.findByTestId('location')).toHaveTextContent('/login');
  });

  it('returns the user to the page they originally wanted after login', async () => {
    render(
      <MemoryRouter
        initialEntries={[{ pathname: '/login', state: { from: { pathname: '/tickets/42', search: '', hash: '' } } }]}
      >
        <AuthProvider>
          <Routes>
            <Route path="/login" element={<LoginPage />} />
            <Route path="/tickets/42" element={<LocationText />} />
          </Routes>
        </AuthProvider>
      </MemoryRouter>,
    );

    fireEvent.change(screen.getByLabelText('Email'), { target: { value: user.email } });
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'password' } });
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByTestId('location')).toHaveTextContent('/tickets/42');
  });

  it('shows validation errors without calling the API', () => {
    renderLogin();

    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'invalid' } });
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(screen.getByText('Enter a valid email address.')).toBeInTheDocument();
    expect(screen.getByText('Enter your password.')).toBeInTheDocument();
    expect(authApi.login).not.toHaveBeenCalled();
  });

  it('shows the server rejection and stores no session', async () => {
    vi.mocked(authApi.login).mockRejectedValueOnce(
      new ApiError(401, 'Invalid email or password.', { detail: 'Invalid email or password.' }),
    );
    renderLogin();

    fireEvent.change(screen.getByLabelText('Email'), { target: { value: user.email } });
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'wrong' } });
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid email or password.');
    expect(window.sessionStorage.getItem('support-desk-session')).toBeNull();
  });
});

function renderLogin() {
  return render(
    <MemoryRouter initialEntries={['/login']}>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  );
}
