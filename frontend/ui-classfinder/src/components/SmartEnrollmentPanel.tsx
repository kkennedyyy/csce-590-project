import { useEffect, useMemo, useState } from 'react';

import { fetchClasses } from '../api/api';
import type { ClassOffering, Day, ScheduledClass } from '../types';
import { WEEK_DAYS } from '../types';
import { getOverlaps } from '../utils/validators';
import {
  type SmartEnrollmentCandidate,
  generateCandidateSchedules,
} from '../utils/smartEnrollment';
import { ScheduleGrid } from './ScheduleGrid';
import styles from './SmartEnrollmentPanel.module.css';

interface SmartEnrollmentPanelProps {
  studentId: string;
  onAcceptCandidate: (candidate: ScheduledClass[]) => Promise<void>;
}

export function SmartEnrollmentPanel({
  studentId,
  onAcceptCandidate,
}: SmartEnrollmentPanelProps): JSX.Element {
  const [catalog, setCatalog] = useState<ClassOffering[]>([]);
  const [loadingCatalog, setLoadingCatalog] = useState(false);
  const [requiredCourses, setRequiredCourses] = useState('');
  const [preferredElectives, setPreferredElectives] = useState('');
  const [electiveSlots, setElectiveSlots] = useState(1);
  const [earliestStart, setEarliestStart] = useState('08:00');
  const [latestEnd, setLatestEnd] = useState('18:30');
  const [minimumBreakMinutes, setMinimumBreakMinutes] = useState(15);
  const [blockedDays, setBlockedDays] = useState<Day[]>([]);
  const [preferredNoClassDay, setPreferredNoClassDay] = useState<Day | ''>('');
  const [candidates, setCandidates] = useState<SmartEnrollmentCandidate[]>([]);
  const [selectedCandidateId, setSelectedCandidateId] = useState('');
  const [previewMode, setPreviewMode] = useState<'list' | 'calendar'>('list');
  const [error, setError] = useState<string | null>(null);
  const [accepting, setAccepting] = useState(false);

  useEffect(() => {
    let active = true;

    async function loadCatalog(): Promise<void> {
      try {
        setLoadingCatalog(true);
        const data = await fetchClasses({
          page: 1,
          pageSize: 100,
          studentId,
        });
        if (!active) {
          return;
        }
        setCatalog(data.classes);
      } catch (err) {
        if (!active) {
          return;
        }
        setError(err instanceof Error ? err.message : 'Unable to load classes for smart enrollment.');
      } finally {
        if (active) {
          setLoadingCatalog(false);
        }
      }
    }

    void loadCatalog();
    return () => {
      active = false;
    };
  }, [studentId]);

  const selectedCandidate = useMemo(
    () => candidates.find((item) => item.id === selectedCandidateId) ?? candidates[0] ?? null,
    [candidates, selectedCandidateId],
  );

  const handleGenerate = () => {
    const generated = generateCandidateSchedules(catalog, {
      requiredCourseCodes: splitCourseCodes(requiredCourses),
      preferredElectiveCourseCodes: splitCourseCodes(preferredElectives),
      electiveSlots,
      earliestStart,
      latestEnd,
      blockedDays,
      preferredNoClassDay,
      minimumBreakMinutes,
    });

    setCandidates(generated);
    setSelectedCandidateId(generated[0]?.id ?? '');
    setError(generated.length === 0 ? 'No valid schedules match the current preferences.' : null);
  };

  const previewSchedule = selectedCandidate?.scheduledClasses ?? [];

  return (
    <section className={styles.panel}>
      <div className={styles.header}>
        <div>
          <p className={styles.eyebrow}>Smart Enrollment</p>
          <h2>Generate schedule options</h2>
          <p>Use hard time limits, ranked electives, and break preferences to build conflict-free options.</p>
        </div>
        <div className={styles.meta}>
          <span>{catalog.length} classes loaded</span>
          <span>{candidates.length} options ready</span>
        </div>
      </div>

      <div className={styles.formGrid}>
        <label>
          <span>Required classes</span>
          <input
            type="text"
            value={requiredCourses}
            onChange={(event) => setRequiredCourses(event.target.value)}
            placeholder="CSCE331, MATH200"
          />
        </label>
        <label>
          <span>Preferred electives</span>
          <input
            type="text"
            value={preferredElectives}
            onChange={(event) => setPreferredElectives(event.target.value)}
            placeholder="PHYS201, HIST210"
          />
        </label>
        <label>
          <span>Elective slots</span>
          <input
            type="number"
            min={0}
            max={4}
            value={electiveSlots}
            onChange={(event) => setElectiveSlots(Math.max(0, Number(event.target.value) || 0))}
          />
        </label>
        <label>
          <span>Earliest start</span>
          <input type="time" value={earliestStart} onChange={(event) => setEarliestStart(event.target.value)} />
        </label>
        <label>
          <span>Latest end</span>
          <input type="time" value={latestEnd} onChange={(event) => setLatestEnd(event.target.value)} />
        </label>
        <label>
          <span>Minimum break</span>
          <input
            type="number"
            min={0}
            max={180}
            value={minimumBreakMinutes}
            onChange={(event) => setMinimumBreakMinutes(Math.max(0, Number(event.target.value) || 0))}
          />
        </label>
      </div>

      <div className={styles.daySection}>
        <div>
          <strong>Blocked days</strong>
          <p>Hard constraint</p>
        </div>
        <div className={styles.dayButtons}>
          {WEEK_DAYS.map((day) => {
            const selected = blockedDays.includes(day);
            return (
              <button
                key={day}
                type="button"
                className={selected ? styles.dayButtonActive : styles.dayButton}
                onClick={() =>
                  setBlockedDays((current) =>
                    current.includes(day) ? current.filter((item) => item !== day) : [...current, day],
                  )
                }
              >
                {day}
              </button>
            );
          })}
        </div>
      </div>

      <div className={styles.daySection}>
        <div>
          <strong>Preferred free day</strong>
          <p>Flexible preference</p>
        </div>
        <div className={styles.dayButtons}>
          <button
            type="button"
            className={!preferredNoClassDay ? styles.dayButtonActive : styles.dayButton}
            onClick={() => setPreferredNoClassDay('')}
          >
            None
          </button>
          {WEEK_DAYS.map((day) => (
            <button
              key={day}
              type="button"
              className={preferredNoClassDay === day ? styles.dayButtonActive : styles.dayButton}
              onClick={() => setPreferredNoClassDay(day)}
            >
              {day}
            </button>
          ))}
        </div>
      </div>

      <div className={styles.actions}>
        <button type="button" onClick={handleGenerate} disabled={loadingCatalog}>
          {loadingCatalog ? 'Loading catalog…' : 'Generate schedules'}
        </button>
        <button
          type="button"
          disabled={!selectedCandidate || accepting}
          onClick={async () => {
            if (!selectedCandidate) {
              return;
            }

            setAccepting(true);
            try {
              await onAcceptCandidate(selectedCandidate.scheduledClasses);
            } finally {
              setAccepting(false);
            }
          }}
        >
          {accepting ? 'Accepting…' : 'Accept selected'}
        </button>
      </div>

      {error && <div className={styles.error}>{error}</div>}

      {candidates.length > 0 && (
        <div className={styles.body}>
          <aside className={styles.options}>
            {candidates.map((candidate) => (
              <button
                key={candidate.id}
                type="button"
                className={candidate.id === selectedCandidate?.id ? styles.optionActive : styles.option}
                onClick={() => setSelectedCandidateId(candidate.id)}
              >
                <strong>{candidate.summary}</strong>
                <span>{candidate.scheduledClasses.map((item) => item.classId).join(', ')}</span>
              </button>
            ))}
          </aside>

          {selectedCandidate && (
            <div className={styles.preview}>
              <div className={styles.previewHeader}>
                <div>
                  <h3>{selectedCandidate.summary}</h3>
                  <p>Switch between list and calendar previews before applying the schedule.</p>
                </div>
                <div className={styles.previewTabs}>
                  <button
                    type="button"
                    className={previewMode === 'list' ? styles.tabActive : styles.tab}
                    onClick={() => setPreviewMode('list')}
                  >
                    List
                  </button>
                  <button
                    type="button"
                    className={previewMode === 'calendar' ? styles.tabActive : styles.tab}
                    onClick={() => setPreviewMode('calendar')}
                  >
                    Calendar
                  </button>
                </div>
              </div>

              {previewMode === 'list' ? (
                <div className={styles.previewList}>
                  {selectedCandidate.scheduledClasses.map((item) => (
                    <article key={item.classId} className={styles.previewCard}>
                      <strong>{item.classId}</strong>
                      <span>{item.title}</span>
                      <span>
                        {item.days.join('/')} {item.startTime}-{item.endTime}
                      </span>
                      <span>
                        {item.instructor} • {item.location ?? item.room} • {item.credits} credits
                      </span>
                    </article>
                  ))}
                </div>
              ) : (
                <div className={styles.calendarPreview}>
                  <ScheduleGrid
                    schedule={previewSchedule}
                    overlaps={getOverlaps(previewSchedule)}
                    onRemoveClass={async () => {}}
                    onKeyboardAdd={async () => {}}
                    onOpenClassDetails={() => {}}
                  />
                </div>
              )}
            </div>
          )}
        </div>
      )}
    </section>
  );
}

function splitCourseCodes(value: string): string[] {
  return value
    .split(',')
    .map((item) => item.trim().toUpperCase())
    .filter((item, index, all) => item.length > 0 && all.indexOf(item) === index);
}
