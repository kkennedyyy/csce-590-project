import { useMemo, useState } from 'react';

import { BrowseList } from '../components/BrowseList';
import { ClassDetailModal } from '../components/ClassDetailModal';
import { SearchBar } from '../components/SearchBar';
import { Toast } from '../components/Toast';
import { useClasses } from '../hooks/useClasses';
import { useSchedule } from '../hooks/useSchedule';
import type { ClassOffering, MeetingTime } from '../types';
import styles from './Page.module.css';

export function BrowsePage(): JSX.Element {
  const [search, setSearch] = useState('');
  const [selected, setSelected] = useState<ClassOffering | null>(null);
  const [modalClass, setModalClass] = useState<ClassOffering | null>(null);
  const [toast, setToast] = useState<{ message: string; tone: 'error' | 'info' | 'success' } | null>(null);
  const [inlineError, setInlineError] = useState<string | null>(null);

  const { filtered, hasMore, loadMore, loading, refresh } = useClasses(search);
  const { addClassToSchedule } = useSchedule();

  const suggestions = useMemo(
    () => filtered.slice(0, 5).map((entry) => `${entry.item.id} ${entry.item.title}`),
    [filtered],
  );

  async function handleAdd(item: ClassOffering, meetingTime?: MeetingTime): Promise<void> {
    const resolvedMeeting = meetingTime ?? item.meetingOptions?.[0];
    const result = await addClassToSchedule(item, { meetingTime: resolvedMeeting });

    if (!result.ok) {
      setInlineError(result.message ?? 'Could not add class.');
      setToast({ message: result.message ?? 'Could not add class.', tone: 'error' });
      return;
    }

    setInlineError(null);
    setToast({ message: `${item.id} added to schedule`, tone: 'success' });
    setModalClass(null);
    await refresh();
  }

  return (
    <main className={styles.container}>
      <div className={styles.row}>
        <SearchBar
          label="browse"
          value={search}
          onChange={setSearch}
          suggestions={suggestions}
          placeholder="Search by title, instructor, or class ID"
        />
      </div>

      {inlineError && (
        <p className={styles.errorText} role="alert">
          {inlineError}
        </p>
      )}

      <BrowseList
        classes={filtered}
        loading={loading}
        hasMore={hasMore}
        selectedId={selected?.id}
        onLoadMore={loadMore}
        onSelect={(item) => setSelected(item)}
        onOpenDetails={(item) => setModalClass(item)}
        onAdd={(item) => {
          if (item.meetingOptions && item.meetingOptions.length > 1) {
            setModalClass(item);
            return;
          }
          void handleAdd(item);
        }}
      />

      <ClassDetailModal
        item={modalClass}
        onClose={() => setModalClass(null)}
        onAdd={(item, meetingTime) => {
          void handleAdd(item, meetingTime);
        }}
      />

      {toast && <Toast tone={toast.tone} message={toast.message} onClose={() => setToast(null)} />}
    </main>
  );
}
