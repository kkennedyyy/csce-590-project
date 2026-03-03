import type { ClassOffering, Overlap, ScheduledClass, StudentSchedule } from '../types';
import {
  DEFAULT_TIME_WINDOW,
  formatTimeRange,
  getRangeIntersection,
  getSharedDays,
  isWithinWindow,
  timeToMinutes,
} from './time';

export const MAX_CREDITS = 19;

export interface AddValidationResult {
  canAddClass: boolean;
  code?: 'CAPACITY' | 'CREDITS' | 'DUPLICATE' | 'TIME_RANGE';
  error?: string;
  hint?: string;
}

export function calculateCurrentCredits(schedule: ScheduledClass[]): number {
  return schedule.reduce((sum, item) => sum + item.credits, 0);
}

export function validateClassAddition(
  schedule: StudentSchedule,
  classToAdd: ClassOffering,
  selection: Pick<ScheduledClass, 'days' | 'startTime' | 'endTime'>,
  strictTimeRange = true,
): AddValidationResult {
  if (schedule.scheduledClasses.some((item) => item.classId === classToAdd.id)) {
    return {
      canAddClass: false,
      code: 'DUPLICATE',
      error: `${classToAdd.id} is already in your schedule.`,
    };
  }

  if (classToAdd.enrolledCount >= classToAdd.capacity) {
    return {
      canAddClass: false,
      code: 'CAPACITY',
      error: `${classToAdd.id} is full (${classToAdd.enrolledCount}/${classToAdd.capacity}).`,
    };
  }

  if (!isWithinWindow(selection.startTime, selection.endTime, DEFAULT_TIME_WINDOW)) {
    const baseError = `Class time ${selection.startTime}-${selection.endTime} is outside your standard schedule (${DEFAULT_TIME_WINDOW.start}-${DEFAULT_TIME_WINDOW.end}).`;
    if (strictTimeRange) {
      return {
        canAddClass: false,
        code: 'TIME_RANGE',
        error: baseError,
        hint: 'Suggest alternative time: choose a section between 08:00 and 20:00.',
      };
    }
  }

  const currentCredits = schedule.currentCredits;
  const attemptedCredits = currentCredits + classToAdd.credits;

  if (attemptedCredits > MAX_CREDITS) {
    return {
      canAddClass: false,
      code: 'CREDITS',
      error: `Adding ${classToAdd.credits} credits would raise total to ${attemptedCredits} which is above the ${MAX_CREDITS} credit limit. Remove classes or contact advisor.`,
    };
  }

  return { canAddClass: true };
}

export function getOverlaps(schedule: ScheduledClass[]): Overlap[] {
  const overlaps: Overlap[] = [];

  for (let i = 0; i < schedule.length; i += 1) {
    for (let j = i + 1; j < schedule.length; j += 1) {
      const first = schedule[i];
      const second = schedule[j];
      const sharedDays = getSharedDays(first.days, second.days);

      if (sharedDays.length === 0) {
        continue;
      }

      const firstStart = timeToMinutes(first.startTime);
      const firstEnd = timeToMinutes(first.endTime);
      const secondStart = timeToMinutes(second.startTime);
      const secondEnd = timeToMinutes(second.endTime);
      const intersection = getRangeIntersection(firstStart, firstEnd, secondStart, secondEnd);

      if (!intersection) {
        continue;
      }

      for (const day of sharedDays) {
        overlaps.push({
          day,
          startMinute: intersection[0],
          endMinute: intersection[1],
          classIds: [first.classId, second.classId],
          classTitles: [first.title, second.title],
        });
      }
    }
  }

  return overlaps;
}

export function buildConflictLabel(overlap: Overlap): string {
  return `Conflict: ${overlap.classIds[0]} vs ${overlap.classIds[1]} (${formatTimeRange(overlap.startMinute, overlap.endMinute)})`;
}
