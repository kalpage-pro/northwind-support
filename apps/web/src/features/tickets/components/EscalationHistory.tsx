import { ErrorMessage } from '../../../components/ErrorMessage';
import { EmptyState } from '../../../components/EmptyState';
import { Spinner } from '../../../components/Spinner';
import { formatDateTime } from '../../../lib/format';
import type { TicketEscalation } from '../../../types/api';

interface EscalationHistoryProps {
  escalations?: TicketEscalation[];
  isLoading: boolean;
  error?: string;
  onRetry: () => void;
}

export function EscalationHistory({ escalations, isLoading, error, onRetry }: EscalationHistoryProps) {
  return (
    <div className="card">
      <h2>Escalation history</h2>
      {isLoading && !escalations && <Spinner label="Loading escalation history" />}
      {error && !escalations && <ErrorMessage message={error} onRetry={onRetry} />}
      {escalations && escalations.length === 0 && <EmptyState title="No escalations yet" />}
      {escalations && escalations.length > 0 && (
        <div className="table-wrapper">
          <table className="table">
            <caption className="visually-hidden">Ticket escalation history</caption>
            <thead><tr><th scope="col">Priority</th><th scope="col">Agent</th><th scope="col">Due date</th><th scope="col">Reason</th><th scope="col">Who</th><th scope="col">When</th></tr></thead>
            <tbody>{escalations.map((escalation) => <HistoryRow key={escalation.id} escalation={escalation} />)}</tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function HistoryRow({ escalation }: { escalation: TicketEscalation }) {
  const agentChange = escalation.fromAgent?.fullName === escalation.toAgent?.fullName
    ? 'kept'
    : `${escalation.fromAgent?.fullName ?? 'unassigned'} -> ${escalation.toAgent?.fullName ?? 'unassigned'}`;
  const dueChange = escalation.fromDueAtUtc === escalation.toDueAtUtc
    ? 'kept'
    : `${formatDateTime(escalation.fromDueAtUtc)} -> ${formatDateTime(escalation.toDueAtUtc)}`;

  return <tr><td>{escalation.fromPriority} {'->'} {escalation.toPriority}</td><td>{agentChange}</td><td>{dueChange}</td><td>{escalation.reason}</td><td>{escalation.escalatedBy}</td><td>{formatDateTime(escalation.escalatedAtUtc)}</td></tr>;
}