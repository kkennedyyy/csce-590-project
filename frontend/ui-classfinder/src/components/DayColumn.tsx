import { useDroppable } from '@dnd-kit/core';

import type { Day, Overlap, ScheduledClass } from '../types';
import { DEFAULT_TIME_WINDOW, timeToMinutes } from '../utils/time';
import { ConflictOverlay } from './ConflictOverlay';
import styles from './DayColumn.module.css';

interface DayColumnProps {
  day: Day;
  classes: ScheduledClass[];
  overlaps: Overlap[];
  pxPerMinute: number;
  onRemoveClass: (classId: string) => void;
  onKeyboardAdd: (day: Day, startTime: string) => void;
}

export function DayColumn({
  day,
  classes,
  overlaps,
  pxPerMinute,
  onRemoveClass,
  onKeyboardAdd,
}: DayColumnProps): JSX.Element {
  const { setNodeRef, isOver } = useDroppable({
    id: `day-${day}`,
    data: { day },
  });

  const windowStart = timeToMinutes(DEFAULT_TIME_WINDOW.start);

  return (
    <section aria-label={`${day} schedule`} className={styles.column}>
      <h3>{day}</h3>
      <div ref={setNodeRef} className={`${styles.canvas} ${isOver ? styles.over : ''}`}>
        {Array.from({ length: 24 }).map((_, index) => {
          const minuteOffset = index * 30;
          const slotMinutes = windowStart + minuteOffset;
          const slotTime = `${String(Math.floor(slotMinutes / 60)).padStart(2, '0')}:${String(
            slotMinutes % 60,
          ).padStart(2, '0')}`;

          return (
            <DropSlot
              key={`${day}-${slotTime}`}
              id={`slot-${day}-${slotTime}`}
              ariaLabel={`${day} ${slotTime}`}
              onActivate={() => onKeyboardAdd(day, slotTime)}
            />
          );
        })}

        {classes.map((item) => {
          const top = (timeToMinutes(item.startTime) - windowStart) * pxPerMinute;
          const height = (timeToMinutes(item.endTime) - timeToMinutes(item.startTime)) * pxPerMinute;

          return (
            <article
              className={`${styles.event} ${styles[item.colorHint ?? 'neutral']}`}
              key={`${item.classId}-${day}`}
              style={{ top: `${top}px`, height: `${Math.max(height, 36)}px` }}
              aria-label={`${item.classId} ${item.startTime}-${item.endTime}`}
            >
              <strong>{item.classId}</strong>
              <span>
                {item.startTime}-{item.endTime}
              </span>
              <button
                type="button"
                onClick={() => onRemoveClass(item.classId)}
                aria-label={`Remove ${item.classId}`}
                title={`Remove ${item.classId}`}
              >
                ×
              </button>
            </article>
          );
        })}

        {overlaps.map((overlap) => {
          const top = (overlap.startMinute - windowStart) * pxPerMinute;
          const height = (overlap.endMinute - overlap.startMinute) * pxPerMinute;

          return (
            <ConflictOverlay
              key={`${overlap.day}-${overlap.classIds.join('-')}-${overlap.startMinute}`}
              overlap={overlap}
              top={top}
              height={height}
            />
          );
        })}
      </div>
    </section>
  );
}

interface DropSlotProps {
  id: string;
  ariaLabel: string;
  onActivate: () => void;
}

function DropSlot({ id, ariaLabel, onActivate }: DropSlotProps): JSX.Element {
  const { setNodeRef, isOver } = useDroppable({
    id,
  });

  return (
    <button
      ref={setNodeRef}
      type="button"
      className={`${styles.slot} ${isOver ? styles.slotOver : ''}`}
      aria-label={ariaLabel}
      onKeyDown={(event) => {
        if (event.key.toLowerCase() === 'a') {
          event.preventDefault();
          onActivate();
        }
      }}
      onClick={onActivate}
    />
  );
}
