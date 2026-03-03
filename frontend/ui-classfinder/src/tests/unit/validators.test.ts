import type { ClassOffering, StudentSchedule } from '../../types';
import { getOverlaps, validateClassAddition } from '../../utils/validators';

const baseClass: ClassOffering = {
  id: 'CSCE101-01',
  title: 'Intro to CS',
  instructor: 'Dr. Smith',
  days: ['Mon', 'Wed'],
  startTime: '09:00',
  endTime: '10:15',
  capacity: 30,
  enrolledCount: 20,
  credits: 3,
  room: 'ENGR 205',
  term: 'Fall 2026',
};

const baseSchedule: StudentSchedule = {
  studentId: 'student-123',
  scheduledClasses: [],
  currentCredits: 15,
};

describe('validators', () => {
  test('adds class when capacity available', () => {
    const result = validateClassAddition(baseSchedule, baseClass, {
      days: baseClass.days,
      startTime: baseClass.startTime,
      endTime: baseClass.endTime,
    });

    expect(result.canAddClass).toBe(true);
  });

  test('prevents add when capacity full', () => {
    const result = validateClassAddition(
      baseSchedule,
      { ...baseClass, enrolledCount: 30 },
      {
        days: baseClass.days,
        startTime: baseClass.startTime,
        endTime: baseClass.endTime,
      },
    );

    expect(result.canAddClass).toBe(false);
    expect(result.error).toContain('full');
  });

  test('prevents add when credit limit exceeded', () => {
    const result = validateClassAddition(
      { ...baseSchedule, currentCredits: 18 },
      { ...baseClass, credits: 3 },
      {
        days: baseClass.days,
        startTime: baseClass.startTime,
        endTime: baseClass.endTime,
      },
    );

    expect(result.canAddClass).toBe(false);
    expect(result.error).toContain('raise total to 21');
  });

  test('detects overlap exactly for 30 minutes', () => {
    const overlaps = getOverlaps([
      {
        classId: 'CSCE101-01',
        title: 'Intro to CS',
        instructor: 'Dr. Smith',
        credits: 3,
        room: 'A',
        term: 'Fall',
        days: ['Mon'],
        startTime: '09:00',
        endTime: '10:00',
      },
      {
        classId: 'MATH200-01',
        title: 'Calc II',
        instructor: 'Prof. Adams',
        credits: 4,
        room: 'B',
        term: 'Fall',
        days: ['Mon'],
        startTime: '09:30',
        endTime: '11:00',
      },
    ]);

    expect(overlaps).toHaveLength(1);
    expect(overlaps[0]?.startMinute).toBe(570);
    expect(overlaps[0]?.endMinute).toBe(600);
  });
});
