import type { Day } from '../types';

export interface TimeWindow {
  start: string;
  end: string;
  slotMinutes: number;
}

export const DEFAULT_TIME_WINDOW: TimeWindow = {
  start: '08:00',
  end: '20:00',
  slotMinutes: 30,
};

export function timeToMinutes(value: string): number {
  const parsed = parseTimeToMinutes(value);
  return parsed ?? 0;
}

export function normalizeClockTime(value: string, fallback = '08:00'): string {
  const parsed = parseTimeToMinutes(value);
  if (parsed === null) {
    return fallback;
  }

  return minutesToTime(parsed);
}

export function minutesToTime(totalMinutes: number): string {
  const hour = Math.floor(totalMinutes / 60)
    .toString()
    .padStart(2, '0');
  const minute = (totalMinutes % 60).toString().padStart(2, '0');
  return `${hour}:${minute}`;
}

export function durationMinutes(startTime: string, endTime: string): number {
  return timeToMinutes(endTime) - timeToMinutes(startTime);
}

export function isWithinWindow(
  startTime: string,
  endTime: string,
  window: Pick<TimeWindow, 'start' | 'end'>,
): boolean {
  const startMinute = timeToMinutes(startTime);
  const endMinute = timeToMinutes(endTime);
  return startMinute >= timeToMinutes(window.start) && endMinute <= timeToMinutes(window.end);
}

export function generateTimeSlots(window: TimeWindow): string[] {
  const slots: string[] = [];
  const startMinute = timeToMinutes(window.start);
  const endMinute = timeToMinutes(window.end);

  for (let current = startMinute; current <= endMinute; current += window.slotMinutes) {
    slots.push(minutesToTime(current));
  }

  return slots;
}

export function getSharedDays(a: Day[], b: Day[]): Day[] {
  return a.filter((day) => b.includes(day));
}

export function getRangeIntersection(
  firstStart: number,
  firstEnd: number,
  secondStart: number,
  secondEnd: number,
): [number, number] | null {
  const start = Math.max(firstStart, secondStart);
  const end = Math.min(firstEnd, secondEnd);
  return start < end ? [start, end] : null;
}

export function formatTimeRange(startMinute: number, endMinute: number): string {
  return `${minutesToTime(startMinute)}-${minutesToTime(endMinute)}`;
}

function parseTimeToMinutes(value: string): number | null {
  const trimmed = value.trim();
  if (!trimmed) {
    return null;
  }

  const directMatch = trimmed.match(/(\d{1,2}):(\d{2})(?::\d{2})?\s*([AaPp][Mm])?/);
  if (directMatch) {
    const rawHour = Number.parseInt(directMatch[1], 10);
    const minute = Number.parseInt(directMatch[2], 10);
    if (Number.isNaN(rawHour) || Number.isNaN(minute)) {
      return null;
    }

    const suffix = directMatch[3]?.toUpperCase();
    let hour = rawHour;

    if (suffix === 'AM') {
      if (hour === 12) {
        hour = 0;
      }
    } else if (suffix === 'PM') {
      if (hour < 12) {
        hour += 12;
      }
    }

    if (hour < 0 || hour > 23 || minute < 0 || minute > 59) {
      return null;
    }

    return hour * 60 + minute;
  }

  const parsedDate = new Date(trimmed);
  if (!Number.isNaN(parsedDate.getTime())) {
    return parsedDate.getHours() * 60 + parsedDate.getMinutes();
  }

  return null;
}
