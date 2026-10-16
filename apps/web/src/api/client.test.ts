import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { request, setUnauthorizedHandler } from './client';
import { clearSession, saveSession, type Session } from '../auth/session';

const session: Session = {
  token: 'token-123',
  expiresAtUtc: '2099-01-01T00:00:00.000Z',
  user: { id: 7, fullName: 'Alex Turner', email: 'alex@example.com' },
};

const response = (status: number, body: object = {}): Response =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });

describe('API client authentication', () => {
  const fetchMock = vi.fn<(input: RequestInfo | URL, init?: RequestInit) => Promise<Response>>();

  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock);
    fetchMock.mockResolvedValue(response(200, { ok: true }));
    clearSession();
    setUnauthorizedHandler(undefined);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('attaches the bearer token when a session exists', async () => {
    saveSession(session);

    await request<{ ok: boolean }>('/tickets');

    const headers = new Headers(fetchMock.mock.calls[0]?.[1]?.headers);
    expect(headers.get('Authorization')).toBe('Bearer token-123');
  });

  it('does not attach a bearer token without a session', async () => {
    await request<{ ok: boolean }>('/tickets');

    const headers = new Headers(fetchMock.mock.calls[0]?.[1]?.headers);
    expect(headers.has('Authorization')).toBe(false);
  });

  it('clears the session and calls the handler for a protected 401', async () => {
    saveSession(session);
    fetchMock.mockResolvedValue(response(401, { detail: 'Unauthorized' }));
    const handler = vi.fn();
    setUnauthorizedHandler(handler);

    await expect(request('/tickets')).rejects.toMatchObject({ status: 401 });

    expect(getStoredSession()).toBeNull();
    expect(handler).toHaveBeenCalledOnce();
  });

  it('does not call the handler for a login 401', async () => {
    fetchMock.mockResolvedValue(response(401, { detail: 'Invalid email or password.' }));
    const handler = vi.fn();
    setUnauthorizedHandler(handler);

    await expect(request('/auth/login', { method: 'POST' })).rejects.toMatchObject({ status: 401 });

    expect(handler).not.toHaveBeenCalled();
  });
});

function getStoredSession(): string | null {
  return window.sessionStorage.getItem('support-desk-session');
}
