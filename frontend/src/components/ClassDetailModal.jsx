import { useEffect, useRef, useState } from 'react';
import { getClassDetails } from '../services/api';

export default function ClassDetailModal({ classId, onClose }) {
  const [classDetail, setClassDetail] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const closeButtonRef = useRef(null);

  useEffect(() => {
    let isMounted = true;

    async function loadClassDetails() {
      setLoading(true);
      setError('');
      try {
        const detail = await getClassDetails(classId);
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
  }, [classId]);

  useEffect(() => {
    function handleKeyDown(event) {
      if (event.key === 'Escape') {
        onClose();
      }
    }

    document.addEventListener('keydown', handleKeyDown);
    closeButtonRef.current?.focus();

    return () => {
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [onClose]);

  return (
    <div
      className="modal-overlay"
      role="presentation"
      onClick={onClose}
      aria-label="Close class details modal"
    >
      <section
        className="modal-card"
        role="dialog"
        aria-modal="true"
        aria-labelledby="class-detail-modal-title"
        onClick={(event) => event.stopPropagation()}
      >
        <header className="modal-header">
          <h2 id="class-detail-modal-title">Class Details</h2>
          <button
            ref={closeButtonRef}
            type="button"
            className="modal-close"
            onClick={onClose}
            aria-label="Close class details"
          >
            Close
          </button>
        </header>

        {loading && <p className="info">Loading class details...</p>}
        {error && (
          <p className="error" role="alert">
            {error}
          </p>
        )}

        {!loading && !error && classDetail && (
          <section className="detail-card modal-detail" aria-label="Selected class details">
            <h3>
              {classDetail.className} ({classDetail.courseCode})
            </h3>
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

            <h4>Waitlist Positions</h4>
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
      </section>
    </div>
  );
}
