import { useEffect, useMemo, useState } from 'react';
import { getStudentClasses, getStudentSchedule } from '../services/api';
import CalendarView from './CalendarView';
import ClassCard from './ClassCard';
import ClassDetailModal from './ClassDetailModal';

const SAMPLE_STUDENT_ID =
  Number((typeof window !== 'undefined' && window.__APP_SAMPLE_STUDENT_ID__) || 1);

export default function Dashboard() {
  const [classItems, setClassItems] = useState([]);
  const [events, setEvents] = useState([]);
  const [viewMode, setViewMode] = useState('list');
  const [searchText, setSearchText] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [selectedClassId, setSelectedClassId] = useState(null);

  useEffect(() => {
    let isMounted = true;

    async function loadData() {
      setLoading(true);
      setError('');
      try {
        const [classesResponse, scheduleResponse] = await Promise.all([
          getStudentClasses(SAMPLE_STUDENT_ID),
          getStudentSchedule(SAMPLE_STUDENT_ID)
        ]);

        if (!isMounted) {
          return;
        }

        setClassItems(classesResponse);
        setEvents(scheduleResponse);
      } catch (requestError) {
        if (!isMounted) {
          return;
        }
        setError(requestError.message || 'Unable to load student dashboard data.');
      } finally {
        if (isMounted) {
          setLoading(false);
        }
      }
    }

    loadData();

    return () => {
      isMounted = false;
    };
  }, []);

  const totalCredits = useMemo(() => classItems.reduce((sum, item) => sum + item.credits, 0), [classItems]);

  const filteredClasses = useMemo(() => {
    if (!searchText.trim()) {
      return classItems;
    }

    const query = searchText.toLowerCase();
    return classItems.filter((item) => {
      return (
        item.className.toLowerCase().includes(query) ||
        item.courseCode.toLowerCase().includes(query) ||
        item.instructorName.toLowerCase().includes(query) ||
        item.location.toLowerCase().includes(query)
      );
    });
  }, [classItems, searchText]);

  return (
    <main className="page" aria-label="Student Dashboard">
      <header className="page-header">
        <h1>Student Dashboard</h1>
        <p>Epic 1 - Sprint 1</p>
      </header>

      <section className="toolbar" aria-label="Dashboard controls">
        <div className="search-wrap">
          <label htmlFor="class-search">Search enrolled classes</label>
          <input
            id="class-search"
            type="search"
            value={searchText}
            onChange={(event) => setSearchText(event.target.value)}
            placeholder="Search by class, code, instructor, or room"
            aria-label="Search enrolled classes"
          />
        </div>

        <div className="view-toggle" role="group" aria-label="Toggle dashboard view">
          <button
            type="button"
            className={viewMode === 'list' ? 'active' : ''}
            onClick={() => setViewMode('list')}
          >
            List View
          </button>
          <button
            type="button"
            className={viewMode === 'calendar' ? 'active' : ''}
            onClick={() => setViewMode('calendar')}
          >
            Calendar View
          </button>
        </div>
      </section>

      <section className="summary" aria-label="Enrollment summary">
        <div>
          <span className="summary-label">Classes</span>
          <strong>{filteredClasses.length}</strong>
        </div>
        <div>
          <span className="summary-label">Total Credits</span>
          <strong>{totalCredits}</strong>
        </div>
      </section>

      {loading && <p className="info">Loading dashboard...</p>}
      {error && (
        <p className="error" role="alert">
          {error}
        </p>
      )}

      {!loading && !error && viewMode === 'list' && (
        <section className="class-list" aria-label="Class list view" role="list">
          {filteredClasses.map((classItem) => (
            <div role="listitem" key={classItem.classId}>
              <ClassCard classItem={classItem} onOpen={setSelectedClassId} />
            </div>
          ))}
        </section>
      )}

      {!loading && !error && viewMode === 'calendar' && (
        <CalendarView events={events} onSelectClass={setSelectedClassId} />
      )}

      {selectedClassId && (
        <ClassDetailModal classId={selectedClassId} onClose={() => setSelectedClassId(null)} />
      )}
    </main>
  );
}
