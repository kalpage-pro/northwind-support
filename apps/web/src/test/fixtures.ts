import type {
  PagedResult,
  TicketDetail,
  TicketEscalation,
  TicketListItem,
  TicketTriage,
} from '../types/api';

/** A list row with sensible defaults, so tests only state what they care about. */
export function ticketListItem(overrides: Partial<TicketListItem> = {}): TicketListItem {
  return {
    id: 12,
    reference: 'TCK-0012',
    title: 'Invoice total does not match the order',
    status: 'InProgress',
    priority: 'High',
    customer: { id: 2, name: 'Fabrikam Inc', tier: 'Standard' },
    category: { id: 2, name: 'Billing' },
    assignedAgent: { id: 4, fullName: 'Sara Lindqvist' },
    createdAtUtc: '2026-09-24T08:12:00Z',
    updatedAtUtc: '2026-09-25T10:03:00Z',
    dueAtUtc: '2026-09-24T16:12:00Z',
    resolvedAtUtc: null,
    slaStatus: 'Breached',
    ...overrides,
  };
}

export function page(items: TicketListItem[]): PagedResult<TicketListItem> {
  return {
    items,
    page: 1,
    pageSize: 20,
    totalCount: items.length,
    totalPages: 1,
  };
}

export function ticketTriage(overrides: Partial<TicketTriage> = {}): TicketTriage {
  return {
    priorityReason: 'Premium customer impact',
    slaReason: 'High priority response window',
    assignmentReason: 'Assigned to the billing specialist',
    slaWindowHours: 8,
    ...overrides,
  };
}

export function ticketDetail(overrides: Partial<TicketDetail> = {}): TicketDetail {
  return {
    id: 12,
    reference: 'TCK-0012',
    title: 'Invoice total does not match the order',
    description: 'The invoice total is different from the order total.',
    status: 'InProgress',
    priority: 'High',
    customer: {
      id: 2,
      name: 'Fabrikam Inc',
      tier: 'Standard',
      email: 'billing@fabrikam.example',
      phone: null,
    },
    category: { id: 2, name: 'Billing', requiresSpecialist: true },
    assignedAgent: { id: 4, fullName: 'Sara Lindqvist' },
    createdAtUtc: '2026-09-24T08:12:00Z',
    updatedAtUtc: '2026-09-25T10:03:00Z',
    dueAtUtc: '2026-09-24T16:12:00Z',
    resolvedAtUtc: null,
    slaStatus: 'AtRisk',
    triage: null,
    ...overrides,
  };
}

export function escalation(overrides: Partial<TicketEscalation> = {}): TicketEscalation {
  return {
    id: 1,
    fromPriority: 'Medium',
    toPriority: 'High',
    fromAgent: null,
    toAgent: { id: 4, fullName: 'Sara Lindqvist' },
    fromDueAtUtc: '2026-09-25T16:12:00Z',
    toDueAtUtc: '2026-09-25T12:12:00Z',
    reason: 'The customer has a production deadline.',
    escalatedBy: 'Alex Morgan',
    escalatedAtUtc: '2026-09-25T10:03:00Z',
    ...overrides,
  };
}
