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
  addLabel?: string;
  secondaryActionLabel?: string;
  onSecondaryAction?: (item: ClassOffering) => void;
  dragEnabled?: boolean;
  statusBadge?: string;
}

export function ClassCard({
  item,
  onAdd,
  compact = false,
  selected = false,
  onSelect,
  addLabel = 'Add to schedule',
  secondaryActionLabel,
  onSecondaryAction,
  dragEnabled = true,
  statusBadge,
}: ClassCardProps) {
  const isFull = item.enrolledCount >= item.capacity;
  const actionDisabled = !item.isStudentEnrolled && isFull;
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({
    id: `class-${item.id}`,
    data: { classId: item.id },
    disabled: !dragEnabled || actionDisabled,
  });

  const style: CSSProperties = {
    transform: CSS.Translate.toString(transform),
    opacity: isDragging ? 0.55 : 1,
  };

  return (
    <article
      ref={setNodeRef}
      style={style}
      {...(!actionDisabled && dragEnabled ? listeners : {})}
      {...(!actionDisabled && dragEnabled ? attributes : {})}
      className={`${styles.card} ${compact ? styles.compact : ''} ${selected ? styles.selected : ''} ${
        actionDisabled ? styles.full : ''
      }`}
      aria-label={`${item.id} ${item.title}`}
      tabIndex={0}
      onFocus={() => onSelect?.(item)}
      onClick={() => onSelect?.(item)}
      onKeyDown={(event) => {
        if (event.key.toLowerCase() === 'a' || event.key === 'Enter') {
          event.preventDefault();
          if (!actionDisabled) {
            onAdd(item);
          }
        }
      }}
    >
      <div className={styles.topLine}>
        <strong>{item.id}</strong>
        {statusBadge ? (
          <span className={styles.statusPill}>{statusBadge}</span>
        ) : isFull ? (
          <span className={styles.fullPill}>Full</span>
        ) : (
          <span>{item.credits} cr</span>
        )}
      </div>
      <h3>{item.title}</h3>
      <p>
        {item.instructor} • {item.room}
      </p>
      <p>
        {item.days.join('/')} {item.startTime}-{item.endTime}
      </p>
      <p>Semester: {item.term || 'Unknown semester'}</p>
      {(item.department || item.departmentCode) && (
        <p>{item.department ?? item.departmentCode}</p>
      )}
      <p className={styles.capacity}>Capacity: {item.enrolledCount}/{item.capacity}</p>
      <div className={styles.actions}>
        <button
          type="button"
          onClick={() => onAdd(item)}
          onPointerDown={(event) => event.stopPropagation()}
          disabled={actionDisabled}
          aria-label={`${addLabel} ${item.id}`}
        >
          {addLabel}
        </button>
        {secondaryActionLabel && onSecondaryAction ? (
          <button
            type="button"
            className={styles.secondaryAction}
            onClick={() => onSecondaryAction(item)}
            onPointerDown={(event) => event.stopPropagation()}
          >
            {secondaryActionLabel}
          </button>
        ) : null}
        {dragEnabled && (
          <span className={styles.dragHandle} aria-hidden="true">
            Drag anywhere
          </span>
        )}
      </div>
    </article>
  );
}
