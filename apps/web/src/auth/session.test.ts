import { clearSession, getSession, saveSession, type Session } from './session';
import { beforeEach, describe, expect, it } from 'vitest';

const session: Session = {
  token: 'token-123',
  expiresAtUtc: '2099-01-01T00:00:00.000Z',
  user: { id: 7, fullName: 'Alex Turner', email: 'alex@example.com' },
};

describe('session storage', () => {
  beforeEach(() => clearSession());

  it('treats an expired token as no session and clears it', () => {
    saveSession({ ...session, expiresAtUtc: '2020-01-01T00:00:00.000Z' });

    expect(getSession()).toBeUndefined();
    expect(window.sessionStorage.getItem('support-desk-session')).toBeNull();
  });

  it('round-trips a valid session', () => {
    saveSession(session);

    expect(getSession()).toEqual(session);
  });
});
