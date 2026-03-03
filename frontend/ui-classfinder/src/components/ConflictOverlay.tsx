import type { Overlap } from '../types';
import { buildConflictLabel } from '../utils/validators';
import styles from './ConflictOverlay.module.css';

interface ConflictOverlayProps {
  overlap: Overlap;
  top: number;
  height: number;
}

export function ConflictOverlay({ overlap, top, height }: ConflictOverlayProps): JSX.Element {
  const label = buildConflictLabel(overlap);

  return (
    <div
      className={styles.overlay}
      style={{ top: `${top}px`, height: `${height}px` }}
      role="note"
      aria-label={label}
      title={label}
      data-testid="conflict-overlay"
    >
      <span>{label}</span>
    </div>
  );
}
