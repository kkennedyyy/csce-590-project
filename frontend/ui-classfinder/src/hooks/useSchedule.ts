import { useCallback, useEffect, useMemo } from 'react';

import { ApiError, deregisterClass, fetchSchedule, registerClass } from '../api/api';
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
          blocking: validation.code === 'CREDITS' || validation.code === 'TIME_RANGE',
        };
      }

      try {
        setLoading(true);
        const updated = await registerClass({
          studentId,
          classId: classOffering.id,
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
    [currentCredits, hydrate, scheduledClasses, setError, setLoading, studentId],
  );

  const removeClassFromSchedule = useCallback(
    async (classId: string): Promise<ActionResult> => {
      try {
        setLoading(true);
        const updated = await deregisterClass({ studentId, classId });
        hydrate(updated);
        setError(null);
        return { ok: true };
      } catch (err) {
        const message = normalizeError(err);
        setError(message);
        return { ok: false, message, blocking: false };
      } finally {
        setLoading(false);
      }
    },
    [hydrate, setError, setLoading, studentId],
  );

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
    }),
    [
      addClassToSchedule,
      currentCredits,
      error,
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
    if (error.status === 423) {
      return 'Class is full. Choose a different class or join the waitlist.';
    }
    if (error.status === 403) {
      return 'This add would exceed 19 credits. Remove classes or contact advisor.';
    }
    if (error.status === 409) {
      return 'This class conflicts with your schedule. Resolve overlaps before finalizing.';
    }
    return error.message;
  }

  return normalizeError(error);
}
