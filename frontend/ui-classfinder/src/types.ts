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
  externalId?: string;
  title: string;
  department?: string;
  departmentCode?: string;
  courseNumber?: number;
  sessionCode?: string;
  instructor: string;
  instructorId?: string;
  capacity: number;
  enrolledCount: number;
  availableSeats?: number;
  credits: number;
  room: string;
  location?: string;
  term: string;
  description?: string;
  colorHint?: 'red' | 'purple' | 'neutral';
  meetingOptions?: MeetingTime[];
  isStudentEnrolled?: boolean;
  isStudentWaitlisted?: boolean;
  studentWaitlistPosition?: number | null;
  enrollmentStatus?: 'Enrolled' | 'Waitlisted' | 'Dropped' | 'NotEnrolled';
  prerequisites?: string[];
  dropDeadlineUtc?: string | null;
}

export interface ScheduledClass extends MeetingTime {
  sectionId?: number;
  classId: string;
  title: string;
  instructor: string;
  credits: number;
  room: string;
  location?: string;
  term: string;
  colorHint?: ClassOffering['colorHint'];
}

export interface RegisteredClass extends ScheduledClass {
  courseCode: string;
  enrollmentStatus: 'Enrolled' | 'Waitlisted';
  waitlistPosition?: number | null;
  capacity: number;
  enrolledCount: number;
  availableSeats: number;
}

export interface StudentSchedule {
  studentId: string;
  scheduledClasses: ScheduledClass[];
  registeredClasses: RegisteredClass[];
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

export interface TeacherCatalog {
  teacherId: string;
  externalId?: string;
  name: string;
  email: string;
  department: string;
  classes: TeacherClass[];
}

export interface TeacherStudent {
  studentId: string;
  name: string;
  email: string;
  enrollmentDateUtc?: string | null;
}

export interface TeacherRoster {
  classInfo: TeacherClass;
  students: TeacherStudent[];
}

export interface TeacherClassUpdateInput {
  title: string;
  location: string;
  capacity: number;
  days: Day[];
  startTime: string;
  endTime: string;
}

export interface TeacherCatalogPage {
  teachers: TeacherCatalog[];
  departments: string[];
  total: number;
}

export interface SmartEnrollmentPreferences {
  prompt: string;
  requiredCourseCodes: string[];
  preferredElectiveCourseCodes: string[];
  requiredKeywords: string[];
  preferredKeywords: string[];
  electiveSlots: number;
  earliestStart: string;
  latestEnd: string;
  blockedDays: Day[];
  preferredNoClassDay?: Day | '';
  minimumBreakMinutes: number;
  summary: string;
}

export interface SmartEnrollmentCandidate {
  id: string;
  scheduledClasses: ScheduledClass[];
  totalCredits: number;
  summary: string;
  rationale: string;
  highlights: string[];
}

export interface SmartEnrollmentResponse {
  usedLlm: boolean;
  plannerMode: string;
  catalogSize: number;
  preferences: SmartEnrollmentPreferences;
  preferenceSummary: string[];
  candidates: SmartEnrollmentCandidate[];
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
  departments?: string[];
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
