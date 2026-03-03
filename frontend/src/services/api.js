const API_BASE_URL =
  (typeof window !== 'undefined' && window.__APP_API_BASE_URL__) || 'http://localhost:8080';

async function request(path) {
  const response = await fetch(`${API_BASE_URL}${path}`);
  if (!response.ok) {
    throw new Error(`API error (${response.status}) for ${path}`);
  }
  return response.json();
}

export function getStudentClasses(studentId) {
  return request(`/api/students/${studentId}/classes`);
}

export function getStudentSchedule(studentId) {
  return request(`/api/students/${studentId}/schedule`);
}

export function getClassDetails(classId) {
  return request(`/api/classes/${classId}`);
}
