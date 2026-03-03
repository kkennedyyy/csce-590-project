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
import { SearchBar } from '../components/SearchBar';
import { Toast } from '../components/Toast';
import { useClasses } from '../hooks/useClasses';
import { useSchedule } from '../hooks/useSchedule';
import type { ClassOffering, Day, MeetingTime, ScheduledClass } from '../types';
import { minutesToTime, timeToMinutes } from '../utils/time';
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

export function SchedulePage(): JSX.Element {
  const navigate = useNavigate();
  const { scheduledClasses, overlaps, currentCredits, addClassToSchedule, removeClassFromSchedule, loading } =
    useSchedule();

  const [searchTerm, setSearchTerm] = useState('');
  const [toast, setToast] = useState<{ message: string; tone: 'info' | 'error' | 'success' } | null>(null);
  const [inlineError, setInlineError] = useState<string | null>(null);
  const [selectedClass, setSelectedClass] = useState<ClassOffering | null>(null);
  const [activeDragClass, setActiveDragClass] = useState<ClassOffering | null>(null);
  const [activeScheduleClass, setActiveScheduleClass] = useState<ScheduledClass | null>(null);
  const { filtered, classes } = useClasses(searchTerm);
  const sensors = useSensors(
    useSensor(PointerSensor, {
      activationConstraint: {
        distance: 8,
      },
    }),
    useSensor(KeyboardSensor),
  );

  const suggestions = useMemo(
    () => filtered.slice(0, 5).map((entry) => `${entry.item.id} ${entry.item.title}`),
    [filtered],
  );

  const previewClasses = filtered.slice(0, 5);
  const classIndex = useMemo(() => new Map(classes.map((item) => [item.id, item])), [classes]);

  const finalizeDisabled = overlaps.length > 0;

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
    <main className={styles.container}>
      <div className={styles.row}>
        <SearchBar
          label="schedule"
          value={searchTerm}
          onChange={setSearchTerm}
          suggestions={suggestions}
          placeholder="Search and press Add or drag into the schedule"
        />
      </div>

      <div className={styles.stickyActionsWrap}>
        <div className={styles.actions} aria-label="Schedule quick actions" role="group">
          <button
            type="button"
            disabled={finalizeDisabled}
            aria-disabled={finalizeDisabled}
            onClick={() => setToast({ message: 'Schedule finalized successfully.', tone: 'success' })}
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
              setToast({ message: `${classId} removed from schedule`, tone: 'info' });
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
            <p>Drag a class here or search above. Current credits: {currentCredits} / 19</p>
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
