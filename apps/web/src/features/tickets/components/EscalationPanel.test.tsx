import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { ticketDetail } from '../../../test/fixtures';
import type { EscalationResult } from '../../../types/api';
import { EscalationPanel } from './EscalationPanel';
import { TicketSummary } from './TicketSummary';

vi.mock('../../../api/tickets', () => ({ ticketsApi: { escalateTicket: vi.fn() } }));

const escalateTicket = vi.mocked(ticketsApi.escalateTicket);
const result: EscalationResult = {
  ticket: ticketDetail({ priority: 'Critical' }),
  escalation: {
    id: 1,
    fromPriority: 'High',
    toPriority: 'Critical',
    fromAgent: null,
    toAgent: null,
    fromDueAtUtc: null,
    toDueAtUtc: null,
    reason: 'A production incident requires attention.',
    escalatedBy: 'Alex',
    escalatedAtUtc: '2026-09-25T10:03:00Z',
  },
};

function renderPanel(overrides: Parameters<typeof ticketDetail>[0] = {}, onSuccess = vi.fn()) {
  return { onSuccess, ...render(<EscalationPanel ticket={ticketDetail(overrides)} onSuccess={onSuccess} />) };
}

describe('EscalationPanel', () => {
  it('validates a short reason and does not call the API', async () => {
    const user = userEvent.setup();
    renderPanel();
    await user.type(screen.getByLabelText('Reason'), 'bad');
    await user.type(screen.getByLabelText('Escalated by'), 'Alex');
    await user.click(screen.getByRole('button', { name: 'Escalate ticket' }));
    expect(screen.getByText('Reason must be between 5 and 500 characters')).toBeInTheDocument();
    expect(escalateTicket).not.toHaveBeenCalled();
  });

  it('blocks an overlong reason and a missing escalated-by value', async () => {
    const user = userEvent.setup();
    renderPanel();
    fireEvent.change(screen.getByLabelText('Reason'), { target: { value: 'x'.repeat(501) } });
    await user.click(screen.getByRole('button', { name: 'Escalate ticket' }));
    expect(screen.getByText('Reason must be between 5 and 500 characters')).toBeInTheDocument();
    expect(screen.getByText('EscalatedBy is required')).toBeInTheDocument();
    expect(escalateTicket).not.toHaveBeenCalled();
  });

  it('submits trimmed values and keeps the button pending', async () => {
    const user = userEvent.setup();
    let resolve: (value: EscalationResult) => void = () => undefined;
    escalateTicket.mockReturnValueOnce(new Promise((done) => { resolve = done; }));
    const { onSuccess } = renderPanel();
    fireEvent.change(screen.getByLabelText('Reason'), { target: { value: '  A valid reason  ' } });
    fireEvent.change(screen.getByLabelText('Escalated by'), { target: { value: '  Alex  ' } });
    await user.click(screen.getByRole('button', { name: 'Escalate ticket' }));
    expect(screen.getByRole('button', { name: 'Escalating ticket...' })).toBeDisabled();
    expect(escalateTicket).toHaveBeenCalledWith(12, { reason: 'A valid reason', escalatedBy: 'Alex' });
    resolve(result);
    expect(await screen.findByText('Ticket escalated successfully.')).toBeInTheDocument();
    expect(onSuccess).toHaveBeenCalledWith(result);
  });

  it('shows a server conflict without changing the ticket or form', async () => {
    const user = userEvent.setup();
    const onSuccess = vi.fn();
    escalateTicket.mockRejectedValueOnce(new ApiError(409, 'Ticket is already Critical'));
    const ticket = ticketDetail();
    render(
      <>
        <TicketSummary ticket={ticket} />
        <EscalationPanel ticket={ticket} onSuccess={onSuccess} />
      </>,
    );
    fireEvent.change(screen.getByLabelText('Reason'), { target: { value: 'A valid reason' } });
    fireEvent.change(screen.getByLabelText('Escalated by'), { target: { value: 'Alex' } });
    await user.click(screen.getByRole('button', { name: 'Escalate ticket' }));
    expect(await screen.findByText('Ticket is already Critical')).toBeInTheDocument();
    expect(screen.getByLabelText('Reason')).toHaveValue('A valid reason');
    expect(screen.getByText('High')).toBeInTheDocument();
    expect(screen.getByText('At risk')).toBeInTheDocument();
    expect(onSuccess).not.toHaveBeenCalled();
  });

  it.each([
    ['Critical', { priority: 'Critical' as const }],
    ['Resolved', { status: 'Resolved' as const }],
  ])('explains why a %s ticket cannot be escalated', (_label, overrides) => {
    renderPanel(overrides);
    expect(screen.getByRole('status')).toHaveTextContent('cannot be escalated');
    expect(screen.queryByRole('button', { name: 'Escalate ticket' })).not.toBeInTheDocument();
  });
});