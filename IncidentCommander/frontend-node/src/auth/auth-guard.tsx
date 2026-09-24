import { useRef, useState, type ReactNode } from 'react';
import { useAuth, useAuthGuard, type AuthConfig, type LoginMethod } from '@ikonai/sdk-react-ui';
import { useI18n } from '../i18n/i18n';
import './auth.css';
import { EmailLoginForm } from './email-login-form';
import { LoginButton, RegisterPasskeyButton } from './login-button';

type ErrorScope = 'primary' | 'passkey' | 'email' | 'guest';

export interface AuthGuardProps {
  children: ReactNode;
  config: AuthConfig;
}

export function formatAuthError(error: string): string {
  const trimmed = error.trim();

  try {
    const parsed = JSON.parse(trimmed) as { message?: unknown };

    if (typeof parsed.message === 'string' && parsed.message.length > 0) {
      return parsed.message;
    }
  } catch {
    /* not JSON */
  }

  return trimmed;
}

export function AuthGuard({ children, config }: AuthGuardProps) {
  const {
    isCheckingAuth,
    shouldRenderChildren,
    isLoginPrompt,
    dismissLoginPrompt,
    loginPromptReason,
  } = useAuthGuard({
    config,
    guestUrlParam: 'guest',
  });
  const [errorScope, setErrorScope] = useState<ErrorScope | null>(null);
  const initialCheckDoneRef = useRef(false);

  if (!isCheckingAuth) {
    initialCheckDoneRef.current = true;
  }

  if (isCheckingAuth && !initialCheckDoneRef.current) {
    return null;
  }

  if (!shouldRenderChildren) {
    return (
      <AuthScreen
        config={config}
        errorScope={errorScope}
        setErrorScope={setErrorScope}
        isLoginPrompt={isLoginPrompt}
        onDismiss={dismissLoginPrompt}
        loginPromptReason={loginPromptReason}
      />
    );
  }

  return <>{children}</>;
}

interface AuthScreenProps {
  config: AuthConfig;
  errorScope: ErrorScope | null;
  setErrorScope: (scope: ErrorScope) => void;
  isLoginPrompt: boolean;
  onDismiss: () => void;
  loginPromptReason: string | null;
}

function AuthScreen({
  config,
  errorScope,
  setErrorScope,
  isLoginPrompt,
  onDismiss,
  loginPromptReason,
}: AuthScreenProps) {
  const { t } = useI18n();
  const { state } = useAuth();

  const primaryMethods = config.methods.filter(
    (m): m is Exclude<LoginMethod, 'email' | 'guest' | 'global' | 'passkey'> =>
      m !== 'email' && m !== 'guest' && m !== 'global' && m !== 'passkey',
  );
  const hasPasskey = config.methods.includes('passkey');
  const hasEmail = config.methods.includes('email');
  const guestProvider = config.methods.includes('global')
    ? ('global' as const)
    : config.methods.includes('guest')
      ? ('guest' as const)
      : null;
  const hasGuest = guestProvider !== null;

  const errorFor = (scope: ErrorScope) =>
    state.error && (errorScope === scope || (errorScope === null && scope === 'primary')) ? (
      <div className="ikon-auth-error">{formatAuthError(state.error)}</div>
    ) : null;

  return (
    <main className="ikon-surface ikon-auth-screen">
      <section className="ikon-auth-container">
        <h1 className="ikon-auth-title">{t('auth.welcome.title')}</h1>
        {/* Whoever raised the prompt said why; that beats the generic welcome, which tells someone
            sent here by another application nothing about what they are about to agree to. */}
        <p className="ikon-auth-subtitle">{loginPromptReason ?? t('auth.welcome.subtitle')}</p>

        {errorFor('primary')}

        <div className="ikon-auth-buttons">
          {primaryMethods.map((method) => (
            <LoginButton
              key={method}
              provider={method}
              disabled={state.isLoading}
              onAttempt={() => {
                setErrorScope('primary');
              }}
            />
          ))}
        </div>

        {hasPasskey && primaryMethods.length > 0 && (
          <div className="ikon-auth-divider">
            <span>{t('auth.divider')}</span>
          </div>
        )}

        {hasPasskey && (
          <>
            {errorFor('passkey')}
            <div className="ikon-auth-buttons">
              <LoginButton
                provider="passkey"
                disabled={state.isLoading}
                onAttempt={() => {
                  setErrorScope('passkey');
                }}
              />
              <RegisterPasskeyButton
                disabled={state.isLoading}
                onAttempt={() => {
                  setErrorScope('passkey');
                }}
              />
            </div>
          </>
        )}

        {hasEmail && (primaryMethods.length > 0 || hasPasskey) && (
          <div className="ikon-auth-divider">
            <span>{t('auth.divider')}</span>
          </div>
        )}

        {hasEmail && (
          <EmailLoginForm
            config={config}
            onAttempt={() => {
              setErrorScope('email');
            }}
          />
        )}

        {hasGuest && (primaryMethods.length > 0 || hasPasskey || hasEmail) && (
          <div className="ikon-auth-divider">
            <span>{t('auth.divider')}</span>
          </div>
        )}

        {hasGuest && (
          <>
            {errorFor('guest')}
            <LoginButton
              provider={guestProvider}
              disabled={state.isLoading}
              onAttempt={() => {
                setErrorScope('guest');
              }}
              {...(isLoginPrompt ? { onClick: onDismiss } : {})}
            />
          </>
        )}

        {isLoginPrompt && !hasGuest && (
          <button type="button" className="ikon-auth-dismiss" onClick={onDismiss}>
            {t('auth.dismiss')}
          </button>
        )}
      </section>
    </main>
  );
}
