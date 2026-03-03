import { useEffect, useState } from 'react';

import { fetchClassById } from '../api/api';
import type { ClassOffering, ScheduledClass } from '../types';
import styles from './ClassDetailModal.module.css';

interface ScheduledClassModalProps {
  item: ScheduledClass | null;
  onClose: () => void;
}

export function ScheduledClassModal({ item, onClose }: ScheduledClassModalProps): JSX.Element | null {
  const [details, setDetails] = useState<ClassOffering | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let isMounted = true;

    if (!item) {
      setDetails(null);
      setError(null);
      setLoading(false);
      return;
    }

    setLoading(true);
    setError(null);
    void fetchClassById(item.classId)
      .then((next) => {
        if (!isMounted) {
          return;
        }
        setDetails(next);
      })
      .catch((err: unknown) => {
        if (!isMounted) {
          return;
        }
        setError(err instanceof Error ? err.message : 'Unable to load class details.');
      })
      .finally(() => {
        if (!isMounted) {
          return;
        }
        setLoading(false);
      });

    return () => {
      isMounted = false;
    };
  }, [item]);

  if (!item) {
    return null;
  }

  return (
    <div className={styles.backdrop} role="presentation" onClick={onClose}>
      <section
        className={styles.modal}
        role="dialog"
        aria-modal="true"
        aria-label={`${item.classId} schedule details`}
        onClick={(event) => event.stopPropagation()}
      >
        <header>
          <h2>
            {item.classId} - {item.title}
          </h2>
          <button type="button" onClick={onClose} aria-label="Close class details">
            Close
          </button>
        </header>

        {loading && <p>Loading class details...</p>}
        {error && <p>{error}</p>}

        <p>
          Instructor: {item.instructor} | Room: {item.room}
        </p>
        <p>
          Meeting: {item.days.join('/')} {item.startTime}-{item.endTime}
        </p>
        <p>Credits: {item.credits}</p>
        <p>Term: {item.term}</p>

        {details?.description && <p>{details.description}</p>}

        {details && (
          <p>
            Seats: {details.enrolledCount}/{details.capacity}
          </p>
        )}
      </section>
    </div>
  );
}
