import { useEffect, useMemo, useState } from 'react';

import {
  ApiError,
  fetchTeacherCatalog,
  fetchTeacherClasses,
  fetchTeacherRoster,
  removeStudentFromTeacherClass,
  updateTeacherClass,
} from '../api/api';
import { ClassCard } from '../components/ClassCard';
import { SearchBar } from '../components/SearchBar';
import { Toast } from '../components/Toast';
import { useSchedule } from '../hooks/useSchedule';
import { useAuthStore } from '../store/authStore';
import { WEEK_DAYS, type ClassOffering, type Day, type TeacherCatalog, type TeacherRoster } from '../types';
import styles from './Page.module.css';

interface TeacherDraft {
  title: string;
  location: string;
  capacity: number;
  days: Day[];
  startTime: string;
  endTime: string;
}

export function TeachersPage(): JSX.Element {
  const user = useAuthStore((state) => state.user);

  if (user?.role === 'teacher') {
    return <TeacherWorkspace teacherId={user.userId} teacherName={user.name} />;
  }

  return <StudentTeacherCatalog />;
}

function StudentTeacherCatalog(): JSX.Element {
  const [search, setSearch] = useState('');
  const [department, setDepartment] = useState('');
  const [teachers, setTeachers] = useState<TeacherCatalog[]>([]);
  const [departments, setDepartments] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [toast, setToast] = useState<{ message: string; tone: 'error' | 'info' | 'success' } | null>(null);

  const { studentId, registeredClasses, addClassToSchedule, removeClassFromSchedule } = useSchedule();
  const registrationsById = useMemo(
    () => new Map(registeredClasses.map((item) => [item.classId, item])),
    [registeredClasses],
  );
  const activeFilters = useMemo(
    () =>
      [
        search.trim() ? `Search: ${search.trim()}` : null,
        department ? `Department: ${department}` : null,
      ].filter(Boolean) as string[],
    [department, search],
  );
  const suggestions = useMemo(
    () =>
      Array.from(
        new Set([
          ...teachers.map((teacher) => teacher.name),
          ...teachers.map((teacher) => teacher.department),
          ...teachers.flatMap((teacher) =>
            teacher.classes.slice(0, 2).map((item) => `${item.id} ${item.title}`),
          ),
        ]),
      ),
    [teachers],
  );

  async function loadCatalog(): Promise<void> {
    try {
      setLoading(true);
      const data = await fetchTeacherCatalog({
        search,
        department,
        studentId,
      });
      setTeachers(data.teachers);
      setDepartments(data.departments);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load teacher catalog.');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void loadCatalog();
  }, [search, department, studentId]);

  async function handlePrimaryAction(item: ClassOffering): Promise<void> {
    const registration = registrationsById.get(item.id);
    const isRegistered = Boolean(registration) || item.isStudentEnrolled || item.isStudentWaitlisted;

    if (isRegistered) {
      const confirmed = window.confirm(
        `Remove ${item.id} from your ${registration?.enrollmentStatus === 'Waitlisted' || item.isStudentWaitlisted ? 'waitlist' : 'schedule'}?`,
      );
      if (!confirmed) {
        return;
      }

      const result = await removeClassFromSchedule(item.id);
      if (!result.ok) {
        setToast({ message: result.message ?? 'Could not drop class.', tone: 'error' });
        return;
      }

      setToast({
        message:
          registration?.enrollmentStatus === 'Waitlisted' || item.isStudentWaitlisted
            ? `${item.id} removed from your waitlist`
            : `${item.id} removed from your schedule`,
        tone: 'info',
      });
      await loadCatalog();
      return;
    }

    const result = await addClassToSchedule(item);
    if (!result.ok) {
      setToast({ message: result.message ?? 'Could not enroll in class.', tone: 'error' });
      return;
    }

    setToast({ message: result.message ?? `${item.id} enrolled successfully`, tone: result.message ? 'info' : 'success' });
    await loadCatalog();
  }

  return (
    <main className={styles.container}>
      <div className={styles.filterBar}>
        <SearchBar
          label="teachers"
          value={search}
          onChange={setSearch}
          suggestions={suggestions}
          placeholder="Search instructors, classes, or departments"
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

      {error && (
        <div className={styles.inlineError} role="alert">
          {error}
        </div>
      )}

      <div className={styles.teacherGrid}>
        {teachers.map((teacher) => (
          <section key={teacher.teacherId} className={styles.teacherCard}>
            <header className={styles.teacherHeader}>
              <div>
                <h2>{teacher.name}</h2>
                <p>{teacher.department}</p>
              </div>
              <span>{teacher.classes.length} classes</span>
            </header>
            <p className={styles.teacherEmail}>{teacher.email}</p>
            <div className={styles.teacherClasses}>
              {teacher.classes.map((item) => {
                const registration = registrationsById.get(item.id);
                const isEnrolled = registration?.enrollmentStatus === 'Enrolled' || item.isStudentEnrolled;
                const isWaitlisted = registration?.enrollmentStatus === 'Waitlisted' || item.isStudentWaitlisted;
                const resolvedItem: ClassOffering = {
                  ...item,
                  isStudentEnrolled: isEnrolled,
                  isStudentWaitlisted: isWaitlisted,
                  studentWaitlistPosition: registration?.waitlistPosition ?? item.studentWaitlistPosition ?? null,
                  enrollmentStatus: (
                    isEnrolled ? 'Enrolled' : isWaitlisted ? 'Waitlisted' : 'NotEnrolled'
                  ) as ClassOffering['enrollmentStatus'],
                };

                return (
                  <ClassCard
                    key={item.id}
                    item={resolvedItem}
                    onAdd={(classItem) => {
                      void handlePrimaryAction(classItem);
                    }}
                    addLabel={isEnrolled || isWaitlisted ? 'Disenroll' : 'Enroll'}
                    dragEnabled={false}
                    statusBadge={
                      isEnrolled
                        ? 'Enrolled'
                        : isWaitlisted
                          ? `Waitlist #${registration?.waitlistPosition ?? item.studentWaitlistPosition ?? 'Pending'}`
                          : undefined
                    }
                  />
                );
              })}
            </div>
          </section>
        ))}
      </div>

      {!loading && teachers.length === 0 && (
        <div className={styles.emptyState}>
          <h2>No instructors match the current filters.</h2>
          <p>Try a different department or a broader search term.</p>
        </div>
      )}

      {loading && <p className={styles.loadingText}>Loading instructor catalog...</p>}
      {toast && <Toast tone={toast.tone} message={toast.message} onClose={() => setToast(null)} />}
    </main>
  );
}

function TeacherWorkspace({ teacherId, teacherName }: { teacherId: string; teacherName: string }): JSX.Element {
  const [classes, setClasses] = useState<ClassOffering[]>([]);
  const [selectedClassId, setSelectedClassId] = useState('');
  const [roster, setRoster] = useState<TeacherRoster | null>(null);
  const [draft, setDraft] = useState<TeacherDraft | null>(null);
  const [removedStudentIds, setRemovedStudentIds] = useState<string[]>([]);
  const [loadingClasses, setLoadingClasses] = useState(false);
  const [loadingRoster, setLoadingRoster] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [toast, setToast] = useState<{ message: string; tone: 'error' | 'info' | 'success' } | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function loadClasses(): Promise<void> {
      try {
        setLoadingClasses(true);
        const nextClasses = await fetchTeacherClasses(teacherId);
        if (cancelled) {
          return;
        }

        setClasses(nextClasses);
        setSelectedClassId((current) => {
          if (current && nextClasses.some((item) => item.id === current)) {
            return current;
          }

          return nextClasses[0]?.id ?? '';
        });
        setError(null);
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : 'Failed to load your classes.');
        }
      } finally {
        if (!cancelled) {
          setLoadingClasses(false);
        }
      }
    }

    void loadClasses();
    return () => {
      cancelled = true;
    };
  }, [teacherId]);

  useEffect(() => {
    if (!selectedClassId) {
      setRoster(null);
      setDraft(null);
      setRemovedStudentIds([]);
      return;
    }

    let cancelled = false;

    async function loadRoster(): Promise<void> {
      try {
        setLoadingRoster(true);
        const nextRoster = await fetchTeacherRoster(teacherId, selectedClassId);
        if (cancelled) {
          return;
        }

        setRoster(nextRoster);
        setDraft(createDraft(nextRoster.classInfo));
        setRemovedStudentIds([]);
        setError(null);
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : 'Failed to load the class roster.');
        }
      } finally {
        if (!cancelled) {
          setLoadingRoster(false);
        }
      }
    }

    void loadRoster();
    return () => {
      cancelled = true;
    };
  }, [selectedClassId, teacherId]);

  const projectedEnrollment = Math.max(0, (roster?.students.length ?? 0) - removedStudentIds.length);
  const selectedCapacity = draft?.capacity ?? roster?.classInfo.capacity ?? 0;
  const selectedFillRate = selectedCapacity > 0 ? Math.round((projectedEnrollment / selectedCapacity) * 100) : 0;
  const hasClassChanges = Boolean(roster && draft && !isDraftEqual(roster.classInfo, draft));
  const hasPendingChanges = Boolean((draft && hasClassChanges) || removedStudentIds.length > 0);

  const handleToggleRemoval = (studentId: string) => {
    setRemovedStudentIds((current) =>
      current.includes(studentId)
        ? current.filter((item) => item !== studentId)
        : [...current, studentId],
    );
  };

  const handleSaveChanges = async () => {
    if (!roster || !draft) {
      return;
    }

    if (draft.capacity < projectedEnrollment) {
      setToast({
        message: `Capacity cannot be lower than projected enrollment (${projectedEnrollment}).`,
        tone: 'error',
      });
      return;
    }

    setSaving(true);
    setError(null);

    try {
      for (const studentId of removedStudentIds) {
        await removeStudentFromTeacherClass(teacherId, roster.classInfo.id, studentId);
      }

      if (hasClassChanges) {
        await updateTeacherClass(teacherId, roster.classInfo.id, {
          title: draft.title.trim(),
          location: draft.location.trim(),
          capacity: draft.capacity,
          days: draft.days,
          startTime: draft.startTime,
          endTime: draft.endTime,
        });
      }

      const [nextClasses, nextRoster] = await Promise.all([
        fetchTeacherClasses(teacherId),
        fetchTeacherRoster(teacherId, roster.classInfo.id),
      ]);

      setClasses(nextClasses);
      setRoster(nextRoster);
      setDraft(createDraft(nextRoster.classInfo));
      setRemovedStudentIds([]);

      const actions: string[] = [];
      if (hasClassChanges) {
        actions.push('class details');
      }
      if (removedStudentIds.length > 0) {
        actions.push(`${removedStudentIds.length} roster change${removedStudentIds.length === 1 ? '' : 's'}`);
      }

      setToast({
        message: actions.length > 0 ? `Saved ${actions.join(' and ')}.` : 'No changes to save.',
        tone: 'success',
      });
    } catch (err) {
      setToast({ message: resolveError(err), tone: 'error' });
    } finally {
      setSaving(false);
    }
  };

  const handleDiscardChanges = () => {
    if (!roster) {
      return;
    }

    setDraft(createDraft(roster.classInfo));
    setRemovedStudentIds([]);
    setToast({ message: 'Unsaved teacher changes cleared.', tone: 'info' });
  };

  return (
    <main className={styles.container}>
      <section className={styles.teacherWorkspaceHero}>
        <div>
          <p className={styles.teacherWorkspaceEyebrow}>Teacher Workspace</p>
          <h1>{teacherName}</h1>
          <p>Manage only the sections assigned to this instructor. Changes are staged locally until you press save.</p>
        </div>
        <div className={styles.teacherWorkspaceSummary}>
          <span>{classes.length} active sections</span>
          <span>{projectedEnrollment} projected students in selected class</span>
        </div>
      </section>

      {error && (
        <div className={styles.inlineError} role="alert">
          {error}
        </div>
      )}

      <section className={styles.teacherWorkspace}>
        <aside className={styles.teacherSidebar}>
          <div className={styles.teacherSidebarHeader}>
            <h2>Your classes</h2>
            <p>Open a section to review its roster and update class details.</p>
          </div>

          <div className={styles.teacherClassList}>
            {classes.map((item) => {
              const isActive = item.id === selectedClassId;
              return (
                <button
                  key={item.id}
                  type="button"
                  className={`${styles.teacherClassButton} ${isActive ? styles.teacherClassButtonActive : ''}`}
                  onClick={() => setSelectedClassId(item.id)}
                >
                  <strong>{item.id}</strong>
                  <span>{item.title}</span>
                  <small>
                    {item.days.join('/')} {item.startTime}-{item.endTime}
                  </small>
                  <small>
                    {item.enrolledCount}/{item.capacity} enrolled • {item.location ?? item.room}
                  </small>
                  <div className={styles.teacherProgress} aria-hidden="true">
                    <div className={styles.teacherProgressBar}>
                      <span style={{ width: `${item.capacity > 0 ? Math.round((item.enrolledCount / item.capacity) * 100) : 0}%` }} />
                    </div>
                  </div>
                </button>
              );
            })}

            {!loadingClasses && classes.length === 0 && (
              <div className={styles.emptyState}>
                <h2>No classes assigned.</h2>
                <p>This teacher account does not currently own any sections.</p>
              </div>
            )}

            {loadingClasses && <p className={styles.loadingText}>Loading your sections...</p>}
          </div>
        </aside>

        <section className={styles.teacherEditorCard}>
          {loadingRoster && <p className={styles.loadingText}>Loading roster and class details...</p>}

          {!loadingRoster && roster && draft && (
            <>
              <header className={styles.teacherEditorHeader}>
                <div>
                  <h2>{roster.classInfo.id}</h2>
                  <p>
                    {roster.classInfo.department} • {roster.classInfo.credits} credits • {roster.classInfo.term}
                  </p>
                </div>
                <div className={styles.teacherStatusPill}>
                  {projectedEnrollment}/{selectedCapacity} enrolled • {removedStudentIds.length} pending removal
                </div>
              </header>

              <div className={styles.teacherProgressMeta}>
                <span>Enrollment rate</span>
                <strong>{selectedFillRate}%</strong>
              </div>
              <div className={styles.teacherProgress} aria-label={`Enrollment rate ${selectedFillRate}%`}>
                <div className={styles.teacherProgressBar}>
                  <span style={{ width: `${selectedFillRate}%` }} />
                </div>
              </div>

              <div className={styles.teacherFormGrid}>
                <label className={styles.filterField}>
                  <span>Class title</span>
                  <input
                    type="text"
                    value={draft.title}
                    onChange={(event) => setDraft((current) => (current ? { ...current, title: event.target.value } : current))}
                  />
                </label>
                <label className={styles.filterField}>
                  <span>Location</span>
                  <input
                    type="text"
                    value={draft.location}
                    onChange={(event) =>
                      setDraft((current) => (current ? { ...current, location: event.target.value } : current))
                    }
                  />
                </label>
                <label className={styles.filterField}>
                  <span>Capacity</span>
                  <input
                    type="number"
                    min={projectedEnrollment}
                    value={draft.capacity}
                    onChange={(event) =>
                      setDraft((current) =>
                        current
                          ? {
                              ...current,
                              capacity: Math.max(0, Number(event.target.value) || 0),
                            }
                          : current,
                      )
                    }
                  />
                </label>
                <label className={styles.filterField}>
                  <span>Start time</span>
                  <input
                    type="time"
                    value={draft.startTime}
                    onChange={(event) =>
                      setDraft((current) => (current ? { ...current, startTime: event.target.value } : current))
                    }
                  />
                </label>
                <label className={styles.filterField}>
                  <span>End time</span>
                  <input
                    type="time"
                    value={draft.endTime}
                    onChange={(event) =>
                      setDraft((current) => (current ? { ...current, endTime: event.target.value } : current))
                    }
                  />
                </label>
              </div>

              <div className={styles.teacherDaysSection}>
                <span>Meeting days</span>
                <div className={styles.teacherDaysRow} role="group" aria-label="Meeting days">
                  {WEEK_DAYS.map((day) => {
                    const selected = draft.days.includes(day);
                    return (
                      <button
                        key={day}
                        type="button"
                        className={`${styles.teacherDayButton} ${selected ? styles.teacherDayButtonActive : ''}`}
                        onClick={() =>
                          setDraft((current) =>
                            current
                              ? {
                                  ...current,
                                  days: selected
                                    ? current.days.filter((item) => item !== day)
                                    : [...current.days, day],
                                }
                              : current,
                          )
                        }
                      >
                        {day}
                      </button>
                    );
                  })}
                </div>
              </div>

              <section className={styles.teacherRosterPanel}>
                <header className={styles.teacherRosterHeader}>
                  <div>
                    <h3>Student roster</h3>
                    <p>Mark removals, then save once to persist every roster and class change together.</p>
                  </div>
                  <span className={styles.teacherSubtleMeta}>
                    Projected seats after save: {draft.capacity - projectedEnrollment}
                  </span>
                </header>

                <div className={styles.teacherStudentList}>
                  {roster.students.map((student) => {
                    const pendingRemoval = removedStudentIds.includes(student.studentId);
                    return (
                      <article
                        key={student.studentId}
                        className={`${styles.teacherStudentCard} ${pendingRemoval ? styles.teacherStudentCardPending : ''}`}
                      >
                        <div>
                          <strong>{student.name}</strong>
                          <p>{student.studentId} • {student.email}</p>
                          <p className={styles.teacherStudentMeta}>
                            Enrolled {formatEnrollmentDate(student.enrollmentDateUtc)}
                          </p>
                        </div>
                        <button
                          type="button"
                          className={pendingRemoval ? styles.teacherUndoButton : styles.teacherRemoveButton}
                          onClick={() => handleToggleRemoval(student.studentId)}
                        >
                          {pendingRemoval ? 'Undo remove' : 'Remove'}
                        </button>
                      </article>
                    );
                  })}

                  {roster.students.length === 0 && (
                    <div className={styles.emptyState}>
                      <h2>No students enrolled.</h2>
                      <p>This section currently has an empty roster.</p>
                    </div>
                  )}
                </div>
              </section>

              <div className={styles.teacherSaveBar}>
                <div className={styles.teacherSaveMeta}>
                  <strong>{hasPendingChanges ? 'Unsaved changes ready' : 'No pending changes'}</strong>
                  <span>
                    Save writes directly to the database and will still be visible after refresh or a new login.
                  </span>
                </div>
                <div className={styles.actions}>
                  <button type="button" onClick={handleDiscardChanges} disabled={!hasPendingChanges || saving}>
                    Discard
                  </button>
                  <button type="button" onClick={() => void handleSaveChanges()} disabled={!hasPendingChanges || saving}>
                    {saving ? 'Saving…' : 'Save changes'}
                  </button>
                </div>
              </div>
            </>
          )}
        </section>
      </section>

      {toast && <Toast tone={toast.tone} message={toast.message} onClose={() => setToast(null)} />}
    </main>
  );
}

function createDraft(classInfo: ClassOffering): TeacherDraft {
  return {
    title: classInfo.title,
    location: classInfo.location ?? classInfo.room,
    capacity: classInfo.capacity,
    days: [...classInfo.days],
    startTime: classInfo.startTime,
    endTime: classInfo.endTime,
  };
}

function isDraftEqual(classInfo: ClassOffering, draft: TeacherDraft): boolean {
  return (
    classInfo.title === draft.title.trim() &&
    (classInfo.location ?? classInfo.room) === draft.location.trim() &&
    classInfo.capacity === draft.capacity &&
    classInfo.startTime === draft.startTime &&
    classInfo.endTime === draft.endTime &&
    classInfo.days.join(',') === draft.days.join(',')
  );
}

function resolveError(error: unknown): string {
  if (error instanceof ApiError && error.message.trim()) {
    return error.message;
  }

  if (error instanceof Error && error.message.trim()) {
    return error.message;
  }

  return 'The teacher change could not be saved.';
}

function formatEnrollmentDate(value?: string | null): string {
  if (!value) {
    return 'date unavailable';
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return 'date unavailable';
  }

  return parsed.toLocaleString([], {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  });
}
