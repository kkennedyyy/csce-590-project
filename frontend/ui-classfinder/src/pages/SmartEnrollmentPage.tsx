import { type FormEvent, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';

import { acceptSmartSchedule, fetchClasses, requestSmartSchedules } from '../api/api';
import { ScheduleGrid } from '../components/ScheduleGrid';
import { ScheduledClassModal } from '../components/ScheduledClassModal';
import { Toast } from '../components/Toast';
import { useClasses } from '../hooks/useClasses';
import { useSchedule } from '../hooks/useSchedule';
import type { Day, ScheduledClass, SmartGeneratedSchedule } from '../types';
import { normalizeClockTime } from '../utils/time';
import { buildConflictLabel, getOverlaps } from '../utils/validators';
import styles from './Page.module.css';

const DAY_OPTIONS: Day[] = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri'];

interface SmartCourseOption {
  optionId: string;
  courseKey: string;
  title: string;
}

function normalizeCourseKey(rawId: string): string {
  return rawId.replace(/-\d+$/u, '').trim().toUpperCase();
}

function normalizeSmartDays(input: string): Day[] {
  const normalized: Day[] = [];

  const pushDay = (day: Day): void => {
    if (!normalized.includes(day)) {
      normalized.push(day);
    }
  };

  const mapToken = (token: string): Day | null => {
    const upper = token.trim().toUpperCase();
    if (!upper) return null;
    if (upper === 'MON' || upper === 'MONDAY' || upper === 'M') return 'Mon';
    if (upper === 'TUE' || upper === 'TUESDAY' || upper === 'TU' || upper === 'T') return 'Tue';
    if (upper === 'WED' || upper === 'WEDNESDAY' || upper === 'W') return 'Wed';
    if (upper === 'THU' || upper === 'THURSDAY' || upper === 'TH' || upper === 'R') return 'Thu';
    if (upper === 'FRI' || upper === 'FRIDAY' || upper === 'F') return 'Fri';
    return null;
  };

  for (const chunk of input.split(/[\s,\/]+/).filter(Boolean)) {
    const mapped = mapToken(chunk);
    if (mapped) {
      pushDay(mapped);
      continue;
    }

    const compact = chunk.toUpperCase();
    for (let index = 0; index < compact.length; index += 1) {
      const token = compact[index];
      if (token === 'M') pushDay('Mon');
      else if (token === 'T') pushDay('Tue');
      else if (token === 'W') pushDay('Wed');
      else if (token === 'R' || token === 'H') pushDay('Thu');
      else if (token === 'F') pushDay('Fri');
    }
  }

  return normalized;
}

export function SmartEnrollmentPage(): JSX.Element {
  const { studentId, initialize, scheduledClasses } = useSchedule();
  const { classes, loading: classLoading } = useClasses('', '', studentId);
  const [allClasses, setAllClasses] = useState<typeof classes>([]);
  const [allClassesLoading, setAllClassesLoading] = useState(false);

  const [selectedRequiredId, setSelectedRequiredId] = useState('');
  const [selectedElectiveId, setSelectedElectiveId] = useState('');
  const [requiredCourseIds, setRequiredCourseIds] = useState<string[]>([]);
  const [preferredElectiveIds, setPreferredElectiveIds] = useState<string[]>([]);
  const [minBreakMinutes, setMinBreakMinutes] = useState(15);
  const [flexibilityLevel, setFlexibilityLevel] = useState(3);
  const [minCredits, setMinCredits] = useState(12);
  const [maxCredits, setMaxCredits] = useState(18);
  const [preferredDaysOff, setPreferredDaysOff] = useState<Day[]>([]);
  const [preferOnlineClasses, setPreferOnlineClasses] = useState(false);
  const [avoidEarlyClasses, setAvoidEarlyClasses] = useState(false);
  const [loading, setLoading] = useState(false);
  const [accepting, setAccepting] = useState(false);
  const [schedules, setSchedules] = useState<SmartGeneratedSchedule[]>([]);
  const [selectedScheduleId, setSelectedScheduleId] = useState<number | null>(null);
  const [inlineError, setInlineError] = useState<string | null>(null);
  const [activeScheduleClass, setActiveScheduleClass] = useState<ScheduledClass | null>(null);
  const [toast, setToast] = useState<{ message: string; tone: 'error' | 'info' | 'success' } | null>(null);

  const selectedSchedule = useMemo(() => {
    if (!schedules.length) {
      return null;
    }

    if (selectedScheduleId === null) {
      return schedules[0];
    }

    return schedules.find((item) => item.scheduleId === selectedScheduleId) ?? schedules[0];
  }, [schedules, selectedScheduleId]);

  useEffect(() => {
    let isMounted = true;

    async function loadAllClasses(): Promise<void> {
      if (!studentId) {
        return;
      }

      setAllClassesLoading(true);
      try {
        const results: typeof classes = [];
        let page = 1;
        let hasMore = true;

        while (hasMore) {
          const response = await fetchClasses({
            page,
            pageSize: 100,
            search: '',
            department: '',
            studentId,
          });
          results.push(...response.classes);
          hasMore = response.hasMore;
          page += 1;
        }

        if (isMounted) {
          setAllClasses(results);
        }
      } finally {
        if (isMounted) {
          setAllClassesLoading(false);
        }
      }
    }

    void loadAllClasses();

    return () => {
      isMounted = false;
    };
  }, [studentId]);

  const selectableClasses = useMemo<SmartCourseOption[]>(() => {
    const source = allClasses.length > 0 ? allClasses : classes;
    const sorted = [...source].sort((a, b) => a.id.localeCompare(b.id));
    const deduped = new Map<string, SmartCourseOption>();

    sorted.forEach((item) => {
      const key = normalizeCourseKey(item.id);
      const optionId = item.sectionId ? String(item.sectionId) : '';
      if (!optionId) {
        return;
      }

      if (!deduped.has(key)) {
        deduped.set(key, {
          optionId,
          courseKey: key,
          title: item.title,
        });
      }
    });

    return Array.from(deduped.values());
  }, [allClasses, classes]);

  const classTermBySectionId = useMemo(() => {
    const source = allClasses.length > 0 ? allClasses : classes;
    const map = new Map<number, string>();
    source.forEach((item) => {
      if (item.sectionId) {
        map.set(item.sectionId, item.term);
      }
    });
    return map;
  }, [allClasses, classes]);

  async function handleGenerate(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    setLoading(true);
    setInlineError(null);

    try {
      if (minCredits > maxCredits) {
        throw new Error('Minimum credits cannot be greater than maximum credits.');
      }

      const generated = await requestSmartSchedules({
        studentId,
        minBreakMinutes,
        flexibilityLevel,
        minCredits,
        maxCredits,
        requiredCourseIds,
        preferredElectiveIds,
        preferredDaysOff,
        preferOnlineClasses,
        avoidEarlyClasses,
      });

      setSchedules(generated);
      setSelectedScheduleId(generated[0]?.scheduleId ?? null);

      if (!generated.length) {
        setToast({ message: 'No schedule options were generated. Try fewer restrictions.', tone: 'info' });
      } else {
        setToast({ message: `Generated ${generated.length} schedule option(s).`, tone: 'success' });
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Unable to generate schedules.';
      setInlineError(message);
      setToast({ message, tone: 'error' });
    } finally {
      setLoading(false);
    }
  }

  function addRequiredCourse(): void {
    if (!selectedRequiredId || requiredCourseIds.includes(selectedRequiredId)) {
      return;
    }

    setRequiredCourseIds((prev) => [...prev, selectedRequiredId]);
    setPreferredElectiveIds((prev) => prev.filter((item) => item !== selectedRequiredId));
  }

  function addPreferredElective(): void {
    if (!selectedElectiveId || preferredElectiveIds.includes(selectedElectiveId)) {
      return;
    }

    setPreferredElectiveIds((prev) => [...prev, selectedElectiveId]);
    setRequiredCourseIds((prev) => prev.filter((item) => item !== selectedElectiveId));
  }

  function toggleDayOff(day: Day): void {
    setPreferredDaysOff((prev) =>
      prev.includes(day) ? prev.filter((item) => item !== day) : [...prev, day],
    );
  }

  function labelForClassId(classId: string): string {
    const found = selectableClasses.find((item) => item.optionId === classId);
    return found ? `${found.courseKey} - ${found.title}` : classId;
  }

  async function handleAcceptSelected(): Promise<void> {
    if (!selectedSchedule) {
      setToast({ message: 'Generate and select a schedule first.', tone: 'info' });
      return;
    }

    if (acceptBlockers.length > 0) {
      const message = acceptBlockers.join(' ');
      setInlineError(message);
      setToast({ message, tone: 'error' });
      return;
    }

    setAccepting(true);
    setInlineError(null);

    try {
      await acceptSmartSchedule({
        studentId,
        schedule: selectedSchedule,
      });

      await initialize();
      setToast({ message: 'Schedule accepted and added to your planner.', tone: 'success' });
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Unable to accept this schedule.';
      setInlineError(message);
      setToast({ message, tone: 'error' });
    } finally {
      setAccepting(false);
    }
  }

  function parseScheduleClass(item: SmartGeneratedSchedule['classes'][number]): {
    days: string[];
    startTime: string;
    endTime: string;
  } {
    if (item.days && item.startTime && item.endTime) {
      return {
        days: item.days,
        startTime: normalizeClockTime(item.startTime, '08:00'),
        endTime: normalizeClockTime(item.endTime, '09:00'),
      };
    }

    const raw = item.daysTimes ?? '';
    const timeMatch = raw.match(/(\d{1,2}:\d{2}(?::\d{2})?\s*(?:AM|PM)?)\s*-\s*(\d{1,2}:\d{2}(?::\d{2})?\s*(?:AM|PM)?)/i);
    const startTime = normalizeClockTime(timeMatch ? timeMatch[1] : '', '08:00');
    const endTime = normalizeClockTime(timeMatch ? timeMatch[2] : '', '09:00');

    const daysPart = timeMatch ? raw.replace(timeMatch[0], '').trim() : raw.trim();
    const normalizedDays = normalizeSmartDays(daysPart);

    return {
      days: normalizedDays,
      startTime,
      endTime,
    };
  }

  const selectedScheduleGrid = useMemo<ScheduledClass[]>(() => {
    if (!selectedSchedule) {
      return [];
    }

    return selectedSchedule.classes
      .map((item) => {
        const parsed = parseScheduleClass(item);
        const days = parsed.days.filter((day): day is Day => DAY_OPTIONS.includes(day as Day));
        if (days.length === 0) {
          return null;
        }

        return {
          sectionId: item.classId,
          classId: item.classCode,
          title: item.className,
          instructor: item.instructorName,
          credits: item.credits,
          room: item.location,
          location: item.location,
          term: item.term ?? classTermBySectionId.get(item.classId) ?? 'Unknown semester',
          days,
          startTime: parsed.startTime,
          endTime: parsed.endTime,
          colorHint:
            item.role === 'required'
              ? 'green'
              : item.role === 'preferred-elective'
                ? 'blue'
                : 'red',
        } as ScheduledClass;
      })
      .filter((item): item is ScheduledClass => Boolean(item));
  }, [classTermBySectionId, selectedSchedule]);

  const selectedScheduleOverlaps = useMemo(() => getOverlaps(selectedScheduleGrid), [selectedScheduleGrid]);

  const noteLines = useMemo(() => {
    if (!selectedSchedule?.warnings) {
      return [];
    }

    return selectedSchedule.warnings
      .map((line: string) => line.trim())
      .filter((line: string) => line.length > 0);
  }, [selectedSchedule]);

  const selectedRequiredPreferenceWarnings = useMemo(
    () => noteLines.filter((line) => line.toLowerCase().startsWith('required class')),
    [noteLines],
  );

  const overlapWarnings = useMemo(() => {
    const labels = selectedScheduleOverlaps.map((overlap) => buildConflictLabel(overlap));
    return Array.from(new Set(labels));
  }, [selectedScheduleOverlaps]);

  const duplicateClassWarnings = useMemo(() => {
    const existingIds = new Set(scheduledClasses.map((item) => item.classId.trim().toUpperCase()));
    const duplicates = selectedScheduleGrid
      .map((item) => item.classId.trim())
      .filter((classId) => existingIds.has(classId.toUpperCase()));

    return Array.from(new Set(duplicates));
  }, [scheduledClasses, selectedScheduleGrid]);

  const unvalidatedScheduleWarnings = useMemo(() => {
    if (!selectedSchedule) {
      return [] as string[];
    }

    const expectedCount = selectedSchedule.classes.length;
    const parsedCount = selectedScheduleGrid.length;
    if (parsedCount < expectedCount) {
      return [
        `Unable to fully validate conflicts: ${expectedCount - parsedCount} class(es) could not be mapped to a valid day/time slot.`,
      ];
    }

    return [] as string[];
  }, [selectedSchedule, selectedScheduleGrid.length]);

  const acceptBlockers = useMemo(() => {
    const blockers: string[] = [];

    if ((selectedSchedule?.hasConflicts ?? false) || overlapWarnings.length > 0) {
      blockers.push('This smart schedule has time conflicts. Resolve conflicts before enrolling.');
    }

    if (duplicateClassWarnings.length > 0) {
      blockers.push(`Already enrolled in: ${duplicateClassWarnings.join(', ')}.`);
    }

    blockers.push(...unvalidatedScheduleWarnings);

    return blockers;
  }, [duplicateClassWarnings, overlapWarnings.length, selectedSchedule?.hasConflicts, unvalidatedScheduleWarnings]);

  return (
    <main className={styles.container}>
      <section className={styles.smartCard}>
        <h2>Smart Enrollment</h2>
        <p className={styles.smartSubtitle}>
          Generate optimized class combinations from your required courses and preferences.
        </p>

        <form className={styles.smartForm} onSubmit={(event) => void handleGenerate(event)}>
          <label htmlFor="requiredCourseSelect">Required classes</label>
          <div className={styles.smartPickerRow}>
            <select
              id="requiredCourseSelect"
              value={selectedRequiredId}
              onChange={(event) => setSelectedRequiredId(event.target.value)}
            >
              <option value="">Select a class...</option>
              {selectableClasses.map((item) => (
                <option key={item.courseKey} value={item.optionId}>
                  {item.courseKey} - {item.title}
                </option>
              ))}
            </select>
            <button type="button" onClick={addRequiredCourse} disabled={!selectedRequiredId}>
              Add required
            </button>
          </div>

          {requiredCourseIds.length > 0 && (
            <ul className={styles.smartTagList}>
              {requiredCourseIds.map((item) => (
                <li key={item}>
                  <span>{labelForClassId(item)}</span>
                  <button
                    type="button"
                    onClick={() => setRequiredCourseIds((prev) => prev.filter((entry) => entry !== item))}
                    aria-label={`Remove ${item}`}
                  >
                    Remove
                  </button>
                </li>
              ))}
            </ul>
          )}

          <label htmlFor="electiveCourseSelect">Preferred electives</label>
          <div className={styles.smartPickerRow}>
            <select
              id="electiveCourseSelect"
              value={selectedElectiveId}
              onChange={(event) => setSelectedElectiveId(event.target.value)}
            >
              <option value="">Select an elective...</option>
              {selectableClasses.map((item) => (
                <option key={`elective-${item.courseKey}`} value={item.optionId}>
                  {item.courseKey} - {item.title}
                </option>
              ))}
            </select>
            <button type="button" onClick={addPreferredElective} disabled={!selectedElectiveId}>
              Add elective
            </button>
          </div>

          {preferredElectiveIds.length > 0 && (
            <ul className={styles.smartTagList}>
              {preferredElectiveIds.map((item) => (
                <li key={item}>
                  <span>{labelForClassId(item)}</span>
                  <button
                    type="button"
                    onClick={() => setPreferredElectiveIds((prev) => prev.filter((entry) => entry !== item))}
                    aria-label={`Remove ${item}`}
                  >
                    Remove
                  </button>
                </li>
              ))}
            </ul>
          )}

          <div className={styles.smartFormGrid}>
            <div>
              <label htmlFor="minBreak">Minimum break (minutes)</label>
              <input
                id="minBreak"
                type="number"
                min={0}
                max={120}
                value={minBreakMinutes}
                onChange={(event) => setMinBreakMinutes(Number.parseInt(event.target.value || '0', 10))}
              />
            </div>
            <div>
              <label htmlFor="flexibility">Flexibility (1-5)</label>
              <input
                id="flexibility"
                type="number"
                min={1}
                max={5}
                value={flexibilityLevel}
                onChange={(event) => setFlexibilityLevel(Number.parseInt(event.target.value || '1', 10))}
              />
            </div>
            <div>
              <label htmlFor="minCredits">Minimum credits</label>
              <input
                id="minCredits"
                type="number"
                min={0}
                max={21}
                value={minCredits}
                onChange={(event) => setMinCredits(Number.parseInt(event.target.value || '0', 10))}
              />
            </div>
            <div>
              <label htmlFor="maxCredits">Maximum credits</label>
              <input
                id="maxCredits"
                type="number"
                min={0}
                max={21}
                value={maxCredits}
                onChange={(event) => setMaxCredits(Number.parseInt(event.target.value || '0', 10))}
              />
            </div>
          </div>

          {(classLoading || allClassesLoading) && <span className={styles.smartHint}>Loading class options...</span>}

          <div className={styles.smartDayGroup}>
            <span>Preferred days off</span>
            <div className={styles.smartDayOptions}>
              {DAY_OPTIONS.map((day) => (
                <label key={day} className={styles.smartCheckLabel}>
                  <input
                    type="checkbox"
                    checked={preferredDaysOff.includes(day)}
                    onChange={() => toggleDayOff(day)}
                  />
                  {day}
                </label>
              ))}
            </div>
          </div>

          <div className={styles.smartOptionsStack}>
            <label className={styles.smartCheckLabel}>
              <input
                type="checkbox"
                checked={avoidEarlyClasses}
                onChange={(event) => setAvoidEarlyClasses(event.target.checked)}
              />
              Prefer later start times (avoid early classes)
            </label>
            <label className={styles.smartCheckLabel}>
              <input
                type="checkbox"
                checked={preferOnlineClasses}
                onChange={(event) => setPreferOnlineClasses(event.target.checked)}
              />
              Prefer online/remote sections when possible
            </label>
          </div>

          <div className={styles.smartActions}>
            <button type="submit" disabled={loading}>
              {loading ? 'Generating...' : 'Generate schedules'}
            </button>
            <Link to="/schedule" className={styles.smartLinkButton}>
              Back to schedule
            </Link>
          </div>
        </form>

        {inlineError && (
          <p className={styles.errorText} role="alert">
            {inlineError}
          </p>
        )}
      </section>

      {schedules.length > 0 && (
        <section className={styles.smartResults}>
          <h3>Generated options</h3>
          <div className={styles.smartOptionTabs}>
            {schedules.map((option) => (
              <button
                key={option.scheduleId}
                type="button"
                className={selectedSchedule?.scheduleId === option.scheduleId ? styles.smartOptionActive : ''}
                onClick={() => setSelectedScheduleId(option.scheduleId)}
              >
                {option.title || `Option ${option.scheduleId}`} ({option.totalCredits} credits)
              </button>
            ))}
          </div>

          {selectedSchedule && (
            <>
              {noteLines.length > 0 && (
                <div className={styles.smartNotes} role="status" aria-live="polite">
                  <strong>Schedule insights</strong>
                  <ul>
                    {noteLines.map((line) => (
                      <li key={line}>{line}</li>
                    ))}
                  </ul>
                </div>
              )}

              {selectedRequiredPreferenceWarnings.length > 0 && (
                <div className={styles.smartWarnBox} role="status" aria-live="polite">
                  <strong>Required classes vs your preferences</strong>
                  <ul>
                    {selectedRequiredPreferenceWarnings.map((line) => (
                      <li key={line}>{line}</li>
                    ))}
                  </ul>
                </div>
              )}

              {(overlapWarnings.length > 0 || duplicateClassWarnings.length > 0 || unvalidatedScheduleWarnings.length > 0) && (
                <div className={styles.smartWarnBox} role="alert" aria-live="assertive">
                  <strong>Enrollment blockers</strong>
                  <ul>
                    {overlapWarnings.map((line) => (
                      <li key={line}>{line}</li>
                    ))}
                    {duplicateClassWarnings.length > 0 && (
                      <li>Already enrolled in: {duplicateClassWarnings.join(', ')}.</li>
                    )}
                    {unvalidatedScheduleWarnings.map((line) => (
                      <li key={line}>{line}</li>
                    ))}
                  </ul>
                </div>
              )}

              <ul className={styles.smartClassList}>
                {selectedSchedule.classes.map((item) => (
                  <li key={`${selectedSchedule.scheduleId}-${item.classId}`}>
                    <strong>{item.classCode}</strong> - {item.className}
                    <span>
                      {item.instructorName} | {item.daysTimes} | {item.location} | {item.term ?? classTermBySectionId.get(item.classId) ?? 'Unknown semester'} | {item.credits} credits
                    </span>
                  </li>
                ))}
              </ul>

              <div className={styles.smartCalendar}>
                <h4>Calendar view</h4>
                <div className={styles.smartLegend}>
                  <span className={`${styles.smartLegendItem} ${styles.smartLegendRequired}`}>Required</span>
                  <span className={`${styles.smartLegendItem} ${styles.smartLegendPreferred}`}>Preferred elective</span>
                  <span className={`${styles.smartLegendItem} ${styles.smartLegendReplacement}`}>Suggested replacement/additional</span>
                </div>
                <ScheduleGrid
                  schedule={selectedScheduleGrid}
                  overlaps={selectedScheduleOverlaps}
                  onRemoveClass={async () => Promise.resolve()}
                  onKeyboardAdd={async () => Promise.resolve()}
                  onOpenClassDetails={(item) => setActiveScheduleClass(item)}
                />
              </div>

              <div className={styles.smartActions}>
                <button
                  type="button"
                  onClick={() => void handleAcceptSelected()}
                  disabled={accepting || acceptBlockers.length > 0}
                >
                  {accepting ? 'Applying...' : 'Accept selected schedule'}
                </button>
                <span className={styles.smartHint}>
                  {acceptBlockers.length > 0
                    ? 'Resolve blockers above before enrolling this smart schedule.'
                    : 'One click enrolls this option and updates your main schedule.'}
                </span>
              </div>
            </>
          )}
        </section>
      )}

      <ScheduledClassModal item={activeScheduleClass} onClose={() => setActiveScheduleClass(null)} />

      {toast && <Toast tone={toast.tone} message={toast.message} onClose={() => setToast(null)} />}
    </main>
  );
}
