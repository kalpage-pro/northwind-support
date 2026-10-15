import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ticketsApi } from '../../../api/tickets';
import { page, ticketListItem } from '../../../test/fixtures';
import { useTicketList } from './useTicketList';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    getTickets: vi.fn(),
  },
}));

const getTickets = vi.mocked(ticketsApi.getTickets);

describe('useTicketList', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    getTickets.mockResolvedValue(page([ticketListItem()]));
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  it('searches with the text the user typed, once the typing settles', async () => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);

    act(() => {
      result.current.updateFilters({ search: 'invoice' });
    });

    // Still inside the debounce window: no second request yet.
    expect(getTickets).toHaveBeenCalledTimes(1);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(2);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: 'invoice', page: 1 }),
    );

    expect(result.current.isLoading).toBe(false);
    expect(result.current.data?.items).toHaveLength(1);
  });
 it("requests immediately when a dropdown filter changes", async () => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });
    getTickets.mockClear();

    act(() => {
      result.current.updateFilters({ status: "InProgress" });
    });
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ status: "InProgress", page: 1 }),
    );
  });

  it("keeps an earlier status when the search text changes, and sends one request per settled search", async () => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    act(() => {
      result.current.updateFilters({ status: "InProgress" });
    });
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });
    getTickets.mockClear();

    for (const text of ["i", "in", "inv"]) {
      act(() => {
        result.current.updateFilters({ search: text });
      });
      await act(async () => {
        await vi.advanceTimersByTimeAsync(100);
      });
    }
    expect(getTickets).not.toHaveBeenCalled();

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: "inv", status: "InProgress" }),
    );
  });

  it("does not send a request for a page reset until the search text settles", async () => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });
    act(() => {
      result.current.updateFilters({ page: 3 });
    });
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });
    getTickets.mockClear();

    act(() => {
      result.current.updateFilters({ search: "x" });
    });
    await act(async () => {
      await vi.advanceTimersByTimeAsync(100);
    });
    expect(getTickets).not.toHaveBeenCalled();

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });
    expect(getTickets).toHaveBeenCalledTimes(1);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: "x", page: 1 }),
    );
  });
});

