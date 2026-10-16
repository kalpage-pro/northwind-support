import { useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/useAuth';
import { ApiError } from '../api/client';
import { Field } from '../components/Field';

interface LoginLocationState {
  from?: { pathname: string; search: string; hash: string };
}

interface FormErrors {
  email?: string;
  password?: string;
}

export function LoginPage() {
  const { isAuthenticated, login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<FormErrors>({});
  const [submitError, setSubmitError] = useState<string>();
  const [isSigningIn, setIsSigningIn] = useState(false);
  const [isRedirectingAfterLogin, setIsRedirectingAfterLogin] = useState(false);

  if (isAuthenticated && !isRedirectingAfterLogin) {
    return <Navigate to="/" replace />;
  }

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const validationErrors = validate(email, password);
    setErrors(validationErrors);
    setSubmitError(undefined);

    if (Object.keys(validationErrors).length > 0) {
      return;
    }

    setIsSigningIn(true);
    try {
      await login(email.trim(), password);
      setIsRedirectingAfterLogin(true);
      const state = location.state as LoginLocationState | null;
      const from = state?.from;
      navigate(from ? `${from.pathname}${from.search}${from.hash}` : '/tickets', { replace: true });
    } catch (error) {
      setSubmitError(error instanceof ApiError ? error.message : 'Unable to sign in.');
    } finally {
      setIsSigningIn(false);
    }
  };

  return (
    <section>
      <header className="page-header">
        <div>
          <h1>Sign in</h1>
          <p className="page-header__subtitle">Use your support agent account.</p>
        </div>
      </header>

      <form className="card form" onSubmit={submit} noValidate>
        <Field id="login-email" label="Email" error={errors.email}>
          {(fieldProps) => (
            <input
              {...fieldProps}
              type="email"
              autoComplete="username"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
            />
          )}
        </Field>

        <Field id="login-password" label="Password" error={errors.password}>
          {(fieldProps) => (
            <input
              {...fieldProps}
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
            />
          )}
        </Field>

        {submitError && <p className="field__error" role="alert">{submitError}</p>}

        <button type="submit" className="button button--primary" disabled={isSigningIn}>
          {isSigningIn ? 'Signing in...' : 'Sign in'}
        </button>
      </form>
    </section>
  );
}

function validate(email: string, password: string): FormErrors {
  const errors: FormErrors = {};
  if (!email.trim()) {
    errors.email = 'Enter your email address.';
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) {
    errors.email = 'Enter a valid email address.';
  }
  if (!password) {
    errors.password = 'Enter your password.';
  }
  return errors;
}