import { useState } from 'react';
import { useAuth } from '../../../auth/useAuth';
import { Field } from '../../../components/Field';
import type { EscalationResult, TicketDetail } from '../../../types/api';
import { useEscalation, type EscalationForm } from '../hooks/useEscalation';

interface EscalationPanelProps {
  ticket: TicketDetail;
  onSuccess: (result: EscalationResult) => void;
}

export function EscalationPanel({ ticket, onSuccess }: EscalationPanelProps) {
  const { user } = useAuth();
  const [form, setForm] = useState<EscalationForm>({ reason: '' });
  const { submit, isSaving, errors, submitError, success } = useEscalation(ticket.id, (result) => {
    setForm({ reason: '' });
    onSuccess(result);
  });

  const blocked = ticket.priority === 'Critical' || ticket.status === 'Resolved' || ticket.status === 'Closed';
  const blockedMessage = ticket.priority === 'Critical'
    ? 'This ticket is already Critical and cannot be escalated.'
    : `This ticket is ${ticket.status} and cannot be escalated.`;

  if (blocked) {
    return <div className="card"><h2>Escalate ticket</h2><p role="status">{blockedMessage}</p></div>;
  }

  return (
    <div className="card">
      <h2>Escalate ticket</h2>
      <p>Escalating as {user?.fullName}</p>
      <form className="form" onSubmit={(event) => { event.preventDefault(); void submit(form); }} noValidate>
        <Field id="escalation-reason" label="Reason" error={errors.reason} hint={`${form.reason.length}/500 characters`}>
          {(fieldProps) => (
            <textarea
              {...fieldProps}
              rows={4}
              maxLength={500}
              value={form.reason}
              onChange={(event) => setForm((current) => ({ ...current, reason: event.target.value }))}
            />
          )}
        </Field>
        {submitError && <p className="field__error" role="alert">{submitError}</p>}
        {success && <p role="status">{success}</p>}
        <button type="submit" className="button button--primary" disabled={isSaving}>
          {isSaving ? 'Escalating ticket...' : 'Escalate ticket'}
        </button>
      </form>
    </div>
  );
}