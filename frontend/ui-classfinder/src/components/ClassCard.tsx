import type { CSSProperties } from 'react';
import { useDraggable } from '@dnd-kit/core';
import { CSS } from '@dnd-kit/utilities';

import type { ClassOffering } from '../types';
import styles from './ClassCard.module.css';

interface ClassCardProps {
  item: ClassOffering;
  onAdd: (item: ClassOffering) => void;
  compact?: boolean;
  selected?: boolean;
  onSelect?: (item: ClassOffering) => void;
}

export function ClassCard({ item, onAdd, compact = false, selected = false, onSelect }: ClassCardProps) {
  const isFull = item.enrolledCount >= item.capacity;
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({
    id: `class-${item.id}`,
    data: { classId: item.id },
    disabled: isFull,
  });

  const style: CSSProperties = {
    transform: CSS.Translate.toString(transform),
    opacity: isDragging ? 0.55 : 1,
  };

  return (
    <article
      ref={setNodeRef}
      style={style}
      {...(!isFull ? listeners : {})}
      {...(!isFull ? attributes : {})}
      className={`${styles.card} ${compact ? styles.compact : ''} ${selected ? styles.selected : ''} ${
        isFull ? styles.full : ''
      }`}
      aria-label={`${item.id} ${item.title}`}
      tabIndex={0}
      onFocus={() => onSelect?.(item)}
      onClick={() => onSelect?.(item)}
      onKeyDown={(event) => {
        if (event.key.toLowerCase() === 'a' || event.key === 'Enter') {
          event.preventDefault();
          if (!isFull) {
            onAdd(item);
          }
        }
      }}
    >
      <div className={styles.topLine}>
        <strong>{item.id}</strong>
        {isFull ? <span className={styles.fullPill}>Full</span> : <span>{item.credits} cr</span>}
      </div>
      <h3>{item.title}</h3>
      <p>
        {item.instructor} • {item.room}
      </p>
      <p>
        {item.days.join('/')} {item.startTime}-{item.endTime}
      </p>
      <p className={styles.capacity}>Capacity: {item.enrolledCount}/{item.capacity}</p>
      <div className={styles.actions}>
        <button
          type="button"
          onClick={() => onAdd(item)}
          onPointerDown={(event) => event.stopPropagation()}
          disabled={isFull}
          aria-label={`Add ${item.id} to schedule`}
        >
          Add to schedule
        </button>
        <span className={styles.dragHandle} aria-hidden="true">
          Drag anywhere
        </span>
      </div>
    </article>
  );
}
