import { useEffect, useRef } from 'react';

import type { ClassOffering } from '../types';
import { ClassCard } from './ClassCard';
import styles from './BrowseList.module.css';

interface BrowseListProps {
  classes: Array<{ item: ClassOffering; badges: string[] }>;
  loading: boolean;
  hasMore: boolean;
  selectedId?: string;
  onLoadMore: () => Promise<void>;
  onAdd: (item: ClassOffering) => void;
  onSelect: (item: ClassOffering) => void;
  onOpenDetails: (item: ClassOffering) => void;
}

export function BrowseList({
  classes,
  loading,
  hasMore,
  selectedId,
  onLoadMore,
  onAdd,
  onSelect,
  onOpenDetails,
}: BrowseListProps): JSX.Element {
  const sentinelRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    const element = sentinelRef.current;

    if (!element || !hasMore) {
      return;
    }

    const observer = new IntersectionObserver((entries) => {
      if (entries[0]?.isIntersecting) {
        void onLoadMore();
      }
    });

    observer.observe(element);
    return () => observer.disconnect();
  }, [hasMore, onLoadMore]);

  return (
    <section aria-label="Browse classes" className={styles.wrapper}>
      <div className={styles.grid}>
        {classes.map((entry) => (
          <div key={entry.item.id} className={styles.item}>
            <ClassCard
              item={entry.item}
              onAdd={onAdd}
              selected={selectedId === entry.item.id}
              onSelect={onSelect}
            />
            <div className={styles.badges}>
              {entry.badges.map((badge) => (
                <span key={`${entry.item.id}-${badge}`}>{badge} match</span>
              ))}
              <button type="button" onClick={() => onOpenDetails(entry.item)}>
                View details
              </button>
            </div>
          </div>
        ))}
      </div>
      <div ref={sentinelRef} className={styles.sentinel} aria-hidden="true" />
      {loading && <p className={styles.info}>Loading classes...</p>}
      {!hasMore && <p className={styles.info}>End of catalog.</p>}
    </section>
  );
}
