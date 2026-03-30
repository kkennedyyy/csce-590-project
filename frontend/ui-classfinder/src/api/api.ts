import { runtimeConfig } from '../config/runtime';
import { mockClasses } from '../data/mockClasses';
import type {
  ApiBehavior,
  AuthUser,
  ClassOffering,
  ClassPage,
  MeetingTime,
  ScheduledClass,
  StudentSchedule,
  TeacherClass,
  TeacherRoster,
  TeacherStudent,
  UserRole,
} from '../types';
import { calculateCurrentCredits, MAX_CREDITS } from '../utils/validators';

const SCHEDULE_STORAGE_KEY = 'classfinder.schedules.v2';
const CLASSES_STORAGE_KEY = 'classfinder.classes.v2';
const ROSTER_STORAGE_KEY = 'classfinder.rosters.v1';
const STUDENT_DIRECTORY_KEY = 'classfinder.student-directory.v1';

const defaultBehavior: ApiBehavior = {
  latencyMs: 220,
  forceConflict: false,
  forceCapacityLocked: false,
  forceCreditExceeded: false,
};

const mockUsers: Array<AuthUser & { password: string }> = [
  {
    userId: 'student-123',
    role: 'student',
    name: 'John Smith',
    email: 'john.smith@email.com',
    password: 'student123',
  },
  {
    userId: 'teacher-1',
    role: 'teacher',
    name: 'Dr. Smith',
    email: 'dr.smith@email.com',
    password: 'teacher123',
  },
  {
    userId: 'teacher-2',
    role: 'teacher',
    name: 'Dr. Anderson',
    email: 'anderson@email.com',
    password: 'teacher123',
  },
];

const mockTeacherInstructorName: Record<string, string> = {
  'teacher-1': 'Dr. Smith',
  'teacher-2': 'Dr. Anderson',
};

let behavior = { ...defaultBehavior };
let classCache = hydrateClasses();
let scheduleCache = hydrateSchedules();
let rosterCache = hydrateRoster();
let studentDirectory = hydrateStudentDirectory(rosterCache);
syncClassEnrollmentWithRoster();

export class ApiError extends Error {
  status: number;

  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}

export function setApiBehavior(next: Partial<ApiBehavior>): void {
  behavior = { ...behavior, ...next };
}

export function resetApiBehavior(): void {
  behavior = { ...defaultBehavior };
}

export function resetMockData(): void {
  classCache = mockClasses.map((item) => ({ ...item }));
  scheduleCache = {};
  rosterCache = generateDefaultRoster(classCache);
  studentDirectory = hydrateStudentDirectory(rosterCache, true);
  syncClassEnrollmentWithRoster();
  saveClasses();
  saveSchedules();
  saveRoster();
  saveStudentDirectory();
  behavior = { ...defaultBehavior };
}

export function setClassEnrollment(classId: string, enrolledCount: number): void {
  classCache = classCache.map((item) =>
    item.id === classId ? { ...item, enrolledCount: Math.max(0, enrolledCount) } : item,
  );

  const list = rosterCache[classId] ?? [];
  if (list.length > enrolledCount) {
    rosterCache[classId] = list.slice(0, enrolledCount);
  }
  if (list.length < enrolledCount) {
    const next = [...list];
    for (let index = list.length; index < enrolledCount; index += 1) {
      const id = `mock-${classId}-${index + 1}`;
      next.push(id);
      studentDirectory[id] = {
        studentId: id,
        name: `Student ${index + 1}`,
        email: `student${index + 1}@email.com`,
      };
    }
    rosterCache[classId] = next;
  }

  saveClasses();
  saveRoster();
  saveStudentDirectory();
}

export async function loginUser(payload: {
  email: string;
  password: string;
  role: UserRole;
}): Promise<AuthUser> {
  if (useCloudApi()) {
    const data = await cloudRequest<{ user: AuthUser }>('/auth/login', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
    return data.user;
  }

  await wait(behavior.latencyMs);
  const found = mockUsers.find(
    (item) =>
      item.email.toLowerCase() === payload.email.trim().toLowerCase() &&
      item.password === payload.password &&
      item.role === payload.role,
  );

  if (!found) {
    throw new ApiError(401, 'Invalid credentials.');
  }

  return {
    userId: found.userId,
    role: found.role,
    name: found.name,
    email: found.email,
  };
}

export async function fetchClasses(params: {
  page: number;
  pageSize?: number;
  search?: string;
}): Promise<ClassPage> {
  if (useCloudApi()) {
    const query = new URLSearchParams({
      page: String(Math.max(1, params.page)),
      pageSize: String(params.pageSize ?? 10),
      search: params.search?.trim() ?? '',
    });

    const data = await cloudRequest<{
      classes: Array<Record<string, unknown>>;
      page: number;
      pageSize: number;
      hasMore: boolean;
      total: number;
    }>(`/classes?${query.toString()}`);

    return {
      classes: data.classes.map(normalizeCloudClass),
      page: data.page,
      pageSize: data.pageSize,
      hasMore: data.hasMore,
      total: data.total,
    };
  }

  await wait(behavior.latencyMs);
  const page = Math.max(1, params.page);
  const pageSize = params.pageSize ?? 10;
  const search = params.search?.trim().toLowerCase();

  const filtered = !search
    ? classCache
    : classCache.filter(
        (item) =>
          item.id.toLowerCase().includes(search) ||
          item.title.toLowerCase().includes(search) ||
          item.instructor.toLowerCase().includes(search) ||
          (item.location ?? item.room).toLowerCase().includes(search),
      );

  const start = (page - 1) * pageSize;
  const slice = filtered.slice(start, start + pageSize);

  return {
    classes: slice.map((item) => ({ ...item })),
    page,
    pageSize,
    hasMore: start + pageSize < filtered.length,
    total: filtered.length,
  };
}

export async function fetchClassById(classId: string): Promise<ClassOffering> {
  if (useCloudApi()) {
    const data = await cloudRequest<Record<string, unknown>>(`/classes/by/${encodeURIComponent(classId)}`);
    return normalizeCloudClass(data);
  }

  await wait(behavior.latencyMs);
  const found = classCache.find((item) => item.id === classId);

  if (!found) {
    throw new ApiError(404, `Class ${classId} was not found.`);
  }

  return { ...found };
}

export async function fetchSchedule(studentId: string): Promise<StudentSchedule> {
  if (useCloudApi()) {
    const encodedStudentId = encodeURIComponent(studentId);
    const data = await cloudRequest<{
      studentId: string;
      scheduledClasses: Array<Record<string, unknown>>;
      currentCredits: number;
    }>(`/students/${encodedStudentId}/schedule/state`);

    const scheduledClasses = data.scheduledClasses.map(normalizeCloudScheduledClass);

    return {
      studentId: data.studentId,
      scheduledClasses,
      currentCredits: data.currentCredits,
    };
  }

  await wait(behavior.latencyMs);
  const items = scheduleCache[studentId] ?? [];
  return {
    studentId,
    scheduledClasses: items,
    currentCredits: calculateCurrentCredits(items),
  };
}

export async function registerClass(payload: {
  studentId: string;
  classId: string;
  sectionId?: number;
  meetingTime?: MeetingTime;
}): Promise<StudentSchedule> {
  if (useCloudApi()) {
    const encodedStudentId = encodeURIComponent(payload.studentId);
    const data = await cloudRequest<{
      studentId: string;
      scheduledClasses: Array<Record<string, unknown>>;
      currentCredits: number;
    }>(`/students/${encodedStudentId}/schedule`, {
      method: 'POST',
      body: JSON.stringify({ sectionId: payload.sectionId, classId: payload.classId }),
    });

    return {
      studentId: data.studentId,
      scheduledClasses: data.scheduledClasses.map(normalizeCloudScheduledClass),
      currentCredits: data.currentCredits,
    };
  }

  await wait(behavior.latencyMs);

  if (behavior.forceCapacityLocked) {
    throw new ApiError(423, 'Class is capacity-locked.');
  }
  if (behavior.forceCreditExceeded) {
    throw new ApiError(403, 'Credit cap reached.');
  }

  const classRecord = classCache.find((item) => item.id === payload.classId);

  if (!classRecord) {
    throw new ApiError(404, 'Class unavailable.');
  }

  if (classRecord.enrolledCount >= classRecord.capacity) {
    throw new ApiError(423, `${classRecord.id} is full.`);
  }

  const schedule = await fetchSchedule(payload.studentId);

  if (schedule.scheduledClasses.some((item) => item.classId === classRecord.id)) {
    throw new ApiError(409, `${classRecord.id} already added.`);
  }

  const meeting = payload.meetingTime ?? {
    days: classRecord.days,
    startTime: classRecord.startTime,
    endTime: classRecord.endTime,
  };

  const scheduledClass: ScheduledClass = {
    sectionId: classRecord.sectionId,
    classId: classRecord.id,
    title: classRecord.title,
    instructor: classRecord.instructor,
    credits: classRecord.credits,
    room: classRecord.room,
    location: classRecord.location ?? classRecord.room,
    term: classRecord.term,
    colorHint: classRecord.colorHint,
    days: meeting.days,
    startTime: meeting.startTime,
    endTime: meeting.endTime,
  };

  const nextClasses = [...schedule.scheduledClasses, scheduledClass];
  const nextCredits = calculateCurrentCredits(nextClasses);

  if (nextCredits > MAX_CREDITS) {
    throw new ApiError(403, 'Credit limit exceeded.');
  }

  if (behavior.forceConflict) {
    throw new ApiError(409, 'Overlap detected.');
  }

  classCache = classCache.map((item) =>
    item.id === classRecord.id ? { ...item, enrolledCount: item.enrolledCount + 1 } : item,
  );

  const studentId = payload.studentId;
  scheduleCache[studentId] = nextClasses;
  if (!rosterCache[classRecord.id]) {
    rosterCache[classRecord.id] = [];
  }
  if (!rosterCache[classRecord.id].includes(studentId)) {
    rosterCache[classRecord.id].push(studentId);
  }

  const nextSchedule: StudentSchedule = {
    studentId,
    scheduledClasses: nextClasses,
    currentCredits: nextCredits,
  };

  saveClasses();
  saveSchedules();
  saveRoster();
  return nextSchedule;
}

export async function deregisterClass(payload: {
  studentId: string;
  classId: string;
  sectionId?: number;
}): Promise<StudentSchedule> {
  if (useCloudApi()) {
    const encodedStudentId = encodeURIComponent(payload.studentId);
    const encodedClassId = encodeURIComponent(String(payload.sectionId ?? payload.classId));
    const data = await cloudRequest<{
      studentId: string;
      scheduledClasses: Array<Record<string, unknown>>;
      currentCredits: number;
    }>(`/students/${encodedStudentId}/schedule/${encodedClassId}`, {
      method: 'DELETE',
    });

    return {
      studentId: data.studentId,
      scheduledClasses: data.scheduledClasses.map(normalizeCloudScheduledClass),
      currentCredits: data.currentCredits,
    };
  }

  await wait(behavior.latencyMs);
  const studentId = payload.studentId;
  const schedule = await fetchSchedule(studentId);
  const existing = schedule.scheduledClasses.find((item) => item.classId === payload.classId);

  if (!existing) {
    return schedule;
  }

  const nextClasses = schedule.scheduledClasses.filter((item) => item.classId !== payload.classId);

  classCache = classCache.map((item) =>
    item.id === payload.classId
      ? { ...item, enrolledCount: Math.max(0, item.enrolledCount - 1) }
      : item,
  );

  scheduleCache[studentId] = nextClasses;
  rosterCache[payload.classId] = (rosterCache[payload.classId] ?? []).filter((id) => id !== studentId);

  const nextSchedule: StudentSchedule = {
    studentId,
    scheduledClasses: nextClasses,
    currentCredits: calculateCurrentCredits(nextClasses),
  };

  saveClasses();
  saveSchedules();
  saveRoster();
  return nextSchedule;
}

export async function fetchTeacherClasses(teacherId: string): Promise<TeacherClass[]> {
  if (useCloudApi()) {
    const encodedTeacherId = encodeURIComponent(teacherId);
    const data = await cloudRequest<{ classes: Array<Record<string, unknown>> }>(
      `/teachers/${encodedTeacherId}/classes`,
    );
    return data.classes.map(normalizeCloudClass);
  }

  await wait(behavior.latencyMs);
  const instructorName = mockTeacherInstructorName[teacherId];
  if (!instructorName) {
    throw new ApiError(404, 'Teacher profile not found.');
  }

  return classCache.filter((item) => item.instructor === instructorName);
}

export async function fetchTeacherRoster(teacherId: string, classIdOrSection: string): Promise<TeacherRoster> {
  if (useCloudApi()) {
    const encodedTeacherId = encodeURIComponent(teacherId);
    const encodedClassId = encodeURIComponent(classIdOrSection);
    const data = await cloudRequest<{
      classInfo: Record<string, unknown>;
      students: Array<Record<string, unknown>>;
    }>(`/teachers/${encodedTeacherId}/classes/${encodedClassId}/roster`);

    return {
      classInfo: normalizeCloudClass(data.classInfo),
      students: data.students.map((item) => ({
        studentId: String(item.studentId),
        name: String(item.name ?? ''),
        email: String(item.email ?? ''),
      })),
    };
  }

  await wait(behavior.latencyMs);
  const classes = await fetchTeacherClasses(teacherId);
  const classInfo = classes.find((item) => item.id === classIdOrSection || String(item.sectionId) === classIdOrSection);

  if (!classInfo) {
    throw new ApiError(404, 'Class not found for this teacher.');
  }

  const studentIds = rosterCache[classInfo.id] ?? [];
  const students: TeacherStudent[] = studentIds
    .map((id) => studentDirectory[id])
    .filter((entry): entry is TeacherStudent => Boolean(entry));

  return {
    classInfo,
    students,
  };
}

export async function updateTeacherClassCapacity(
  teacherId: string,
  classIdOrSection: string,
  capacity: number,
): Promise<TeacherClass> {
  if (useCloudApi()) {
    const encodedTeacherId = encodeURIComponent(teacherId);
    const encodedClassId = encodeURIComponent(classIdOrSection);
    await cloudRequest(`/teachers/${encodedTeacherId}/classes/${encodedClassId}/capacity`, {
      method: 'PUT',
      body: JSON.stringify({ capacity }),
    });

    const refreshed = await fetchTeacherRoster(teacherId, classIdOrSection);
    return refreshed.classInfo;
  }

  await wait(behavior.latencyMs);
  const classes = await fetchTeacherClasses(teacherId);
  const target = classes.find((item) => item.id === classIdOrSection || String(item.sectionId) === classIdOrSection);

  if (!target) {
    throw new ApiError(404, 'Class not found for this teacher.');
  }

  const enrolled = (rosterCache[target.id] ?? []).length;
  if (capacity < enrolled) {
    throw new ApiError(400, `Capacity cannot be lower than enrolled (${enrolled}).`);
  }

  classCache = classCache.map((item) => (item.id === target.id ? { ...item, capacity } : item));
  saveClasses();

  const updated = classCache.find((item) => item.id === target.id);
  if (!updated) {
    throw new ApiError(404, 'Updated class not found.');
  }

  return updated;
}

export async function removeStudentFromTeacherClass(
  teacherId: string,
  classIdOrSection: string,
  studentId: string,
): Promise<TeacherRoster> {
  if (useCloudApi()) {
    const encodedTeacherId = encodeURIComponent(teacherId);
    const encodedClassId = encodeURIComponent(classIdOrSection);
    const encodedStudentId = encodeURIComponent(studentId);
    await cloudRequest(`/teachers/${encodedTeacherId}/classes/${encodedClassId}/students/${encodedStudentId}`, {
      method: 'DELETE',
    });
    return fetchTeacherRoster(teacherId, classIdOrSection);
  }

  await wait(behavior.latencyMs);
  const classes = await fetchTeacherClasses(teacherId);
  const target = classes.find((item) => item.id === classIdOrSection || String(item.sectionId) === classIdOrSection);
  if (!target) {
    throw new ApiError(404, 'Class not found for this teacher.');
  }

  rosterCache[target.id] = (rosterCache[target.id] ?? []).filter((id) => id !== studentId);
  classCache = classCache.map((item) =>
    item.id === target.id
      ? {
          ...item,
          enrolledCount: Math.max(0, rosterCache[target.id].length),
        }
      : item,
  );

  // If this student had the class in their schedule, remove it there as well.
  const schedule = scheduleCache[studentId];
  if (schedule) {
    scheduleCache[studentId] = schedule.filter((item) => item.classId !== target.id);
  }

  saveRoster();
  saveClasses();
  saveSchedules();

  return fetchTeacherRoster(teacherId, target.id);
}

async function cloudRequest<T>(path: string, init?: RequestInit): Promise<T> {
  const base = runtimeConfig.apiBaseUrl.trim().replace(/\/$/, '');
  const normalizedPath =
    path.startsWith('/api/') || base.endsWith('/api')
      ? path
      : `/api${path.startsWith('/') ? path : `/${path}`}`;

  const response = await fetch(`${base}${normalizedPath}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(init?.headers ?? {}),
    },
  });

  if (!response.ok) {
    let message = `Request failed with status ${response.status}`;
    try {
      const payload = (await response.json()) as { message?: string };
      if (payload.message) {
        message = payload.message;
      }
    } catch {
      // ignore parse errors
    }
    throw new ApiError(response.status, message);
  }

  return (await response.json()) as T;
}

function useCloudApi(): boolean {
  return runtimeConfig.apiBaseUrl.trim().length > 0;
}

function normalizeCloudClass(input: Record<string, unknown>): ClassOffering {
  const room = String(input.room);
  return {
    sectionId: Number(input.sectionId),
    id: String(input.id),
    title: String(input.title),
    instructor: String(input.instructor),
    days: Array.isArray(input.days) ? input.days.map((item) => String(item)) as ClassOffering['days'] : ['Mon'],
    startTime: String(input.startTime),
    endTime: String(input.endTime),
    capacity: Number(input.capacity),
    enrolledCount: Number(input.enrolledCount),
    credits: Number(input.credits),
    room,
    location: input.location ? String(input.location) : room,
    term: String(input.term),
    colorHint: (input.colorHint ? String(input.colorHint) : 'neutral') as ClassOffering['colorHint'],
  };
}

function normalizeCloudScheduledClass(input: Record<string, unknown>): ScheduledClass {
  const room = String(input.room);
  return {
    sectionId: Number(input.sectionId),
    classId: String(input.classId),
    title: String(input.title),
    instructor: String(input.instructor),
    credits: Number(input.credits),
    room,
    location: input.location ? String(input.location) : room,
    term: String(input.term),
    days: Array.isArray(input.days)
      ? (input.days.map((item) => String(item)) as ScheduledClass['days'])
      : ['Mon'],
    startTime: String(input.startTime),
    endTime: String(input.endTime),
    colorHint: (input.colorHint ? String(input.colorHint) : 'neutral') as ScheduledClass['colorHint'],
  };
}

function wait(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function hydrateClasses(): ClassOffering[] {
  const saved = localStorage.getItem(CLASSES_STORAGE_KEY);
  if (saved) {
    const parsed = JSON.parse(saved) as ClassOffering[];
    return parsed.map((item) => ({
      ...item,
      location: item.location ?? item.room,
    }));
  }

  const defaults = mockClasses.map((item) => ({
    ...item,
    location: item.location ?? item.room,
  }));
  localStorage.setItem(CLASSES_STORAGE_KEY, JSON.stringify(defaults));
  return defaults;
}

function hydrateSchedules(): Record<string, ScheduledClass[]> {
  const saved = localStorage.getItem(SCHEDULE_STORAGE_KEY);
  if (!saved) {
    return {};
  }
  return JSON.parse(saved) as Record<string, ScheduledClass[]>;
}

function hydrateRoster(): Record<string, string[]> {
  const saved = localStorage.getItem(ROSTER_STORAGE_KEY);
  if (saved) {
    return JSON.parse(saved) as Record<string, string[]>;
  }

  const defaults = generateDefaultRoster(classCache);
  localStorage.setItem(ROSTER_STORAGE_KEY, JSON.stringify(defaults));
  return defaults;
}

function hydrateStudentDirectory(
  roster: Record<string, string[]>,
  forceGenerate = false,
): Record<string, TeacherStudent> {
  if (!forceGenerate) {
    const saved = localStorage.getItem(STUDENT_DIRECTORY_KEY);
    if (saved) {
      return JSON.parse(saved) as Record<string, TeacherStudent>;
    }
  }

  const directory: Record<string, TeacherStudent> = {
    'student-123': {
      studentId: 'student-123',
      name: 'John Smith',
      email: 'john.smith@email.com',
    },
    'student-456': {
      studentId: 'student-456',
      name: 'Ava Thomas',
      email: 'ava.thomas@email.com',
    },
  };

  let generatedIndex = 1;
  Object.values(roster).forEach((ids) => {
    ids.forEach((id) => {
      if (!directory[id]) {
        directory[id] = {
          studentId: id,
          name: `Student ${generatedIndex}`,
          email: `student${generatedIndex}@email.com`,
        };
        generatedIndex += 1;
      }
    });
  });

  localStorage.setItem(STUDENT_DIRECTORY_KEY, JSON.stringify(directory));
  return directory;
}

function generateDefaultRoster(classes: ClassOffering[]): Record<string, string[]> {
  const roster: Record<string, string[]> = {};
  classes.forEach((item) => {
    roster[item.id] = Array.from({ length: item.enrolledCount }).map(
      (_, index) => `mock-${item.id}-${index + 1}`,
    );
  });
  return roster;
}

function syncClassEnrollmentWithRoster(): void {
  classCache = classCache.map((item) => ({
    ...item,
    enrolledCount: rosterCache[item.id]?.length ?? 0,
  }));
  saveClasses();
}

function saveClasses(): void {
  localStorage.setItem(CLASSES_STORAGE_KEY, JSON.stringify(classCache));
}

function saveSchedules(): void {
  localStorage.setItem(SCHEDULE_STORAGE_KEY, JSON.stringify(scheduleCache));
}

function saveRoster(): void {
  localStorage.setItem(ROSTER_STORAGE_KEY, JSON.stringify(rosterCache));
}

function saveStudentDirectory(): void {
  localStorage.setItem(STUDENT_DIRECTORY_KEY, JSON.stringify(studentDirectory));
}
