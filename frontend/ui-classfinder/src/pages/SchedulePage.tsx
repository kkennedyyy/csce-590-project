import {
  DndContext,
  DragOverlay,
  KeyboardSensor,
  PointerSensor,
  type DragEndEvent,
  type DragStartEvent,
  useSensor,
  useSensors,
} from '@dnd-kit/core';
import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import { fetchStudentEnrolledClasses } from '../api/api';
import { ClassCard } from '../components/ClassCard';
import { ScheduledClassModal } from '../components/ScheduledClassModal';
import { ScheduleGrid } from '../components/ScheduleGrid';
import { SearchBar } from '../components/SearchBar';
import { Toast } from '../components/Toast';
import { useClasses } from '../hooks/useClasses';
import { useSchedule } from '../hooks/useSchedule';
import type { ClassOffering, Day, MeetingTime, ScheduledClass, SmartScheduleClass } from '../types';
import { minutesToTime, timeToMinutes } from '../utils/time';
import { getOverlaps, MAX_CREDITS } from '../utils/validators';
import styles from './Page.module.css';

function createMeetingFromDrop(source: ClassOffering, startTime: string): MeetingTime {
  const duration = timeToMinutes(source.endTime) - timeToMinutes(source.startTime);
  const startMinute = timeToMinutes(startTime);

  return {
    // Preserve a class's configured meeting pattern (e.g., Mon/Wed) when dragging.
    // Drop position controls start/end time, not collapsing to a single day.
    days: source.days,
    startTime,
    endTime: minutesToTime(startMinute + duration),
  };
}

interface SchedulePageProps {
  searchTerm: string;
}

export function SchedulePage({ searchTerm }: SchedulePageProps): JSX.Element {
  const navigate = useNavigate();
  const {
    studentId,
    scheduledClasses,
    overlaps,
    currentCredits,
    addClassToSchedule,
    removeClassFromSchedule,
    finalizeRegistration,
    loading,
  } = useSchedule();

  const [toast, setToast] = useState<{ message: string; tone: 'info' | 'error' | 'success' } | null>(null);
  const [inlineError, setInlineError] = useState<string | null>(null);
  const [selectedClass, setSelectedClass] = useState<ClassOffering | null>(null);
  const [activeDragClass, setActiveDragClass] = useState<ClassOffering | null>(null);
  const [activeScheduleClass, setActiveScheduleClass] = useState<ScheduledClass | null>(null);
  const [scheduleView, setScheduleView] = useState<'planner' | 'enrolled'>('enrolled');
  const [plannerSearch, setPlannerSearch] = useState('');
  const [enrolledClasses, setEnrolledClasses] = useState<SmartScheduleClass[]>([]);
  const [enrolledLoading, setEnrolledLoading] = useState(false);
  const effectivePlannerSearch = plannerSearch.trim().length > 0 ? plannerSearch : searchTerm;
  const { filtered, classes, refresh } = useClasses(effectivePlannerSearch, '', studentId);

  useEffect(() => {
    if (!studentId) return;
    setEnrolledLoading(true);
    fetchStudentEnrolledClasses(studentId)
      .then((data) => setEnrolledClasses(data))
      .catch(() => { /* silently handle */ })
      .finally(() => setEnrolledLoading(false));
  }, [studentId]);


  const enrolledSchedule = useMemo<ScheduledClass[]>(
    () =>
      enrolledClasses
        .filter((item) => item.days && item.startTime && item.endTime)
        .map((item) => ({
          sectionId: item.classId,
          classId: item.classCode,
          title: item.className,
          instructor: item.instructorName,
          credits: item.credits,
          room: item.location,
          location: item.location,
          term: item.term ?? 'Unknown semester',
          days: item.days ?? ([] as Day[]),
          startTime: item.startTime ?? '08:00',
          endTime: item.endTime ?? '09:00',
          colorHint: 'neutral',
        })),
    [enrolledClasses],
  );

  const enrolledOverlaps = useMemo(() => getOverlaps(enrolledSchedule), [enrolledSchedule]);
  const sensors = useSensors(
    useSensor(PointerSensor, {
      activationConstraint: {
        distance: 8,
      },
    }),
    useSensor(KeyboardSensor),
  );

  const previewClasses = filtered.slice(0, 5);
  const plannerSuggestions = useMemo(
    () => filtered.slice(0, 20).map((entry) => `${entry.item.id} ${entry.item.title}`),
    [filtered],
  );
  const classIndex = useMemo(() => new Map(classes.map((item) => [item.id, item])), [classes]);

  const finalizeDisabled = overlaps.length > 0;
  const finalizeBlocked = finalizeDisabled || loading;
  const creditProgress = Math.min(100, Math.round((currentCredits / MAX_CREDITS) * 100));

  const setGlobalDragState = (dragging: boolean) => {
    document.body.classList.toggle('drag-active', dragging);
  };

  const addWithFeedback = async (
    classOffering: ClassOffering,
    options: { meetingTime?: MeetingTime; successMessage: string },
  ) => {
    const result = await addClassToSchedule(classOffering, { meetingTime: options.meetingTime });
    if (!result.ok) {
      setInlineError(result.message ?? 'Unable to add class.');
      setToast({ message: result.message ?? 'Unable to add class.', tone: 'error' });
      return;
    }

    setInlineError(null);
    setToast({ message: options.successMessage, tone: 'success' });
    await refresh();
  };

  const handleFinalizeRegistration = async () => {
    const result = await finalizeRegistration();
    if (!result.ok) {
      const message = result.message ?? 'Unable to finalize registration.';
      setInlineError(message);
      setToast({ message, tone: 'error' });
      return;
    }

    setInlineError(null);
    setToast({ message: 'Registration finalized and saved.', tone: 'success' });
  };

  const handleDragStart = (event: DragStartEvent) => {
    const classId = String(event.active.id).replace('class-', '');
    const picked = classIndex.get(classId) ?? previewClasses.find((entry) => entry.item.id === classId)?.item;
    if (picked) {
      setActiveDragClass(picked);
      setGlobalDragState(true);
    }
  };

  const handleDragEnd = async (event: DragEndEvent) => {
    setGlobalDragState(false);
    const dragged = activeDragClass;
    setActiveDragClass(null);

    if (!dragged || !event.over?.id) {
      return;
    }

    const overId = String(event.over.id);

    if (overId.startsWith('slot-')) {
      const [, rawDay, startTime] = overId.split('-');
      const day = rawDay as Day;
      await addWithFeedback(dragged, {
        meetingTime: createMeetingFromDrop(dragged, startTime),
        successMessage: `${dragged.id} placed on ${day} ${startTime}`,
      });
      return;
    }

    if (overId.startsWith('day-')) {
      const day = overId.replace('day-', '') as Day;
      await addWithFeedback(dragged, {
        meetingTime: createMeetingFromDrop(dragged, dragged.startTime),
        successMessage: `${dragged.id} placed on ${day}`,
      });
    }
  };

  return (
    <main className={`${styles.container} ${styles.scheduleContainer}`}>
      <div className={styles.stickyActionsWrap}>
        <section className={styles.stickyCredits} aria-label="Current credits" role="status" aria-live="polite">
          <span>
            Current credits: {currentCredits} / {MAX_CREDITS}
          </span>
          <div className={styles.stickyProgress} aria-hidden="true">
            <span style={{ width: `${creditProgress}%` }} />
          </div>
          {overlaps.length > 0 && <span className={styles.stickyConflictPill}>Conflicts present</span>}
        </section>

        <div className={styles.actions} aria-label="Schedule quick actions" role="group">
          <button
            type="button"
            disabled={finalizeBlocked}
            aria-disabled={finalizeBlocked}
            onClick={() => {
              void handleFinalizeRegistration();
            }}
          >
            Finalize registration
          </button>
          <button
            type="button"
            onClick={() => {
              if (!overlaps.length) {
                setToast({ message: 'No conflicts to resolve.', tone: 'info' });
                return;
              }

              const firstConflict = overlaps[0];
              setToast({
                message: `Drop ${firstConflict.classIds[0]} or ${firstConflict.classIds[1]} to make room.`,
                tone: 'error',
              });
            }}
          >
            Resolve conflicts
          </button>
          <button type="button" onClick={() => navigate('/browse')}>
            Open browse page
          </button>
        </div>
      </div>

      {inlineError && (
        <div className={styles.inlineError} role="alert">
          {inlineError}
        </div>
      )}

      <div className={`${styles.smartOptionTabs} ${styles.viewTabs}`}>
        <button
          type="button"
          className={scheduleView === 'enrolled' ? styles.smartOptionActive : ''}
          onClick={() => setScheduleView('enrolled')}
        >
          My Enrolled Classes
        </button>
        <button
          type="button"
          className={scheduleView === 'planner' ? styles.smartOptionActive : ''}
          onClick={() => setScheduleView('planner')}
        >
          Schedule Planner
        </button>
      </div>

      {scheduleView === 'enrolled' && (
        <section className={styles.smartResults} aria-label="Enrolled classes calendar">
          <h3>Current Enrollments</h3>
          {enrolledLoading && <p className={styles.smartHint}>Loading your enrollments...</p>}
          {!enrolledLoading && enrolledClasses.length === 0 && (
            <p className={styles.smartHint}>
              No active enrollments found. Use <strong>Smart Enrollment</strong> or the <strong>Browse</strong> page to add classes.
            </p>
          )}
          {!enrolledLoading && enrolledClasses.length > 0 && (
            <div className={styles.sidebar}>
              <ScheduleGrid
                schedule={enrolledSchedule}
                overlaps={enrolledOverlaps}
                onRemoveClass={async () => Promise.resolve()}
                onKeyboardAdd={async () => Promise.resolve()}
                onOpenClassDetails={(item) => setActiveScheduleClass(item)}
              />

              <aside className={styles.panel}>
                <h2>Enrolled classes</h2>
                <p>Your currently enrolled classes from the database.</p>
                <div className={styles.selectedCard}>
                  {enrolledClasses.map((item) => (
                    <article key={item.classId} className={styles.enrolledClassCard}>
                      <strong>{item.classCode}</strong>
                      <h4>{item.className}</h4>
                      <p>{item.instructorName}</p>
                      <p>{item.daysTimes}</p>
                      <p>{item.term ?? 'Unknown semester'}</p>
                      <p>{item.location}</p>
                    </article>
                  ))}
                </div>
              </aside>
            </div>
          )}
        </section>
      )}

      {scheduleView === 'planner' && <DndContext
        sensors={sensors}
        onDragStart={handleDragStart}
        onDragEnd={(event) => {
          void handleDragEnd(event);
        }}
        onDragCancel={() => {
          setGlobalDragState(false);
          setActiveDragClass(null);
        }}
      >
        <div className={styles.sidebar}>
          <ScheduleGrid
            schedule={scheduledClasses}
            overlaps={overlaps}
            onRemoveClass={async (classId) => {
              const result = await removeClassFromSchedule(classId);
              if (!result.ok) {
                setToast({ message: result.message ?? 'Unable to remove class.', tone: 'error' });
                return;
              }
              setInlineError(null);
              setToast({ message: `${classId} removed from schedule`, tone: 'info' });
              await refresh();
            }}
            onKeyboardAdd={async (day, startTime) => {
              if (!selectedClass) {
                setToast({ message: 'Select a class first in suggestions.', tone: 'info' });
                return;
              }
              await addWithFeedback(selectedClass, {
                meetingTime: createMeetingFromDrop(selectedClass, startTime),
                successMessage: `${selectedClass.id} placed on ${day} ${startTime}`,
              });
            }}
            onOpenClassDetails={(item) => setActiveScheduleClass(item)}
          />

          <aside className={styles.panel}>
            <h2>Pinned suggestions</h2>
            <p>Drag a class here or use header search.</p>
            <SearchBar
              label="planner"
              value={plannerSearch}
              onChange={setPlannerSearch}
              suggestions={plannerSuggestions}
              placeholder="Search classes in planner"
              className={styles.schedulePlannerSearch}
            />
            {plannerSearch.trim().length > 0 && (
              <button
                type="button"
                className={styles.clearPlannerSearch}
                onClick={() => setPlannerSearch('')}
              >
                Clear planner search
              </button>
            )}
            <div className={styles.selectedCard}>
              {previewClasses.map((entry) => (
                <ClassCard
                  key={entry.item.id}
                  item={entry.item}
                  compact
                  selected={selectedClass?.id === entry.item.id}
                  onSelect={(item) => setSelectedClass(item)}
                  onAdd={async (item) => {
                    const meetingTime = item.meetingOptions?.[0];
                    await addWithFeedback(item, {
                      meetingTime,
                      successMessage: `${item.id} added`,
                    });
                  }}
                />
              ))}
              {!previewClasses.length && <p>No matching classes.</p>}
            </div>
            {loading && <p>Updating schedule...</p>}
          </aside>
        </div>

        <DragOverlay>
          {activeDragClass ? (
            <div className={styles.dragPreview} role="presentation">
              <strong>{activeDragClass.id}</strong>
              <h4>{activeDragClass.title}</h4>
              <p>
                {activeDragClass.days.join('/')} {activeDragClass.startTime}-{activeDragClass.endTime}
              </p>
              <p>{activeDragClass.instructor}</p>
            </div>
          ) : null}
        </DragOverlay>
      </DndContext>}

      <ScheduledClassModal item={activeScheduleClass} onClose={() => setActiveScheduleClass(null)} />

      {toast && (
        <Toast
          tone={toast.tone}
          message={toast.message}
          onClose={() => {
            setToast(null);
          }}
        />
      )}
    </main>
  );
}
