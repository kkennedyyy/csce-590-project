import { Link, NavLink } from 'react-router-dom';

import { MAX_CREDITS } from '../utils/validators';
import styles from './Header.module.css';

interface HeaderProps {
  currentCredits: number;
  hasConflicts: boolean;
  theme: 'dark' | 'light';
  onToggleTheme: () => void;
}

export function Header({ currentCredits, hasConflicts, theme, onToggleTheme }: HeaderProps): JSX.Element {
  const progress = Math.min(100, Math.round((currentCredits / MAX_CREDITS) * 100));

  return (
    <header className={styles.header}>
      <div className={styles.inner}>
        <Link className={styles.title} to="/schedule">
          ClassFinder Scheduler
        </Link>
        <p className={styles.subtitle}>Plan with confidence. Drag, validate, and finalize.</p>
        <nav aria-label="Main Navigation" className={styles.nav}>
          <NavLink to="/schedule" className={({ isActive }) => (isActive ? styles.active : '')}>
            Schedule
          </NavLink>
          <NavLink to="/browse" className={({ isActive }) => (isActive ? styles.active : '')}>
            Browse
          </NavLink>
          <button
            type="button"
            className={styles.themeToggle}
            onClick={onToggleTheme}
            aria-label={`Switch to ${theme === 'dark' ? 'light' : 'dark'} mode`}
          >
            {theme === 'dark' ? 'Light mode' : 'Dark mode'}
          </button>
        </nav>
        <div className={styles.credits}>
          <span>Current credits: {currentCredits} / 19</span>
          <div className={styles.progress} aria-hidden="true">
            <span style={{ width: `${progress}%` }} />
          </div>
          {hasConflicts && (
            <span className={styles.conflictPill} role="status" aria-live="polite">
              Conflicts present
            </span>
          )}
        </div>
      </div>
    </header>
  );
}
