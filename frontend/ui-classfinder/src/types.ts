export const WEEK_DAYS = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri'] as const;

export type Day = (typeof WEEK_DAYS)[number];

export interface MeetingTime {
  days: Day[];
  startTime: string;
  endTime: string;
}

export interface ClassOffering extends MeetingTime {
  sectionId?: number;
  id: string;
  title: string;
  instructor: string;
  capacity: number;
  enrolledCount: number;
  credits: number;
  room: string;
  term: string;
  description?: string;
  colorHint?: 'red' | 'purple' | 'neutral';
  meetingOptions?: MeetingTime[];
}

export interface ScheduledClass extends MeetingTime {
  sectionId?: number;
  classId: string;
  title: string;
  instructor: string;
  credits: number;
  room: string;
  term: string;
  colorHint?: ClassOffering['colorHint'];
}

export interface StudentSchedule {
  studentId: string;
  scheduledClasses: ScheduledClass[];
  currentCredits: number;
}

export type UserRole = 'student' | 'teacher';

export interface AuthUser {
  userId: string;
  role: UserRole;
  name: string;
  email: string;
}

export interface TeacherClass extends ClassOffering {}

export interface TeacherStudent {
  studentId: string;
  name: string;
  email: string;
}

export interface TeacherRoster {
  classInfo: TeacherClass;
  students: TeacherStudent[];
}

export interface Overlap {
  day: Day;
  startMinute: number;
  endMinute: number;
  classIds: [string, string];
  classTitles: [string, string];
}

export interface ClassPage {
  classes: ClassOffering[];
  page: number;
  pageSize: number;
  hasMore: boolean;
  total: number;
}

export interface ApiBehavior {
  latencyMs: number;
  forceConflict: boolean;
  forceCapacityLocked: boolean;
  forceCreditExceeded: boolean;
}
