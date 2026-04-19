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
import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import { ClassCard } from '../components/ClassCard';
import { ScheduledClassModal } from '../components/ScheduledClassModal';
import { ScheduleGrid } from '../components/ScheduleGrid';
import { SmartEnrollmentPanel } from '../components/SmartEnrollmentPanel';
import { Toast } from '../components/Toast';
import { useClasses } from '../hooks/useClasses';
import { useSchedule } from '../hooks/useSchedule';
import type { ClassOffering, Day, MeetingTime, RegisteredClass, ScheduledClass } from '../types';
import { formatUpcomingSession, getNextMeetingDate, minutesToTime, timeToMinutes } from '../utils/time';
import { MAX_CREDITS } from '../utils/validators';
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
    registeredClasses,
    overlaps,
    currentCredits,
    addClassToSchedule,
    removeClassFromSchedule,
    finalizeRegistration,
    applyGeneratedSchedule,
    loading,
  } = useSchedule();

  const [toast, setToast] = useState<{ message: string; tone: 'info' | 'error' | 'success' } | null>(null);
  const [inlineError, setInlineError] = useState<string | null>(null);
  const [selectedClass, setSelectedClass] = useState<ClassOffering | null>(null);
  const [activeDragClass, setActiveDragClass] = useState<ClassOffering | null>(null);
  const [activeScheduleClass, setActiveScheduleClass] = useState<ScheduledClass | RegisteredClass | null>(null);
  const { filtered, classes, refresh } = useClasses(searchTerm, '', studentId);
  const sensors = useSensors(
    useSensor(PointerSensor, {
      activationConstraint: {
        distance: 8,
      },
    }),
    useSensor(KeyboardSensor),
  );

  const previewClasses = filtered.slice(0, 5);
  const classIndex = useMemo(() => new Map(classes.map((item) => [item.id, item])), [classes]);
  const waitlistedClasses = useMemo(
    () => registeredClasses.filter((item) => item.enrollmentStatus === 'Waitlisted'),
    [registeredClasses],
  );
  const upcomingSessions = useMemo(
    () =>
      registeredClasses
        .filter((item) => item.enrollmentStatus === 'Enrolled')
        .map((item) => ({
          item,
          nextDate: getNextMeetingDate(item.days, item.startTime),
        }))
        .filter((entry): entry is { item: RegisteredClass; nextDate: Date } => Boolean(entry.nextDate))
        .sort((left, right) => left.nextDate.getTime() - right.nextDate.getTime())
        .slice(0, 3),
    [registeredClasses],
  );

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
    setToast({ message: result.message ?? options.successMessage, tone: result.message ? 'info' : 'success' });
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

      <section className={styles.dashboardGrid}>
        <article className={styles.dashboardCard}>
          <p className={styles.dashboardEyebrow}>Student Dashboard</p>
          <h2>{registeredClasses.length} registered classes</h2>
          <p>
            {scheduledClasses.length} enrolled • {waitlistedClasses.length} waitlisted
          </p>
        </article>
        <article className={styles.dashboardCard}>
          <p className={styles.dashboardEyebrow}>Upcoming Session</p>
          <h2>{upcomingSessions[0]?.item.classId ?? 'Nothing scheduled'}</h2>
          <p>
            {upcomingSessions[0]
              ? formatUpcomingSession(
                  upcomingSessions[0].item.days,
                  upcomingSessions[0].item.startTime,
                  upcomingSessions[0].item.endTime,
                )
              : 'Add a class or generate a candidate schedule.'}
          </p>
        </article>
        <article className={styles.dashboardCard}>
          <p className={styles.dashboardEyebrow}>Conflict Status</p>
          <h2>{overlaps.length === 0 ? 'Clear' : `${overlaps.length} conflict${overlaps.length === 1 ? '' : 's'}`}</h2>
          <p>{overlaps.length === 0 ? 'Your enrolled schedule is conflict-free.' : 'Resolve overlaps before finalizing.'}</p>
        </article>
      </section>

      <section className={styles.dashboardPanels}>
        <article className={styles.registeredPanel}>
          <div className={styles.panelHeader}>
            <div>
              <p className={styles.dashboardEyebrow}>Registered Courses</p>
              <h2>List view</h2>
            </div>
            <span>{registeredClasses.length} total</span>
          </div>
          <div className={styles.registeredList}>
            {registeredClasses.map((item) => (
              <button
                key={`${item.classId}-${item.enrollmentStatus}`}
                type="button"
                className={styles.registeredCard}
                onClick={() => setActiveScheduleClass(item)}
              >
                <div>
                  <strong>{item.classId}</strong>
                  <span>{item.title}</span>
                </div>
                <div>
                  <span>{item.instructor}</span>
                  <span>
                    {item.days.join('/')} {item.startTime}-{item.endTime}
                  </span>
                  <span>
                    {item.location ?? item.room} • {item.credits} credits
                  </span>
                </div>
                <div>
                  <span className={item.enrollmentStatus === 'Waitlisted' ? styles.waitlistBadge : styles.enrolledBadge}>
                    {item.enrollmentStatus === 'Waitlisted'
                      ? `Waitlist #${item.waitlistPosition ?? 'Pending'}`
                      : 'Enrolled'}
                  </span>
                  <span>
                    {item.enrollmentStatus === 'Waitlisted'
                      ? `${item.availableSeats} seats open`
                      : formatUpcomingSession(item.days, item.startTime, item.endTime)}
                  </span>
                </div>
              </button>
            ))}
            {registeredClasses.length === 0 && <p>No registered classes yet.</p>}
          </div>
        </article>

        <article className={styles.registeredPanel}>
          <div className={styles.panelHeader}>
            <div>
              <p className={styles.dashboardEyebrow}>Upcoming Sessions</p>
              <h2>Next on your calendar</h2>
            </div>
          </div>
          <div className={styles.upcomingList}>
            {upcomingSessions.map(({ item, nextDate }) => (
              <button
                key={`${item.classId}-upcoming`}
                type="button"
                className={styles.upcomingCard}
                onClick={() => setActiveScheduleClass(item)}
              >
                <strong>{item.classId}</strong>
                <span>{item.title}</span>
                <span>{nextDate.toLocaleString(undefined, { weekday: 'short', month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' })}</span>
                <span>
                  {item.instructor} • {item.location ?? item.room}
                </span>
              </button>
            ))}
            {upcomingSessions.length === 0 && <p>No upcoming sessions yet.</p>}
          </div>
        </article>
      </section>

      <SmartEnrollmentPanel
        studentId={studentId}
        onAcceptCandidate={async (candidate) => {
          const result = await applyGeneratedSchedule(candidate);
          if (!result.ok) {
            setInlineError(result.message ?? 'Unable to apply generated schedule.');
            setToast({ message: result.message ?? 'Unable to apply generated schedule.', tone: 'error' });
            return;
          }

          setInlineError(null);
          setToast({ message: 'Generated schedule accepted.', tone: 'success' });
          await refresh();
        }}
      />

      <DndContext
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
              setToast({ message: `${classId} removed from your dashboard`, tone: 'info' });
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
      </DndContext>

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
