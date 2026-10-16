import { Badge, type BadgeTone } from '../../../components/Badge';
import type { SlaStatus } from '../../../types/api';

const states: Record<SlaStatus, { label: string; symbol: string; tone: BadgeTone }> = {
  WithinSla: { label: 'Within SLA', symbol: '+', tone: 'info' },
  AtRisk: { label: 'At risk', symbol: '!', tone: 'warning' },
  Breached: { label: 'Breached', symbol: 'x', tone: 'danger' },
  Met: { label: 'Met', symbol: '=', tone: 'success' },
  NotApplicable: { label: 'No SLA', symbol: '-', tone: 'neutral' },
};

export function SlaIndicator({ status }: { status: SlaStatus }) {
  const state = states[status];

  return (
    <Badge tone={state.tone}>
      <span aria-hidden="true">{state.symbol}</span> {state.label}
    </Badge>
  );
}