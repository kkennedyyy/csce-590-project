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
  const [department, setDepartment] = useState('');
  const [selected, setSelected] = useState<ClassOffering | null>(null);
  const [modalClass, setModalClass] = useState<ClassOffering | null>(null);
  const [toast, setToast] = useState<{ message: string; tone: 'error' | 'info' | 'success' } | null>(null);
  const [inlineError, setInlineError] = useState<string | null>(null);

  const { studentId, addClassToSchedule, removeClassFromSchedule, scheduledClasses } = useSchedule();
  const { filtered, classes, departments, hasMore, loadMore, loading, refresh } = useClasses(
    search,
    department,
    studentId,
  );

  const suggestions = useMemo(
    () => filtered.slice(0, 5).map((entry) => `${entry.item.id} ${entry.item.title}`),
    [filtered],
  );
  const activeFilters = useMemo(
    () =>
      [
        search.trim() ? `Search: ${search.trim()}` : null,
        department ? `Department: ${department}` : null,
      ].filter(Boolean) as string[],
    [department, search],
  );
  const scheduledIds = useMemo(
    () => new Set(scheduledClasses.map((item) => item.classId)),
    [scheduledClasses],
  );
  const visibleClasses = useMemo(
    () =>
      filtered.map((entry) => ({
        ...entry,
        item: {
          ...entry.item,
          isStudentEnrolled: entry.item.isStudentEnrolled || scheduledIds.has(entry.item.id),
          enrollmentStatus: (
            entry.item.isStudentEnrolled || scheduledIds.has(entry.item.id) ? 'Enrolled' : 'NotEnrolled'
          ) as ClassOffering['enrollmentStatus'],
        },
      })),
    [filtered, scheduledIds],
  );

  async function handleEnroll(item: ClassOffering, meetingTime?: MeetingTime): Promise<void> {
    const resolvedMeeting = meetingTime ?? item.meetingOptions?.[0];
    const result = await addClassToSchedule(item, { meetingTime: resolvedMeeting });

    if (!result.ok) {
      setInlineError(result.message ?? 'Could not enroll in class.');
      setToast({ message: result.message ?? 'Could not enroll in class.', tone: 'error' });
      return;
    }

    setInlineError(null);
    setToast({ message: `${item.id} enrolled successfully`, tone: 'success' });
    setModalClass(null);
    await refresh();
  }

  async function handleDrop(item: ClassOffering): Promise<void> {
    const result = await removeClassFromSchedule(item.id);

    if (!result.ok) {
      setInlineError(result.message ?? 'Could not drop class.');
      setToast({ message: result.message ?? 'Could not drop class.', tone: 'error' });
      return;
    }

    setInlineError(null);
    setToast({ message: `${item.id} removed from your schedule`, tone: 'info' });
    setModalClass(null);
    await refresh();
  }

  async function handlePrimaryAction(item: ClassOffering, meetingTime?: MeetingTime): Promise<void> {
    if (item.isStudentEnrolled) {
      await handleDrop(item);
      return;
    }

    await handleEnroll(item, meetingTime);
  }

  return (
    <main className={styles.container}>
      <div className={styles.filterBar}>
        <SearchBar
          label="browse"
          value={search}
          onChange={setSearch}
          suggestions={suggestions}
          placeholder="Search by title, instructor, class ID, or department"
        />
        <label className={styles.filterField}>
          <span>Department</span>
          <select value={department} onChange={(event) => setDepartment(event.target.value)}>
            <option value="">All departments</option>
            {departments.map((item) => (
              <option key={item} value={item}>
                {item}
              </option>
            ))}
          </select>
        </label>
        <button
          type="button"
          className={styles.clearFilters}
          onClick={() => {
            setSearch('');
            setDepartment('');
          }}
          disabled={!search.trim() && !department}
        >
          Clear filters
        </button>
      </div>

      {activeFilters.length > 0 && (
        <div className={styles.activeFilters} aria-label="Active filters">
          {activeFilters.map((filter) => (
            <span key={filter}>{filter}</span>
          ))}
        </div>
      )}

      {inlineError && (
        <p className={styles.errorText} role="alert">
          {inlineError}
        </p>
      )}

      <BrowseList
        classes={visibleClasses}
        loading={loading}
        hasMore={hasMore}
        selectedId={selected?.id}
        onLoadMore={loadMore}
        onSelect={(item) => setSelected(item)}
        onOpenDetails={(item) => setModalClass(item)}
        onAdd={(item) => {
          if (item.isStudentEnrolled) {
            void handleDrop(item);
            return;
          }

          if (item.meetingOptions && item.meetingOptions.length > 1) {
            setModalClass(item);
            return;
          }

          void handleEnroll(item);
        }}
      />

      {!loading && classes.length === 0 && (
        <div className={styles.emptyState}>
          <h2>No classes match the current filters.</h2>
          <p>Reset filters or try a broader search term.</p>
        </div>
      )}

      <ClassDetailModal
        item={modalClass}
        onClose={() => setModalClass(null)}
        onAdd={(item, meetingTime) => {
          void handlePrimaryAction(item, meetingTime);
        }}
      />

      {toast && <Toast tone={toast.tone} message={toast.message} onClose={() => setToast(null)} />}
    </main>
  );
}
