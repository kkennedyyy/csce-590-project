import { useMemo, useState } from 'react';

import type { Day, Overlap, ScheduledClass } from '../types';
import { WEEK_DAYS } from '../types';
import { DEFAULT_TIME_WINDOW, timeToMinutes } from '../utils/time';
import { DayColumn } from './DayColumn';
import styles from './ScheduleGrid.module.css';

interface ScheduleGridProps {
  schedule: ScheduledClass[];
  overlaps: Overlap[];
  onRemoveClass: (classId: string) => Promise<void>;
  onKeyboardAdd: (day: Day, startTime: string) => Promise<void>;
  onOpenClassDetails: (item: ScheduledClass) => void;
}

export function ScheduleGrid({
  schedule,
  overlaps,
  onRemoveClass,
  onKeyboardAdd,
  onOpenClassDetails,
}: ScheduleGridProps): JSX.Element {
  const [mobileDay, setMobileDay] = useState<Day>('Mon');

  const startMinute = timeToMinutes(DEFAULT_TIME_WINDOW.start);
  const endMinute = timeToMinutes(DEFAULT_TIME_WINDOW.end);
  const pxPerMinute = 2 / 3;
  const gridHeight = (endMinute - startMinute) * pxPerMinute;

  const scheduleByDay = useMemo(
    () =>
      WEEK_DAYS.reduce<Record<Day, ScheduledClass[]>>(
        (acc, day) => {
          acc[day] = schedule.filter((item) => item.days.includes(day));
          return acc;
        },
        {
          Mon: [],
          Tue: [],
          Wed: [],
          Thu: [],
          Fri: [],
        },
      ),
    [schedule],
  );

  const overlapsByDay = useMemo(
    () =>
      WEEK_DAYS.reduce<Record<Day, typeof overlaps>>(
        (acc, day) => {
          acc[day] = overlaps.filter((item) => item.day === day);
          return acc;
        },
        {
          Mon: [],
          Tue: [],
          Wed: [],
          Thu: [],
          Fri: [],
        },
      ),
    [overlaps],
  );

  return (
    <section className={styles.wrapper} aria-label="Schedule Grid">
      <div className={styles.headerRow}>
        <div className={styles.timeColHeader}>Time</div>
        {WEEK_DAYS.map((day) => (
          <button
            key={day}
            type="button"
            className={`${styles.daySwitch} ${mobileDay === day ? styles.daySwitchActive : ''}`}
            onClick={() => setMobileDay(day)}
          >
            {day}
          </button>
        ))}
      </div>

      <div className={styles.grid} style={{ minHeight: `${gridHeight + 4}px` }}>
        <aside className={styles.timeCol} aria-hidden="true">
          {Array.from({ length: 25 }).map((_, index) => {
            const minute = startMinute + index * 30;
            const label = `${String(Math.floor(minute / 60)).padStart(2, '0')}:${String(
              minute % 60,
            ).padStart(2, '0')}`;

            return <span key={label}>{label}</span>;
          })}
        </aside>

        {WEEK_DAYS.map((day) => (
          <div
            key={day}
            className={`${styles.dayCol} ${mobileDay !== day ? styles.mobileHidden : ''}`}
            style={{ minHeight: `${gridHeight}px` }}
          >
            <DayColumn
              day={day}
              classes={scheduleByDay[day]}
              overlaps={overlapsByDay[day]}
              pxPerMinute={pxPerMinute}
              onRemoveClass={(classId) => {
                void onRemoveClass(classId);
              }}
              onKeyboardAdd={(pickedDay, startTime) => {
                void onKeyboardAdd(pickedDay, startTime);
              }}
              onOpenClassDetails={onOpenClassDetails}
            />
          </div>
        ))}
      </div>

      <p className={styles.helpText}>
        Keyboard help: focus a class card and press <kbd>A</kbd> to add. Focus a time slot and press
        <kbd>A</kbd> to add the selected class to that slot.
      </p>
    </section>
  );
}
