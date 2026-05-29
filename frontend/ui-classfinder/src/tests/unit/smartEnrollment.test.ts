import type { ClassOffering } from '../../types';
import type { SmartEnrollmentPreferences } from '../../types';
import { generateCandidateSchedules } from '../../utils/smartEnrollment';

const basePreferences: SmartEnrollmentPreferences = {
  prompt: 'Need CSCE210 and one ranked elective with Friday free.',
  requiredCourseCodes: ['CSCE210'],
  preferredElectiveCourseCodes: ['HIST101', 'ARTS201'],
  requiredKeywords: [],
  preferredKeywords: ['history', 'design'],
  electiveSlots: 1,
  earliestStart: '08:00',
  latestEnd: '18:00',
  blockedDays: [],
  preferredNoClassDay: 'Fri',
  minimumBreakMinutes: 15,
  summary: 'Prompt interpreted into ranked schedule constraints.',
};

function buildOffering(overrides: Partial<ClassOffering> & Pick<ClassOffering, 'id' | 'title'>): ClassOffering {
  return {
    instructor: overrides.instructor ?? 'Dr. Test',
    capacity: overrides.capacity ?? 25,
    enrolledCount: overrides.enrolledCount ?? 10,
    availableSeats: overrides.availableSeats ?? 15,
    credits: overrides.credits ?? 3,
    room: overrides.room ?? 'ZACH 100',
    location: overrides.location ?? overrides.room ?? 'ZACH 100',
    term: overrides.term ?? 'Fall 2026',
    days: overrides.days ?? ['Mon'],
    startTime: overrides.startTime ?? '09:00',
    endTime: overrides.endTime ?? '10:15',
    department: overrides.department ?? 'Computer Science',
    departmentCode: overrides.departmentCode ?? 'CSCE',
    colorHint: overrides.colorHint ?? 'neutral',
    prerequisites: overrides.prerequisites ?? [],
    ...overrides,
  };
}

describe('generateCandidateSchedules', () => {
  test('builds ranked schedules with required classes and preferred electives', () => {
    const offerings: ClassOffering[] = [
      buildOffering({ id: 'CSCE210-01', title: 'Data Structures', days: ['Mon', 'Wed'], startTime: '09:00', endTime: '10:15' }),
      buildOffering({ id: 'HIST101-01', title: 'World History', department: 'History', departmentCode: 'HIST', days: ['Tue'], startTime: '11:00', endTime: '12:15' }),
      buildOffering({ id: 'ARTS201-01', title: 'Design Studio', department: 'Art', departmentCode: 'ARTS', days: ['Thu'], startTime: '11:00', endTime: '12:15' }),
    ];

    const candidates = generateCandidateSchedules(offerings, basePreferences, 2);

    expect(candidates).toHaveLength(2);
    expect(candidates).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          scheduledClasses: expect.arrayContaining([
            expect.objectContaining({ classId: 'CSCE210-01' }),
            expect.objectContaining({ classId: 'HIST101-01' }),
          ]),
        }),
      ]),
    );
  });

  test('respects blocked days and minimum break constraints when choosing sections', () => {
    const offerings: ClassOffering[] = [
      buildOffering({ id: 'CSCE210-01', title: 'Data Structures', days: ['Tue'], startTime: '10:00', endTime: '11:00' }),
      buildOffering({ id: 'MATH200-01', title: 'Calculus II', department: 'Mathematics', departmentCode: 'MATH', days: ['Tue'], startTime: '11:05', endTime: '12:20' }),
      buildOffering({ id: 'MATH200-02', title: 'Calculus II', department: 'Mathematics', departmentCode: 'MATH', days: ['Tue'], startTime: '11:30', endTime: '12:45' }),
      buildOffering({ id: 'MATH200-03', title: 'Calculus II', department: 'Mathematics', departmentCode: 'MATH', days: ['Fri'], startTime: '11:30', endTime: '12:45' }),
    ];

    const preferences: SmartEnrollmentPreferences = {
      ...basePreferences,
      requiredCourseCodes: ['CSCE210', 'MATH200'],
      preferredElectiveCourseCodes: [],
      electiveSlots: 0,
      blockedDays: ['Fri'],
      minimumBreakMinutes: 15,
    };

    const candidates = generateCandidateSchedules(offerings, preferences, 2);

    expect(candidates).toHaveLength(1);
    expect(candidates[0]?.scheduledClasses.map((item) => item.classId)).toEqual(
      expect.arrayContaining(['CSCE210-01', 'MATH200-02']),
    );
    expect(candidates[0]?.scheduledClasses.map((item) => item.classId)).not.toContain('MATH200-01');
    expect(candidates[0]?.scheduledClasses.map((item) => item.classId)).not.toContain('MATH200-03');
  });

  test('returns no candidates when a required course has no open section', () => {
    const offerings: ClassOffering[] = [
      buildOffering({
        id: 'CSCE210-01',
        title: 'Data Structures',
        capacity: 25,
        enrolledCount: 25,
        availableSeats: 0,
      }),
    ];

    const candidates = generateCandidateSchedules(offerings, basePreferences, 3);

    expect(candidates).toEqual([]);
  });
});
