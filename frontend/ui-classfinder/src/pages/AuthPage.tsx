import { type FormEvent, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import { ApiError, loginUser, signupStudent } from '../api/api';
import { runtimeConfig } from '../config/runtime';
import type { UserRole } from '../types';
import { useAuthStore } from '../store/authStore';
import { useScheduleStore } from '../store/scheduleStore';
import styles from './AuthPage.module.css';

type AuthMode = 'login' | 'signup';

type DemoCredential = {
  email: string;
  password: string;
};

const demoTeacherCredential: DemoCredential = runtimeConfig.apiBaseUrl.trim()
  ? {
      email: 'brown@email.com',
      password: 'teacher123',
    }
  : {
      email: 'brown@email.com',
      password: 'teacher123',
    };

const demoCredentials: Record<UserRole, DemoCredential> = {
  student: {
    email: 'john.smith@email.com',
    password: 'student123',
  },
  teacher: demoTeacherCredential,
};

export function AuthPage(): JSX.Element {
  const navigate = useNavigate();
  const login = useAuthStore((state) => state.login);
  const setStudentId = useScheduleStore((state) => state.setStudentId);
  const [role, setRole] = useState<UserRole>('student');
  const [mode, setMode] = useState<AuthMode>('login');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loginForm, setLoginForm] = useState({
    email: demoCredentials.student.email,
    password: demoCredentials.student.password,
  });
  const [signupForm, setSignupForm] = useState({
    firstName: '',
    lastName: '',
    email: '',
    password: '',
  });

  const copy = useMemo(
    () =>
      role === 'teacher'
        ? {
            eyebrow: 'Teacher Access',
            title: 'Sign in to manage your teaching sections',
            submit: loading ? 'Signing in…' : 'Sign in',
            demoButton: 'Use demo teacher',
          }
        : {
            eyebrow: 'Student Access',
            title:
              mode === 'signup' ? 'Create a student account' : 'Sign in or create a student account',
            submit: mode === 'signup' ? (loading ? 'Creating account…' : 'Create account') : loading ? 'Signing in…' : 'Sign in',
            demoButton: 'Use demo student',
          },
    [loading, mode, role],
  );

  const handleRoleChange = (nextRole: UserRole) => {
    setRole(nextRole);
    setError(null);
    if (nextRole === 'teacher') {
      setMode('login');
    }
    setLoginForm({
      email: demoCredentials[nextRole].email,
      password: demoCredentials[nextRole].password,
    });
  };

  const handleLoginSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setLoading(true);
    setError(null);

    try {
      const user = await loginUser({
        email: loginForm.email,
        password: loginForm.password,
        role,
      });
      login(user);
      if (user.role === 'student') {
        setStudentId(user.userId);
      }
      navigate(user.role === 'teacher' ? '/teachers' : '/schedule', { replace: true });
    } catch (err) {
      setError(resolveError(err));
    } finally {
      setLoading(false);
    }
  };

  const handleSignupSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setLoading(true);
    setError(null);

    try {
      const user = await signupStudent(signupForm);
      login(user);
      setStudentId(user.userId);
      navigate('/schedule', { replace: true });
    } catch (err) {
      setError(resolveError(err));
    } finally {
      setLoading(false);
    }
  };

  return (
    <main className={styles.shell}>
      <section className={styles.card} aria-labelledby="auth-title">
        <div className={styles.copy}>
          <span className={styles.eyebrow}>{copy.eyebrow}</span>
          <h1 id="auth-title">{copy.title}</h1>
        </div>

        <div className={styles.roleRow} role="tablist" aria-label="Access role">
          <button
            type="button"
            role="tab"
            aria-selected={role === 'student'}
            className={role === 'student' ? styles.modeActive : styles.modeButton}
            onClick={() => handleRoleChange('student')}
          >
            Student
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={role === 'teacher'}
            className={role === 'teacher' ? styles.modeActive : styles.modeButton}
            onClick={() => handleRoleChange('teacher')}
          >
            Teacher
          </button>
        </div>

        {role === 'student' && (
          <div className={styles.modeRow} role="tablist" aria-label="Authentication mode">
            <button
              type="button"
              role="tab"
              aria-selected={mode === 'login'}
              className={mode === 'login' ? styles.modeActive : styles.modeButton}
              onClick={() => {
                setMode('login');
                setError(null);
              }}
            >
              Sign in
            </button>
            <button
              type="button"
              role="tab"
              aria-selected={mode === 'signup'}
              className={mode === 'signup' ? styles.modeActive : styles.modeButton}
              onClick={() => {
                setMode('signup');
                setError(null);
              }}
            >
              Create account
            </button>
          </div>
        )}

        {role === 'student' && mode === 'signup' ? (
          <form className={styles.form} onSubmit={(event) => void handleSignupSubmit(event)}>
            <label className={styles.field}>
              <span>First name</span>
              <input
                type="text"
                value={signupForm.firstName}
                onChange={(event) =>
                  setSignupForm((current) => ({ ...current, firstName: event.target.value }))
                }
                autoComplete="given-name"
                required
              />
            </label>
            <label className={styles.field}>
              <span>Last name</span>
              <input
                type="text"
                value={signupForm.lastName}
                onChange={(event) =>
                  setSignupForm((current) => ({ ...current, lastName: event.target.value }))
                }
                autoComplete="family-name"
                required
              />
            </label>
            <label className={styles.field}>
              <span>Email</span>
              <input
                type="email"
                value={signupForm.email}
                onChange={(event) => setSignupForm((current) => ({ ...current, email: event.target.value }))}
                autoComplete="email"
                required
              />
            </label>
            <label className={styles.field}>
              <span>Password</span>
              <input
                type="password"
                value={signupForm.password}
                onChange={(event) =>
                  setSignupForm((current) => ({ ...current, password: event.target.value }))
                }
                autoComplete="new-password"
                minLength={8}
                required
              />
            </label>
            <button type="submit" className={styles.submitButton} disabled={loading}>
              {copy.submit}
            </button>
          </form>
        ) : (
          <form className={styles.form} onSubmit={(event) => void handleLoginSubmit(event)}>
            <label className={styles.field}>
              <span>Email</span>
              <input
                type="email"
                value={loginForm.email}
                onChange={(event) => setLoginForm((current) => ({ ...current, email: event.target.value }))}
                autoComplete="email"
                required
              />
            </label>
            <label className={styles.field}>
              <span>Password</span>
              <input
                type="password"
                value={loginForm.password}
                onChange={(event) => setLoginForm((current) => ({ ...current, password: event.target.value }))}
                autoComplete="current-password"
                required
              />
            </label>
            <button type="submit" className={styles.submitButton} disabled={loading}>
              {copy.submit}
            </button>
            <button
              type="button"
              className={styles.secondaryButton}
              onClick={() =>
                setLoginForm({
                  email: demoCredentials[role].email,
                  password: demoCredentials[role].password,
                })
              }
            >
              {copy.demoButton}
            </button>
          </form>
        )}

        {error ? (
          <div className={styles.error} role="alert">
            {error}
          </div>
        ) : null}
      </section>
    </main>
  );
}

function resolveError(error: unknown): string {
  if (error instanceof ApiError && error.message.trim()) {
    return error.message;
  }

  if (error instanceof Error && error.message.trim()) {
    return error.message;
  }

  return 'Authentication failed. Try again.';
}
