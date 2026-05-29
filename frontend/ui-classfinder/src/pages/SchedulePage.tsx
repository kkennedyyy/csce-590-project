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

import { ClassCard } from '../components/ClassCard';
import { ScheduledClassModal } from '../components/ScheduledClassModal';
import { ScheduleGrid } from '../components/ScheduleGrid';
import { SmartEnrollmentPanel } from '../components/SmartEnrollmentPanel';
import { Toast } from '../components/Toast';
import { useClasses } from '../hooks/useClasses';
import { useSchedule } from '../hooks/useSchedule';
import type {
  ClassOffering,
  Day,
  MeetingTime,
  RegisteredClass,
  ScheduledClass,
  SmartEnrollmentCandidate,
} from '../types';
import { formatUpcomingSession, getNextMeetingDate, minutesToTime, timeToMinutes } from '../utils/time';
import { MAX_CREDITS, getOverlaps } from '../utils/validators';
import styles from './Page.module.css';

function createMeetingFromDrop(source: ClassOffering, startTime: string): MeetingTime {
  const duration = timeToMinutes(source.endTime) - timeToMinutes(source.startTime);
  const startMinute = timeToMinutes(startTime);

  return {
    days: source.days,
    startTime,
    endTime: minutesToTime(startMinute + duration),
  };
}

function mapOfferingToScheduledClass(source: ClassOffering): ScheduledClass {
  return {
    sectionId: source.sectionId,
    classId: source.id,
    title: source.title,
    instructor: source.instructor,
    credits: source.credits,
    room: source.room,
    location: source.location ?? source.room,
    term: source.term,
    colorHint: source.colorHint,
    days: source.days,
    startTime: source.startTime,
    endTime: source.endTime,
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
  const [previewCandidate, setPreviewCandidate] = useState<SmartEnrollmentCandidate | null>(null);
  const { filtered, classes, refresh } = useClasses(searchTerm, '', studentId);
  const sensors = useSensors(
    useSensor(PointerSensor, {
      activationConstraint: {
        distance: 8,
      },
    }),
    useSensor(KeyboardSensor),
  );

  const classIndex = useMemo(() => new Map(classes.map((item) => [item.id, item])), [classes]);
  const topSearchMatch = useMemo(() => {
    if (!searchTerm.trim()) {
      return null;
    }

    const normalized = searchTerm.trim().toLowerCase();
    return (
      filtered.find((entry) => entry.item.id.toLowerCase() === normalized)?.item
      ?? filtered.find((entry) => entry.item.title.toLowerCase() === normalized)?.item
      ?? filtered[0]?.item
      ?? null
    );
  }, [filtered, searchTerm]);
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
  const previewSchedule = previewCandidate?.scheduledClasses ?? null;
  const visualizedSchedule = previewSchedule ?? scheduledClasses;
  const visualizedOverlaps = previewSchedule ? getOverlaps(previewSchedule) : overlaps;
  const previewMode = Boolean(previewCandidate);

  useEffect(() => {
    if (!searchTerm.trim()) {
      setSelectedClass(null);
      return;
    }

    setSelectedClass((current) => {
      if (current && filtered.some((entry) => entry.item.id === current.id)) {
        return current;
      }

      return topSearchMatch;
    });
  }, [filtered, searchTerm, topSearchMatch]);

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
    if (previewMode) {
      setToast({ message: 'Apply the previewed smart-enrollment option from the planner panel.', tone: 'info' });
      return;
    }

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
    if (previewMode) {
      return;
    }

    const classId = String(event.active.id).replace('class-', '');
    const picked = classIndex.get(classId) ?? (selectedClass?.id === classId ? selectedClass : null);
    if (picked) {
      setActiveDragClass(picked);
      setGlobalDragState(true);
    }
  };

  const handleDragEnd = async (event: DragEndEvent) => {
    setGlobalDragState(false);
    const dragged = activeDragClass;
    setActiveDragClass(null);

    if (previewMode || !dragged || !event.over?.id) {
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
          {previewMode ? (
            <span className={styles.previewBadge}>Previewing {previewCandidate?.summary}</span>
          ) : overlaps.length > 0 ? (
            <span className={styles.stickyConflictPill}>Conflicts present</span>
          ) : null}
        </section>

        <div className={styles.actions} aria-label="Schedule quick actions" role="group">
          <button
            type="button"
            disabled={previewMode ? false : finalizeBlocked}
            aria-disabled={previewMode ? false : finalizeBlocked}
            onClick={() => {
              void handleFinalizeRegistration();
            }}
          >
            {previewMode ? 'Apply preview from planner' : 'Finalize registration'}
          </button>
          <button
            type="button"
            onClick={() => {
              if (previewMode) {
                setPreviewCandidate(null);
                setToast({ message: 'Returned to your live registered schedule.', tone: 'info' });
                return;
              }

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
            {previewMode ? 'Exit preview' : 'Resolve conflicts'}
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
          <h2>{visualizedOverlaps.length === 0 ? 'Clear' : `${visualizedOverlaps.length} conflict${visualizedOverlaps.length === 1 ? '' : 's'}`}</h2>
          <p>
            {previewMode
              ? 'This preview updates the main calendar without changing your saved schedule.'
              : overlaps.length === 0
                ? 'Your enrolled schedule is conflict-free.'
                : 'Resolve overlaps before finalizing.'}
          </p>
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
        <section className={styles.scheduleStudio}>
          <article className={styles.scheduleStage}>
            <div className={styles.stageHeader}>
              <div>
                <p className={styles.dashboardEyebrow}>Schedule Visualizer</p>
                <h2>{previewMode ? 'Planner preview' : 'Live schedule'}</h2>
              </div>
              <div className={styles.stageMeta}>
                <span>{visualizedSchedule.length} classes shown</span>
                <span>{previewMode ? 'Preview only until applied' : 'Directly reflects registration state'}</span>
              </div>
            </div>

            {previewMode && previewCandidate ? (
              <div className={styles.previewBanner}>
                <div>
                  <strong>{previewCandidate.summary}</strong>
                  <p>{previewCandidate.rationale}</p>
                </div>
                <div className={styles.activeFilters}>
                  {previewCandidate.highlights.map((item) => (
                    <span key={item}>{item}</span>
                  ))}
                </div>
              </div>
            ) : selectedClass ? (
              <div className={styles.previewBanner}>
                <div>
                  <strong>{selectedClass.id} ready to place</strong>
                  <p>
                    Type in the header search to swap the active class, then drag it onto the grid or click a
                    time slot to place it.
                  </p>
                </div>
                <div className={styles.activeFilters}>
                  <span>{searchTerm.trim() ? `${filtered.length} search matches` : 'Manual add ready'}</span>
                </div>
              </div>
            ) : (
              <div className={styles.scheduleHint}>
                <strong>Schedule studio is ready.</strong>
                <p>
                  Use the planner prompt to generate options, or search from the header to stage one class for
                  manual placement.
                </p>
              </div>
            )}

            <ScheduleGrid
              schedule={visualizedSchedule}
              overlaps={visualizedOverlaps}
              readOnly={previewMode}
              helpText={
                previewMode
                  ? 'Preview mode: inspect the generated option on the calendar, then apply it from the planner when it looks right.'
                  : undefined
              }
              onRemoveClass={async (classId) => {
                if (previewMode) {
                  setToast({ message: 'Exit preview mode before editing your live schedule.', tone: 'info' });
                  return;
                }

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
                if (previewMode) {
                  setToast({ message: 'Exit preview mode before placing a class manually.', tone: 'info' });
                  return;
                }

                if (!selectedClass) {
                  setToast({ message: 'Search for a class in the header or use Browse to stage one for placement.', tone: 'info' });
                  return;
                }
                await addWithFeedback(selectedClass, {
                  meetingTime: createMeetingFromDrop(selectedClass, startTime),
                  successMessage: `${selectedClass.id} placed on ${day} ${startTime}`,
                });
              }}
              onOpenClassDetails={(item) => setActiveScheduleClass(item)}
            />
          </article>

          <aside className={styles.scheduleSideRail}>
            <SmartEnrollmentPanel
              studentId={studentId}
              previewCandidateId={previewCandidate?.id}
              onPreviewCandidate={setPreviewCandidate}
              onAcceptCandidate={async (candidate) => {
                const result = await applyGeneratedSchedule(candidate);
                if (!result.ok) {
                  setInlineError(result.message ?? 'Unable to apply generated schedule.');
                  setToast({ message: result.message ?? 'Unable to apply generated schedule.', tone: 'error' });
                  return;
                }

                setInlineError(null);
                setPreviewCandidate(null);
                setToast({ message: 'Generated schedule accepted.', tone: 'success' });
                await refresh();
              }}
            />

            <section className={styles.selectionCard}>
              <div className={styles.panelHeader}>
                <div>
                  <p className={styles.dashboardEyebrow}>Manual Add</p>
                  <h2>{selectedClass ? 'Selected search result' : 'Search to stage a class'}</h2>
                </div>
                <span>{searchTerm.trim() ? `Query: ${searchTerm.trim()}` : 'Header search'}</span>
              </div>

              {selectedClass ? (
                <>
                  <ClassCard
                    item={selectedClass}
                    compact
                    selected
                    dragEnabled={!previewMode}
                    onSelect={(item) => setSelectedClass(item)}
                    onAdd={async (item) => {
                      await addWithFeedback(item, {
                        meetingTime: item.meetingOptions?.[0],
                        successMessage: `${item.id} added`,
                      });
                    }}
                    addLabel="Add now"
                  />
                  <div className={styles.selectionActions}>
                    <button type="button" onClick={() => setActiveScheduleClass(mapOfferingToScheduledClass(selectedClass))}>
                      View details
                    </button>
                    <button type="button" onClick={() => navigate('/browse')}>
                      Browse more matches
                    </button>
                  </div>
                </>
              ) : (
                <div className={styles.scheduleHint}>
                  <strong>No staged class yet.</strong>
                  <p>
                    Search in the header to pull a class into this slot, or use the browse page for the full
                    catalog.
                  </p>
                </div>
              )}
            </section>
          </aside>
        </section>

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
