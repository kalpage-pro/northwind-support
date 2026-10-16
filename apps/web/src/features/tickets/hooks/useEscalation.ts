import { useState } from 'react';
import { ApiError, toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { EscalationResult } from '../../../types/api';

export interface EscalationForm {
  reason: string;
}

export function useEscalation(ticketId: number, onSuccess: (result: EscalationResult) => void) {
  const [isSaving, setIsSaving] = useState(false);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitError, setSubmitError] = useState<string>();
  const [success, setSuccess] = useState<string>();

  const submit = async (form: EscalationForm) => {
    const validationErrors = validate(form);
    setErrors(validationErrors);
    setSubmitError(undefined);
    setSuccess(undefined);
    if (Object.keys(validationErrors).length > 0) return false;

    setIsSaving(true);
    try {
      const result = await ticketsApi.escalateTicket(ticketId, {
        reason: form.reason.trim(),
      });
      onSuccess(result);
      setSuccess('Ticket escalated successfully.');
      setErrors({});
      return true;
    } catch (error: unknown) {
      if (error instanceof ApiError && Object.keys(error.fieldErrors).length > 0) {
        setErrors(fromApiErrors(error.fieldErrors));
      }
      setSubmitError(toErrorMessage(error, 'Could not escalate the ticket.'));
      return false;
    } finally {
      setIsSaving(false);
    }
  };

  return { submit, isSaving, errors, submitError, success };
}

function validate(form: EscalationForm): Record<string, string> {
  const errors: Record<string, string> = {};
  if (form.reason.trim().length < 5 || form.reason.trim().length > 500) {
    errors.reason = 'Reason must be between 5 and 500 characters';
  }
  return errors;
}

function fromApiErrors(fieldErrors: Record<string, string[]>): Record<string, string> {
  return Object.fromEntries(
    Object.entries(fieldErrors).map(([field, messages]) => [
      field.charAt(0).toLowerCase() + field.slice(1),
      messages[0],
    ]),
  );
}