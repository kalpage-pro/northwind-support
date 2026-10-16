import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { escalation } from '../../../test/fixtures';
import { EscalationHistory } from './EscalationHistory';

describe('EscalationHistory', () => {
  it('renders newest-first rows and change details', () => {
    render(<EscalationHistory escalations={[escalation({ id: 2, reason: 'Newer' }), escalation({ id: 1, reason: 'Older' })]} isLoading={false} onRetry={vi.fn()} />);
    const rows = screen.getAllByRole('row');
    expect(rows[1]).toHaveTextContent('Newer');
    expect(rows[2]).toHaveTextContent('Older');
    expect(rows[1]).toHaveTextContent('Medium -> High');
  });

  it('renders empty and error states', () => {
    const { rerender } = render(<EscalationHistory escalations={[]} isLoading={false} onRetry={vi.fn()} />);
    expect(screen.getByText('No escalations yet')).toBeInTheDocument();
    rerender(<EscalationHistory isLoading={false} error="History failed" onRetry={vi.fn()} />);
    expect(screen.getByRole('alert')).toHaveTextContent('History failed');
  });
});