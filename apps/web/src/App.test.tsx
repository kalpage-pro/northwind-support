import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it } from 'vitest';
import { AuthProvider } from './auth/AuthProvider';
import { clearSession, saveSession } from './auth/session';
import { App } from './App';

describe('App shell', () => {
  beforeEach(() => {
    clearSession();
    saveSession({
      token: 'token-123',
      expiresAtUtc: '2099-01-01T00:00:00.000Z',
      user: { id: 7, fullName: 'Alex Turner', email: 'alex@example.com' },
    });
  });

  it('signs out by clearing the session and navigating to login', async () => {
    render(
      <MemoryRouter initialEntries={['/tickets']}>
        <AuthProvider>
          <Routes>
            <Route element={<App />}>
              <Route path="/tickets" element={<p>Tickets</p>} />
              <Route path="/login" element={<p>Login page</p>} />
            </Route>
          </Routes>
        </AuthProvider>
      </MemoryRouter>,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Sign out' }));

    expect(await screen.findByText('Login page')).toBeInTheDocument();
    expect(window.sessionStorage.getItem('support-desk-session')).toBeNull();
  });
});