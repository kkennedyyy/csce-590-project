import type { ClassOffering, MeetingTime } from '../types';
import styles from './ClassDetailModal.module.css';

interface ClassDetailModalProps {
  item: ClassOffering | null;
  onClose: () => void;
  onAdd: (item: ClassOffering, meetingTime?: MeetingTime) => void;
}

export function ClassDetailModal({ item, onClose, onAdd }: ClassDetailModalProps): JSX.Element | null {
  if (!item) {
    return null;
  }

  const options = item.meetingOptions ?? [
    {
      days: item.days,
      startTime: item.startTime,
      endTime: item.endTime,
    },
  ];

  return (
    <div className={styles.backdrop} role="presentation" onClick={onClose}>
      <section
        className={styles.modal}
        role="dialog"
        aria-modal="true"
        aria-label={`${item.id} details`}
        onClick={(event) => event.stopPropagation()}
      >
        <header>
          <h2>
            {item.id} - {item.title}
          </h2>
          <button type="button" onClick={onClose} aria-label="Close class details">
            Close
          </button>
        </header>
        <p>{item.description ?? 'No description available.'}</p>
        <p>
          Instructor: {item.instructor} | Room: {item.room}
        </p>
        <p>
          Seats: {item.enrolledCount}/{item.capacity} | Credits: {item.credits}
        </p>
        <h3>Meeting options</h3>
        <div className={styles.options}>
          {options.map((option, index) => (
            <button
              key={`${item.id}-${index}`}
              type="button"
              onClick={() => onAdd(item, option)}
              disabled={item.enrolledCount >= item.capacity}
            >
              {option.days.join('/')} {option.startTime}-{option.endTime}
            </button>
          ))}
        </div>
      </section>
    </div>
  );
}
