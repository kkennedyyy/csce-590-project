import { runtimeConfig } from '../config/runtime';
import { mockClasses } from '../data/mockClasses';
import type {
  ApiBehavior,
  AuthUser,
  ClassOffering,
  ClassPage,
  Day,
  MeetingTime,
  RegisteredClass,
  ScheduledClass,
  StudentSchedule,
  TeacherCatalog,
  TeacherCatalogPage,
  TeacherClass,
  TeacherClassUpdateInput,
  TeacherRoster,
  TeacherStudent,
  UserRole,
} from '../types';
import { calculateCurrentCredits, MAX_CREDITS } from '../utils/validators';

const SCHEDULE_STORAGE_KEY = 'classfinder.schedules.v2';
const CLASSES_STORAGE_KEY = 'classfinder.classes.v2';
const ROSTER_STORAGE_KEY = 'classfinder.rosters.v1';
const WAITLIST_STORAGE_KEY = 'classfinder.waitlists.v1';
const ROSTER_DATES_STORAGE_KEY = 'classfinder.roster-dates.v1';
const STUDENT_DIRECTORY_KEY = 'classfinder.student-directory.v1';
const AUTH_USERS_STORAGE_KEY = 'classfinder.auth-users.v1';

const defaultBehavior: ApiBehavior = {
  latencyMs: 220,
  forceConflict: false,
  forceCapacityLocked: false,
  forceCreditExceeded: false,
};

const defaultMockUsers: Array<AuthUser & { password: string }> = [
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
    name: 'Dr. Brown',
    email: 'brown@email.com',
    password: 'teacher123',
  },
];

let mockUsers = hydrateMockUsers();

const mockTeacherInstructorName: Record<string, string> = {
  'teacher-1': 'Dr. Smith',
  'teacher-2': 'Dr. Brown',
};

const defaultCompletedCourseHistoryByStudent: Record<string, string[]> = {
  'student-123': ['CSCE101', 'CSCE210'],
  'student-456': ['MATH151'],
};

let behavior = { ...defaultBehavior };
let classCache = hydrateClasses();
let scheduleCache = hydrateSchedules();
let rosterCache = hydrateRoster();
let waitlistCache = hydrateWaitlists();
let rosterDateCache = hydrateRosterDates(rosterCache);
let studentDirectory = hydrateStudentDirectory(rosterCache);
let studentCompletedCourseHistory = createDefaultCompletedCourseHistory();
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
  mockUsers = defaultMockUsers.map((item) => ({ ...item }));
  classCache = mockClasses.map((item) => ({ ...item }));
  scheduleCache = {};
  rosterCache = generateDefaultRoster(classCache);
  waitlistCache = {};
  rosterDateCache = createDefaultRosterDates(rosterCache);
  studentDirectory = hydrateStudentDirectory(rosterCache, true);
  studentCompletedCourseHistory = createDefaultCompletedCourseHistory();
  syncClassEnrollmentWithRoster();
  saveClasses();
  saveSchedules();
  saveRoster();
  saveWaitlists();
  saveRosterDates();
  saveStudentDirectory();
  saveMockUsers();
  behavior = { ...defaultBehavior };
}

export function setClassEnrollment(classId: string, enrolledCount: number): void {
  classCache = classCache.map((item) =>
    item.id === classId
      ? {
          ...item,
          enrolledCount: Math.max(0, enrolledCount),
          availableSeats: Math.max(0, item.capacity - Math.max(0, enrolledCount)),
        }
      : item,
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

  syncClassEnrollmentWithRoster();
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

export async function signupStudent(payload: {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  major?: string;
  classification?: string;
}): Promise<AuthUser> {
  if (useCloudApi()) {
    const data = await cloudRequest<{ user: AuthUser }>('/auth/signup/student', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
    return data.user;
  }

  await wait(behavior.latencyMs);

  const firstName = payload.firstName.trim();
  const lastName = payload.lastName.trim();
  const email = payload.email.trim().toLowerCase();
  const password = payload.password.trim();

  if (!firstName || !lastName) {
    throw new ApiError(400, 'First name and last name are required.');
  }
  if (!email.includes('@')) {
    throw new ApiError(400, 'Enter a valid email address.');
  }
  if (password.length < 8) {
    throw new ApiError(400, 'Password must be at least 8 characters long.');
  }
  if (mockUsers.some((item) => item.email.toLowerCase() === email)) {
    throw new ApiError(409, 'An account already exists for that email address.');
  }

  const userId = `student-${Date.now().toString(36)}${Math.floor(Math.random() * 4096).toString(36)}`;
  const user: AuthUser = {
    userId,
    role: 'student',
    name: `${firstName} ${lastName}`,
    email,
  };

  mockUsers.push({
    ...user,
    password,
  });
  studentDirectory[userId] = {
    studentId: userId,
    name: user.name,
    email,
  };
  scheduleCache[userId] = [];
  studentCompletedCourseHistory[userId] = [];
  saveMockUsers();
  saveStudentDirectory();
  saveSchedules();

  return user;
}

export async function fetchClasses(params: {
  page: number;
  pageSize?: number;
  search?: string;
  department?: string;
  studentId?: string;
}): Promise<ClassPage> {
  if (useCloudApi()) {
    const query = new URLSearchParams();
    query.set('page', String(Math.max(1, params.page)));
    query.set('pageSize', String(params.pageSize ?? 10));
    if (params.search?.trim()) {
      query.set('search', params.search.trim());
    }
    if (params.department?.trim()) {
      query.set('department', params.department.trim());
    }
    if (params.studentId?.trim()) {
      query.set('studentId', params.studentId.trim());
    }

    const data = await cloudRequest<{
      classes: Array<Record<string, unknown>>;
      departments?: string[];
      page: number;
      pageSize: number;
      hasMore: boolean;
      total: number;
    }>(`/classes?${query.toString()}`);

    return {
      classes: data.classes.map(normalizeCloudClass),
      departments: data.departments ?? [],
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
  const department = params.department?.trim().toLowerCase();

  const filtered = !search
    ? classCache
    : classCache.filter(
        (item) =>
          item.id.toLowerCase().includes(search) ||
          item.title.toLowerCase().includes(search) ||
          item.instructor.toLowerCase().includes(search) ||
          (item.location ?? item.room).toLowerCase().includes(search) ||
          (item.department ?? '').toLowerCase().includes(search) ||
          (item.departmentCode ?? '').toLowerCase().includes(search),
      );
  const departmentFiltered = !department
    ? filtered
    : filtered.filter(
        (item) =>
          (item.department ?? '').toLowerCase() === department ||
          (item.departmentCode ?? '').toLowerCase() === department,
      );

  const start = (page - 1) * pageSize;
  const slice = departmentFiltered.slice(start, start + pageSize);

  return {
    classes: slice.map((item) => (params.studentId ? applyStudentScheduleState(item, params.studentId) : { ...item })),
    departments: Array.from(
      new Set(classCache.map((item) => item.department ?? inferDepartmentName(item.id)).filter(Boolean)),
    ).sort(),
    page,
    pageSize,
    hasMore: start + pageSize < departmentFiltered.length,
    total: departmentFiltered.length,
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
      registeredClasses?: Array<Record<string, unknown>>;
      currentCredits: number;
    }>(`/students/${encodedStudentId}/schedule/state`);

    const scheduledClasses = data.scheduledClasses.map(normalizeCloudScheduledClass);

    return {
      studentId: data.studentId,
      scheduledClasses,
      registeredClasses: Array.isArray(data.registeredClasses)
        ? data.registeredClasses.map(normalizeCloudRegisteredClass)
        : scheduledClasses.map((item) => normalizeScheduledToRegistered(item)),
      currentCredits: data.currentCredits,
    };
  }

  await wait(behavior.latencyMs);
  return buildStudentSchedule(studentId);
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
      registeredClasses?: Array<Record<string, unknown>>;
      currentCredits: number;
    }>(`/students/${encodedStudentId}/schedule`, {
      method: 'POST',
      body: JSON.stringify({ sectionId: payload.sectionId, classId: payload.classId }),
    });

    return {
      studentId: data.studentId,
      scheduledClasses: data.scheduledClasses.map(normalizeCloudScheduledClass),
      registeredClasses: Array.isArray(data.registeredClasses)
        ? data.registeredClasses.map(normalizeCloudRegisteredClass)
        : data.scheduledClasses.map((item) => normalizeScheduledToRegistered(normalizeCloudScheduledClass(item))),
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

  const schedule = await fetchSchedule(payload.studentId);

  if (schedule.scheduledClasses.some((item) => item.classId === classRecord.id)) {
    throw new ApiError(409, `${classRecord.id} already added.`);
  }
  if ((waitlistCache[classRecord.id] ?? []).includes(payload.studentId)) {
    throw new ApiError(409, `You are already waitlisted for ${classRecord.id}.`);
  }

  const satisfiedCourseCodes = new Set<string>([
    ...(studentCompletedCourseHistory[payload.studentId] ?? []),
    ...schedule.scheduledClasses.map((item) => extractCourseCode(item.classId)),
  ]);
  const unmetPrerequisites = (classRecord.prerequisites ?? []).filter(
    (courseCode) => !satisfiedCourseCodes.has(courseCode),
  );

  if (unmetPrerequisites.length > 0) {
    throw new ApiError(403, `Missing prerequisites: ${unmetPrerequisites.join(', ')}.`);
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

  if (behavior.forceConflict || hasScheduleConflict(schedule.scheduledClasses, scheduledClass)) {
    throw new ApiError(409, 'Overlap detected.');
  }

  const studentId = payload.studentId;
  if (classRecord.enrolledCount >= classRecord.capacity) {
    if (!waitlistCache[classRecord.id]) {
      waitlistCache[classRecord.id] = [];
    }
    waitlistCache[classRecord.id].push(studentId);
    saveWaitlists();
    return buildStudentSchedule(studentId);
  }

  classCache = classCache.map((item) =>
    item.id === classRecord.id
      ? {
          ...item,
          enrolledCount: item.enrolledCount + 1,
          availableSeats: Math.max(0, item.capacity - (item.enrolledCount + 1)),
        }
      : item,
  );

  scheduleCache[studentId] = nextClasses;
  if (!rosterCache[classRecord.id]) {
    rosterCache[classRecord.id] = [];
  }
  if (!rosterCache[classRecord.id].includes(studentId)) {
    rosterCache[classRecord.id].push(studentId);
  }

  saveClasses();
  saveSchedules();
  saveRoster();
  syncRosterEnrollmentDates();
  saveRosterDates();
  return buildStudentSchedule(studentId);
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
      registeredClasses?: Array<Record<string, unknown>>;
      currentCredits: number;
    }>(`/students/${encodedStudentId}/schedule/${encodedClassId}`, {
      method: 'DELETE',
    });

    return {
      studentId: data.studentId,
      scheduledClasses: data.scheduledClasses.map(normalizeCloudScheduledClass),
      registeredClasses: Array.isArray(data.registeredClasses)
        ? data.registeredClasses.map(normalizeCloudRegisteredClass)
        : data.scheduledClasses.map((item) => normalizeScheduledToRegistered(normalizeCloudScheduledClass(item))),
      currentCredits: data.currentCredits,
    };
  }

  await wait(behavior.latencyMs);
  const studentId = payload.studentId;
  const schedule = await fetchSchedule(studentId);
  const existing = schedule.scheduledClasses.find((item) => item.classId === payload.classId);
  const classRecord = classCache.find((item) => item.id === payload.classId);

  if (classRecord?.dropDeadlineUtc && new Date(classRecord.dropDeadlineUtc).getTime() < Date.now()) {
    throw new ApiError(403, `The drop deadline for ${classRecord.id} has passed.`);
  }

  if (!existing) {
    if ((waitlistCache[payload.classId] ?? []).includes(studentId)) {
      waitlistCache[payload.classId] = (waitlistCache[payload.classId] ?? []).filter((id) => id !== studentId);
      saveWaitlists();
      return buildStudentSchedule(studentId);
    }

    return schedule;
  }

  const nextClasses = schedule.scheduledClasses.filter((item) => item.classId !== payload.classId);

  classCache = classCache.map((item) =>
    item.id === payload.classId
      ? {
          ...item,
          enrolledCount: Math.max(0, item.enrolledCount - 1),
          availableSeats: Math.max(0, item.capacity - Math.max(0, item.enrolledCount - 1)),
        }
      : item,
  );

  scheduleCache[studentId] = nextClasses;
  rosterCache[payload.classId] = (rosterCache[payload.classId] ?? []).filter((id) => id !== studentId);
  const nextWaitlistedStudent = waitlistCache[payload.classId]?.shift();
  if (nextWaitlistedStudent) {
    scheduleCache[nextWaitlistedStudent] = [
      ...(scheduleCache[nextWaitlistedStudent] ?? []),
      createScheduledClass(classRecord!, {
        days: classRecord!.days,
        startTime: classRecord!.startTime,
        endTime: classRecord!.endTime,
      }),
    ];
    rosterCache[payload.classId] = [...(rosterCache[payload.classId] ?? []), nextWaitlistedStudent];
  }

  saveClasses();
  saveSchedules();
  saveRoster();
  saveWaitlists();
  syncClassEnrollmentWithRoster();
  return buildStudentSchedule(studentId);
}

export async function finalizeSchedule(payload: {
  studentId: string;
  scheduledClasses: ScheduledClass[];
}): Promise<StudentSchedule> {
  if (useCloudApi()) {
    const encodedStudentId = encodeURIComponent(payload.studentId);
    const data = await cloudRequest<{
      studentId: string;
      scheduledClasses: Array<Record<string, unknown>>;
      registeredClasses?: Array<Record<string, unknown>>;
      currentCredits: number;
    }>(`/students/${encodedStudentId}/schedule/finalize`, {
      method: 'POST',
      body: JSON.stringify({
        scheduledClasses: payload.scheduledClasses.map((item) => ({
          sectionId: item.sectionId,
          classId: item.classId,
        })),
      }),
    });

    return {
      studentId: data.studentId,
      scheduledClasses: data.scheduledClasses.map(normalizeCloudScheduledClass),
      registeredClasses: Array.isArray(data.registeredClasses)
        ? data.registeredClasses.map(normalizeCloudRegisteredClass)
        : data.scheduledClasses.map((item) => normalizeScheduledToRegistered(normalizeCloudScheduledClass(item))),
      currentCredits: data.currentCredits,
    };
  }

  await wait(behavior.latencyMs);
  const previous = scheduleCache[payload.studentId] ?? [];
  const persisted = payload.scheduledClasses.map((item) => ({ ...item }));
  const previousClassIds = new Set(previous.map((item) => item.classId));
  const nextClassIds = new Set(persisted.map((item) => item.classId));

  previousClassIds.forEach((classId) => {
    if (nextClassIds.has(classId)) {
      return;
    }
    rosterCache[classId] = (rosterCache[classId] ?? []).filter((id) => id !== payload.studentId);
  });

  nextClassIds.forEach((classId) => {
    if (previousClassIds.has(classId)) {
      return;
    }
    if (!rosterCache[classId]) {
      rosterCache[classId] = [];
    }
    if (!rosterCache[classId].includes(payload.studentId)) {
      rosterCache[classId].push(payload.studentId);
    }
  });

  Object.keys(waitlistCache).forEach((classId) => {
    waitlistCache[classId] = (waitlistCache[classId] ?? []).filter((id) => id !== payload.studentId);
  });

  scheduleCache[payload.studentId] = persisted;
  syncClassEnrollmentWithRoster();
  saveSchedules();
  saveRoster();
  saveWaitlists();

  return buildStudentSchedule(payload.studentId);
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

export async function fetchTeacherCatalog(params: {
  search?: string;
  department?: string;
  studentId?: string;
}): Promise<TeacherCatalogPage> {
  if (useCloudApi()) {
    const query = new URLSearchParams();
    if (params.search?.trim()) {
      query.set('search', params.search.trim());
    }
    if (params.department?.trim()) {
      query.set('department', params.department.trim());
    }
    if (params.studentId?.trim()) {
      query.set('studentId', params.studentId.trim());
    }

    const suffix = query.toString() ? `?${query.toString()}` : '';
    const data = await cloudRequest<{
      teachers: Array<Record<string, unknown>>;
      departments: string[];
      total: number;
    }>(`/teachers${suffix}`);

    return {
      teachers: data.teachers.map(normalizeCloudTeacherCatalog),
      departments: data.departments,
      total: data.total,
    };
  }

  await wait(behavior.latencyMs);
  const teacherMap = new Map<string, TeacherCatalog>();
  classCache.forEach((item) => {
    const teacherId = Object.entries(mockTeacherInstructorName).find(
      ([, value]) => value === item.instructor,
    )?.[0] ?? item.instructor.toLowerCase().replace(/\s+/g, '-');
    const existing = teacherMap.get(teacherId);
    const classWithState = params.studentId
      ? applyStudentScheduleState(item, params.studentId)
      : { ...item };

    if (existing) {
      existing.classes.push(classWithState);
      return;
    }

    teacherMap.set(teacherId, {
      teacherId,
      name: item.instructor,
      email: `${teacherId.replace(/^teacher-/, 'faculty-')}@university.edu`,
      department: item.department ?? inferDepartmentName(item.id),
      classes: [classWithState],
    });
  });

  let teachers = Array.from(teacherMap.values())
    .map((teacher) => ({
      ...teacher,
      classes: teacher.classes.sort((left, right) => left.id.localeCompare(right.id)),
    }))
    .sort((left, right) => left.name.localeCompare(right.name));

  if (params.department?.trim()) {
    const filter = params.department.trim().toLowerCase();
    teachers = teachers
      .map((teacher) => ({
        ...teacher,
        classes: teacher.classes.filter(
          (item) =>
            (item.department ?? '').toLowerCase() === filter ||
            (item.departmentCode ?? '').toLowerCase() === filter,
        ),
      }))
      .filter((teacher) => teacher.classes.length > 0);
  }

  if (params.search?.trim()) {
    const term = params.search.trim().toLowerCase();
    teachers = teachers
      .map((teacher) => {
        const teacherMatches =
          teacher.name.toLowerCase().includes(term) || teacher.email.toLowerCase().includes(term);

        if (teacherMatches) {
          return teacher;
        }

        return {
          ...teacher,
          classes: teacher.classes.filter(
            (item) =>
              item.id.toLowerCase().includes(term) ||
              item.title.toLowerCase().includes(term) ||
              (item.department ?? '').toLowerCase().includes(term),
          ),
        };
      })
      .filter((teacher) => teacher.classes.length > 0);
  }

  const departments = Array.from(
    new Set(classCache.map((item) => item.department ?? inferDepartmentName(item.id)).filter(Boolean)),
  ).sort();

  return {
    teachers,
    departments,
    total: teachers.length,
  };
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
        enrollmentDateUtc: item.enrollmentDateUtc ? String(item.enrollmentDateUtc) : null,
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
      students: students.map((student) => ({
        ...student,
        enrollmentDateUtc: rosterDateCache[classInfo.id]?.[student.studentId] ?? null,
      })),
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
  classCache = classCache.map((item) =>
    item.id === target.id
      ? { ...item, availableSeats: Math.max(0, capacity - item.enrolledCount) }
      : item,
  );
  saveClasses();

  const updated = classCache.find((item) => item.id === target.id);
  if (!updated) {
    throw new ApiError(404, 'Updated class not found.');
  }

  return updated;
}

export async function updateTeacherClass(
  teacherId: string,
  classIdOrSection: string,
  payload: TeacherClassUpdateInput,
): Promise<TeacherClass> {
  if (useCloudApi()) {
    const encodedTeacherId = encodeURIComponent(teacherId);
    const encodedClassId = encodeURIComponent(classIdOrSection);
    const data = await cloudRequest<Record<string, unknown>>(
      `/teachers/${encodedTeacherId}/classes/${encodedClassId}`,
      {
        method: 'PUT',
        body: JSON.stringify(payload),
      },
    );

    return normalizeCloudClass(data);
  }

  await wait(behavior.latencyMs);
  const classes = await fetchTeacherClasses(teacherId);
  const target = classes.find((item) => item.id === classIdOrSection || String(item.sectionId) === classIdOrSection);

  if (!target) {
    throw new ApiError(404, 'Class not found for this teacher.');
  }

  const title = payload.title.trim();
  const location = payload.location.trim();
  const days = payload.days.filter((day): day is Day => day.trim().length > 0);
  const enrolled = (rosterCache[target.id] ?? []).length;

  if (!title) {
    throw new ApiError(400, 'Class title is required.');
  }
  if (!location) {
    throw new ApiError(400, 'Class location is required.');
  }
  if (days.length === 0) {
    throw new ApiError(400, 'Select at least one meeting day.');
  }
  if (!Number.isInteger(payload.capacity) || payload.capacity < enrolled) {
    throw new ApiError(400, `Capacity cannot be lower than enrolled (${enrolled}).`);
  }
  if (!/^\d{2}:\d{2}$/.test(payload.startTime) || !/^\d{2}:\d{2}$/.test(payload.endTime)) {
    throw new ApiError(400, 'Enter valid start and end times in HH:mm format.');
  }
  if (payload.startTime >= payload.endTime) {
    throw new ApiError(400, 'End time must be later than start time.');
  }

  classCache = classCache.map((item) =>
    item.id === target.id
      ? {
          ...item,
          title,
          location,
          room: location,
          capacity: payload.capacity,
          availableSeats: Math.max(0, payload.capacity - item.enrolledCount),
          days,
          startTime: payload.startTime,
          endTime: payload.endTime,
        }
      : item,
  );
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
          availableSeats: Math.max(0, item.capacity - Math.max(0, rosterCache[target.id].length)),
        }
      : item,
  );

  // If this student had the class in their schedule, remove it there as well.
  const schedule = scheduleCache[studentId];
  if (schedule) {
    scheduleCache[studentId] = schedule.filter((item) => item.classId !== target.id);
  }

  const nextWaitlistedStudent = waitlistCache[target.id]?.shift();
  if (nextWaitlistedStudent) {
    scheduleCache[nextWaitlistedStudent] = [
      ...(scheduleCache[nextWaitlistedStudent] ?? []),
      createScheduledClass(target, {
        days: target.days,
        startTime: target.startTime,
        endTime: target.endTime,
      }),
    ];
    rosterCache[target.id] = [...(rosterCache[target.id] ?? []), nextWaitlistedStudent];
  }

  saveRoster();
  saveClasses();
  saveSchedules();
  saveWaitlists();
  syncClassEnrollmentWithRoster();

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
  const room = String(input.room ?? input.location ?? '');
  const departmentCode =
    input.departmentCode !== undefined && input.departmentCode !== null
      ? String(input.departmentCode)
      : inferDepartmentCode(String(input.id));
  return {
    sectionId:
      input.sectionId === undefined || input.sectionId === null ? undefined : Number(input.sectionId),
    id: String(input.id),
    externalId: input.externalId ? String(input.externalId) : undefined,
    title: String(input.title),
    department: input.department ? String(input.department) : inferDepartmentName(String(input.id)),
    departmentCode,
    courseNumber: input.courseNumber ? Number(input.courseNumber) : inferCourseNumber(String(input.id)),
    sessionCode: input.sessionCode ? String(input.sessionCode) : inferSessionCode(String(input.id)),
    instructor: String(input.instructor),
    instructorId: input.instructorId ? String(input.instructorId) : undefined,
    days: Array.isArray(input.days) ? input.days.map((item) => String(item)) as ClassOffering['days'] : ['Mon'],
    startTime: String(input.startTime),
    endTime: String(input.endTime),
    capacity: Number(input.capacity),
    enrolledCount: Number(input.enrolledCount),
    availableSeats:
      input.availableSeats === undefined || input.availableSeats === null
        ? undefined
        : Number(input.availableSeats),
    credits: Number(input.credits),
    room,
    location: input.location ? String(input.location) : room,
    term: String(input.term),
    colorHint: (input.colorHint ? String(input.colorHint) : 'neutral') as ClassOffering['colorHint'],
    isStudentEnrolled: Boolean(input.isStudentEnrolled),
    isStudentWaitlisted: Boolean(input.isStudentWaitlisted),
    studentWaitlistPosition:
      input.studentWaitlistPosition === null || input.studentWaitlistPosition === undefined
        ? null
        : Number(input.studentWaitlistPosition),
    enrollmentStatus: (input.enrollmentStatus ? String(input.enrollmentStatus) : 'NotEnrolled') as ClassOffering['enrollmentStatus'],
    prerequisites: Array.isArray(input.prerequisites)
      ? input.prerequisites.map((item) => String(item))
      : [],
    dropDeadlineUtc: input.dropDeadlineUtc ? String(input.dropDeadlineUtc) : null,
  };
}

function normalizeCloudTeacherCatalog(input: Record<string, unknown>): TeacherCatalog {
  return {
    teacherId: String(input.teacherId),
    externalId: input.externalId ? String(input.externalId) : undefined,
    name: String(input.name ?? ''),
    email: String(input.email ?? ''),
    department: String(input.department ?? ''),
    classes: Array.isArray(input.classes)
      ? input.classes.map((item) => normalizeCloudClass(item as Record<string, unknown>))
      : [],
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

function normalizeCloudRegisteredClass(input: Record<string, unknown>): RegisteredClass {
  const scheduled = normalizeCloudScheduledClass(input);
  return {
    ...scheduled,
    courseCode: String(input.courseCode ?? extractCourseCode(scheduled.classId)),
    enrollmentStatus: (input.enrollmentStatus ? String(input.enrollmentStatus) : 'Enrolled') as RegisteredClass['enrollmentStatus'],
    waitlistPosition:
      input.waitlistPosition === null || input.waitlistPosition === undefined
        ? null
        : Number(input.waitlistPosition),
    capacity: Number(input.capacity ?? 0),
    enrolledCount: Number(input.enrolledCount ?? 0),
    availableSeats: Number(input.availableSeats ?? 0),
  };
}

function normalizeScheduledToRegistered(input: ScheduledClass): RegisteredClass {
  const classRecord = classCache.find((item) => item.id === input.classId);
  return {
    ...input,
    courseCode: extractCourseCode(input.classId),
    enrollmentStatus: 'Enrolled',
    waitlistPosition: null,
    capacity: classRecord?.capacity ?? 0,
    enrolledCount: classRecord?.enrolledCount ?? 0,
    availableSeats: classRecord?.availableSeats ?? 0,
  };
}

function wait(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function createDefaultCompletedCourseHistory(): Record<string, string[]> {
  return Object.fromEntries(
    Object.entries(defaultCompletedCourseHistoryByStudent).map(([studentId, courseCodes]) => [
      studentId,
      [...courseCodes],
    ]),
  );
}

function hydrateClasses(): ClassOffering[] {
  const saved = localStorage.getItem(CLASSES_STORAGE_KEY);
  if (saved) {
    const parsed = JSON.parse(saved) as ClassOffering[];
    return parsed.map(enrichClassDefaults);
  }

  const defaults = mockClasses.map(enrichClassDefaults);
  localStorage.setItem(CLASSES_STORAGE_KEY, JSON.stringify(defaults));
  return defaults;
}

function hydrateMockUsers(): Array<AuthUser & { password: string }> {
  const saved = localStorage.getItem(AUTH_USERS_STORAGE_KEY);
  if (saved) {
    return JSON.parse(saved) as Array<AuthUser & { password: string }>;
  }

  const defaults = defaultMockUsers.map((item) => ({ ...item }));
  localStorage.setItem(AUTH_USERS_STORAGE_KEY, JSON.stringify(defaults));
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

function hydrateRosterDates(roster: Record<string, string[]>): Record<string, Record<string, string>> {
  const saved = localStorage.getItem(ROSTER_DATES_STORAGE_KEY);
  if (saved) {
    return JSON.parse(saved) as Record<string, Record<string, string>>;
  }

  const defaults = createDefaultRosterDates(roster);
  localStorage.setItem(ROSTER_DATES_STORAGE_KEY, JSON.stringify(defaults));
  return defaults;
}

function hydrateWaitlists(): Record<string, string[]> {
  const saved = localStorage.getItem(WAITLIST_STORAGE_KEY);
  if (!saved) {
    return {};
  }

  return JSON.parse(saved) as Record<string, string[]>;
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
  syncRosterEnrollmentDates();
  classCache = classCache.map((item) => ({
    ...item,
    enrolledCount: rosterCache[item.id]?.length ?? 0,
    availableSeats: Math.max(0, item.capacity - (rosterCache[item.id]?.length ?? 0)),
  }));
  saveClasses();
  saveRosterDates();
}

function syncRosterEnrollmentDates(): void {
  const nextDates: Record<string, Record<string, string>> = {};

  Object.entries(rosterCache).forEach(([classId, studentIds]) => {
    nextDates[classId] = {};
    studentIds.forEach((studentId, index) => {
      nextDates[classId]![studentId] =
        rosterDateCache[classId]?.[studentId]
        ?? new Date(Date.now() - index * 60_000).toISOString();
    });
  });

  rosterDateCache = nextDates;
}

function createDefaultRosterDates(roster: Record<string, string[]>): Record<string, Record<string, string>> {
  const baseTimestamp = Date.UTC(2026, 0, 15, 14, 0, 0);
  const dates: Record<string, Record<string, string>> = {};

  Object.entries(roster).forEach(([classId, studentIds], classIndex) => {
    dates[classId] = {};
    studentIds.forEach((studentId, studentIndex) => {
      dates[classId]![studentId] = new Date(
        baseTimestamp - (classIndex * 12 + studentIndex) * 86_400_000,
      ).toISOString();
    });
  });

  return dates;
}

function enrichClassDefaults(item: ClassOffering): ClassOffering {
  const departmentCode = item.departmentCode ?? inferDepartmentCode(item.id);
  return {
    ...item,
    department: item.department ?? inferDepartmentName(item.id),
    departmentCode,
    courseNumber: item.courseNumber ?? inferCourseNumber(item.id),
    sessionCode: item.sessionCode ?? inferSessionCode(item.id),
    location: item.location ?? item.room,
    availableSeats: item.availableSeats ?? Math.max(0, item.capacity - item.enrolledCount),
    enrollmentStatus: item.enrollmentStatus ?? 'NotEnrolled',
    prerequisites: item.prerequisites ?? defaultPrerequisites(item.id),
  };
}

function applyStudentScheduleState(item: ClassOffering, studentId: string): ClassOffering {
  const isEnrolled = (scheduleCache[studentId] ?? []).some((scheduled) => scheduled.classId === item.id);
  const waitlistPosition = (waitlistCache[item.id] ?? []).findIndex((id) => id === studentId);
  return {
    ...enrichClassDefaults(item),
    isStudentEnrolled: isEnrolled,
    isStudentWaitlisted: waitlistPosition >= 0,
    studentWaitlistPosition: waitlistPosition >= 0 ? waitlistPosition + 1 : null,
    enrollmentStatus: isEnrolled ? 'Enrolled' : waitlistPosition >= 0 ? 'Waitlisted' : 'NotEnrolled',
  };
}

function buildStudentSchedule(studentId: string): StudentSchedule {
  const scheduledClasses = scheduleCache[studentId] ?? [];
  return {
    studentId,
    scheduledClasses,
    registeredClasses: buildRegisteredClasses(studentId),
    currentCredits: calculateCurrentCredits(scheduledClasses),
  };
}

function buildRegisteredClasses(studentId: string): RegisteredClass[] {
  const enrolled = (scheduleCache[studentId] ?? []).map((item) => normalizeScheduledToRegistered(item));
  const waitlisted = Object.entries(waitlistCache)
    .flatMap(([classId, ids]) => {
      const position = ids.findIndex((id) => id === studentId);
      if (position < 0) {
        return [];
      }

      const classRecord = classCache.find((item) => item.id === classId);
      if (!classRecord) {
        return [];
      }

      return [
        {
          ...createScheduledClass(classRecord, {
            days: classRecord.days,
            startTime: classRecord.startTime,
            endTime: classRecord.endTime,
          }),
          courseCode: extractCourseCode(classId),
          enrollmentStatus: 'Waitlisted' as const,
          waitlistPosition: position + 1,
          capacity: classRecord.capacity,
          enrolledCount: classRecord.enrolledCount,
          availableSeats: classRecord.availableSeats ?? 0,
        },
      ];
    })
    .sort((left, right) => left.classId.localeCompare(right.classId));

  return [...enrolled, ...waitlisted];
}

function createScheduledClass(
  classRecord: ClassOffering,
  meeting: Pick<MeetingTime, 'days' | 'startTime' | 'endTime'>,
): ScheduledClass {
  return {
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
}

function extractCourseCode(classId: string): string {
  return classId.split('-')[0] ?? classId;
}

function hasScheduleConflict(schedule: ScheduledClass[], candidate: ScheduledClass): boolean {
  return schedule.some((scheduledClass) => {
    const sharedDay = scheduledClass.days.some((day) => candidate.days.includes(day));
    if (!sharedDay) {
      return false;
    }

    return candidate.startTime < scheduledClass.endTime && scheduledClass.startTime < candidate.endTime;
  });
}

function inferDepartmentCode(classId: string): string {
  return (classId.match(/^[A-Z]+/)?.[0] ?? 'GEN').toUpperCase();
}

function inferDepartmentName(classId: string): string {
  const code = inferDepartmentCode(classId);
  return (
    {
      CSCE: 'Computer Science',
      MATH: 'Mathematics',
      PHYS: 'Physics',
      HIST: 'History',
      ENGL: 'English',
      CHEM: 'Chemistry',
      STAT: 'Statistics',
      BIOL: 'Biology',
      ARTS: 'Art',
      PHIL: 'Philosophy',
      ECON: 'Economics',
      MUSC: 'Music',
      PSYC: 'Psychology',
    }[code] ?? code
  );
}

function inferCourseNumber(classId: string): number | undefined {
  const digits = classId.match(/(\d{3})/);
  return digits ? Number(digits[1]) : undefined;
}

function inferSessionCode(classId: string): string | undefined {
  return classId.split('-')[1];
}

function defaultPrerequisites(classId: string): string[] {
  if (classId.startsWith('CSCE210')) {
    return ['CSCE101'];
  }
  if (classId.startsWith('CSCE312') || classId.startsWith('CSCE331')) {
    return ['CSCE210'];
  }
  if (classId.startsWith('CSCE420') || classId.startsWith('CSCE451')) {
    return ['CSCE331'];
  }
  return [];
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

function saveWaitlists(): void {
  localStorage.setItem(WAITLIST_STORAGE_KEY, JSON.stringify(waitlistCache));
}

function saveRosterDates(): void {
  localStorage.setItem(ROSTER_DATES_STORAGE_KEY, JSON.stringify(rosterDateCache));
}

function saveStudentDirectory(): void {
  localStorage.setItem(STUDENT_DIRECTORY_KEY, JSON.stringify(studentDirectory));
}

function saveMockUsers(): void {
  localStorage.setItem(AUTH_USERS_STORAGE_KEY, JSON.stringify(mockUsers));
}
