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
  const [hour, minute] = value.split(':').map(Number);
  return hour * 60 + minute;
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

const DAY_TO_INDEX: Record<Day, number> = {
  Mon: 1,
  Tue: 2,
  Wed: 3,
  Thu: 4,
  Fri: 5,
};

export function getNextMeetingDate(days: Day[], startTime: string, now = new Date()): Date | null {
  if (days.length === 0) {
    return null;
  }

  let best: Date | null = null;
  const [hour, minute] = startTime.split(':').map(Number);

  days.forEach((day) => {
    const target = new Date(now);
    target.setSeconds(0, 0);
    target.setHours(hour, minute, 0, 0);

    const currentDay = now.getDay();
    const targetDay = DAY_TO_INDEX[day];
    let delta = targetDay - currentDay;
    if (delta < 0 || (delta === 0 && target <= now)) {
      delta += 7;
    }

    target.setDate(target.getDate() + delta);
    if (!best || target < best) {
      best = target;
    }
  });

  return best;
}

export function formatUpcomingSession(days: Day[], startTime: string, endTime: string, now = new Date()): string {
  const nextMeeting = getNextMeetingDate(days, startTime, now);
  if (!nextMeeting) {
    return 'No upcoming session';
  }

  return nextMeeting.toLocaleString(undefined, {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  }) + ` - ${endTime}`;
}
