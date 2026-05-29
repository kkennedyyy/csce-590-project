import { create } from 'zustand';

import type { Overlap, RegisteredClass, ScheduledClass, StudentSchedule } from '../types';
import { calculateCurrentCredits, getOverlaps } from '../utils/validators';

interface ScheduleState {
  studentId: string;
  scheduledClasses: ScheduledClass[];
  registeredClasses: RegisteredClass[];
  currentCredits: number;
  overlaps: Overlap[];
  loading: boolean;
  error: string | null;
  initialized: boolean;
  setLoading: (loading: boolean) => void;
  setError: (error: string | null) => void;
  setStudentId: (studentId: string) => void;
  hydrate: (schedule: StudentSchedule) => void;
  setScheduleClasses: (items: ScheduledClass[]) => void;
  reset: () => void;
}

export const useScheduleStore = create<ScheduleState>((set) => ({
  studentId: 'student-123',
  scheduledClasses: [],
  registeredClasses: [],
  currentCredits: 0,
  overlaps: [],
  loading: false,
  error: null,
  initialized: false,
  setLoading: (loading) => set({ loading }),
  setError: (error) => set({ error }),
  setStudentId: (studentId) =>
    set({
      studentId,
      scheduledClasses: [],
      registeredClasses: [],
      currentCredits: 0,
      overlaps: [],
      initialized: false,
    }),
  hydrate: (schedule) =>
    set({
      studentId: schedule.studentId,
      scheduledClasses: schedule.scheduledClasses,
      registeredClasses: schedule.registeredClasses,
      currentCredits: calculateCurrentCredits(schedule.scheduledClasses),
      overlaps: getOverlaps(schedule.scheduledClasses),
      initialized: true,
    }),
  setScheduleClasses: (items) =>
    set({
      scheduledClasses: items,
      currentCredits: calculateCurrentCredits(items),
      overlaps: getOverlaps(items),
    }),
  reset: () =>
    set({
      studentId: 'student-123',
      scheduledClasses: [],
      registeredClasses: [],
      currentCredits: 0,
      overlaps: [],
      loading: false,
      error: null,
      initialized: false,
    }),
}));
