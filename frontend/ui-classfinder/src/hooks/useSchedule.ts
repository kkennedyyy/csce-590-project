import { useCallback, useEffect, useMemo } from 'react';

import { ApiError, deregisterClass, fetchSchedule, finalizeSchedule, registerClass } from '../api/api';
import type { ClassOffering, MeetingTime, ScheduledClass } from '../types';
import { useScheduleStore } from '../store/scheduleStore';
import { validateClassAddition } from '../utils/validators';

interface AddClassOptions {
  meetingTime?: MeetingTime;
  strictTimeRange?: boolean;
}

interface ActionResult {
  ok: boolean;
  message?: string;
  blocking?: boolean;
}

export function useSchedule(): {
  studentId: string;
  scheduledClasses: ScheduledClass[];
  overlaps: ReturnType<typeof useScheduleStore.getState>['overlaps'];
  currentCredits: number;
  loading: boolean;
  error: string | null;
  initialize: () => Promise<void>;
  addClassToSchedule: (classOffering: ClassOffering, options?: AddClassOptions) => Promise<ActionResult>;
  removeClassFromSchedule: (classId: string) => Promise<ActionResult>;
  finalizeRegistration: () => Promise<ActionResult>;
} {
  const {
    studentId,
    scheduledClasses,
    overlaps,
    currentCredits,
    loading,
    error,
    initialized,
    setLoading,
    setError,
    hydrate,
  } = useScheduleStore();

  const initialize = useCallback(async () => {
    if (initialized) {
      return;
    }
    if (!studentId?.trim()) {
      return;
    }
    try {
      setLoading(true);
      const schedule = await fetchSchedule(studentId);
      hydrate(schedule);
      setError(null);
    } catch (err) {
      setError(normalizeError(err));
    } finally {
      setLoading(false);
    }
  }, [hydrate, initialized, setError, setLoading, studentId]);

  useEffect(() => {
    void initialize();
  }, [initialize]);

  const addClassToSchedule = useCallback(
    async (classOffering: ClassOffering, options?: AddClassOptions): Promise<ActionResult> => {
      if (overlaps.length > 0) {
        return {
          ok: false,
          message: 'Resolve existing schedule conflicts before adding or moving classes.',
          blocking: true,
        };
      }

      const meetingTime = options?.meetingTime ?? {
        days: classOffering.days,
        startTime: classOffering.startTime,
        endTime: classOffering.endTime,
      };

      const validation = validateClassAddition(
        {
          studentId,
          scheduledClasses,
          currentCredits,
        },
        classOffering,
        meetingTime,
        options?.strictTimeRange ?? true,
      );

      if (!validation.canAddClass) {
        return {
          ok: false,
          message: validation.hint ? `${validation.error} ${validation.hint}` : validation.error,
          blocking:
            validation.code === 'CREDITS'
            || validation.code === 'TIME_RANGE'
            || validation.code === 'CONFLICT'
            || validation.code === 'SEMESTER',
        };
      }

      try {
        setLoading(true);
        const updated = await registerClass({
          studentId,
          classId: classOffering.id,
          sectionId: classOffering.sectionId,
          meetingTime,
        });
        hydrate(updated);
        setError(null);
        return { ok: true };
      } catch (err) {
        const message = mapApiError(err);
        setError(message);
        return {
          ok: false,
          message,
          blocking: true,
        };
      } finally {
        setLoading(false);
      }
    },
    [currentCredits, hydrate, overlaps.length, scheduledClasses, setError, setLoading, studentId],
  );

  const removeClassFromSchedule = useCallback(
    async (classId: string): Promise<ActionResult> => {
      if (!scheduledClasses.some((item) => item.classId === classId)) {
        return { ok: true };
      }

      try {
        setLoading(true);
        const existing = scheduledClasses.find((item) => item.classId === classId);
        const updated = await deregisterClass({
          studentId,
          classId,
          sectionId: existing?.sectionId,
        });
        hydrate(updated);
        setError(null);
        return { ok: true };
      } catch (err) {
        const message = mapApiError(err);
        setError(message);
        return {
          ok: false,
          message,
          blocking: true,
        };
      } finally {
        setLoading(false);
      }
    },
    [hydrate, scheduledClasses, setError, setLoading, studentId],
  );

  const finalizeRegistration = useCallback(async (): Promise<ActionResult> => {
    try {
      setLoading(true);
      const updated = await finalizeSchedule({
        studentId,
        scheduledClasses,
      });
      hydrate(updated);
      setError(null);
      return { ok: true };
    } catch (err) {
      const message = mapApiError(err);
      setError(message);
      return {
        ok: false,
        message,
        blocking: true,
      };
    } finally {
      setLoading(false);
    }
  }, [hydrate, scheduledClasses, setError, setLoading, studentId]);

  return useMemo(
    () => ({
      studentId,
      scheduledClasses,
      overlaps,
      currentCredits,
      loading,
      error,
      initialize,
      addClassToSchedule,
      removeClassFromSchedule,
      finalizeRegistration,
    }),
    [
      addClassToSchedule,
      currentCredits,
      error,
      finalizeRegistration,
      initialize,
      loading,
      overlaps,
      removeClassFromSchedule,
      scheduledClasses,
      studentId,
    ],
  );
}

function normalizeError(error: unknown): string {
  if (error instanceof Error) {
    return error.message;
  }
  return 'Something went wrong. Please try again.';
}

function mapApiError(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.message.trim()) {
      return error.message;
    }

    if (error.status === 423) {
      return 'Class is full. Choose a different class or join the waitlist.';
    }
    if (error.status === 403) {
      return 'This action is blocked by a registration policy.';
    }
    if (error.status === 409) {
      return 'This action conflicts with the current schedule state.';
    }
    return 'The request could not be completed.';
  }

  return normalizeError(error);
}
