import type { ClassOffering, Day, ScheduledClass } from '../types';
import { DEFAULT_TIME_WINDOW, timeToMinutes } from './time';
import { MAX_CREDITS, getOverlaps } from './validators';

export interface SmartEnrollmentPreferences {
  requiredCourseCodes: string[];
  preferredElectiveCourseCodes: string[];
  electiveSlots: number;
  earliestStart: string;
  latestEnd: string;
  blockedDays: Day[];
  preferredNoClassDay?: Day | '';
  minimumBreakMinutes: number;
}

export interface SmartEnrollmentCandidate {
  id: string;
  scheduledClasses: ScheduledClass[];
  totalCredits: number;
  summary: string;
}

export function generateCandidateSchedules(
  offerings: ClassOffering[],
  preferences: SmartEnrollmentPreferences,
  limit = 3,
): SmartEnrollmentCandidate[] {
  const available = offerings.filter(
    (item) =>
      (item.availableSeats ?? Math.max(0, item.capacity - item.enrolledCount)) > 0
      && !item.isStudentEnrolled
      && !item.isStudentWaitlisted,
  );
  const grouped = new Map<string, ClassOffering[]>();

  available.forEach((item) => {
    const courseCode = extractCourseCode(item.id);
    const list = grouped.get(courseCode) ?? [];
    list.push(item);
    grouped.set(courseCode, list);
  });

  const requiredCourseCodes = normalizeCourseCodes(preferences.requiredCourseCodes);
  const electiveCourseCodes = normalizeCourseCodes(preferences.preferredElectiveCourseCodes).filter(
    (code) => !requiredCourseCodes.includes(code),
  );
  if (requiredCourseCodes.some((code) => !grouped.has(code))) {
    return [];
  }

  const electivePlans = buildElectivePlans(electiveCourseCodes, preferences.electiveSlots);
  const candidates: Array<{ scheduledClasses: ScheduledClass[]; score: number }> = [];

  electivePlans.forEach((electivePlan) => {
    const plan = [...requiredCourseCodes, ...electivePlan].filter((code) => grouped.has(code));
    if (plan.length === 0) {
      return;
    }

    const sections = plan.map((code) =>
      (grouped.get(code) ?? []).slice().sort((left, right) => {
        const seatDelta = (right.availableSeats ?? 0) - (left.availableSeats ?? 0);
        if (seatDelta !== 0) {
          return seatDelta;
        }
        return left.id.localeCompare(right.id);
      }),
    );

    searchSections(
      sections,
      preferences,
      0,
      [],
      plan,
      candidates,
      electiveCourseCodes,
      limit,
    );
  });

  return candidates
    .sort((left, right) => left.score - right.score || left.scheduledClasses.length - right.scheduledClasses.length)
    .slice(0, limit)
    .map((candidate, index) => ({
      id: `candidate-${index + 1}`,
      scheduledClasses: candidate.scheduledClasses,
      totalCredits: candidate.scheduledClasses.reduce((sum, item) => sum + item.credits, 0),
      summary: buildCandidateSummary(candidate.scheduledClasses, index),
    }));
}

function searchSections(
  sectionsByCourse: ClassOffering[][],
  preferences: SmartEnrollmentPreferences,
  courseIndex: number,
  current: ScheduledClass[],
  coursePlan: string[],
  candidates: Array<{ scheduledClasses: ScheduledClass[]; score: number }>,
  electiveCourseCodes: string[],
  limit: number,
): void {
  if (candidates.length >= limit * 4) {
    return;
  }

  if (courseIndex >= sectionsByCourse.length) {
    const overlaps = getOverlaps(current);
    if (overlaps.length > 0) {
      return;
    }

    const score = scoreCandidate(current, coursePlan, electiveCourseCodes, preferences.preferredNoClassDay);
    candidates.push({
      scheduledClasses: current.slice().sort((left, right) => left.classId.localeCompare(right.classId)),
      score,
    });
    return;
  }

  sectionsByCourse[courseIndex]?.forEach((offering) => {
    const next = createScheduledClass(offering);
    if (!fitsHardConstraints(current, next, preferences)) {
      return;
    }

    const nextCredits = current.reduce((sum, item) => sum + item.credits, 0) + next.credits;
    if (nextCredits > MAX_CREDITS) {
      return;
    }

    searchSections(
      sectionsByCourse,
      preferences,
      courseIndex + 1,
      [...current, next],
      coursePlan,
      candidates,
      electiveCourseCodes,
      limit,
    );
  });
}

function fitsHardConstraints(
  current: ScheduledClass[],
  candidate: ScheduledClass,
  preferences: SmartEnrollmentPreferences,
): boolean {
  const earliestStart = preferences.earliestStart || DEFAULT_TIME_WINDOW.start;
  const latestEnd = preferences.latestEnd || DEFAULT_TIME_WINDOW.end;

  if (timeToMinutes(candidate.startTime) < timeToMinutes(earliestStart)) {
    return false;
  }
  if (timeToMinutes(candidate.endTime) > timeToMinutes(latestEnd)) {
    return false;
  }
  if (candidate.days.some((day) => preferences.blockedDays.includes(day))) {
    return false;
  }

  return current.every((scheduled) => {
    const sharedDays = scheduled.days.filter((day) => candidate.days.includes(day));
    if (sharedDays.length === 0) {
      return true;
    }

    const scheduledStart = timeToMinutes(scheduled.startTime);
    const scheduledEnd = timeToMinutes(scheduled.endTime);
    const candidateStart = timeToMinutes(candidate.startTime);
    const candidateEnd = timeToMinutes(candidate.endTime);

    if (candidateStart < scheduledEnd && scheduledStart < candidateEnd) {
      return false;
    }

    const forwardGap = candidateStart >= scheduledEnd ? candidateStart - scheduledEnd : scheduledStart - candidateEnd;
    return forwardGap >= preferences.minimumBreakMinutes;
  });
}

function scoreCandidate(
  current: ScheduledClass[],
  coursePlan: string[],
  electiveCourseCodes: string[],
  preferredNoClassDay?: Day | '',
): number {
  const offDayPenalty =
    preferredNoClassDay && current.some((item) => item.days.includes(preferredNoClassDay)) ? 50 : 0;
  const electivePenalty = coursePlan.reduce((sum, courseCode) => {
    const index = electiveCourseCodes.indexOf(courseCode);
    return index >= 0 ? sum + index * 5 : sum;
  }, 0);

  return offDayPenalty + electivePenalty;
}

function buildElectivePlans(electiveCourseCodes: string[], electiveSlots: number): string[][] {
  const normalizedSlots = Math.max(0, electiveSlots);
  if (normalizedSlots === 0 || electiveCourseCodes.length === 0) {
    return [[]];
  }

  const plans: string[][] = [];
  const target = Math.min(normalizedSlots, electiveCourseCodes.length);

  const build = (index: number, current: string[]) => {
    if (current.length === target || index >= electiveCourseCodes.length) {
      plans.push(current.slice());
      return;
    }

    build(index + 1, [...current, electiveCourseCodes[index]!]);
    build(index + 1, current);
  };

  build(0, []);
  return plans.filter((plan, index, all) => plan.length > 0 || index === all.length - 1);
}

function createScheduledClass(offering: ClassOffering): ScheduledClass {
  return {
    sectionId: offering.sectionId,
    classId: offering.id,
    title: offering.title,
    instructor: offering.instructor,
    credits: offering.credits,
    room: offering.room,
    location: offering.location ?? offering.room,
    term: offering.term,
    colorHint: offering.colorHint,
    days: offering.days,
    startTime: offering.startTime,
    endTime: offering.endTime,
  };
}

function normalizeCourseCodes(values: string[]): string[] {
  return values
    .map((value) => value.trim().toUpperCase())
    .filter((value) => value.length > 0)
    .filter((value, index, all) => all.indexOf(value) === index);
}

function extractCourseCode(classId: string): string {
  return classId.split('-')[0] ?? classId;
}

function buildCandidateSummary(schedule: ScheduledClass[], index: number): string {
  const credits = schedule.reduce((sum, item) => sum + item.credits, 0);
  return `Option ${index + 1}: ${schedule.length} classes, ${credits} credits`;
}
