import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { SlaStatus } from '../../../types/api';
import { SlaIndicator } from './SlaIndicator';

describe('SlaIndicator', () => {
  it('renders every SLA state with a unique visible label and symbol', () => {
    const states: Array<[SlaStatus, string, string]> = [
      ['WithinSla', 'Within SLA', '+'],
      ['AtRisk', 'At risk', '!'],
      ['Breached', 'Breached', 'x'],
      ['Met', 'Met', '='],
      ['NotApplicable', 'No SLA', '-'],
    ];

    const { container } = render(
      <div>{states.map(([status]) => <SlaIndicator key={status} status={status} />)}</div>,
    );

    const indicators = Array.from(container.querySelectorAll('.badge')).map(
      (indicator) => indicator.textContent?.trim() ?? '',
    );
    expect(indicators).toHaveLength(5);
    expect(new Set(indicators).size).toBe(5);

    for (const [, label, symbol] of states) {
      expect(screen.getByText(label)).toBeInTheDocument();
      expect(indicators.some((indicator) => indicator.includes(label) && indicator.includes(symbol))).toBe(true);
    }
  });
});