import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { getClassDetails } from '../services/api';

export default function ClassDetail() {
  const { id } = useParams();
  const [classDetail, setClassDetail] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    let isMounted = true;

    async function loadClassDetails() {
      setLoading(true);
      setError('');
      try {
        const detail = await getClassDetails(id);
        if (isMounted) {
          setClassDetail(detail);
        }
      } catch (requestError) {
        if (isMounted) {
          setError(requestError.message || 'Unable to load class details.');
        }
      } finally {
        if (isMounted) {
          setLoading(false);
        }
      }
    }

    loadClassDetails();

    return () => {
      isMounted = false;
    };
  }, [id]);

  return (
    <main className="page" aria-label="Class detail page">
      <header className="page-header">
        <h1>Class Details</h1>
        <p>Review enrollment and waitlist information</p>
      </header>

      <Link className="back-link" to="/dashboard" aria-label="Back to student dashboard">
        Back to Dashboard
      </Link>

      {loading && <p className="info">Loading class details...</p>}
      {error && (
        <p className="error" role="alert">
          {error}
        </p>
      )}

      {!loading && !error && classDetail && (
        <section className="detail-card" aria-label="Selected class details">
          <h2>
            {classDetail.className} ({classDetail.courseCode})
          </h2>
          <dl className="detail-grid">
            <div>
              <dt>Professor</dt>
              <dd>{classDetail.professor}</dd>
            </div>
            <div>
              <dt>Location</dt>
              <dd>{classDetail.location}</dd>
            </div>
            <div>
              <dt>Schedule</dt>
              <dd>
                {classDetail.daysOfWeek} {classDetail.startTime} - {classDetail.endTime}
              </dd>
            </div>
            <div>
              <dt>Credits</dt>
              <dd>{classDetail.credits}</dd>
            </div>
            <div>
              <dt>Capacity</dt>
              <dd>
                {classDetail.enrolledCount} / {classDetail.capacity}{' '}
                {classDetail.isAtCapacity ? '(Full)' : '(Open)'}
              </dd>
            </div>
            <div>
              <dt>Waitlist</dt>
              <dd>{classDetail.waitlistCount}</dd>
            </div>
          </dl>

          <h3>Waitlist Positions</h3>
          {classDetail.waitlistPositions.length === 0 ? (
            <p>No students currently waitlisted.</p>
          ) : (
            <ul className="waitlist-list" aria-label="Waitlisted students">
              {classDetail.waitlistPositions.map((entry) => (
                <li key={entry.studentId}>
                  #{entry.position} - {entry.studentName}
                </li>
              ))}
            </ul>
          )}
        </section>
      )}
    </main>
  );
}
