import { useEffect } from 'react';

import styles from './Toast.module.css';

interface ToastProps {
  message: string;
  tone?: 'error' | 'info' | 'success';
  onClose: () => void;
  timeoutMs?: number;
}

export function Toast({ message, tone = 'info', onClose, timeoutMs = 3200 }: ToastProps): JSX.Element {
  useEffect(() => {
    const timer = window.setTimeout(onClose, timeoutMs);
    return () => window.clearTimeout(timer);
  }, [onClose, timeoutMs]);

  return (
    <div className={`${styles.toast} ${styles[tone]}`} role="status" aria-live="polite">
      <span>{message}</span>
      <button type="button" onClick={onClose} aria-label="Dismiss notification">
        Dismiss
      </button>
    </div>
  );
}
