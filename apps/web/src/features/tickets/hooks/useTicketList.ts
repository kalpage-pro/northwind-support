import { useEffect, useState } from 'react';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { PagedResult, TicketFilters, TicketListItem } from '../../../types/api';

const DEBOUNCE_MS = 300;

export const emptyFilters: TicketFilters = {
  search: '',
  status: undefined,
  priority: undefined,
  categoryId: undefined,
  customerId: undefined,
  assignedAgentId: undefined,
  unassignedOnly: false,
  page: 1,
  pageSize: 20,
  sortBy: 'createdAtUtc',
  sortDirection: 'desc',
};

interface State {
  /** The request this state belongs to; while it differs from the current request we are loading. */
  settledFor?: string;
  data?: PagedResult<TicketListItem>;
  error?: string;
}

/** Owns the ticket list's filters and the data they produce. */
export function useTicketList() {
  const [filters, setFilters] = useState<TicketFilters>(emptyFilters);
  const [debouncedSearch, setDebouncedSearch] = useState(emptyFilters.search);
  const [reloadCount, setReloadCount] = useState(0);
  const [state, setState] = useState<State>({});

  useEffect(() => {
    const handle = window.setTimeout(() => {
      setDebouncedSearch(filters.search);
      // The result set is changing, so go back to page 1 in the same render as the new search.
      setFilters((current) =>
        current.page === 1 ? current : { ...current, page: 1 },
      );
    }, DEBOUNCE_MS);

    return () => window.clearTimeout(handle);
  }, [filters.search]);

  const requestKey = JSON.stringify({
    query: { ...filters, search: debouncedSearch },
    reloadCount,
  });

  useEffect(() => {
    let cancelled = false;
    const { query } = JSON.parse(requestKey) as { query: TicketFilters };

    ticketsApi
      .getTickets(query)
      .then((data) => {
        if (!cancelled) setState({ settledFor: requestKey, data });
      })
      .catch((error: unknown) => {
        if (cancelled) return;
        const message =
          error instanceof ApiError ? error.message : 'Unable to load tickets.';
        setState((current) => ({
          ...current,
          settledFor: requestKey,
          error: message,
        }));
      });

    // A newer request supersedes this one, so a slow old response can never overwrite fresh data.
    return () => {
      cancelled = true;
    };
  }, [requestKey]);

  const isLoading = state.settledFor !== requestKey;

  // A search edit keeps the current page until the debounced search is applied (see above).
  const updateFilters = (patch: Partial<TicketFilters>) =>
    setFilters((current) => ({
      ...current,
      ...patch,
      page: patch.page ?? ("search" in patch ? current.page : 1),
    }));

  const resetFilters = () => setFilters(emptyFilters);

  const reload = () => setReloadCount((count) => count + 1);

  return {
    filters,
    updateFilters,
    resetFilters,
    reload,
    isLoading,
    data: state.data,
    error: isLoading ? undefined : state.error,
  };
}

