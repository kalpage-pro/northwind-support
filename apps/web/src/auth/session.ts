import type { AuthUser } from '../types/api';

const storageKey = 'support-desk-session';

export interface Session {
  token: string;
  expiresAtUtc: string;
  user: AuthUser;
}

// sessionStorage closes with the tab, but injected scripts can still read it. An HttpOnly
// cookie would reduce that XSS exposure but would require CSRF protection.
export function getSession(): Session | undefined {
  try {
    const raw = window.sessionStorage.getItem(storageKey);
    if (!raw) {
      return undefined;
    }

    const session = JSON.parse(raw) as Session;
    if (!isSession(session) || Date.parse(session.expiresAtUtc) <= Date.now()) {
      clearSession();
      return undefined;
    }

    return session;
  } catch {
    return undefined;
  }
}

export function saveSession(session: Session): void {
  try {
    window.sessionStorage.setItem(storageKey, JSON.stringify(session));
  } catch {
    // Storage may be disabled or unavailable; the caller still keeps its in-memory state.
  }
}

export function clearSession(): void {
  try {
    window.sessionStorage.removeItem(storageKey);
  } catch {
    // Storage may be disabled or unavailable.
  }
}

function isSession(value: Session): value is Session {
  return Boolean(
    value &&
      typeof value.token === 'string' &&
      typeof value.expiresAtUtc === 'string' &&
      value.user &&
      typeof value.user.id === 'number' &&
      typeof value.user.fullName === 'string' &&
      typeof value.user.email === 'string',
  );
}